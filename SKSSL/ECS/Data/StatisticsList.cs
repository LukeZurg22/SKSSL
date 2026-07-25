using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using SKSSL.ECS.Registry;
using SKSSL.Mathematics;

namespace SKSSL.ECS;

/// A modifier is being added when one already exists, and stacking is not permitted.
public class ModifierCannotStackException(string s) : Exception(s);

/// An attempt to find a modifier in a modifier registry was made, and it failed.
public class ModifierNotFoundException(string s) : Exception(s);

/// An attempt to find whether a modifier is capable of being added without problems went awry.
public class BadModifierException(string s, Exception? inner = null) : Exception(s, inner);

/// <summary>
/// A specialist list storing Statistics Objects paired with unique IDs.
/// </summary>
/// <remarks>
///  Note that for ownership, a UID could just be generated on the fly if a single object owns all of the
///  statistics involved. Using a <see cref="GenericUid"/> for lazy ownership would make this simple.
/// </remarks>
/// <seealso cref="PackableUid"/>
/// <seealso cref="UidList{T}"/>
public class StatisticsList : UidList<Statistic>
{
    /// Stat UID | Mod UID Pair
    private readonly record struct UidPair(PackableUid statUid, PackableUid modUid);

    private readonly IReadOnlyRegistry<Statistic> _statisticRegistry =
        MasterRegistryManager.GetRegistry<Statistic, StatisticRegistry>().AsReadOnly();

    private readonly IReadOnlyRegistry<Modifier> _modifierRegistry =
        MasterRegistryManager.GetRegistry<Modifier, ModifierRegistry>().AsReadOnly();

    private readonly UidList<Modifier> _modifiers = new();

    /*This storage method will store all Statistic instances with special Uids. Those Uids can be re-referenced
     in the hope that those references don't go stale! Handles are blanket-wide. It is best NOT to get statistics
     using handles if possible.*/
    // -->+ _internal_storage (string to HashSet<Uid>)

    // STATISTIC -> MODIFIERS
    /// Stores Modifier Uid values based on a Statistic UID- Modifier Handle pair.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _statUidToModifiers = [];

    // STATISTIC -> MODIFIERS (SORTED)
    /// Pre-sorted list of modifiers indexed by the Statistic that owns them.
    private readonly Dictionary<PackableUid, List<(PackableUid ModUid, Modifier Modifier)>>
        _sortedModifiersPerStat = new();

    // MODIFIER -> STATISTIC REFERENCES
    /// <summary>
    /// For reverse-tracing of modifiers to statistic references. If a statistic is changed, a modifier referencing
    /// that statistic is changed which will call for the dirtying of its parent.
    /// </summary>
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _modifiersReferencingStatistics = [];

    // MODIFIER -> STATISTIC (PARENT)
    private readonly Dictionary<PackableUid, PackableUid> _modifierUidToParentStatisticUid = [];

    // SHUNTING YARD ALGORITHM
    private readonly ShuntingYard _shuntingYard;

    //@formatter:off
    [UsedImplicitly] public StatisticsList() => _shuntingYard = new ShuntingYard(this);
    [UsedImplicitly] public StatisticsList(ShuntingYard yard) => _shuntingYard = yard;
    //@formatter:on

    #region Caching

    /// <summary>
    /// Caching the updated calculated values for a simple O(1) retrieval. Hinges on these values being updated
    /// consistently and elsewhere.
    /// </summary>
    private readonly Dictionary<UidPair, double> _cachedModifierValues = new();

    /// <summary>
    /// If a statistic can somehow be defined as a constant value, then storing that value for raw O(1) retrieval
    /// will alleviate some of the processing burden.
    /// </summary>
    private readonly Dictionary<PackableUid, double> _cachedStatisticValues = new();

    /// <summary>
    /// Clean/Dirty listing for statistics.
    /// </summary>
    private readonly List<PackableUid> _statisticsThatRequireUpdates = [];

    #endregion

    #region Get Statistic

    /// <summary>
    /// Acquire a statistic using a handle, and preferably the Uid of its owner.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="owner"></param>
    /// <returns></returns>
    /// <remarks>
    /// Without context, this will break when there is more than one statistic of the same handle.
    /// </remarks>
    public PackableUid? GetStatistic(string handle, PackableUid? owner = null)
    {
        // Lazy nabbing. Without context, it's impossible to find which iteration of the handle we're looking for.
        if (owner is null)
            return ContainsHandle(handle) ? GetUids(handle).First() : null;

        // ReSharper disable once InvertIf
        // With the provided owner as context, multiple statistics uids MAY be obtained. It is a bit tedious, but
        // seems to be a necessary evil to loop over all owned IDs.
        return TryResolve(handle, owner, out PackableUid? foundStatistic) ? foundStatistic : null;
    }

    #endregion

    #region Adding Statistics

    /// Merely handling the registration interim, but nary more.
    [UsedImplicitly]
    public PackableUid? AddStatistic(PackableUid owner, string statisticHandle)
    {
        // If the statistic does not exist, don't use it!
        if (!_statisticRegistry.Contains(statisticHandle))
        {
            Log($"Failed to find statistic \'{statisticHandle}\' in registry.", LOG.SYSTEM_WARNING);
            return null;
        }

        // Get a copy of the statistic.
        Statistic statistic = _statisticRegistry.Clone(statisticHandle);
        PackableUid uid = New();

        // Ownership is tracked internally.
        Set(statistic, uid, statisticHandle, owner); // Set statistic in internal list.

        // Create Statistic-Modifier key pair, even if there are no modifiers.
        if (!_statUidToModifiers.ContainsKey(uid))
            _statUidToModifiers[uid] = [];

        // Add all of the referenced modifiers to storage.
        foreach (var modifierHandle in statistic.Modifiers) AddModifier(uid, modifierHandle, owner);

        // Regardless of modifiers, a statistic on-add will be called for an update at least once.
        DirtyStatistic(uid);
        return uid;
    }

    #endregion

    #region Adding Modifiers

    public void AddModifier(PackableUid statisticUid, string modifierHandle, PackableUid owner)
    {
        // Since it exists, it is assumed that it can be cloned as modifiers implement ICloneable<T> by default.
        Modifier modifier = _modifierRegistry.Clone(modifierHandle);

        // Ensure that actually adding this modifier would be fine to add.
        try
        {
            // This is expecting crashes. Might as well do damage control.
            TestModifierForWeaknesses(statisticUid, modifier);
        }
        catch (Exception innerException)
        {
            Log($"Failed to add modifier \'{modifierHandle}\': {innerException.Message}", LOG.SYSTEM_ERROR);
            return; // Don't even bother adding it or doing any treatment.
        }

        lock (_statUidToModifiers)
        lock (_modifierUidToParentStatisticUid)
        lock (_modifiers)
        {
            // Generate new uid for this modifier.
            PackableUid modifierUid = _modifiers.New();

            // Store the modifier's unique ID internally.
            _modifiers.Set(modifier, modifierUid, modifier.Handle);

            // Add the uid to the statistic. It's assumed that by now, the modifier either can stack, or is unique.
            _statUidToModifiers[statisticUid].Add(modifierUid);

            // Assign ownership for quick indexing.
            _modifierUidToParentStatisticUid[modifierUid] = statisticUid;

            RebuildSortedModifiers(statisticUid);

            // Since a modifier was added, the statistic is now "dirtied".
            DirtyStatistic(statisticUid);
        }
    }

    private void RebuildSortedModifiers(PackableUid statUid)
    {
        if (!_statUidToModifiers.TryGetValue(statUid, out var modUids))
        {
            _sortedModifiersPerStat.Remove(statUid);
            return;
        }

        var sortedModifiers = new List<(PackableUid ModUid, Modifier Modifier)>(modUids.Count);
        foreach (PackableUid uid in modUids)
        {
            Modifier modifier = _modifiers.Get(uid);
            sortedModifiers.Add((uid, modifier));
        }

        // Sort based on provided step, then operator.
        sortedModifiers.Sort((a, b) =>
        {
            int stepCompare = a.Modifier.Step.CompareTo(b.Modifier.Step);
            if (stepCompare != 0)
                return stepCompare;
            int op = a.Modifier.Operator.CompareTo(b.Modifier.Operator);
            return op;
        });

        _sortedModifiersPerStat[statUid] = sortedModifiers;
    }

    #endregion

    #region Dirtying / Cleaning

    /// Mark a the corresponding statistic as "dirty".
    private void DirtyStatistic(PackableUid statisticUid)
    {
        lock (_statisticsThatRequireUpdates)
        {
            if (_statisticsThatRequireUpdates.Contains(statisticUid))
                return; // Short-circuit. Already dirtied.

            // Add to the "dirty" list.
            _statisticsThatRequireUpdates.Add(statisticUid);

            // Invalidate this statistic’s own modifier cache.
            if (_statUidToModifiers.TryGetValue(statisticUid, out var modUids))
                foreach (PackableUid modUid in modUids)
                    _cachedModifierValues.Remove(new UidPair(statisticUid, modUid));

            // Propagate to any modifier referencing this statistic.
            if (_modifiersReferencingStatistics.TryGetValue(statisticUid, out var dependentModifiers))
                foreach (PackableUid modUid in dependentModifiers)
                    if (_modifierUidToParentStatisticUid.TryGetValue(modUid, out PackableUid? parentStatistic))
                        DirtyStatistic(parentStatistic); // Recursive – the early-out above prevents infinite loops.
        }
    }

    /// Remove a corresponding statistic from the list of statistics that actively need updates. 
    private void CleanStatistic(PackableUid statisticUid)
    {
        lock (_statisticsThatRequireUpdates)
            _statisticsThatRequireUpdates.Remove(statisticUid);
    }

    public void UpdateStatistic(PackableUid uid)
    {
        // Sort the modifiers.
        RebuildSortedModifiers(uid);

        CalculateStatisticValue(uid, out double value);
        _cachedStatisticValues[uid] = value;

        CleanStatistic(uid);
    }

    public bool IsStatisticDirty(PackableUid uid) => _statisticsThatRequireUpdates.Contains(uid);

    #endregion

    #region Calculating Statistic's Value

    /// <returns>Full value of statistic adjusted by its modifiers.</returns>
    public void CalculateStatisticValue(
        PackableUid statisticUid,
        out double output,
        PackableUid? parent = null,
        HashSet<PackableUid>? visited = null)
    {
        // If the statistic does not require updates, utilized a cached value.
        // Statistics that are simple numbers are left as-is.
        if (!_statisticsThatRequireUpdates.Contains(statisticUid) &&
            _cachedStatisticValues.TryGetValue(statisticUid, out output))
            return;

        // Force the shunting yard algorithm to crash if any statistics are self-referential. 
        visited ??= [statisticUid];

        // A non-cached statistic means it isn't very cut-and-dry.
        // Sort the modifiers by step, starting from base, then by operator which roughly follows PEMDAS.
        if (!_sortedModifiersPerStat.TryGetValue(statisticUid, out var sortedModifiers))
        {
            RebuildSortedModifiers(statisticUid);
            sortedModifiers = _sortedModifiersPerStat[statisticUid];
        }

        // Afterwards the process becomes applying all the modifiers available to this statistic.
        Statistic statistic = Get(statisticUid); // Get internally-stored statistic.
        output = statistic.BaseValue; // Start with base value and go through each of the modifiers.
        foreach ((PackableUid, Modifier Mod) modKvp in sortedModifiers)
        {
            ApplyModifierValue(ref output, statisticUid, modKvp, parent, visited);
        }

        // Enforce the minimum-maximum boundaries.
        output = Math.Clamp(output, statistic.MinValue, statistic.MaxValue);
    }

    private void ApplyModifierValue(ref double output,
        PackableUid statUid,
        (PackableUid Uid, Modifier Mod) modKvp,
        PackableUid? parent,
        HashSet<PackableUid>? visited)
    {
        // Check if the modifier contains an expression. If it has an expression that is not numerical-only, then
        //  it MUST be recalculated.
        PackableUid modifierUid = modKvp.Uid;
        ref Modifier modifier = ref modKvp.Mod;

        // Check if the modifier is cached.
        //  For each modifier and attempt to get a raw cached value.
        var key = new UidPair(statUid, modifierUid);

        // Check the cache.
        if (_cachedModifierValues.TryGetValue(key, out double cachedValue))
        {
            // Short-circuit. Cached value is nice.
            ApplyModifierStep(ref output, modifier.Operator, cachedValue);
            return;
        }

        // If not cached somehow, then attempt to parse as a simple number.
        if (double.TryParse(modifier.Expression, out double operand))
        {
            // The worst case is updating a modifier that has a constant with something that is not a constant.
            ApplyModifierStep(ref output, modifier.Operator, operand);
            return;
        }

        /*
            By this point if it is not in the cache then it must be an expression. It is assumed that if a modifier's
         expression isn't valid at the start, then it would never reach this point to begin with as it would have
         never been added.

            The statistic this is being applied-to is being fed into the shunting yard algorithm, which will catch any
        stray top-player recursions. By this point with all these checks for redundancy, this should evaluate. The REAL
        test, however, shall come from the anti-recursion arrangement put in place which heavily depends on all
        available statistics simply being present.
         */
        double valueFromExpression = _shuntingYard.Evaluate(modifier.Expression, parent, visited);
        ApplyModifierStep(ref output, modifier.Operator, valueFromExpression);
        return;

        void ApplyModifierStep(ref double output, ModifierOperator @operator, double @out)
        {
            switch (@operator)
            {
                case ModifierOperator.NoOperator:
                    // NoOp - Lmao.
                    break;
                case ModifierOperator.Add:
                    output += @out;
                    break;
                case ModifierOperator.Subtract:
                    output -= @out;
                    break;
                case ModifierOperator.Divide:
                    output /= @out;
                    break;
                case ModifierOperator.Multiply:
                    output *= @out;
                    break;
                case ModifierOperator.Power:
                    output = Math.Pow(output, @out);
                    break;
                case ModifierOperator.Override:
                    output = @out;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null);
            }
        }
    }

    private void TestModifierForWeaknesses(PackableUid statisticUid, Modifier modifier)
    {
        var modifierRegistryCasted = (ModifierRegistry)_modifierRegistry;

        // Check and ensure that the modifier exists in the registry.
        if (!modifierRegistryCasted.Contains(modifier.Handle))
            throw new ModifierNotFoundException($"Failed to find modifier handle \'{modifier.Handle}\' in registry. " +
                                                $"Did you forget to add it to the Modifier Registry?");

        // Early stacking check.
        if (_statUidToModifiers.TryGetValue(statisticUid, out var existingModUids))
        {
            bool alreadyHasThisModifier = existingModUids.Any(modUid =>
                _modifiers.GetHandle(modUid)?.Equals(modifier.Handle, StringComparison.Ordinal) == true);

            // Use the concrete registry or better yet, expose CanStack properly.
            if (alreadyHasThisModifier && !modifierRegistryCasted.CanStack(modifier.Handle))
                throw new ModifierCannotStackException(
                    $"Modifier \'{modifier.Handle}\' cannot stack to statistic ({statisticUid})!");
        }

        // The expression is empty.
        if (string.IsNullOrEmpty(modifier.Expression))
            throw new BadModifierException($"Modifier \'{modifier.Handle}\' has a blank expression!");

        // Check the expression. Does it parse into a number as text? If not, then that's bad.
        if (ExpressionContainsText(modifier.Expression))
        {
            // WIP: Check for looping.

            var statisticHandle = GetHandle(statisticUid);
            var variables = EnumerateIdentifiers(modifier.Expression);
            foreach (var variable in variables)
            {
                // Ensure that this particular variable has no references to the current variable.
                if (variable.Equals(statisticHandle))
                    throw new BadModifierException(
                        $"Modifier \'{modifier.Handle}\' expression \'{modifier.Expression}\' " +
                        $"contains a self-referential loop to \'{statisticHandle}\' ({statisticUid.ToString()})");
            }
        }
        // If the expression doesn't have text, it might still be usable.
        else if (!double.TryParse(modifier.Expression, out double _))
            throw new BadModifierException(
                $"Modifier \'{modifier.Handle}\' has an invalid expression \'{modifier.Expression}\'!");
    }

    private static List<string> EnumerateIdentifiers(string expression)
    {
        int i = 0;
        List<string> identifiers = [];

        while (i < expression.Length)
        {
            if (char.IsLetter(expression[i]) || expression[i] == '_')
            {
                int start = i++;

                while (i < expression.Length &&
                       (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                {
                    i++;
                }

                var identifier = expression[start..i];
                identifiers.Add(identifier);
            }
            else
            {
                i++;
            }
        }

        return identifiers;
    }

    private bool ExpressionContainsText(string expression) => Regex.IsMatch(expression, "[a-zA-Z]+");

    #endregion


    #region Statistic and Modifier Removal

    public override void Destroy(PackableUid statisticUid)
    {
        // Clear internal storage.
        _statisticsThatRequireUpdates.Remove(statisticUid);

        // WIP: Clean up the remaining lists one by one.

        // Clean up modifiers.
        if (_statUidToModifiers.TryGetValue(statisticUid, out var modUids))
        {
            foreach (PackableUid modUid in modUids)
            {
                _modifierUidToParentStatisticUid.Remove(modUid);
                _cachedModifierValues.Remove(new UidPair(statisticUid, modUid));
                _modifiers.Destroy(modUid);
            }

            _statUidToModifiers.Remove(statisticUid);
        }

        // Clean this statistic from history itself. Ownership is handled internally. Begone!
        base.Destroy(statisticUid);
        RebuildSortedModifiers(statisticUid);
    }

    /// Destroy / Removal intermediate function needs an override for when a UID becomes invalid.
    public void DestroyModifier(PackableUid statisticUid, PackableUid modifierUid)
    {
        if (!_statUidToModifiers.TryGetValue(statisticUid, out var modifiers) ||
            !modifiers.Remove(modifierUid))
            return;

        // TODO: Walk through the modifier's expression and recursively destroy references, if any.

        _modifierUidToParentStatisticUid.Remove(modifierUid);
        _cachedModifierValues.Remove(new UidPair(statisticUid, modifierUid));
        _modifiers.Destroy(modifierUid);

        RebuildSortedModifiers(statisticUid);
        DirtyStatistic(statisticUid);
    }

    #endregion

    #region Modifier Adjustment

    public void AdjustModifier(PackableUid modifierUid, double value) => throw new NotImplementedException();

    public void SwitchModifier(PackableUid modifierUid, Modifier replacement)
    {
        // Replace the internal modifier with the replacement.
        _modifiers.Replace(replacement, modifierUid);

        // Get owner of this modifier Uid and mark that it requires an update.
        PackableUid parentStatisticUid = _modifierUidToParentStatisticUid[modifierUid];
        if (!IsStatisticDirty(parentStatisticUid))
            DirtyStatistic(parentStatisticUid);
    }

    #endregion

    public override void Update(GameTime gameTime)
    {
        // Updates then cleans.
        foreach (PackableUid statistic in _statisticsThatRequireUpdates.ToList())
            UpdateStatistic(statistic);
    }

    #region Recursion Checking

    public bool HasCycle(PackableUid statistic) => HasCycle(statistic, [], []);

    private bool HasCycle(PackableUid statistic, HashSet<PackableUid> visited, HashSet<PackableUid> recursionStack)
    {
        if (!visited.Add(statistic))
            return recursionStack.Contains(statistic);

        recursionStack.Add(statistic);
        foreach (PackableUid dependency in GetDependencies(statistic))
            if (HasCycle(dependency, visited, recursionStack))
                return true;

        recursionStack.Remove(statistic);
        return false;
    }

    private IEnumerable<PackableUid> GetDependencies(PackableUid statisticUid)
    {
        if (!_statUidToModifiers.TryGetValue(statisticUid, out var modUids))
            yield break;

        // Resolve handle -> UID under the same owner, or global.
        foreach (PackableUid modUid in modUids)
        foreach (string identifier in EnumerateIdentifiers(_modifiers.Get(modUid).Expression))
        {
            PackableUid owner = GetOwner(statisticUid);

            // Resolve a handle using a provide downer, outputting a uid of a dependant.
            if (TryResolve(identifier, owner, out PackableUid? dependentUid))
            {
                if (!_modifiersReferencingStatistics.TryGetValue(modUid, out var modifierIsReferencing))
                    _modifiersReferencingStatistics[modUid] = modifierIsReferencing = [];
                modifierIsReferencing.Add(dependentUid);
                yield return dependentUid;
            }
        }
    }

    #endregion
}