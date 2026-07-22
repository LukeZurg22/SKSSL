using System;
using System.Collections.Generic;
using System.Data;
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

    /// Store owner Uids to Uids of statistics they own in here.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _ownerUidToOwnedStatUids = [];

    /// Stores Modifier Uid values based on a Statistic UID- Modifier Handle pair.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _statUidHandleToModifierList = [];

    /// Pre-sorted list of modifiers indexed by the Statistic that owns them.
    private readonly Dictionary<PackableUid, List<(PackableUid, Modifier)>> _sortedModifiersPerStat = new();

    private readonly Dictionary<PackableUid, PackableUid> _modifierUidToParentStatisticUid = [];

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
    /// 
    /// </summary>
    private readonly Dictionary<PackableUid, bool> _statisticsThatRequireUpdates = new();

    #endregion

    private readonly ShuntingYard _shuntingYard;

    //@formatter:off
    [UsedImplicitly] public StatisticsList() => _shuntingYard = new ShuntingYard(this);
    [UsedImplicitly] public StatisticsList(ShuntingYard yard) => _shuntingYard = yard;
    //@formatter:on

    // TEMP: !!!!README!!!! - StatisticsList shall be a "global" list to solve the ShuntingYard problem.
    //  To fix the ownership issue, the owner uids will need to be stored somehow.
    //  The secondary dictionary storing modifiers per statistic ID is fine.
    //  - [ ] The removal method needs finishing. Ensure that all dictionaries above are accounted for.
    //  - [X] The pre-calc needs finishing.
    //  -> List.Suid.Stats
    //      The source has a list of statistics Uids, which then can be indexed through in the Local string -> list, obtaining the first instance that matches:
    //  - SUID
    //  - The handle containing the UIDs, only some of which are referenced by the source.
    //  - The UID designed, if contained. Do it hashset.contains, then hashset.get; hash set may be inefficient if indexing doesn’t work out.
    //  - Remove Statistic Uid list in statistic component, since the owner Uid is already being stored elsewhere.
    //      Additionally, create a statistics system. Statistics are looped through but only if the modifiers have been changed at all.


    /// <summary>
    /// 
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
        //@formatter:off
        // With the provided owner as context, multiple statistics uids MAY be obtained. It is a bit tedious, but
        // seems to be a necessary evil to loop over all owned IDs.
        if (_ownerUidToOwnedStatUids.TryGetValue(owner, out var statsUnderOwner))
            return statsUnderOwner.FirstOrDefault(statUid => HasHandle(statUid, handle));
        //@formatter:on

        // Default back to the first handle.
        return null;
    }

    #region Add

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
        Set(statistic, uid, statisticHandle);

        // Track the ownership over statistic uids.
        if (!_ownerUidToOwnedStatUids.TryGetValue(owner, out _))
            _ownerUidToOwnedStatUids[owner] = [];
        _ownerUidToOwnedStatUids[owner].Add(uid);

        // Create Statistic-Modifier key pair, even if there are no modifiers.
        if (!_statUidHandleToModifierList.ContainsKey(uid))
            _statUidHandleToModifierList[uid] = [];

        // Add all of the referenced modifiers to storage.
        foreach (var modifierHandle in statistic.Modifiers)
        {
            AddModifier(uid, modifierHandle, owner);
        }

        // Attempt to pre-calculate this statistic.
        if (CalculateValue(uid, out double result, owner))
        {
            _cachedStatisticValues.Add(uid, result);
        }

        return uid;
    }

    public void AddModifier(PackableUid statisticUid, string modifierHandle, PackableUid? container = null)
    {
        var modifierRegistryCasted = (ModifierRegistry)_modifierRegistry;

        // Check and ensure that the modifier exists in the registry.
        if (!modifierRegistryCasted.Contains(modifierHandle))
            throw new ModifierNotFoundException($"Failed to find modifier handle \'{modifierHandle}\' in registry.");

        // Early stacking check.
        if (_statUidHandleToModifierList.TryGetValue(statisticUid, out var existingModUids))
        {
            bool alreadyHasThisModifier = existingModUids.Any(modUid =>
                _modifiers.GetHandle(modUid)?.Equals(modifierHandle, StringComparison.Ordinal) == true);

            // Use the concrete registry or better yet, expose CanStack properly.
            if (alreadyHasThisModifier && !modifierRegistryCasted.CanStack(modifierHandle))
                throw new ModifierCannotStackException(
                    $"Modifier \'{modifierHandle}\' cannot stack to statistic ({statisticUid})!");
        }

        // Since it exists, it is assumed that it can be cloned as modifiers implement ICloneable<T> by default.
        Modifier modifier = _modifierRegistry.Clone(modifierHandle);

        // From here the goal is to attempt to pre-calculate value of the modifier for later.
        // If it cannot be pre-calculated, then it is still inserted assuming the right conditions.
        // Insert the modifier if one still can.
        if (EvaluateModifier(modifier.Expression, container, [statisticUid], out var cachedResult))
        {
            // Generate new uid for this modifier.
            PackableUid modifierUid = _modifiers.New();

            // Store the modifier's unique ID internally.
            _modifiers.Set(modifier, modifierUid, modifier.Handle);

            // Add the uid to the statistic. It's assumed that by now, the modifier either can stack, or is unique.
            _statUidHandleToModifierList[statisticUid].Add(modifierUid);

            // Assign ownership for quick indexing.
            _modifierUidToParentStatisticUid[modifierUid] = statisticUid;

            RebuildSortedModifiers(statisticUid);

            // Attempt to cache the modifier value if and only if it isn't an expression
            if (!Regex.IsMatch(modifier.Expression, "[a-zA-Z]+") && cachedResult != null)
            {
                // Returning true for successful caching.
                _cachedModifierValues[new UidPair(statisticUid, modifierUid)] = (double)cachedResult;
            }

            return;
        }

        throw new Exception($"Failed to insert modifier \'{modifierHandle}\' to statistic \'{statisticUid}\'.");
    }

    /// Conditionally evalulate the shunting yard algorithm, and forgive specific exceptions.
    private bool EvaluateModifier(
        string expression,
        PackableUid? parent,
        HashSet<PackableUid>? visited,
        out double? result)
    {
        result = null;
        bool canInsert;
        // Trying to pre-calculate.
        //@formatter:off
        try { result = _shuntingYard.Evaluate(expression, parent, visited); canInsert = true; }
            // EvaluateException - The expression wasn't able to be evaluated through the statistic list.
            // Could show sign of a simple missing statistic.
            catch (EvaluateException) { canInsert = true; }
            catch (MissingStatisticException) { canInsert = true; }
            // SyntaxErrorException - The expression is simply wrong.
            // RecursiveEvaluateException - The expression calls upon the statistic itself.
            catch (Exception e) {throw new Exception("Failed to evaluate modifier!", e); }
        //@formatter:on
        return canInsert;
    }

    #endregion

    private void RebuildSortedModifiers(PackableUid statUid)
    {
        if (!_statUidHandleToModifierList.TryGetValue(statUid, out var modUids))
        {
            _sortedModifiersPerStat.Remove(statUid);
            return;
        }

        var modifierList = new List<(PackableUid ModUid, Modifier Modifier)>(modUids.Count);
        foreach (PackableUid uid in modUids)
        {
            Modifier modifier = _modifiers.Get(uid);
            modifierList.Add((uid, modifier));
        }

        // Sort based on provided step, then operator.
        modifierList.Sort((a, b) =>
        {
            int stepCompare = a.Modifier.Step.CompareTo(b.Modifier.Step);
            if (stepCompare != 0)
                return stepCompare;
            int op = a.Modifier.Operator.CompareTo(b.Modifier.Operator);
            return op;
        });

        _sortedModifiersPerStat[statUid] = modifierList;
    }

    /// <returns>Full value of statistic adjusted by its modifiers.</returns>
    public bool CalculateValue(
        PackableUid statisticUid,
        out double output,
        PackableUid? parent = null,
        HashSet<PackableUid>? visited = null)
    {
        // Exception - Statistic is recursively being called.

        // If the statistic does not require updates, utilized a cached value.
        // Statistics that are simple numbers are left as-is.
        if (!_statisticsThatRequireUpdates.ContainsKey(statisticUid) &&
            _cachedStatisticValues.TryGetValue(statisticUid, out output))
            return true;

        // Sort the modifiers by step, starting from base, then by operator which roughly follows PEMDAS.
        if (!_sortedModifiersPerStat.TryGetValue(statisticUid, out var sortedModifiers))
        {
            RebuildSortedModifiers(statisticUid);
            sortedModifiers = _sortedModifiersPerStat[statisticUid];
        }

        Statistic statistic = Get(statisticUid); // Get internally-stored statistic.
        output = statistic.BaseValue; // Start with base value and go through each of the modifiers.
        foreach ((PackableUid, Modifier Mod) modKvp in sortedModifiers)
        {
            ModifyStatWithModifier(ref output, statisticUid, modKvp, parent, visited);
        }

        // Enforce the minimum-maximum boundaries.
        output = Math.Clamp(output, statistic.MinValue, statistic.MaxValue);
        return true;
    }

    private void ModifyStatWithModifier(
        ref double output,
        PackableUid statUid,
        (PackableUid Uid, Modifier Mod) modKvp,
        PackableUid? parent,
        HashSet<PackableUid>? visited)
    {
        // Check if the modifier contains an expression. If it has an expression that is not numerical-only, then
        //  it MUST be recalculated.
        PackableUid modifierUid = modKvp.Uid;
        ref Modifier modifier = ref modKvp.Mod;
        string expression = modifier.Expression;

        double operand = 0.0;
        var modOperator = ModifierOperator.NoOperator;

        // Check if the modifier is cached.
        //  For each modifier and attempt to get a raw cached value.
        var key = new UidPair(statUid, modifierUid);

        // If not cached somehow, then attempt to parse as a simple number.
        if (_cachedModifierValues.TryGetValue(key, out double cachedValue))
        {
            modOperator = modifier.Operator;
            operand = cachedValue;
            ApplyModifierStep(ref output, modOperator, operand);
            return;
        }

        // Attempt to parse a raw value as the cached value was not found.
        if (double.TryParse(expression, out var value))
        {
            modOperator = modifier.Operator;
            operand = value;

            // Cache it, now!
            _cachedModifierValues[key] = cachedValue;
            ApplyModifierStep(ref output, modOperator, operand);
            return;
        }

        // Expression contains letters, which means it must be a "proper" expression and must be evaluated.
        if (Regex.IsMatch(expression, "[a-zA-Z]+"))
        {
            if (EvaluateModifier(expression, parent, visited, out var result))
            {
                modOperator = modifier.Operator;
                if (result != null)
                    operand = (double)result;
                _cachedModifierValues[key] = operand; // Cache the value, even though it uses a statistic?
                // WARN: Not sure about this due to recursion issues still. Needs testng.
            }

            ApplyModifierStep(ref output, modOperator, operand);
        }

        throw new InvalidExpressionException(
            $"Expression {expression} is not valid due to no text nor being parsable as a number!");
    }

    private static void ApplyModifierStep(ref double output, ModifierOperator @operator, double operand)
    {
        switch (@operator)
        {
            case ModifierOperator.NoOperator:
                // NoOp - Lmao.
                break;
            case ModifierOperator.Add:
                output += operand;
                break;
            case ModifierOperator.Subtract:
                output -= operand;
                break;
            case ModifierOperator.Divide:
                output /= operand;
                break;
            case ModifierOperator.Multiply:
                output *= operand;
                break;
            case ModifierOperator.Power:
                output = Math.Pow(output, operand);
                break;
            case ModifierOperator.Override:
                output = operand;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(@operator), @operator, null);
        }
    }

    public override void Destroy(PackableUid statisticUid)
    {
        // Clear internal storage.
        _statisticsThatRequireUpdates.Remove(statisticUid);

        // Clean up modifiers.
        if (_statUidHandleToModifierList.TryGetValue(statisticUid, out var modUids))
        {
            foreach (PackableUid modUid in modUids)
            {
                _modifierUidToParentStatisticUid.Remove(modUid);
                _cachedModifierValues.Remove(new UidPair(statisticUid, modUid));
                _modifiers.Destroy(modUid);
            }

            _statUidHandleToModifierList.Remove(statisticUid);
        }

        // Clean up ownership.
        foreach (var kvp in _ownerUidToOwnedStatUids)
        {
            kvp.Value.Remove(statisticUid);
        }

        // Clean this statistic from history itself. Begone!
        base.Destroy(statisticUid);
        RebuildSortedModifiers(statisticUid);
    }

    /// Destroy / Removal intermediate function needs an override for when a UID becomes invalid.
    public void RemoveModifier(string statistic, string modifier)
    {
        /*// WIP: ELABORATE ON THIS
        Stack<PackableUid> uidStack = new();
        if (_statUidHandleToModifierList.TryGetValue(statistic, out var modifierHandles))
        {
            // Short circuit.
            if (!modifierHandles.Contains((PackableUid)modifier))
                return;

            _statisticHandleToModifierUids[statistic]

            foreach (PackableUid? handle in modifierHandles)
            {
                if (handle.Equals(modifier))
                {
                }
                // Use handle
            }
        }

        Destroy(uid);*/
        // RebuildSortedModifiers(statisticUid);
    }

    public void ChangeModifier(PackableUid modifierUid, Modifier replacement)
    {
        // Replace the internal modifier with the replacement.
        _modifiers.Replace(replacement, modifierUid);

        // Get owner of this modifier Uid and mark that it requires an update.
        PackableUid parentStatisticUid = _modifierUidToParentStatisticUid[modifierUid];
        _statisticsThatRequireUpdates[parentStatisticUid] = true;
        RebuildSortedModifiers(parentStatisticUid);
    }

    public override void Update(GameTime gameTime)
    {
        // WIP: A usage of the "DIRTY" system needs to be here, too.
        //  Specifically so I can loop over statistics that actually need changing, and not the ENTIRETY of the game's
        //  stat listS- PLURAL.
    }

    public void Clear()
    {
        base.Clear();
    }
}