using System;

namespace SKSSL.ECS;

public class StatisticsList : UidList<StatisticPrototype>
{
    public double GetValue(string variable)
    {
        // WIP: Assume that this list contains valid statistics.
        //  [ ] Throw not found exception if not found.
        //  [ ] 
         throw new NotImplementedException("This method is not implemented");
        return default;
    }
}