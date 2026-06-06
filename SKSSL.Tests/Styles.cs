using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Utilities;
using static SKSSL.Utilities.StyleSheet;

namespace SKSSL.Tests;

[TestClass, TestSubject(typeof(StyleSheet))]
// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
public class Styles
{
    [TestMethod]
    public void TEST_SHEET_LOAD()
    {
        LoadStyles();
        Assert.IsNotEmpty(UIColorSheet);
        Assert.IsTrue(UIColorSheet[0].Key.Equals("default"));
    }
    
    [TestMethod]
    public void TEST_GET_STYLE()
    {
        LoadStyles();
        UIStyle style = GetStyle("default");
        Assert.IsTrue(style.ColorHexStringForeground.Equals("#000000"));
    }
}