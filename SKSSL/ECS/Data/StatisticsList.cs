using System.Collections.Generic;
using SKSSL.ECS.Registry;

namespace SKSSL.ECS;

public class StatisticsList : UidList<Statistic>
{
    private readonly IReadOnlyRegistry<Statistic> _statisticRegistry =
        MasterRegistryManager.GetRegistry<Statistic, StatisticRegistry>().AsReadOnly();

    private readonly IReadOnlyRegistry<Modifier> _modifierRegistry =
        MasterRegistryManager.GetRegistry<Modifier, ModifierRegistry>().AsReadOnly();

    // TODO: Expand this into a Struct of Arrays arrangement by overriding the base methods. The regular enumerator
    //  won't be very effective.

    /// Modifier linking between a statistic Uid and the modifier handles it contains.
    /// Linking modifier handles to the sets of modifier prototypes contained within.
    private readonly Dictionary<(PackableUid Statistic, string Handle), HashSet<Modifier>> _statsToModifiers = new();

    /// <returns>Full value of statistic with respect to its modifiers.</returns>
    public bool TryGetValue(string variable, out double output)
    {
        (PackableUid uid, Statistic? statistic) = GetKVP(variable);

        // Start with base value.
        output = statistic.BaseValue;

        foreach (var modifierHandle in statistic.Modifiers)
        {
            // WIP: Get modifier from the registry.
            //  Oh boy. That might cost some overhead...
            //  Think, man, think! This function is being called at Standard Runtime. Boot time is when modifiers
            //  can be off-loaded from the ModifierRegistry onto the Statistics Registry.
            //  WARN: Linking modifiers to statistics and vice-versa? SoA? The registry isn't the problem.
            //   An intermediate on-add handling is needed for when this list is given new values.
        }

        // TODO: Calculate the effects of modifiers.
        //  Something like... foreach modifier:
        //      get the modifier from somewhere, like the registry
        //      attempt to get a raw cached value
        //          otherwise, parse that bad boy with shunting yard. hm. I sure hope this doesn't loop...!
        //          somehow prevent or pass looping.
        //      switch through each of the modifier's steps
        //      ->+<- Apply the modifiers according to each step.
        //  -> (Now, by here, there SHOULD be some sort of caching?)
        //  -> (Perhaps by here, there would even be another need to get the value of other statistics, this is when it
        //  get REALLY -LOOPY-!)
        //  ???
        //  Profit.

        // WIP: Assume that this list contains valid statistics.
        //  [X] Throw not found exception if not found.
        //  [ ] Handle modifiers, somehow.
        //  --> After handling Replace for InternalUids (aka Entities), now i must determine if Statistics can be replaced.
        //      Modifiers can be replaced for sure, but what about resetting a statistic back? Don't see why not.
        //      The problem comes from finding out -when- to replace it, which may require a "unique" tag for the
        //          -statistic prototype such that the game will determined if it STACKS, or if it is UNIQUE. It'll-
        //          -need to be a boolean.

        return true;
        
    }

    /// Merely handling the registration interim, but nary more.
    protected override void ObjectAddOrSet(Statistic statistic, PackableUid? uid)
    {
        // Base statistics handling in the regular dense list.
        base.ObjectAddOrSet(statistic, uid);

        if (uid == null)
            return;

        foreach (var modifierHandle in statistic.Modifiers)
        {
            // Check and ensure that the modifier exists in the registry.
            if (!_modifierRegistry.Contains(modifierHandle))
            {
                Log($"Failed to find modifier handle \'{modifierHandle}\' in registry.", LOG.SYSTEM_WARNING);
                continue;
            }

            // Since it exists, it is assumed that it can be cloned as modifiers implement ICloneable<T> by default.
            Modifier modifier = _modifierRegistry.Clone(modifierHandle);

            // Ensure that each statistic always has a modifier set to work with.
            if (!_statsToModifiers.ContainsKey((uid, modifierHandle)))
                _statsToModifiers[(uid, modifierHandle)] = [];

            // Add clones of the prototypes from the registry. These clones are fresh!
            _statsToModifiers[(uid, modifierHandle)].Add(modifier);
        }
    }

    public void Clear()
    {
        base.Clear();
        _statsToModifiers.Clear();
    }
}

//        ModifierPrototype[] modifiers = [];
//        foreach (var modifierHandle in Modifiers)
//        {
//            ModifierRegistry registry = MasterRegistryManager.GetRegistry<ModifierPrototype, ModifierRegistry>();
//            if (registry.TryGet(modifierHandle, out ModifierPrototype? definition))
//            {
//            }
//        }
//
//        // Sort the list first by applicative step, then by precedence implicit by the operator position in the
//        //  enumerable type.
//        Array.Sort(modifiers, (a, b) =>
//        {
//            int stage = a.Step.CompareTo(b.Step);
//            return stage != 0 ? stage : b.Operator.CompareTo(a.Operator);
//        });
//
//        foreach (var modifier in modifiers)
//            modifier.ModifyValue(ref value);
//        return value;