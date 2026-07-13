using System;
using System.Data;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.ECS;
using SKSSL.Mathematics;

// ReSharper disable RedundantNameQualifier

namespace SKSSL.Tests;

[TestClass, UsedImplicitly]
public class Algorithms
{
    private ShuntingYard ShuntingYard;

    [TestInitialize, UsedImplicitly]
    public void Initialize()
    {
        SKSSL.ECS.Registry.StatisticRegistry registry = SKSSL.ECS.Registry.MasterRegistryManager
            .GetRegistry<SKSSL.ECS.StatisticPrototype, SKSSL.ECS.Registry.StatisticRegistry>();

        // Force a prototype for testing.
        StatisticPrototype pseudoPrototype = new()
        {
            Handle = "test_statistic",
            NameKey = string.Empty, // No custom name. Just leave blank.
            DescriptionKey = string.Empty, // No custom description, either! 
            BaseValue = 9,
            InitialValue = 9,
            Source = "game",
        };

        // Register the prototype.
        registry.Register("test_statistic", pseudoPrototype);
        registry.TryGet("test_statistic", out StatisticPrototype statistic);
        Assert.IsNotNull(statistic); // Re-testing the retrieval anyway.

        // Create a place to store statistics with Uids.
        var statisticsStorage = new StatisticsList();
        statisticsStorage.Set(statistic, statisticsStorage.New(), statistic.Handle);
        registry.Clear();

        // Huzzah for object instantiation.
        ShuntingYard = new ShuntingYard(statisticsStorage);
    }

    [TestMethod, UsedImplicitly]
    public void TEST_SHUNTING_YARD()
    {
        // Bad Delimiter.
        const string badDelimiterFormula = "(5/3--6"; // -> ERROR
        Assert.Throws<EvaluateException>(() => ShuntingYard.Evaluate(badDelimiterFormula, out double _));

        // Bad Variable.
        const string badVariableFormula = "(5+3*invalid_statistic)/3--6"; // -> ERROR
        Assert.Throws<Exception>(() => ShuntingYard.Evaluate(badVariableFormula, out double _));

        /*
         * (5 + 3 * test_statistic) / 3 - -6
         *              (5 + 3 * 9) / 3 + 6
         *                 (5 + 27) / 3 + 6
         *                       32 / 3 + 6
         *            10.66666666666667 + 6
         * -> 16.66666666666667
         */

        // Simple.
        const string formulaNormal = "(5+3*9)/3--6"; // -> 16.66666666666667
        ShuntingYard.Evaluate(formulaNormal, out var result);
        Assert.IsLessThan(0.001, Math.Abs(result - 16.666));

        // Variable.

        const string goodFormula = "(5+3*test_statistic)/3--6"; // -> 16.666
        ShuntingYard.Evaluate(goodFormula, out result);
        Assert.IsLessThan(0.001, Math.Abs(result - 16.666));
    }
}