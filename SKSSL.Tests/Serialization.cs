using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SKSSL.Serializing;
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

        // Localization keys.
        var locId = new LocKey("test-key");
        var serializedLocId = serializer.Serialize(locId);
        var recoveredKey = serializer.Deserialize<LocKey>(serializedLocId);
        var resolved = recoveredKey.Resolve();
        Assert.IsNotEmpty(resolved);

        // Handles.
        var handle = new Handle("test-handle");
        var serializedHandle = serializer.Serialize(handle);
        Assert.AreEqual(handle.Unwrap(), serializedHandle.TrimEnd('\r', '\n'));

        // Custom object using the types noted above, but also as a wrapped list type. Good testing for prototypes.
        var @object = new[] { new TestSerialObject { FileInfo = fileInfo, LocKey = locId, Handle = handle } };
        var serializedObject = serializer.Serialize(@object);
        var deserializedObject = serializer.Deserialize<List<TestSerialObject>>(serializedObject);
        Assert.AreEqual(@object[0].Handle, deserializedObject[0].Handle);
    }
}

internal class TestSerialObject : IEquatable<TestSerialObject>
{
    public FileInfo FileInfo;
    public Handle Handle;
    public LocKey LocKey;

    public override bool Equals(object obj)
    {
        if (obj is not TestSerialObject other)
            return false;
        return FileInfo.FullName.Equals(other.FileInfo.FullName) &&
               Handle.Equals(other.Handle) &&
               LocKey.Equals(other.LocKey);
    }

    public bool Equals(TestSerialObject other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return Equals(FileInfo, other.FileInfo) && Handle.Equals(other.Handle) && LocKey.Equals(other.LocKey);
    }

    public override int GetHashCode()
    {
        // ReSharper disable NonReadonlyMemberInGetHashCode
        return HashCode.Combine(FileInfo, Handle, LocKey);
    }
}