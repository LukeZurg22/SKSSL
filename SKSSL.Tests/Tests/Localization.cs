using System;
using System.IO;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Localization;

namespace SKSSL.Tests.Tests;

[TestClass]
[TestSubject(typeof(Loc))]
public class LocTest
{

    [TestMethod]
    public void TEST_LOCALE()
    {
        Loc.Load(Path.Combine(Environment.CurrentDirectory, "Localization"));
        var name = Loc.Get("block-name-default");
    }
}