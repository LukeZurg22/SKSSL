using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SKSSL.Tests;

[TestClass]
[TestSubject(typeof(DustLogger))]
public class Logging
{
    [TestMethod]
    public void TEST_LOG()
    {
        DustLogger.Log("This is a test", DustLogger.LOG.FILE_ERROR);
    }
}