namespace SharedBase.Tests.Archive.Tests;

using System.IO;
using System.Text;
using SharedBase.Archive;
using Xunit;

public class BasicArchiveTests
{
    private readonly DefaultArchiveManager sharedManager = new(false);

    [Fact]
    public void BasicArchive_IntWritingAndReading()
    {
        var manager = new DefaultArchiveManager(false);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        foreach (var value in new[] { 1, 42, int.MaxValue, -1, -500, int.MinValue })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadInt32());
        }

        foreach (var value in new short[] { 1, 42, short.MaxValue, -1, -500, short.MinValue })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadInt16());
        }

        foreach (var value in new[] { 1, 42, long.MaxValue, -1, -500, long.MinValue })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadInt64());
        }

        foreach (var value in new byte[] { 1, 42, byte.MaxValue, 0, byte.MinValue })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadInt8());
        }
    }

    [Fact]
    public void BasicArchive_UIntWritingAndReading()
    {
        var manager = new DefaultArchiveManager(false);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        foreach (var value in new uint[] { 1, 42, uint.MaxValue, 0 })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadUInt32());
        }

        foreach (var value in new ushort[] { 1, 42, ushort.MaxValue, ushort.MinValue })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadUInt16());
        }

        foreach (var value in new ulong[] { 1, 42, ulong.MaxValue, 0, ulong.MinValue })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadUInt64());
        }
    }

    [Fact]
    public void BasicArchive_FloatWritingAndReading()
    {
        var manager = new DefaultArchiveManager(false);
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, manager);
        var reader = new SArchiveMemoryReader(memoryStream, manager);

        foreach (var value in new[]
                 {
                     1.0f, 42.0f, float.MaxValue, float.MinValue, float.Epsilon, float.NegativeInfinity,
                     float.PositiveInfinity, float.NaN, 0,
                 })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadFloat());
        }

        foreach (var value in new[]
                 {
                     1.0f, 42.0f, double.MaxValue, double.MinValue, double.Epsilon, double.NegativeInfinity,
                     double.PositiveInfinity, double.NaN, 0,
                 })
        {
            memoryStream.Seek(0, SeekOrigin.Begin);
            writer.Write(value);

            memoryStream.Seek(0, SeekOrigin.Begin);
            Assert.Equal(value, reader.ReadDouble());
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(42, 1)]
    [InlineData(-1, 5)]
    [InlineData(-500, 5)]
    [InlineData(int.MinValue, 5)]
    [InlineData(432567, 3)]
    [InlineData(int.MaxValue, 5)]
    public void BasicArchive_VariableLengthValue(long value, int expectedLength)
    {
        var memoryStream = new MemoryStream(expectedLength * 2);
        var writer = new SArchiveMemoryWriter(memoryStream, sharedManager);
        var reader = new SArchiveMemoryReader(memoryStream, sharedManager);

        var converted = (uint)value;

        writer.WriteVariableLengthField32(converted);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var read = reader.ReadVariableLengthField32();

        Assert.Equal(converted, read);
        Assert.Equal(value, unchecked((int)read));

        Assert.Equal(expectedLength, memoryStream.Length);
    }

    [Theory]
    [InlineData("hello", 6)]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("Hello World!", 13)]
    [InlineData("This is a much longer string.", 30)]
    [InlineData("This is a much longer string. That even has \0null embedded in it, take that!", 78)]
    public void BasicArchive_StringWritingAndReading(string? testData, int expectedLength)
    {
        var memoryStream = new MemoryStream(expectedLength * 2);
        var writer = new SArchiveMemoryWriter(memoryStream, sharedManager);
        var reader = new SArchiveMemoryReader(memoryStream, sharedManager);

        writer.Write(testData);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var read = reader.ReadString();

        Assert.Equal(testData, read);
        Assert.Equal(expectedLength, memoryStream.Length);
    }

    [Fact]
    public void BasicArchive_ReallyLongString()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, sharedManager);
        var reader = new SArchiveMemoryReader(memoryStream, sharedManager);

        var sb = new StringBuilder();

        for (int i = 0; i < 100; ++i)
        {
            sb.Append("This is a really long string that should be written to the archive.\n");
        }

        var asStr = sb.ToString();

        writer.Write(asStr);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadString();
        Assert.Equal(asStr, read);
    }

    // Apparently it is not even possible to allocate this much memory...
    /*[Fact]
    public void BasicArchive_StringMemoryExhaustionTesting()
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, sharedManager);
        var reader = new SArchiveMemoryReader(memoryStream, sharedManager);

        var sb = new StringBuilder(int.MaxValue);

        while (sb.Length < int.MaxValue)
        {
            sb.Append("A");
        }

        var asStr = sb.ToString();

        writer.Write(asStr);
        memoryStream.Seek(0, SeekOrigin.Begin);

        var read = reader.ReadString();
        Assert.Equal(asStr, read);
    }*/

    [Theory]
    [InlineData("", 1)]
    [InlineData(null, 1)]
    [InlineData("Hello World!", 13)]
    [InlineData("This is a much longer string. That even has \0null embedded in it, take that!", 78)]
    public void BasicArchive_MemoryAndStreamVariantWorkTogether(string? testData, int expectedLength)
    {
        var memoryStream = new MemoryStream(expectedLength * 2);
        var writer1 = new SArchiveMemoryWriter(memoryStream, sharedManager);
        var writer2 = new SArchiveWriteStream(memoryStream, sharedManager);
        var reader = new SArchiveMemoryReader(memoryStream, sharedManager);

        writer1.Write(testData);

        memoryStream.Seek(0, SeekOrigin.Begin);
        var read = reader.ReadString();

        Assert.Equal(testData, read);
        Assert.Equal(expectedLength, memoryStream.Length);

        memoryStream.Seek(0, SeekOrigin.Begin);

        var data = memoryStream.ToArray();
        memoryStream.SetLength(0);

        writer2.Write(testData);

        memoryStream.Seek(0, SeekOrigin.Begin);
        read = reader.ReadString();

        Assert.Equal(testData, read);
        Assert.Equal(expectedLength, memoryStream.Length);

        Assert.Equal(data, memoryStream.ToArray());
    }

    [Theory]
    [InlineData(ArchiveObjectType.Byte, true, false, 1)]
    [InlineData(ArchiveObjectType.Int64, true, false, 3)]
    [InlineData(ArchiveObjectType.StartOfCustomTypes, true, false, 320)]
    [InlineData(ArchiveObjectType.LastValidObjectType, true, false, 1)]
    [InlineData(ArchiveObjectType.Byte, false, false, 1)]
    [InlineData(ArchiveObjectType.Byte, false, true, 320)]
    [InlineData(ArchiveObjectType.Byte, true, true, 320)]
    [InlineData(ArchiveObjectType.Byte, true, true, 1)]
    [InlineData(ArchiveObjectType.Dictionary, true, false, ushort.MaxValue)]
    public void BasicArchive_HeaderWritingAndReading(ArchiveObjectType type, bool canBeReference, bool isNull,
        ushort version)
    {
        var memoryStream = new MemoryStream();
        var writer = new SArchiveMemoryWriter(memoryStream, sharedManager);
        var reader = new SArchiveMemoryReader(memoryStream, sharedManager);

        writer.WriteObjectHeader(type, canBeReference, isNull, version);

        // Write placeholder object reference id as the plain header write doesn't write it.
        if (canBeReference && !isNull)
            writer.Write(0);

        memoryStream.Seek(0, SeekOrigin.Begin);
        reader.ReadObjectHeader(out var readType, out var referenceId, out var readIsNull, out var readVersion);

        Assert.Equal(type, readType);

        if (canBeReference && !isNull)
        {
            Assert.Equal(0, referenceId);
        }
        else
        {
            Assert.Equal(-1, referenceId);
        }

        Assert.Equal(isNull, readIsNull);

        if (!isNull)
        {
            Assert.Equal(version, readVersion);
        }
        else
        {
            Assert.Equal(0, readVersion);
        }
    }
}
