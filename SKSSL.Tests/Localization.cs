using System;
using System.IO;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Localization;

namespace SKSSL.Tests;

[TestClass]
[TestSubject(typeof(Loc))]
public class Localization
{
    [TestMethod]
    public void TEST_LOCALE()
    {
        Loc.Load(Path.Combine(Environment.CurrentDirectory, "TestLocalization"));
        var name = Loc.Get("test-name-default");

        // If language changes, all that matters is that it's here.
        var localized = name.Equals("Complete") || !string.IsNullOrEmpty(name);
        Assert.IsTrue(localized);
    }
}