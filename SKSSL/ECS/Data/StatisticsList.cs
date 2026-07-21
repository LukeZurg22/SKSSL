using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Xna.Framework;
using SKSSL.ECS.Registry;
using SKSSL.Mathematics;

namespace SKSSL.ECS;

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


    // TODO: Expand this into a Struct of Arrays arrangement by overriding the base methods. The regular enumerator
    //  won't be very effective.

    /// Store owner Uids to Uids of statistics they own in here.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _ownerUidToOwnedStatUids = [];

    /// Stores Modifier Uid values based on a Statistic UID- Modifier Handle pair.
    private readonly Dictionary<PackableUid, HashSet<PackableUid>> _statUidHandleToModifierList = [];

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
    public PackableUid GetStatistic(string handle, PackableUid? owner = null)
    {
        // Lazy nabbing. Without context, it's impossible to find which iteration of the handle we're looking for.
        if (owner is null)
            return GetUids(handle).First();

        // ReSharper disable once InvertIf
        //@formatter:off
        // With the provided owner as context, multiple statistics uids MAY be obtained. It is a bit tedious, but
        // seems to be a necessary evil to loop over all owned IDs.
        if (_ownerUidToOwnedStatUids.TryGetValue(owner, out var statsUnderOwner))
            foreach (PackableUid statUid in statsUnderOwner) if (HasHandle(statUid, handle)) return statUid;
        //@formatter:on

        // Default back to the first handle.
        return GetUids(handle).First();
    }

    #region Add

    /// Merely handling the registration interim, but nary more.
    public PackableUid? AddStatistic(string statisticHandle, PackableUid owner)
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
        Set(statistic, uid);

        // Track the ownership over statistic uids.
        if (_ownerUidToOwnedStatUids.TryGetValue(owner, out var statUids))
            statUids.Add(uid);
        else _ownerUidToOwnedStatUids[owner] = [];

        // Add all of the referenced modifiers to storage.
        foreach (var modifierHandle in statistic.Modifiers) AddModifier(uid, modifierHandle);

        // Attempt to pre-calculate this statistic.
        if (CalculateValue(uid, out double result))
            _cachedStatisticValues.Add(uid, result);

        return uid;
    }

    public void AddModifier(PackableUid statUid, string modifierHandle)
    {
        // Check and ensure that the modifier exists in the registry.
        if (!_modifierRegistry.Contains(modifierHandle))
        {
            Log($"Failed to find modifier handle \'{modifierHandle}\' in registry.", LOG.SYSTEM_WARNING);
            return;
        }

        // Since it exists, it is assumed that it can be cloned as modifiers
        //  implement ICloneable<T> by default.
        Modifier modifier = _modifierRegistry.Clone(modifierHandle);

        double? cachedResult = null;
        bool canInsert = false;
        // From here the goal is to attempt to pre-calculate value of the modifier for later.
        try
        {
            // Trying to pre-calculate.
            _shuntingYard.Evaluate(modifier.Expression, out var result, statUid);
            cachedResult = result;
            canInsert = true;
        }
        //@formatter:off
        // EvaluateException - The expression wasn't able to be evaluated through the statistic list.
        // Could show sign of a simple missing statistic.
        catch (EvaluateException) { canInsert = true; }
        catch (NullReferenceException) { canInsert = true; }
        // SyntaxErrorException - The expression is simply wrong.
        // MaximumRecursionLevelReachedException - The expression calls upon the statistic itself.
        catch (Exception) {/**/}
        
        // Insert the modifier if one still can.
        if (canInsert) { InsertModifier(statUid, modifier, cachedResult); }
        else {Log($"Failed to insert modifier \'{modifierHandle}\' to statistic \'{statUid}\'.", LOG.SYSTEM_ERROR);}
        //@formatter:on
    }

    private void InsertModifier(PackableUid statisticUid, Modifier modifier, double? cachedResult)
    {
        // Generate new uid for this modifier.
        PackableUid modifierUid = _modifiers.New();

        // Store the modifier's unique ID internally.
        _modifiers.Set(modifier, modifierUid, modifier.Handle);

        // Create Statistic-Modifier key pair.
        // If it cannot stack, and already has a handle, then it's reasonable to assume that it must
        // not be added. Checks if this statistic contains the modifier.
        if (_statUidHandleToModifierList.ContainsKey(statisticUid))
        {
            // If it contains a key of a statistic and a handle, then clearly there is an angry present.
            // The stacking prevention occurs here.
            if (!modifier.CanStack)
                return; // TODO: Consider making it replace instead?
        } // Ensure that each statistic-modifier pair always has a set to work with.
        else _statUidHandleToModifierList[statisticUid] = [];

        // Add the uid to the statistic.
        _statUidHandleToModifierList[statisticUid].Add(modifierUid);

        // Assign ownership for quick indexing.
        _modifierUidToParentStatisticUid[modifierUid] = statisticUid;

        // Attempt to cache the modifier value.
        if (cachedResult != null)
            _cachedModifierValues[new UidPair(statisticUid, modifierUid)] = (double)cachedResult;
    }

    #endregion

    /// <returns>Full value of statistic adjusted by its modifiers.</returns>
    public bool CalculateValue(PackableUid statisticUid, out double output)
    {
        // Forces re-calculating the statistic.
        if (!_statisticsThatRequireUpdates.ContainsKey(statisticUid))
        {
            // Statistics that are simple numbers are left as-is.
            if (_cachedStatisticValues.TryGetValue(statisticUid, out output))
                return true;
        }

        // Get internally-stored statistic.
        Statistic statistic = Get(statisticUid);

        List<(PackableUid ModUid, Modifier Mod)> modifiers = [];
        var modifierUids = _statUidHandleToModifierList[statisticUid];
        modifiers.AddRange(modifierUids.Select(uid => (uid, _modifiers.Get(uid))));

        // Sort the modifiers by step, starting from base,
        //  -> then by operator which roughly follows PEMDAS.
        modifiers.Sort((a, b) =>
        {
            int step = a.Mod.Step.CompareTo(b.Mod.Step);
            if (step != 0)
                return step;
            int operate = a.Mod.Operator.CompareTo(b.Mod.Operator);
            return operate;
        });

        // Start with base value and go through each of the modifiers.
        output = statistic.BaseValue;
        foreach ((PackableUid ModUid, Modifier Mod) modKvp in modifiers)
        {
            try
            {
                AdjustStatByModifier(ref output, statisticUid, modKvp);
            }
            catch
            {
                Log($"Failed to calculate \'{statistic.Handle} ({statisticUid})\''s \'{modKvp.Mod.Handle}\' modifer.",
                    LOG.SYSTEM_ERROR);
            }
        }

        // Enforce the minimum-maximum boundaries.
        Math.Clamp(output, statistic.MinValue, statistic.MaxValue);

        return true;
    }

    private void AdjustStatByModifier(ref double output, PackableUid statistic, (PackableUid ModUid, Modifier Mod) kvp)
    {
        // Check if the modifier contains an expression. If it has an expression that is not numerical-only, then
        //  it MUST be recalculated.
        string expression = kvp.Mod.Expression;
        double operand = 0.0;
        var modOperator = ModifierOperator.NoOperator;

        // Expression contains letters, which means it must be a "proper" expression and must be evaluated.
        if (expression.Contains("[a-zA-Z]+") && expression.Length > 2)
        {
            if (_shuntingYard.Evaluate(expression, out var result, kvp.ModUid))
            {
                modOperator = kvp.Mod.Operator;
                operand = result;
            }
        }
        else // The modifier must be a simple number.
        {
            //  For each modifier and attempt to get a raw cached value.
            var key = new UidPair(statistic, kvp.ModUid);

            // Check if the modifier is cached.
            if (_cachedModifierValues.TryGetValue(key, out double cachedValue))
            {
                modOperator = kvp.Mod.Operator;
                operand = cachedValue;
            }
            // If not cached somehow, then attempt to parse as a simple number.
            else
            {
                modOperator = kvp.Mod.Operator;
                operand = double.Parse(expression);
            }
        }

        ApplyModifierStep(ref output, modOperator, operand);
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
                output = Math.Pow(operand, operand);
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
        _statUidHandleToModifierList.Remove(statisticUid);

        base.Destroy(statisticUid);

        // TODO: Handle destruction.
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
    }

    public void ChangeModifier(PackableUid modifierUid, Modifier template)
    {
        // Replace the internal modifier with the replacement.
        _modifiers.Replace(template, modifierUid);

        // Get owner of this modifier Uid and mark that it requires an update.
        _statisticsThatRequireUpdates[_modifierUidToParentStatisticUid[modifierUid]] = true;
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

    public bool CheckStatisticTrace(string handle, PackableUid? source)
    {
        return true;
    }
}