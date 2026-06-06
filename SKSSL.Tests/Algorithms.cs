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
        // Bad Delimiter.
        const string badDelimiterFormula = "(5/3--6"; // -> ERROR
        Assert.IsFalse(ShuntingYard.Evaluate(badDelimiterFormula, out double result));
        // CONSOLE ERRORS ARE NORMAL HERE! DO NOT MIND THE ERRORS FROM DUST-LOGGER IF THEY SHOW UP, IT'S DOING ITS JOB!
        
        // Bad Variable.
        const string badVariableFormula = "(5+3*invalid_statistic)/3--6"; // -> ERROR
        Assert.IsFalse(ShuntingYard.Evaluate(badVariableFormula, out result));
        // CONSOLE ERRORS ARE NORMAL HERE! DO NOT MIND THE ERRORS FROM DUST-LOGGER IF THEY SHOW UP, IT'S DOING ITS JOB!
        
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
        ShuntingYard.Evaluate(formulaNormal, out result);
        Assert.IsLessThan(0.001, Math.Abs(result - 16.666));
        
        // Variable.
        StatisticsVariables.Statistics.Clear();
        StatisticsVariables.Statistics.Add("test_statistic", 9);
        const string goodFormula = "(5+3*test_statistic)/3--6"; // -> 16.666
        ShuntingYard.Evaluate(goodFormula, out result);
        Assert.IsLessThan(0.001, Math.Abs(result - 16.666));
    }
}