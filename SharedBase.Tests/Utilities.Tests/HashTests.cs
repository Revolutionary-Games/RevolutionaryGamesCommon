namespace SharedBase.Tests.Utilities.Tests;

using System;
using System.Linq;
using SHA3.Net;
using Xunit;

public class HashTests
{
    [Fact]
    public void Sha3_IncrementalHashingWorksAsExpected()
    {
        var dataToHash = new byte[] { 12, 15, 17, 19, 100, 125, 150 };
        int incrementalSize = 3;

        var fullHasher = Sha3.Sha3256();

        var expectedHash = fullHasher.ComputeHash(dataToHash, 0, dataToHash.Length);

        var incrementalHasher = Sha3.Sha3256();

        foreach (var chunk in dataToHash.Chunk(incrementalSize))
        {
            incrementalHasher.TransformBlock(chunk, 0, chunk.Length, null, 0);
        }

        incrementalHasher.TransformFinalBlock(Array.Empty<byte>(), 0, 0);

        var incrementalHash = incrementalHasher.Hash ?? throw new Exception("No hash calculated");

        Assert.Equal(expectedHash, incrementalHash);
    }
}
