using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SKSSL.Tests.Tests;

[TestClass]
[TestSubject(typeof(DustLogger))]
public class LOGGING_TESTS
{

    [TestMethod]
    public void TEST_LOG()
    {
        DustLogger.Log("This is a test", DustLogger.LOG.FILE_ERROR);
    }
}