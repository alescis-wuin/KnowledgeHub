using System.Text;
using System.Text.Json;
using KnowledgeHub.Application.Verification;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Verification;

public sealed class ClaimProposalJsonParserTests
{
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly string Digest = new('a', 64);

    private static string ValidJson(string quote = "Texte exact") => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        claims = new[]
        {
            new
            {
                id = "c1",
                statement = "Proposition à vérifier",
                evidence = new[]
                {
                    new
                    {
                        documentId = DocumentId,
                        chunkId = ChunkId,
                        startUtf16 = 0,
                        lengthUtf16 = quote.Length,
                        quote,
                        chunkSha256 = Digest
                    }
                }
            }
        }
    });

    [Fact]
    public void ValidJson_ProducesTypedSnapshotWithoutSemanticVerdict()
    {
        var status = Parse(ValidJson(), out var batch);
        Assert.Equal(ClaimProposalParseStatus.Valid, status);
        Assert.NotNull(batch);
        Assert.Equal(1, batch.SchemaVersion);
        var claim = Assert.Single(batch.Claims);
        Assert.Equal("c1", claim.Id);
        Assert.Single(claim.Evidence);
        Assert.Equal(DocumentId, claim.Evidence[0].DocumentId);
        Assert.Equal(ChunkId, claim.Evidence[0].ChunkId);
    }

    [Theory]
    [InlineData("{}")] // Required fields missing.
    [InlineData("[]")]
    [InlineData("null")]
    [InlineData("{\"schemaVersion\":2,\"claims\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"claims\":[],\"verdict\":\"ANSWER\"}")]
    [InlineData("{\"schemaVersion\":1,\"schemaVersion\":1,\"claims\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"claims\":{}}")]
    [InlineData("{\"schemaVersion\":true,\"claims\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"claims\":[]}")]
    public void InvalidStructures_AreRejected(string input)
    {
        var status = Parse(input, out var batch);
        Assert.NotEqual(ClaimProposalParseStatus.Valid, status);
        Assert.Null(batch);
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"claims\":[")]
    [InlineData("{\"schemaVersion\":1,\"claims\":[]} trailing")]
    [InlineData("/* comment */ {\"schemaVersion\":1,\"claims\":[]}")]
    public void InvalidJson_IsRejected(string input)
    {
        Assert.Equal(ClaimProposalParseStatus.InvalidJson, Parse(input, out _));
    }

    [Fact]
    public void OversizedPayload_IsRejectedBeforeJsonParsing()
    {
        var input = new byte[ClaimProposalJsonParser.MaxJsonBytes + 1];
        Assert.Equal(
            ClaimProposalParseStatus.LimitExceeded,
            ClaimProposalJsonParser.TryParse(input, out var batch));
        Assert.Null(batch);
    }

    [Fact]
    public void DuplicateNestedProperty_IsRejected()
    {
        var json = ValidJson();
        json = json.Replace("\"id\":\"c1\"", "\"id\":\"c1\",\"id\":\"c2\"", StringComparison.Ordinal);
        Assert.Equal(ClaimProposalParseStatus.InvalidContract, Parse(json, out _));
    }

    [Fact]
    public void UnknownNestedProperty_IsRejected()
    {
        var json = ValidJson();
        json = json.Replace("\"startUtf16\":0", "\"startUtf16\":0,\"ignoreRules\":true", StringComparison.Ordinal);
        Assert.Equal(ClaimProposalParseStatus.InvalidContract, Parse(json, out _));
    }

    [Fact]
    public void DuplicateEvidenceReference_IsRejected()
    {
        using var source = JsonDocument.Parse(ValidJson());
        var item = source.RootElement.GetProperty("claims")[0].GetProperty("evidence")[0].GetRawText();
        var duplicate = $"{{\"schemaVersion\":1,\"claims\":[{{\"id\":\"c1\",\"statement\":\"fait\",\"evidence\":[{item},{item}]}}]}}";
        Assert.Equal(ClaimProposalParseStatus.InvalidContract, Parse(duplicate, out _));
    }

    [Fact]
    public void DuplicateClaimId_IsRejected()
    {
        using var source = JsonDocument.Parse(ValidJson());
        var item = source.RootElement.GetProperty("claims")[0].GetRawText();
        var duplicate = $"{{\"schemaVersion\":1,\"claims\":[{item},{item}]}}";
        Assert.Equal(ClaimProposalParseStatus.InvalidContract, Parse(duplicate, out _));
    }

    [Fact]
    public void MoreThanTwelveClaims_IsRejected()
    {
        using var source = JsonDocument.Parse(ValidJson());
        var item = source.RootElement.GetProperty("claims")[0].GetRawText();
        var tooMany = $"{{\"schemaVersion\":1,\"claims\":[{string.Join(',', Enumerable.Repeat(item, 13))}]}}";
        Assert.Equal(ClaimProposalParseStatus.LimitExceeded, Parse(tooMany, out _));
    }

    [Fact]
    public void ExcessiveQuoteLength_IsRejected()
    {
        Assert.Equal(
            ClaimProposalParseStatus.InvalidContract,
            Parse(ValidJson(new string('x', ClaimProposalJsonParser.MaxQuoteUtf16 + 1)), out _));
    }

    [Fact]
    public void MissingOrInvalidHash_IsRejected()
    {
        var input = ValidJson();
        Assert.Equal(ClaimProposalParseStatus.InvalidContract,
            Parse(input.Replace(Digest, new string('A', 64), StringComparison.Ordinal), out _));
        Assert.Equal(ClaimProposalParseStatus.InvalidContract,
            Parse(input.Replace(Digest, "not-a-hash", StringComparison.Ordinal), out _));
    }

    [Fact]
    public void NonIntegerOffsets_AreRejected()
    {
        var input = ValidJson();
        Assert.Equal(ClaimProposalParseStatus.InvalidContract,
            Parse(input.Replace("\"startUtf16\":0", "\"startUtf16\":0.5", StringComparison.Ordinal), out _));
    }

    [Fact]
    public void EmojiQuote_IsCountedInUtf16CodeUnits()
    {
        var json = ValidJson("\ud83d\ude80");
        var status = Parse(json, out var batch);
        Assert.Equal(ClaimProposalParseStatus.Valid, status);
        Assert.Equal(2, Assert.Single(Assert.Single(batch!.Claims).Evidence).LengthUtf16);
    }

    [Fact]
    public void InvalidBytes_AreNotAccepted()
    {
        byte[] invalid = [0xff, 0xfe, 0xff];
        Assert.NotEqual(ClaimProposalParseStatus.Valid,
            ClaimProposalJsonParser.TryParse(invalid, out var batch));
        Assert.Null(batch);
    }

    private static ClaimProposalParseStatus Parse(string text, out ClaimProposalBatch? batch) =>
        ClaimProposalJsonParser.TryParse(Encoding.UTF8.GetBytes(text), out batch);
}
