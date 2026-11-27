using AuditLog.BLL.Models;
using AuditLog.BLL.Services;
using System.Security.Cryptography;
using System.Text.Json;

namespace AuditLog.Tests;

public sealed class AuditHashingServiceTests
{
    private readonly AuditHashingService _service = new();

    [Fact]
    public void CreateGenesisHash_ReturnsThirtyTwoZeroBytes()
    {
        Assert.Equal(new byte[32], _service.CreateGenesisHash());
    }

    [Fact]
    public void ComputeContentHash_CanonicalizesObjectPropertyOrderAndTimeZone()
    {
        var first = CreateEvent(
            """{"outer":{"b":2,"a":1},"items":[true,false]}""",
            new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.FromHours(2)));
        var equivalent = CreateEvent(
            """{"items":[true,false],"outer":{"a":1,"b":2}}""",
            new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(
            _service.ComputeContentHash(first),
            _service.ComputeContentHash(equivalent));
    }

    [Fact]
    public void ComputeContentHash_PreservesArrayOrder()
    {
        var first = CreateEvent("""{"items":[1,2]}""");
        var reordered = CreateEvent("""{"items":[2,1]}""");

        Assert.NotEqual(
            _service.ComputeContentHash(first),
            _service.ComputeContentHash(reordered));
    }

    [Fact]
    public void ComputeContentHash_RejectsUndefinedPayload()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _service.ComputeContentHash(new AuditEventData()));
    }

    [Fact]
    public void ComputeContentHash_RejectsNullEvent()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.ComputeContentHash(null!));
    }

    [Fact]
    public void ComputeRecordHash_HashesPreviousHashFollowedByContentHash()
    {
        var previousHash = Enumerable.Range(0, 32)
            .Select(value => (byte)value)
            .ToArray();
        var contentHash = Enumerable.Range(32, 32)
            .Select(value => (byte)value)
            .ToArray();

        var result = _service.ComputeRecordHash(previousHash, contentHash);
        var expected = SHA256.HashData(previousHash.Concat(contentHash).ToArray());

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ComputeRecordHash_RejectsInvalidHashLength(bool invalidPreviousHash)
    {
        var previousHash = new byte[32];
        var contentHash = new byte[32];

        if (invalidPreviousHash)
        {
            previousHash = new byte[31];
        }
        else
        {
            contentHash = new byte[31];
        }

        var exception = Assert.Throws<ArgumentException>(() =>
            _service.ComputeRecordHash(previousHash, contentHash));

        Assert.Equal(
            invalidPreviousHash
                ? "previousHash"
                : "contentHash",
            exception.ParamName);
    }

    [Fact]
    public void ComputeRecordHash_RejectsNullHashes()
    {
        Assert.Throws<ArgumentNullException>(() =>
            _service.ComputeRecordHash(null!, new byte[32]));
        Assert.Throws<ArgumentNullException>(() =>
            _service.ComputeRecordHash(new byte[32], null!));
    }

    [Fact]
    public void ToHex_ReturnsUppercaseHexadecimal()
    {
        Assert.Equal("00ABFF", _service.ToHex([0, 0xAB, 0xFF]));
    }

    [Fact]
    public void ToHex_RejectsNullHash()
    {
        Assert.Throws<ArgumentNullException>(() => _service.ToHex(null!));
    }

    private static AuditEventData CreateEvent(
        string payloadJson,
        DateTimeOffset? timestamp = null)
    {
        using var document = JsonDocument.Parse(payloadJson);

        return new AuditEventData
        {
            EventType = "ACCOUNT_VIEWED",
            ActorId = "USER001",
            ResourceType = "ACCOUNT",
            ResourceId = "ACCOUNT100",
            Payload = document.RootElement.Clone(),
            EventTimestampUtc = timestamp
                ?? new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero)
        };
    }
}
