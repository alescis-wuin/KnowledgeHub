using System.Text.Json;

namespace KnowledgeHub.Application.Verification;

public enum ClaimProposalParseStatus
{
    Valid = 0,
    InvalidJson = 1,
    InvalidContract = 2,
    LimitExceeded = 3
}

/// <summary>
/// Strict, bounded parsing of untrusted model output, with no error detail
/// derived from private user or document text.
/// </summary>
public static class ClaimProposalJsonParser
{
    public const int MaxJsonBytes = 64 * 1024;
    public const int MaxClaims = 12;
    public const int MaxEvidencePerClaim = 3;
    public const int MaxQuoteUtf16 = 600;
    public const int MaxStatementUtf16 = 2000;

    private static readonly string[] BatchProperties = ["schemaVersion", "claims"];
    private static readonly string[] ClaimProperties = ["id", "statement", "evidence"];
    private static readonly string[] EvidenceProperties =
    [
        "documentId", "chunkId", "startUtf16", "lengthUtf16", "quote", "chunkSha256"
    ];

    public static ClaimProposalParseStatus TryParse(
        ReadOnlySpan<byte> utf8Json,
        out ClaimProposalBatch? batch)
    {
        batch = null;
        if (utf8Json.Length > MaxJsonBytes)
        {
            return ClaimProposalParseStatus.LimitExceeded;
        }

        try
        {
            using var json = JsonDocument.Parse(utf8Json.ToArray(), new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 16
            });

            var root = json.RootElement;
            if (!HasExactProperties(root, BatchProperties) ||
                !TryInt32(root.GetProperty("schemaVersion"), out var version) ||
                version != 1 || root.GetProperty("claims").ValueKind != JsonValueKind.Array)
            {
                return ClaimProposalParseStatus.InvalidContract;
            }

            var values = root.GetProperty("claims");
            if (values.GetArrayLength() is < 1 or > MaxClaims)
            {
                return ClaimProposalParseStatus.LimitExceeded;
            }

            var claims = new List<ClaimProposal>();
            var uniqueIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in values.EnumerateArray())
            {
                if (!HasExactProperties(item, ClaimProperties) ||
                    !TryString(item.GetProperty("id"), 1, 32, out var id) ||
                    !id.All(IsIdentifierCharacter) || !uniqueIds.Add(id) ||
                    !TryString(item.GetProperty("statement"), 1, MaxStatementUtf16,
                        out var statement) || string.IsNullOrWhiteSpace(statement) ||
                    item.GetProperty("evidence").ValueKind != JsonValueKind.Array)
                {
                    return ClaimProposalParseStatus.InvalidContract;
                }

                var refs = item.GetProperty("evidence");
                if (refs.GetArrayLength() is < 1 or > MaxEvidencePerClaim)
                {
                    return ClaimProposalParseStatus.LimitExceeded;
                }

                var citations = new List<EvidencePointer>();
                var uniqueCitations = new HashSet<(Guid, Guid, int, int)>();
                foreach (var reference in refs.EnumerateArray())
                {
                    if (!HasExactProperties(reference, EvidenceProperties) ||
                        !TryGuid(reference.GetProperty("documentId"), out var documentId) ||
                        !TryGuid(reference.GetProperty("chunkId"), out var chunkId) ||
                        !TryInt32(reference.GetProperty("startUtf16"), out var start) ||
                        !TryInt32(reference.GetProperty("lengthUtf16"), out var length) ||
                        start < 0 || length is < 1 or > MaxQuoteUtf16 ||
                        !TryString(reference.GetProperty("quote"), 1, MaxQuoteUtf16,
                            out var quote) || quote.Length != length ||
                        !TryString(reference.GetProperty("chunkSha256"), 64, 64,
                            out var digest) || !digest.All(IsLowerHex) ||
                        !uniqueCitations.Add((documentId, chunkId, start, length)))
                    {
                        return ClaimProposalParseStatus.InvalidContract;
                    }

                    citations.Add(new EvidencePointer(
                        documentId, chunkId, start, length, quote, digest));
                }

                claims.Add(new ClaimProposal(id, statement, citations.AsReadOnly()));
            }

            batch = new ClaimProposalBatch(version, claims.AsReadOnly());
            return ClaimProposalParseStatus.Valid;
        }
        catch (JsonException)
        {
            return ClaimProposalParseStatus.InvalidJson;
        }
    }

    private static bool HasExactProperties(JsonElement item, IReadOnlyCollection<string> names)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var found = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in item.EnumerateObject())
        {
            if (!names.Contains(property.Name) || !found.Add(property.Name))
            {
                return false;
            }
        }

        return found.Count == names.Count;
    }

    private static bool TryInt32(JsonElement element, out int value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Number && element.TryGetInt32(out value);
    }

    private static bool TryGuid(JsonElement element, out Guid value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.String &&
            Guid.TryParseExact(element.GetString(), "D", out value) && value != Guid.Empty;
    }

    private static bool TryString(JsonElement element, int minimum, int maximum,
        out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (text is null || text.Length < minimum || text.Length > maximum)
        {
            return false;
        }

        value = text;
        return true;
    }

    private static bool IsIdentifierCharacter(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or
            >= '0' and <= '9' or '_' or '-';

    private static bool IsLowerHex(char value) =>
        value is >= '0' and <= '9' or >= 'a' and <= 'f';
}
