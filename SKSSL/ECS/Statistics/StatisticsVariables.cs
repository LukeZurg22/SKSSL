using System.Collections.Generic;

namespace SKSSL.Mathematics;


/// <summary>
/// Where all mathematical algorithms derive their variable-values from when evaluating expressions.
/// </summary>
public abstract class StatisticsVariables
{
    /// WARN: Not fully implemented, yet.
    /// Used by the Mathematics classes, and any additional ones.
    public static Dictionary<string, double> Statistics { get; set; } = new();
}
