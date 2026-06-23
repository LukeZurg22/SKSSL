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

        // Read a file? This worked fine in testing, so not much may be needed here..
        // Benchmarking could suffice, but it does not matter very much.
    }
}