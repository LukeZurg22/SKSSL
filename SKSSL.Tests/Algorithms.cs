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
        double result = 0f;
        const string goodFormula = "(5+3*example_statistic)/3--6";
        var ppa = ShuntingYard.Evaluate(goodFormula, out result);
        const string badDelimiterFormula = "(5+3*example_statistic/3--6";
        Assert.IsFalse(ShuntingYard.Evaluate(badDelimiterFormula, out result));
        // WIP: finish this evaluation & shunting yard.
    }
}