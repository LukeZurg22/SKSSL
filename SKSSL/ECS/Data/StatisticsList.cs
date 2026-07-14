namespace SKSSL.ECS;

public class StatisticsList : UidList<StatisticPrototype>
{
    // TODO: Expand this into a Struct of Arrays arrangement by overriding the base methods. The regular enumerator
    //  won't be very effective.

    /// <returns>Full value of statistic with respect to its modifiers.</returns>
    public bool TryGetValue(string variable, out double output)
    {
        StatisticPrototype statistic = Get(variable);
        
        // Start with base value.
        output = statistic.BaseValue;
        
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