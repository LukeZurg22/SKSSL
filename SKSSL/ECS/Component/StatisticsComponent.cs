using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace SKSSL.ECS;

public record StatisticsComponent : Component
{
    [YamlMember(Alias = "entries")] public List<string> StatisticHandles = [];
    
    [YamlIgnore] public HashSet<PackableUid> StatisticUidReferences = [];
}