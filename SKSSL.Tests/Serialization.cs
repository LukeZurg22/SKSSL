using System.IO;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.IO.Path;

namespace SKSSL.Tests;

[TestClass, TestSubject(typeof(Serialization)), UsedImplicitly]
public class Serialization
{
    [TestMethod, UsedImplicitly]
    public void SERIALIZE_EXTRA()
    {
        string absolutePath = Combine(GameDirectory.BuildDirectory, @"game\localization\en-US\Test\test.ftl");
        var fileInfo = new FileInfo(absolutePath);
        var serializer = new Serializing.SerializerDefaultYaml();
        var serializedA = serializer.Serialize(fileInfo);

        // Deserialize FilePath.
        var deserialized = serializer.Deserialize<FileInfo>(serializedA);
        Assert.IsNotNull(deserialized);

        // Serialize it back and ensure that the path relative to root is preserved.
        var serializedB = serializer.Serialize(fileInfo);
        Assert.AreEqual(serializedA, serializedB);
        
        
    }
}