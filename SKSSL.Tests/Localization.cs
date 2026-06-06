using System;
using System.IO;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SKSSL.Tests;

[TestClass, TestSubject(typeof(Loc)), UsedImplicitly]
public class Localization
{
    [TestMethod, UsedImplicitly]
    public void TEST_LOCALE()
    {
        Loc.Load(Path.Combine(Environment.CurrentDirectory, "localization"));
        var name = Loc.Get("test-name-default");

        // If language changes, all that matters is that it's here.
        var localized = name.Equals("Complete") || (!Loc.CurrentLanguage.Equals("en-US") && !string.IsNullOrEmpty(name));
        Assert.IsTrue(localized);
    }
}