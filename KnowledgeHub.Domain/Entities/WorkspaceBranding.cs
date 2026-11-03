namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Tenant-owned visual identity. The caller must load and authorize assets
/// from trusted persistence; these checks alone do not prove permission.
/// </summary>
public sealed class WorkspaceBranding
{
    public Guid WorkspaceId { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public Guid? LogoAssetId { get; private set; }
    public Guid? BannerAssetId { get; private set; }
    public string PrimaryColor { get; private set; } = string.Empty;
    public string SecondaryColor { get; private set; } = string.Empty;
    public string AccentColor { get; private set; } = string.Empty;
    public string BackgroundColor { get; private set; } = string.Empty;
    public string SurfaceColor { get; private set; } = string.Empty;
    public string TextColor { get; private set; } = string.Empty;
    public string TextOnPrimaryColor { get; private set; } = string.Empty;
    public long Version { get; private set; }

    private WorkspaceBranding() { } // EF Core

    public WorkspaceBranding(
        Guid workspaceId,
        string displayName,
        WorkspaceBrandingColors colors,
        WorkspaceAsset? logo = null,
        WorkspaceAsset? banner = null)
    {
        WorkspacePresentationValidation.RequireId(workspaceId, nameof(workspaceId));
        ArgumentNullException.ThrowIfNull(colors);
        var name = WorkspacePresentationValidation.RequiredText(displayName, 150, nameof(displayName));
        ValidateAssets(workspaceId, logo, banner);

        WorkspaceId = workspaceId;
        DisplayName = name;
        LogoAssetId = logo?.Id;
        BannerAssetId = banner?.Id;
        SetColors(colors);
        Version = 1;
    }

    public void Replace(
        string displayName,
        WorkspaceBrandingColors colors,
        WorkspaceAsset? logo,
        WorkspaceAsset? banner,
        long expectedVersion)
    {
        WorkspacePresentationValidation.RequireVersion(Version, expectedVersion);
        ArgumentNullException.ThrowIfNull(colors);
        var name = WorkspacePresentationValidation.RequiredText(displayName, 150, nameof(displayName));
        ValidateAssets(WorkspaceId, logo, banner);

        DisplayName = name;
        LogoAssetId = logo?.Id;
        BannerAssetId = banner?.Id;
        SetColors(colors);
        Version++;
    }

    private void SetColors(WorkspaceBrandingColors colors)
    {
        PrimaryColor = colors.PrimaryColor;
        SecondaryColor = colors.SecondaryColor;
        AccentColor = colors.AccentColor;
        BackgroundColor = colors.BackgroundColor;
        SurfaceColor = colors.SurfaceColor;
        TextColor = colors.TextColor;
        TextOnPrimaryColor = colors.TextOnPrimaryColor;
    }

    private static void ValidateAssets(Guid workspaceId, WorkspaceAsset? logo, WorkspaceAsset? banner)
    {
        if (logo is not null &&
            (logo.WorkspaceId != workspaceId || logo.Purpose != WorkspaceAssetPurpose.LOGO))
            throw new ArgumentException("Logo belongs to another workspace or has the wrong purpose.", nameof(logo));
        if (banner is not null &&
            (banner.WorkspaceId != workspaceId || banner.Purpose != WorkspaceAssetPurpose.BANNER))
            throw new ArgumentException("Banner belongs to another workspace or has the wrong purpose.", nameof(banner));
        if (logo is not null && banner is not null && logo.Id == banner.Id)
            throw new ArgumentException("Logo and banner must reference distinct assets.");
    }
}
