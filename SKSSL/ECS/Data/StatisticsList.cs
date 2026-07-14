namespace SKSSL.ECS;

public class StatisticsList : UidList<StatisticPrototype>
{
    // TODO: Expand this into a Struct of Arrays arrangement.

    /// <returns>Full value of statistic with respect to its modifiers.</returns>
    public bool TryGetValue(string variable, out double output)
    {
        StatisticPrototype statistic = Get(variable);
        
        // Start with base value.
        output = statistic.BaseValue; 
        // WIP: Assume that this list contains valid statistics.
        //  [ ] Throw not found exception if not found.
        //  [ ] Handle modifiers, somehow.
        //  Statistics are stored internally in this list as their prototype, which include reference to modifiers.
        //  A statistic means nothing on its own but the modifiers it is meant to represent. So that's cool, except
        //  they also contain base values. There is also a bit of a conflict with uidList, which permits multiple of
        //  the same handle. NOT ideal! Instead it may need a custom type -again- and the handle would be used?
        //  Or set a toggle for one UID per active handle.
        //  --> After handling Replace for InternalUids (aka Entities), now i must determine if Statistics can be replaced.
        //      Modifiers can be replaced for sure, but what about resetting a statistic back? Don't see why not.
        //      The problem comes from finding out -when- to replace it, which may require a "unique" tag for the
        //          -statistic prototype such that the game will determined if it STACKS, or if it is UNIQUE. It'll-
        //          -need to be a boolean.

        return true;
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