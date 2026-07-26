#nullable enable
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
    private ShuntingYard ShuntingYard = null!;

    private readonly SKSSL.ECS.Registry.ModifierRegistry _modifierRegistry = SKSSL.ECS.Registry.MasterRegistryManager
        .GetRegistry<SKSSL.ECS.Modifier, SKSSL.ECS.Registry.ModifierRegistry>();

    private readonly SKSSL.ECS.Registry.StatisticRegistry _statisticRegistry = SKSSL.ECS.Registry.MasterRegistryManager
        .GetRegistry<SKSSL.ECS.Statistic, SKSSL.ECS.Registry.StatisticRegistry>();


    private PackableUid thisEntityContainer;

    private StatisticsList statisticsStorage = new();

    [TestInitialize, UsedImplicitly]
    public void Initialize()
    {
        _statisticRegistry.Clear();

        #region Regular Statistic

        // Force a prototype for testing.
        Statistic pseudo = new()
        {
            Handle = "test_statistic",
            NameKey = string.Empty, // No custom name. Just leave blank.
            DescriptionKey = string.Empty, // No custom description, either! 
            BaseValue = 9,
            Source = "game",
            Modifiers = [] // No modifiers!
        };

        // Register the prototype.
        _statisticRegistry.Register("test_statistic", pseudo);
        _statisticRegistry.TryGet("test_statistic", out Statistic? statistic);
        Assert.IsNotNull(statistic); // Re-testing the retrieval anyway.

        #endregion

        #region Recursion

        // Force a prototype for testing.
        _statisticRegistry.Register("recursive_statistic",
            new Statistic
            {
                Handle = "recursive_statistic",
                NameKey = string.Empty, // No custom name. Just leave blank.
                DescriptionKey = string.Empty, // No custom description, either! 
                BaseValue = 9,
                Source = "game",
                Modifiers = ["recursive_modifier"]
            });
        _statisticRegistry.TryGet("recursive_statistic", out Statistic? recursiveStatistic);
        _modifierRegistry.Register("recursive_modifier", new Modifier
        {
            Handle = "recursive_modifier",
            Duration = 1000f,
            Source = "game",
            Operator = ModifierOperator.Add,
            CanStack = true,
            Expression = "1+recursive_statistic" // Simple recursion.
        });
        _modifierRegistry.TryGet("recursive_modifier", out Modifier? modifier);
        Assert.IsNotNull(modifier); // Re-testing the retrieval anyway.

        #endregion

        #region Indirect Recursion

        _statisticRegistry.Register("recursive_statistic_b",
            new Statistic
            {
                Handle = "recursive_statistic_b",
                NameKey = string.Empty, // No custom name. Just leave blank.
                DescriptionKey = string.Empty, // No custom description, either! 
                BaseValue = 9,
                Source = "game",
                Modifiers = ["recursive_modifier_b"]
            });

        _modifierRegistry.Register("recursive_modifier_b", new Modifier
        {
            Handle = "recursive_modifier_b",
            Duration = 1000f,
            Source = "game",
            Operator = ModifierOperator.Add,
            CanStack = true,
            Expression = "1+recursive_statistic_c" // Simple recursion.
        });
        //_modifierRegistry.TryGet("recursive_modifier_b", out Modifier? _);
        _statisticRegistry.Register("recursive_statistic_c",
            new Statistic
            {
                Handle = "recursive_statistic_c",
                NameKey = string.Empty, // No custom name. Just leave blank.
                DescriptionKey = string.Empty, // No custom description, either! 
                BaseValue = 9,
                Source = "game",
                Modifiers = ["recursive_modifier_c"]
            });
        _modifierRegistry.Register("recursive_modifier_c", new Modifier
        {
            Handle = "recursive_modifier_c",
            Duration = 1000f,
            Source = "game",
            Operator = ModifierOperator.Add,
            CanStack = true,
            Expression = "1+recursive_statistic_b" // Simple recursion.
        });

        #endregion

        // Create a place to store statistics with Uids.
        statisticsStorage = new StatisticsList();
        thisEntityContainer = statisticsStorage.New();
        statisticsStorage.AddStatistic(thisEntityContainer, statistic.Handle);

        // Expecting a bad, recursive modifier.
        Assert.Throws<RecursiveEvaluateException>(() =>
            statisticsStorage.AddStatistic(thisEntityContainer, recursiveStatistic?.Handle!));

        _statisticRegistry.TryGet("recursive_statistic_b", out Statistic? recursiveStatisticB);

        // Statistic B is innocent?
        statisticsStorage.AddStatistic(thisEntityContainer, recursiveStatisticB?.Handle!);

        // Statistic C is the real recursive test.
        _statisticRegistry.TryGet("recursive_statistic_c", out Statistic? recursiveStatisticC);
        Assert.Throws<RecursiveEvaluateException>(() =>
            statisticsStorage.AddStatistic(thisEntityContainer, recursiveStatisticC?.Handle!));

        // Huzzah for object instantiation.
        ShuntingYard = new ShuntingYard(statisticsStorage);
    }

    [TestMethod, UsedImplicitly]
    public void TEST_SHUNTING_YARD()
    {
        // Bad Delimiter.
        const string badDelimiterFormula = "(5/3--6"; // -> ERROR
        Assert.Throws<SyntaxErrorException>(() => ShuntingYard.Evaluate(badDelimiterFormula));

        // Bad Variable.
        const string badVariableFormula = "(5+3*invalid_statistic)/3--6"; // -> ERROR
        Assert.Throws<MissingStatisticException>(() => ShuntingYard.Evaluate(badVariableFormula));

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
        var result = ShuntingYard.Evaluate(formulaNormal);
        Assert.IsLessThan(0.001, Math.Abs(result - 16.666));

        // Variable.
        const string goodFormula = "(5+3*test_statistic)/3--6"; // -> 16.666
        result = ShuntingYard.Evaluate(goodFormula);
        Assert.IsLessThan(0.001, Math.Abs(result - 16.666));
    }

    [TestMethod, UsedImplicitly]
    public void TEST_STATISTICS()
    {
        // Surface-Level Recursive calls.
        Assert.Throws<RecursiveEvaluateException>(() =>
            statisticsStorage.CalculateStatisticValue("recursive_statistic", thisEntityContainer));

        // Multi-Layer recursive calls.
        Assert.Throws<RecursiveEvaluateException>(() =>
            statisticsStorage.CalculateStatisticValue("recursive_statistic_b", thisEntityContainer));
    }
}