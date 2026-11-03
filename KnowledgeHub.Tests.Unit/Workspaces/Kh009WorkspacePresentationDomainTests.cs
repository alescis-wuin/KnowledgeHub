using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Tests.Unit.Workspaces;

public sealed class Kh009WorkspacePresentationDomainTests
{
    private static readonly Guid WorkspaceA = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid WorkspaceB = Guid.Parse("20000000-0000-4000-8000-000000000002");
    private static readonly Guid Actor = Guid.Parse("30000000-0000-4000-8000-000000000003");
    private static readonly DateTime Now = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string Key = new('1', 64);
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void Branding_defaults_validate_and_normalize_colors()
    {
        var palette = WorkspaceBrandingColors.Default();
        var branding = new WorkspaceBranding(WorkspaceA, "  Example  ", palette);
        Assert.Equal(WorkspaceA, branding.WorkspaceId);
        Assert.Equal("Example", branding.DisplayName);
        Assert.Equal("#2563EB", branding.PrimaryColor);
        Assert.Equal("#FFFFFF", branding.SurfaceColor);
        Assert.Equal(1, branding.Version);
        Assert.Null(branding.LogoAssetId);
        Assert.Null(branding.BannerAssetId);

        var normalized = Palette(primary: "#2563eb");
        Assert.Equal("#2563EB", normalized.PrimaryColor);
        Assert.Equal("#0F172A", normalized.TextColor);
    }

    [Theory]
    [InlineData("#123")]
    [InlineData("#12345678")]
    [InlineData("1234567")]
    [InlineData("#GGGGGG")]
    [InlineData(" #123456")]
    [InlineData("red")]
    public void Branding_rejects_invalid_color(string value) =>
        Assert.Throws<ArgumentException>(() => Palette(primary: value));

    [Fact]
    public void Branding_rejects_unreadable_color_pairs()
    {
        Assert.Throws<ArgumentException>(() => Palette(text: "#FFFFFF"));
        Assert.Throws<ArgumentException>(() => Palette(onPrimary: "#2563EB"));
        Assert.Throws<ArgumentException>(() => Palette(surface: "#0F172A"));
    }

    [Fact]
    public void Branding_rejects_invalid_identity_and_display_name()
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(Guid.Empty, "A", Palette()));
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(WorkspaceA, " ", Palette()));
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(WorkspaceA, "Bad\nName", Palette()));
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(WorkspaceA, new string('a', 151), Palette()));
        Assert.Throws<ArgumentNullException>(() => new WorkspaceBranding(WorkspaceA, "A", null!));
    }

    [Fact]
    public void Branding_references_only_owned_purpose_correct_assets()
    {
        var logo = Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO);
        var banner = Asset(WorkspaceA, WorkspaceAssetPurpose.BANNER);
        var branding = new WorkspaceBranding(WorkspaceA, "A", Palette(), logo, banner);
        Assert.Equal(logo.Id, branding.LogoAssetId);
        Assert.Equal(banner.Id, branding.BannerAssetId);

        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(
            WorkspaceA, "A", Palette(), Asset(WorkspaceB, WorkspaceAssetPurpose.LOGO)));
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(WorkspaceA, "A", Palette(), banner));
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(WorkspaceA, "A", Palette(), null, logo));
        Assert.Throws<ArgumentException>(() => new WorkspaceBranding(
            WorkspaceA, "A", Palette(), null, Asset(WorkspaceB, WorkspaceAssetPurpose.BANNER)));
    }

    [Fact]
    public void Branding_replace_checks_version_before_mutation()
    {
        var branding = new WorkspaceBranding(WorkspaceA, "Before", Palette());
        var logo = Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO);
        Assert.Throws<InvalidOperationException>(() => branding.Replace("After", Palette(), logo, null, 2));
        Assert.Equal("Before", branding.DisplayName);
        Assert.Null(branding.LogoAssetId);
        Assert.Throws<ArgumentException>(() => branding.Replace("Bad\n", Palette(), null, null, 1));
        Assert.Equal(1, branding.Version);

        branding.Replace("After", Palette(), logo, null, 1);
        Assert.Equal(2, branding.Version);
        Assert.Equal("After", branding.DisplayName);
        Assert.Equal(logo.Id, branding.LogoAssetId);
        Assert.Throws<InvalidOperationException>(() => branding.Replace("Stale", Palette(), null, null, 1));
    }

    [Fact]
    public void Branding_fails_on_version_overflow()
    {
        var branding = new WorkspaceBranding(WorkspaceA, "A", Palette());
        typeof(WorkspaceBranding).GetProperty(nameof(WorkspaceBranding.Version))!
            .SetValue(branding, long.MaxValue);
        Assert.Throws<InvalidOperationException>(() => branding.Replace("B", Palette(), null, null, long.MaxValue));
    }

    [Fact]
    public void Text_section_validates_body_and_never_exposes_mutable_links()
    {
        var section = Text(WorkspaceA, 0);
        Assert.NotEqual(Guid.Empty, section.Id);
        Assert.Equal(WorkspaceSectionType.TEXT, section.Type);
        Assert.Equal("Content", section.Body);
        Assert.Empty(section.Links);
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.TEXT, "Title", null, 0));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.TEXT, "Title", "Bad\nBody", 0));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.TEXT, "Title", new string('a', 4001), 0));
    }

    [Fact]
    public void Links_section_requires_only_typed_internal_navigation()
    {
        var links = new[]
        {
            new WorkspaceHomeLink(" Documents ", WorkspaceHomeLinkTarget.DOCUMENTS),
            new WorkspaceHomeLink("Conversation", WorkspaceHomeLinkTarget.CHAT)
        };
        var section = new WorkspaceHomeSection(WorkspaceA, WorkspaceSectionType.LINKS, "Links", null, 0, links);
        Assert.Null(section.Body);
        Assert.Equal("Documents", section.Links[0].Label);
        Assert.Equal(WorkspaceHomeLinkTarget.CHAT, section.Links[1].Target);
        Assert.NotSame(links, section.Links);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<WorkspaceHomeLink>)section.Links)[0] = new WorkspaceHomeLink("Other", WorkspaceHomeLinkTarget.SOURCES));

        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceHomeLink("A", (WorkspaceHomeLinkTarget)999));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeLink("bad\n", WorkspaceHomeLinkTarget.CHAT));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeLink(new string('a', 101), WorkspaceHomeLinkTarget.CHAT));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.LINKS, "Links", null, 0));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.LINKS, "Links", "body", 0, links));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.LINKS, "Links", null, 0,
            Enumerable.Range(0, 11).Select(i => new WorkspaceHomeLink($"Link {i}", WorkspaceHomeLinkTarget.CHAT))));
    }

    [Fact]
    public void Links_section_allows_the_documented_maximum_of_ten_links()
    {
        var links = Enumerable.Range(0, 10)
            .Select(i => new WorkspaceHomeLink($"Link {i}", WorkspaceHomeLinkTarget.CHAT));
        var section = new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.LINKS, "Navigation", null, 0, links);
        Assert.Equal(10, section.Links.Count);
    }

    [Fact]
    public void Stats_section_does_not_accept_custom_data_or_links()
    {
        var section = new WorkspaceHomeSection(WorkspaceA, WorkspaceSectionType.STATS, "Stats", null, 0);
        Assert.Empty(section.Links);
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.STATS, "Stats", "SELECT * FROM Users", 0));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.STATS, "Stats", null, 0,
            [new WorkspaceHomeLink("Source", WorkspaceHomeLinkTarget.SOURCES)]));
    }

    [Fact]
    public void Section_rejects_unknown_types_missing_tenant_and_invalid_positions()
    {
        Assert.Throws<ArgumentException>(() => Text(Guid.Empty, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Text(WorkspaceA, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Text(WorkspaceA, 20));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceHomeSection(
            WorkspaceA, (WorkspaceSectionType)8, "Bad", "Content", 0));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomeSection(
            WorkspaceA, WorkspaceSectionType.TEXT, new string('a', 151), "Content", 0));
    }

    [Fact]
    public void Homepage_accepts_twenty_sections_and_rejects_twenty_one()
    {
        var twenty = Enumerable.Range(0, 20).Select(i => Text(WorkspaceA, i)).ToArray();
        var home = Homepage(twenty);
        Assert.Equal(20, home.Sections.Count);
        Assert.Equal(1, home.Version);
        Assert.NotEqual(Guid.Empty, home.WorkspaceId);
        Assert.Throws<ArgumentException>(() => Homepage(
            Enumerable.Range(0, 21).Select(i => Text(WorkspaceA, i % 20))));
    }

    [Fact]
    public void Homepage_rejects_cross_tenant_sections_duplicate_ids_and_order_gaps()
    {
        var first = Text(WorkspaceA, 0);
        Assert.Throws<ArgumentException>(() => Homepage([first, first]));
        Assert.Throws<ArgumentException>(() => Homepage([Text(WorkspaceA, 0), Text(WorkspaceA, 0)]));
        Assert.Throws<ArgumentException>(() => Homepage([Text(WorkspaceA, 1)]));
        Assert.Throws<ArgumentException>(() => Homepage([Text(WorkspaceB, 0)]));
    }

    [Fact]
    public void Homepage_validates_labels_and_optional_text_limits()
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceHomepage(Guid.Empty, "Title"));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomepage(WorkspaceA, " "));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomepage(WorkspaceA, new string('a', 151)));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomepage(WorkspaceA, "Title", new string('a', 301)));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomepage(WorkspaceA, "Title", null, new string('a', 2001)));
        Assert.Throws<ArgumentException>(() => new WorkspaceHomepage(WorkspaceA, "Title", "Bad\n", null));
        var home = new WorkspaceHomepage(WorkspaceA, "  Home  ", "   ", "   ");
        Assert.Equal("Home", home.HeaderTitle);
        Assert.Null(home.Subtitle);
        Assert.Null(home.WelcomeMessage);
    }

    [Fact]
    public void Homepage_replace_is_atomic_with_respect_to_validation_and_version()
    {
        var home = Homepage([Text(WorkspaceA, 0)]);
        Assert.Throws<InvalidOperationException>(() => home.Replace("Other", null, null, [], 2));
        Assert.Throws<ArgumentException>(() => home.Replace("Bad\n", null, null, [], 1));
        Assert.Throws<ArgumentException>(() => home.Replace("New", null, null, [Text(WorkspaceB, 0)], 1));
        Assert.Equal("Welcome", home.HeaderTitle);
        Assert.Single(home.Sections);
        Assert.Equal(1, home.Version);
        home.Replace("New", "Subtitle", "Message", [], 1);
        Assert.Equal(2, home.Version);
        Assert.Empty(home.Sections);
        Assert.Equal("New", home.HeaderTitle);
    }

    [Fact]
    public void Homepage_reorder_preserves_owned_ids_and_bumps_only_root_version()
    {
        var home = Homepage([Text(WorkspaceA, 0), Text(WorkspaceA, 1)]);
        var firstId = home.Sections[0].Id;
        var secondId = home.Sections[1].Id;
        home.Reorder([secondId, firstId], 1);
        Assert.Equal(2, home.Version);
        Assert.Equal(secondId, home.Sections[0].Id);
        Assert.Equal(0, home.Sections[0].SortOrder);
        Assert.Equal(firstId, home.Sections[1].Id);
        Assert.Equal(1, home.Sections[1].SortOrder);
        Assert.Throws<ArgumentException>(() => home.Reorder([firstId, firstId], 2));
        Assert.Throws<ArgumentException>(() => home.Reorder([firstId, Guid.NewGuid()], 2));
        Assert.Throws<ArgumentException>(() => home.Reorder([firstId], 2));
        Assert.Throws<InvalidOperationException>(() => home.Reorder([firstId, secondId], 1));
        Assert.Equal(2, home.Version);
    }

    [Fact]
    public void Homepage_rejects_version_overflow()
    {
        var home = Homepage([]);
        typeof(WorkspaceHomepage).GetProperty(nameof(WorkspaceHomepage.Version))!
            .SetValue(home, long.MaxValue);
        Assert.Throws<InvalidOperationException>(() => home.Replace("New", null, null, [], long.MaxValue));
    }

    [Theory]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    [InlineData("image/webp")]
    public void Asset_accepts_only_allowed_canonical_content_types(string mime)
    {
        var asset = Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, mime);
        Assert.Equal(mime, asset.ContentType);
        Assert.Equal(WorkspaceA, asset.WorkspaceId);
        Assert.Equal(Actor, asset.CreatedByUserId);
        Assert.Equal(Hash, asset.Sha256);
        Assert.Equal(Key, asset.StorageKey);
        Assert.Equal(Now, asset.CreatedAtUtc);
    }

    [Theory]
    [InlineData("image/svg+xml")]
    [InlineData("text/html")]
    [InlineData("IMAGE/PNG")]
    [InlineData("image/png; charset=utf-8")]
    [InlineData("")]
    public void Asset_rejects_unapproved_mime(string mime) =>
        Assert.Throws<ArgumentException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, mime));

    [Fact]
    public void Asset_rejects_untrusted_storage_identifiers()
    {
        Assert.Throws<ArgumentException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, key: "../../private"));
        Assert.Throws<ArgumentException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, key: new string('A', 64)));
        Assert.Throws<ArgumentException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, hash: new string('A', 64)));
        Assert.Throws<ArgumentException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, hash: "raw bytes"));
    }

    [Fact]
    public void Asset_enforces_logo_and_banner_byte_limits()
    {
        Assert.Equal(WorkspaceAsset.MaxLogoBytes,
            Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, length: WorkspaceAsset.MaxLogoBytes).Length);
        Assert.Equal(WorkspaceAsset.MaxBannerBytes,
            Asset(WorkspaceA, WorkspaceAssetPurpose.BANNER, length: WorkspaceAsset.MaxBannerBytes).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, length: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(
            WorkspaceA, WorkspaceAssetPurpose.LOGO, length: WorkspaceAsset.MaxLogoBytes + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(
            WorkspaceA, WorkspaceAssetPurpose.BANNER, length: WorkspaceAsset.MaxBannerBytes + 1));
    }

    [Fact]
    public void Asset_bounds_dimensions_and_area_without_integer_overflow()
    {
        Assert.Equal(8192, Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, width: 8192, height: 2048).PixelWidth);
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, width: 8193));
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, height: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, width: 8192, height: 8192));
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO, width: int.MaxValue));
    }

    [Fact]
    public void Asset_requires_valid_ids_enums_and_utc_timestamp()
    {
        Assert.Throws<ArgumentException>(() => Asset(Guid.Empty, WorkspaceAssetPurpose.LOGO));
        Assert.Throws<ArgumentException>(() => new WorkspaceAsset(
            WorkspaceA, Guid.Empty, WorkspaceAssetPurpose.LOGO, "image/png", Key, Hash, 1, 1, 1, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Asset(WorkspaceA, (WorkspaceAssetPurpose)8));
        Assert.Throws<ArgumentException>(() => Asset(WorkspaceA, WorkspaceAssetPurpose.LOGO,
            createdAtUtc: DateTime.SpecifyKind(Now, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Audit_records_only_structured_metadata_with_trusted_actor_identity()
    {
        var audit = WorkspaceAuditEvent.ForActor(
            WorkspaceA, Actor, WorkspaceAuditEventType.MEMBER_ROLE_CHANGED,
            WorkspaceAuditTargetType.MEMBER, Actor, WorkspaceAuditOutcome.SUCCEEDED, Now);
        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(WorkspaceA, audit.WorkspaceId);
        Assert.Equal(Actor, audit.ActorUserId);
        Assert.Equal(WorkspaceAuditOutcome.SUCCEEDED, audit.Outcome);
        Assert.Equal(Now, audit.OccurredAtUtc);
        Assert.Equal("ActorUserId,EventType,Id,OccurredAtUtc,Outcome,TargetId,TargetType,WorkspaceId",
            string.Join(",", typeof(WorkspaceAuditEvent).GetProperties().Select(p => p.Name)
                .OrderBy(name => name, StringComparer.Ordinal)));
        Assert.DoesNotContain(typeof(WorkspaceAuditEvent).GetMethods(), method =>
            method.IsPublic && method.DeclaringType == typeof(WorkspaceAuditEvent) &&
            method.Name.StartsWith("set_", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(WorkspaceAuditEventType.WORKSPACE_CREATED, WorkspaceAuditTargetType.WORKSPACE)]
    [InlineData(WorkspaceAuditEventType.WORKSPACE_UPDATED, WorkspaceAuditTargetType.WORKSPACE)]
    [InlineData(WorkspaceAuditEventType.WORKSPACE_ARCHIVED, WorkspaceAuditTargetType.WORKSPACE)]
    [InlineData(WorkspaceAuditEventType.WORKSPACE_RESTORED, WorkspaceAuditTargetType.WORKSPACE)]
    [InlineData(WorkspaceAuditEventType.OWNER_TRANSFERRED, WorkspaceAuditTargetType.MEMBER)]
    [InlineData(WorkspaceAuditEventType.MEMBER_ROLE_CHANGED, WorkspaceAuditTargetType.MEMBER)]
    [InlineData(WorkspaceAuditEventType.MEMBER_SUSPENDED, WorkspaceAuditTargetType.MEMBER)]
    [InlineData(WorkspaceAuditEventType.MEMBER_REACTIVATED, WorkspaceAuditTargetType.MEMBER)]
    [InlineData(WorkspaceAuditEventType.INVITATION_CREATED, WorkspaceAuditTargetType.INVITATION)]
    [InlineData(WorkspaceAuditEventType.INVITATION_ACCEPTED, WorkspaceAuditTargetType.INVITATION)]
    [InlineData(WorkspaceAuditEventType.INVITATION_REVOKED, WorkspaceAuditTargetType.INVITATION)]
    [InlineData(WorkspaceAuditEventType.BRANDING_UPDATED, WorkspaceAuditTargetType.BRANDING)]
    [InlineData(WorkspaceAuditEventType.HOMEPAGE_UPDATED, WorkspaceAuditTargetType.HOMEPAGE)]
    [InlineData(WorkspaceAuditEventType.ASSET_CREATED, WorkspaceAuditTargetType.ASSET)]
    [InlineData(WorkspaceAuditEventType.ASSET_DELETED, WorkspaceAuditTargetType.ASSET)]
    [InlineData(WorkspaceAuditEventType.ORGANIZATION_VERIFICATION_CHANGED, WorkspaceAuditTargetType.WORKSPACE)]
    public void Audit_event_type_and_target_type_are_consistent(
        WorkspaceAuditEventType eventType, WorkspaceAuditTargetType targetType)
    {
        var targetId = targetType is WorkspaceAuditTargetType.WORKSPACE or
            WorkspaceAuditTargetType.BRANDING or WorkspaceAuditTargetType.HOMEPAGE
            ? WorkspaceA : Guid.NewGuid();
        var ev = WorkspaceAuditEvent.ForActor(
            WorkspaceA, Actor, eventType, targetType, targetId, WorkspaceAuditOutcome.SUCCEEDED, Now);
        Assert.Equal(targetId, ev.TargetId);
        Assert.Equal(eventType, ev.EventType);
    }

    [Fact]
    public void Audit_supports_separate_system_factory()
    {
        var audit = WorkspaceAuditEvent.ForSystem(
            WorkspaceA, WorkspaceAuditEventType.ORGANIZATION_VERIFICATION_CHANGED,
            WorkspaceAuditTargetType.WORKSPACE, WorkspaceA, WorkspaceAuditOutcome.SUCCEEDED, Now);
        Assert.Null(audit.ActorUserId);
        Assert.Equal(WorkspaceA, audit.TargetId);
    }

    [Fact]
    public void Audit_rejects_wrong_target_or_undocumented_enum_values()
    {
        Assert.Throws<ArgumentException>(() => WorkspaceAuditEvent.ForActor(
            WorkspaceA, Actor, WorkspaceAuditEventType.BRANDING_UPDATED,
            WorkspaceAuditTargetType.BRANDING, WorkspaceB, WorkspaceAuditOutcome.SUCCEEDED, Now));
        Assert.Throws<ArgumentException>(() => WorkspaceAuditEvent.ForActor(
            WorkspaceA, Actor, WorkspaceAuditEventType.ASSET_CREATED,
            WorkspaceAuditTargetType.WORKSPACE, WorkspaceA, WorkspaceAuditOutcome.SUCCEEDED, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceAuditEvent.ForActor(
            WorkspaceA, Actor, (WorkspaceAuditEventType)999,
            WorkspaceAuditTargetType.WORKSPACE, WorkspaceA, WorkspaceAuditOutcome.SUCCEEDED, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkspaceAuditEvent.ForActor(
            WorkspaceA, Actor, WorkspaceAuditEventType.WORKSPACE_UPDATED,
            WorkspaceAuditTargetType.WORKSPACE, WorkspaceA, (WorkspaceAuditOutcome)9, Now));
        Assert.Throws<ArgumentException>(() => WorkspaceAuditEvent.ForActor(
            WorkspaceA, Guid.Empty, WorkspaceAuditEventType.WORKSPACE_UPDATED,
            WorkspaceAuditTargetType.WORKSPACE, WorkspaceA, WorkspaceAuditOutcome.SUCCEEDED, Now));
        Assert.Throws<ArgumentException>(() => WorkspaceAuditEvent.ForSystem(
            WorkspaceA, WorkspaceAuditEventType.WORKSPACE_UPDATED,
            WorkspaceAuditTargetType.WORKSPACE, Guid.Empty, WorkspaceAuditOutcome.SUCCEEDED, Now));
        Assert.Throws<ArgumentException>(() => WorkspaceAuditEvent.ForSystem(
            WorkspaceA, WorkspaceAuditEventType.WORKSPACE_UPDATED,
            WorkspaceAuditTargetType.WORKSPACE, WorkspaceA, WorkspaceAuditOutcome.SUCCEEDED,
            DateTime.SpecifyKind(Now, DateTimeKind.Local)));
    }

    [Fact]
    public void Five_presentation_entities_are_in_production_model_after_migration()
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql("Host=localhost;Database=not_connected;Username=test;Password=test")
            .Options;
        using var db = new KnowledgeDbContext(options);
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceBranding)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceHomepage)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceHomeSection)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceAsset)));
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceAuditEvent)));
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static WorkspaceBrandingColors Palette(
        string primary = "#2563EB",
        string surface = "#FFFFFF",
        string text = "#0F172A",
        string onPrimary = "#FFFFFF") =>
        new(primary, "#64748B", "#0D9488", "#F8FAFC", surface, text, onPrimary);

    private static WorkspaceAsset Asset(
        Guid workspaceId,
        WorkspaceAssetPurpose purpose,
        string mime = "image/png",
        string? key = null,
        string? hash = null,
        long length = 1024,
        int width = 100,
        int height = 100,
        DateTime? createdAtUtc = null) =>
        new(workspaceId, Actor, purpose, mime, key ?? Key, hash ?? Hash,
            length, width, height, createdAtUtc ?? Now);

    private static WorkspaceHomeSection Text(Guid workspaceId, int order) =>
        new(workspaceId, WorkspaceSectionType.TEXT, "Title", "Content", order);

    private static WorkspaceHomepage Homepage(IEnumerable<WorkspaceHomeSection> sections) =>
        new(WorkspaceA, "Welcome", "Description", "Text", sections);
}
