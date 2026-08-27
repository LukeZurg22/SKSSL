using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
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
///  statistics involved. Using a <see cref="PackableUid"/> for lazy ownership would make this simple.
/// </remarks>
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
    //@formatter:off

    // STATISTIC -> MODIFIERS
    /// Stores Modifier Uid values based on a Statistic UID- Modifier Handle pair.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _statToMods = [];
    // STATISTIC -> MODIFIERS (SORTED)
    /// Pre-sorted list of modifiers indexed by the Statistic that owns them.
    private readonly Dictionary<PackableUid, List<(PackableUid ModUid, Modifier Modifier)>> _sortedMods = new();
    // MODIFIER -> STATISTIC REFERENCES
    /// Modifier UIDs bound to the list of statistics their expressions reference.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _statReferencedBy = [];
    // MODIFIER -> STATISTIC (PARENT)
    private readonly Dictionary<PackableUid, PackableUid> _modToParentStat = [];
    // SHUNTING YARD ALGORITHM
    private readonly ShuntingYard _shuntingYard;
    /// Caching the updated calculated values for a simple O(1) retrieval. Hinges on these values being updated
    /// consistently and elsewhere.
    private readonly Dictionary<UidPair, double> _modifierCache = new();
    /// If a statistic can somehow be defined as a constant value, then storing that value for raw O(1) retrieval
    /// will alleviate some of the processing burden.
    private readonly Dictionary<PackableUid, double> _statisticCache = new();
    /// Clean/Dirty listing for statistics.
    private readonly HashSet<PackableUid> _dirtiedStatistics = [];
    [UsedImplicitly] public StatisticsList() => _shuntingYard = new ShuntingYard(this);
    [UsedImplicitly] public StatisticsList(ShuntingYard yard) => _shuntingYard = yard;
    //@formatter:on

    #region Adding Statistics & Modifiers

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
        _statToMods[uid] = []; // Create Statistic-Modifier key pair, even if there are no modifiers.

        foreach (var modifierHandle in statistic.Modifiers)
            AddModifier(uid, modifierHandle);

        // Regardless of modifiers, a statistic on-add will be called for an update at least once.
        Dirty(uid);
        return uid;
    }


    public void AddModifier(PackableUid statisticUid, string modifierHandle)
    {
        // Since it exists, it is assumed that it can be cloned as modifiers implement ICloneable<T> by default.
        Modifier modifier = _modifierRegistry.Clone(modifierHandle);

        // Ensure that actually adding this modifier would be fine to add.
        try
        {
            ValidateModifierBeforeAdd(statisticUid, modifier);
        }
        // ReSharper disable once RedundantCatchClause
        catch (Exception innerException)
        {
            Log($"Failed to add modifier \'{modifierHandle}\': {innerException.Message}", LOG.SYSTEM_ERROR);
            return; // Don't even bother adding it or doing any treatment.
        }

        // Generate new uid for this modifier.
        PackableUid modifierUid = _modifiers.New();
        _modifiers.Set(modifier, modifierUid, modifier.Handle); // Store the modifier's unique ID internally.
        _statToMods[statisticUid].Add(modifierUid); // Add the uid to the statistic. Assume can-stack, or unique.
        _modToParentStat[modifierUid] = statisticUid; // Assign ownership for quick indexing.

        // Populate reverse map
        foreach (PackableUid dependency in GetDependenciesFromExpression(modifier.Expression, GetOwner(statisticUid)))
        {
            if (!_statReferencedBy.TryGetValue(dependency, out var set))
                _statReferencedBy[dependency] = set = [];
            set.Add(modifierUid);
        }

        RebuildSortedModifiers(statisticUid);
        Dirty(statisticUid);
    }

    private void RebuildSortedModifiers(PackableUid statUid)
    {
        if (!_statToMods.TryGetValue(statUid, out var modUids))
        {
            _sortedMods.Remove(statUid);
            return;
        }

        // Sort based on provided step, then operator.
        var sortedModifiers = new List<(PackableUid ModUid, Modifier Modifier)>(modUids.Count);
        sortedModifiers.AddRange(from uid in modUids let modifier = _modifiers.Get(uid) select (uid, modifier));
        sortedModifiers.Sort((a, b) =>
        {
            int stepCompare = a.Modifier.Step.CompareTo(b.Modifier.Step);
            if (stepCompare != 0)
                return stepCompare;
            int op = a.Modifier.Operator.CompareTo(b.Modifier.Operator);
            return op;
        });

        _sortedMods[statUid] = sortedModifiers;
    }

    #endregion

    #region Get Methods

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

    #region Dirtying / Cleaning

    /// Mark a the corresponding statistic as "dirty".
    private void Dirty(PackableUid statisticUid)
    {
        if (!_dirtiedStatistics.Add(statisticUid)) return; // Short-circuit. Already dirtied.

        // Invalidate this statistic’s own modifier cache.
        if (_statToMods.TryGetValue(statisticUid, out var modUids))
            foreach (PackableUid modUid in modUids)
                _modifierCache.Remove(new UidPair(statisticUid, modUid));

        // Propagate to any modifier referencing this statistic.
        if (_statReferencedBy.TryGetValue(statisticUid, out var dependentModifiers))
            foreach (PackableUid modUid in dependentModifiers)
                if (_modToParentStat.TryGetValue(modUid, out PackableUid parentStatistic))
                    Dirty(parentStatistic); // Recursive – the early-out above prevents infinite loops.
    }

    public void UpdateStatistic(PackableUid uid)
    {
        var value = CalculateStatisticValue(uid); // Sort the modifiers.
        _statisticCache[uid] = value;
        _dirtiedStatistics.Remove(uid); // "Clean" statistic.
    }

    public bool IsDirty(PackableUid statisticUid) => _dirtiedStatistics.Contains(statisticUid);

    #endregion

    #region Calculating Statistic's Value

    public double CalculateStatisticValue(string handle, PackableUid owner)
    {
        var statistic = GetStatistic(handle, owner);
        if (statistic is null)
            throw new Exception($"Failed to get statistic \'{handle}\' through {nameof(CalculateStatisticValue)}.");
        return CalculateStatisticValue(statistic.Value, null, []);
    }

    /// <returns>Full value of statistic adjusted by its modifiers.</returns>
    public double CalculateStatisticValue(PackableUid statisticUid, PackableUid? owner = null,
        HashSet<PackableUid>? visited = null)
    {
        // If the statistic does not require updates, utilized a cached value.
        // Statistics that are simple numbers are left as-is.
        if (!_dirtiedStatistics.Contains(statisticUid) && _statisticCache.TryGetValue(statisticUid, out var value))
            return value;

        // Force the shunting yard algorithm to crash if any statistics are self-referential. 
        visited ??= [];
        if (!visited.Add(statisticUid))
            throw new RecursiveEvaluateException($"Infinite recursion involving statistic ({statisticUid})");

        // A non-cached statistic means it isn't very cut-and-dry.
        // Sort the modifiers by step, starting from base, then by operator which roughly follows PEMDAS.
        if (!_sortedMods.TryGetValue(statisticUid, out var sortedModifiers) ||
            sortedModifiers.Count > 0)
        {
            RebuildSortedModifiers(statisticUid);
            sortedModifiers = _sortedMods[statisticUid];
        }

        // Afterwards the process becomes applying all the modifiers available to this statistic.
        Statistic statistic = Get(statisticUid); // Get internally-stored statistic.
        value = statistic.BaseValue; // Start with base value and go through each of the modifiers.
        foreach ((PackableUid ModUid, Modifier Mod) modKvp in sortedModifiers)
        {
            ApplyModifierValue(ref value, statisticUid, modKvp.ModUid, modKvp.Mod, owner, visited);
        }

        // Enforce the minimum-maximum boundaries.
        value = Math.Clamp(value, statistic.MinValue, statistic.MaxValue);
        return value;
    }

    private void ApplyModifierValue(ref double output,
        PackableUid statUid, PackableUid modifierUid, Modifier modifier,
        PackableUid? parent, HashSet<PackableUid>? visited)
    {
        // Check if the modifier is cached.
        //  For each modifier and attempt to get a raw cached value.
        var key = new UidPair(statUid, modifierUid);

        // Check the cache.
        if (_modifierCache.TryGetValue(key, out double cachedValue))
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
        _modifierCache[key] = valueFromExpression; // Cache the value.
        ApplyModifierStep(ref output, modifier.Operator, valueFromExpression);
    }

    private static void ApplyModifierStep(ref double output, ModifierOperator @operator, double operand)
    {
        switch (@operator)
        {
            case ModifierOperator.NoOperator: /*NoOp - Lmao.*/ break;
            case ModifierOperator.Add: output += operand; break;
            case ModifierOperator.Subtract: output -= operand; break;
            case ModifierOperator.Divide: output /= operand; break;
            case ModifierOperator.Multiply: output *= operand; break;
            case ModifierOperator.Power: output = Math.Pow(output, operand); break;
            case ModifierOperator.Override: output = operand; break;
            default: throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null);
        }
    }

    private void ValidateModifierBeforeAdd(PackableUid statisticUid, Modifier modifier)
    {
        var concrete = (ModifierRegistry)_modifierRegistry;
        var statisticHandle = GetHandle(statisticUid);

        // Check and ensure that the modifier exists in the registry.
        if (!concrete.Contains(modifier.Handle))
            throw new ModifierNotFoundException($"Modifier '{modifier.Handle}' not in registry.");

        // Early stacking check.
        if (_statToMods.TryGetValue(statisticUid, out var existing) &&
            existing.Any(u => _modifiers.GetHandle(u) == modifier.Handle) &&
            !concrete.CanStack(modifier.Handle))
            throw new ModifierCannotStackException(
                $"Modifier '{modifier.Handle}' cannot stack on stat. \'statisticHandle\' {statisticUid}.");

        // The expression is empty.
        if (string.IsNullOrWhiteSpace(modifier.Expression))
            throw new BadModifierException($"Modifier \'{modifier.Handle}\' has a blank expression!");

        // If the expression doesn't have text, it might still be usable.
        if (!ContainsText(modifier.Expression) && !double.TryParse(modifier.Expression, out double _))
            throw new BadModifierException(
                $"Modifier \'{modifier.Handle}\' has an invalid expression \'{modifier.Expression}\'!");

        // Cycle check *before* permanently adding anything
        if (ContainsText(modifier.Expression) &&
            WouldCreateCycle(statisticUid, modifier.Expression, out var stack))
        {
            StringBuilder stb = new();
            stb.Append($"Modifier '{modifier.Handle}' is recursive for stat. ");
            stb.Append($"\'{statisticHandle}\' {statisticUid} ");
            switch (stack.Count)
            {
                // Surface-level recursion.
                case 0:
                    stb.Append("and it's surface-level, ");
                    break;
                // Multi-level recursion.
                default:
                {
                    stb.Append(", with the following references: ");
                    foreach (PackableUid stackEntry in stack)
                        stb.Append($"\'{GetHandle(stackEntry)}\' {stackEntry}, ");
                    break;
                }
            }

            stb.Append("please fix this.");
            throw new RecursiveEvaluateException(stb.ToString());
        }
    }

    private bool WouldCreateCycle(PackableUid ownerStat, string expression, out HashSet<PackableUid> stack)
    {
        PackableUid owner = GetOwner(ownerStat);
        var deps = GetDependenciesFromExpression(expression, owner).ToList();

        // Temporary edge: ownerStat → each dependency
        // We only need to check whether any dep can already reach ownerStat
        var visited = new HashSet<PackableUid>();
        stack = new HashSet<PackableUid>();

        foreach (PackableUid d in deps)
            if (HasPath(d, ownerStat, visited, stack))
                return true;

        return false;
    }

    private bool HasPath(PackableUid from, PackableUid target,
        HashSet<PackableUid> visited, HashSet<PackableUid> stack)
    {
        if (!visited.Add(from)) return stack.Contains(from);
        if (Equals(from, target)) return true;

        stack.Add(from);
        foreach (PackableUid next in GetDependencies(from))
            if (HasPath(next, target, visited, stack))
                return true;
        stack.Remove(from);
        return false;
    }

    private IEnumerable<PackableUid> GetDependencies(PackableUid statisticUid)
    {
        if (!_statToMods.TryGetValue(statisticUid, out var modUids))
            yield break;

        PackableUid owner = GetOwner(statisticUid);
        foreach (var modUid in modUids)
        foreach (string identifier in EnumerateIdentifiers(_modifiers.Get(modUid).Expression))
            if (TryResolve(identifier, owner, out var dependentUid))
                yield return dependentUid.Value;
    }

    private IEnumerable<PackableUid> GetDependenciesFromExpression(string expr, PackableUid owner)
    {
        foreach (var id in EnumerateIdentifiers(expr))
            if (TryResolve(id, owner, out var uid))
                yield return uid.Value;
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
                while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_')) i++;
                var identifier = expression[start..i];
                identifiers.Add(identifier);
            }
            else i++;
        }

        return identifiers;
    }

    private static bool ContainsText(string expression) => Regex.IsMatch(expression, "[a-zA-Z]+");

    #endregion

    #region Statistic and Modifier Removal

    /// <summary>
    /// Removes the first (or owner-scoped) statistic that matches the handle.
    /// Returns true if something was removed.
    /// </summary>
    public bool RemoveStatistic(string handle, PackableUid? owner = null)
    {
        PackableUid? uid = GetStatistic(handle, owner);
        if (uid is null) return false;
        Destroy(uid.Value);
        return true;
    }

    public override void Destroy(PackableUid statisticUid)
    {
        // Clean up modifiers.
        if (_statToMods.TryGetValue(statisticUid, out var modUids))
        {
            foreach (PackableUid modUid in modUids) DestroyModifier(modUid);
            _statToMods.Remove(statisticUid);
        }

        // Remove the statistic from all work.
        _dirtiedStatistics.Remove(statisticUid);
        _sortedMods.Remove(statisticUid);
        _statisticCache.Remove(statisticUid);
        _dirtiedStatistics.Remove(statisticUid);
        base.Destroy(statisticUid);
    }

    /// <summary>
    /// Removes every modifier with the given handle that belongs to the given statistic.
    /// Returns the number of modifiers removed.
    /// </summary>
    public int RemoveModifier(string modifierHandle, PackableUid statisticUid)
    {
        if (!_statToMods.TryGetValue(statisticUid, out var modUids))
            return 0;

        var toRemove = modUids
            .Where(uid => _modifiers.GetHandle(uid)?.Equals(modifierHandle, StringComparison.Ordinal) == true)
            .ToList();

        foreach (PackableUid uid in toRemove)
            DestroyModifier(uid);

        return toRemove.Count;
    }

    /// <summary>
    /// Convenience: remove a modifier by handle from a statistic identified by handle.
    /// </summary>
    public int RemoveModifier(string modHandle, string statHandle, PackableUid? owner = null)
    {
        var statisticGuid = GetStatistic(statHandle, owner);
        return statisticGuid is null ? 0 : RemoveModifier(modHandle, statisticGuid.Value);
    }

    /// Destroy / Removal intermediate function needs an override for when a UID becomes invalid.
    public void DestroyModifier(PackableUid modifierUid)
    {
        if (!_modToParentStat.TryGetValue(modifierUid, out var parent))
            return;

        PackableUid statisticUid = _modToParentStat[modifierUid];

        // Remove this modifier from every reverse set
        foreach (var statisticReferences in _statReferencedBy.Values)
            statisticReferences.Remove(modifierUid);

        _modToParentStat.Remove(modifierUid);
        _modifierCache.Remove(new UidPair(statisticUid, modifierUid));
        _statToMods[parent].Remove(modifierUid);
        _modifiers.Destroy(modifierUid);

        RebuildSortedModifiers(statisticUid);
        Dirty(statisticUid);
    }

    #endregion

    #region Modifier Adjustment

    public void AdjustModifier(PackableUid modifierUid, double value) => throw new NotImplementedException();

    public void SwitchModifier(PackableUid modifierUid, Modifier replacement)
    {
        // Replace the internal modifier with the replacement.
        _modifiers.Replace(replacement, modifierUid);

        // Get owner of this modifier Uid and mark that it requires an update.
        PackableUid parentStatisticUid = _modToParentStat[modifierUid];
        if (!IsDirty(parentStatisticUid))
            Dirty(parentStatisticUid);
    }

    #endregion

    #region Recursion Checking

    // ReSharper disable once UnusedMember.Global
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

    #endregion

    public override void Update(Microsoft.Xna.Framework.GameTime gameTime)
    {
        // Updates then cleans.
        foreach (PackableUid statistic in _dirtiedStatistics.ToList())
            UpdateStatistic(statistic);
    }
}