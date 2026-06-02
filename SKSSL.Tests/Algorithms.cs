using System;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Mathematics;

namespace SKSSL.Tests;

[TestClass, UsedImplicitly]
public class Algorithms
{
    [TestInitialize, UsedImplicitly]
    public void Initialize()
    {
        // WIP: Ensure to read any loaded statistics data. Consider involving prototyping system.
        //  Personal remark: It would be supremely retarded to tie a mathematical algorithm directly to the ECS without
        //  recourse! It may be best to provide a parameter of defined variables to evaluate the algorithm upon, rather
        //  than connect to the ECS directly.
    }

    [TestMethod, UsedImplicitly]
    public void TEST_SHUNTING_YARD()
    {
        // Simple.
        double result = 0f;
        const string formulaNormal = "(5+3*9)/3--6"; // -> 4.666666666666667
        ShuntingYard.Evaluate(formulaNormal, out result);
        Assert.IsTrue(Math.Abs(result - 4.666) < 0.05);
        
        // Variable.
        StatisticsVariables.Statistics.Clear();
        StatisticsVariables.Statistics.Add("test_statistic", 4);
        const string goodFormula = "(5+3*test_statistic)/3--6";
        /*
         * (5 + 3 * test_statistic) / 3 - -6
         * (5 + 3 * 4)              / 3 + 6
         * (5 + 12)                 / 9
         * (17) / 9
         * -> 1.888888888888889
         */
        ShuntingYard.Evaluate(goodFormula, out result);
        Assert.IsTrue(Math.Abs(result - 1.888) < 0.05);

        // Bad Variable.
        const string badDelimiterFormula = "(5+3*invalid_statistic/3--6";
        Assert.IsFalse(ShuntingYard.Evaluate(badDelimiterFormula, out result));
        // WIP: finish this evaluation & shunting yard.
    }
}