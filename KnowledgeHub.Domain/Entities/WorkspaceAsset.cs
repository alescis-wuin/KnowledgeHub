namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Immutable metadata for private, server-verified raster data. This entity
/// neither opens a file nor authenticates the creator; the upload service must
/// re-encode, inspect content and derive identities from trusted sources.
/// </summary>
public sealed class WorkspaceAsset
{
    public const long MaxLogoBytes = 5L * 1024 * 1024;
    public const long MaxBannerBytes = 10L * 1024 * 1024;
    public const int MaxAxisPixels = 8192;
    public const long MaxAreaPixels = 16_777_216;

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public WorkspaceAssetPurpose Purpose { get; private set; }
    public string ContentType { get; private set; } = string.Empty;
    public string StorageKey { get; private set; } = string.Empty;
    public string Sha256 { get; private set; } = string.Empty;
    public long Length { get; private set; }
    public int PixelWidth { get; private set; }
    public int PixelHeight { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public Guid CreatedByUserId { get; private set; }

    private WorkspaceAsset() { } // EF Core

    public WorkspaceAsset(
        Guid workspaceId,
        Guid createdByUserId,
        WorkspaceAssetPurpose purpose,
        string contentType,
        string storageKey,
        string sha256,
        long length,
        int pixelWidth,
        int pixelHeight,
        DateTime createdAtUtc)
    {
        WorkspacePresentationValidation.RequireId(workspaceId, nameof(workspaceId));
        WorkspacePresentationValidation.RequireId(createdByUserId, nameof(createdByUserId));
        if (!Enum.IsDefined(purpose))
            throw new ArgumentOutOfRangeException(nameof(purpose));
        if (contentType is not ("image/png" or "image/jpeg" or "image/webp"))
            throw new ArgumentException("Only canonical raster MIME types are allowed.", nameof(contentType));
        if (!WorkspacePresentationValidation.IsLowerHex64(storageKey))
            throw new ArgumentException("Storage key must be 64 lowercase hex characters.", nameof(storageKey));
        if (!WorkspacePresentationValidation.IsLowerHex64(sha256))
            throw new ArgumentException("SHA-256 must be 64 lowercase hex characters.", nameof(sha256));
        if (length < 1 || length > (purpose == WorkspaceAssetPurpose.LOGO ? MaxLogoBytes : MaxBannerBytes))
            throw new ArgumentOutOfRangeException(nameof(length));
        if (pixelWidth is < 1 or > MaxAxisPixels ||
            pixelHeight is < 1 or > MaxAxisPixels ||
            (long)pixelWidth * pixelHeight > MaxAreaPixels)
            throw new ArgumentOutOfRangeException(nameof(pixelWidth), "Invalid raster dimensions.");
        Workspace.RequireUtc(createdAtUtc, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        CreatedByUserId = createdByUserId;
        Purpose = purpose;
        ContentType = contentType;
        StorageKey = storageKey;
        Sha256 = sha256;
        Length = length;
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        CreatedAtUtc = createdAtUtc;
    }
}
