using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;

namespace KnowledgeHub.Tests.Unit.Workspaces;

public sealed class Kh009WorkspaceDomainTests
{
    private static readonly DateTime Clock = new(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid CreatorId = Guid.Parse("10000000-0000-4000-8000-000000000001");
    private static readonly Guid MemberId = Guid.Parse("20000000-0000-4000-8000-000000000002");
    private static readonly Guid WorkspaceId = Guid.Parse("30000000-0000-4000-8000-000000000003");
    private static readonly string Digest = new('a', 64);

    [Fact]
    public void Workspace_has_stable_identity_version_and_author()
    {
        var w = new Workspace(CreatorId, "  Demo Workspace  ", "demo-workspace", "  Texte  ", Clock);
        Assert.NotEqual(Guid.Empty, w.Id);
        Assert.Equal("Demo Workspace", w.Name);
        Assert.Equal("demo-workspace", w.SlugNormalized);
        Assert.Equal("Texte", w.Description);
        Assert.Equal(CreatorId, w.CreatedByUserId);
        Assert.Equal(Clock, w.CreatedAtUtc);
        Assert.Equal(1, w.Version);
        Assert.False(w.IsVerifiedOrganization);
    }

    [Theory]
    [InlineData("ABcd")]
    [InlineData("équipe")]
    [InlineData("-bad")]
    [InlineData("bad-")]
    [InlineData("two--dashes")]
    [InlineData(" a ")]
    [InlineData("ab")]
    public void Workspace_rejects_invalid_slug(string slug) =>
        Assert.Throws<ArgumentException>(() => new Workspace(CreatorId, "A", slug, null, Clock));

    [Fact]
    public void Workspace_rejects_overlong_or_unsafe_metadata()
    {
        Assert.Throws<ArgumentException>(() => new Workspace(CreatorId, "\nBad", "valid", null, Clock));
        Assert.Throws<ArgumentException>(() => new Workspace(CreatorId, new string('x', 151), "valid", null, Clock));
        Assert.Throws<ArgumentException>(() => new Workspace(CreatorId, "Valid", "valid", new string('x', 1001), Clock));
        Assert.Throws<ArgumentException>(() => new Workspace(Guid.Empty, "Valid", "valid", null, Clock));
        Assert.Throws<ArgumentException>(() => new Workspace(CreatorId, "Valid", "valid", null, Clock.ToLocalTime()));
    }

    [Fact]
    public void Workspace_rejects_stale_version_without_change()
    {
        var w = new Workspace(CreatorId, "A", "valid", null, Clock);
        Assert.Throws<InvalidOperationException>(() => w.UpdateInformation("B", null, 2));
        Assert.Equal("A", w.Name);
        Assert.Equal(1, w.Version);
        w.UpdateInformation("B", null, 1);
        Assert.Equal(2, w.Version);
        Assert.Equal("B", w.Name);
    }

    [Fact]
    public void Archived_workspace_is_read_only_until_restored()
    {
        var w = new Workspace(CreatorId, "A", "valid", null, Clock);
        w.Archive(1, Clock.AddDays(1));
        Assert.Equal(2, w.Version);
        Assert.Equal(Clock.AddDays(1), w.ArchivedAtUtc);
        Assert.Throws<InvalidOperationException>(() => w.UpdateInformation("B", null, 2));
        Assert.Throws<InvalidOperationException>(() => w.Restore(1));
        w.Restore(2);
        Assert.Null(w.ArchivedAtUtc);
        Assert.Equal(3, w.Version);
    }

    [Fact]
    public void Workspace_rejects_invalid_archival_timestamp()
    {
        var w = new Workspace(CreatorId, "A", "valid", null, Clock);
        Assert.Throws<ArgumentException>(() => w.Archive(1, DateTime.SpecifyKind(Clock, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentOutOfRangeException>(() => w.Archive(1, Clock.AddSeconds(-1)));
    }

    [Fact]
    public void Member_requires_nonempty_identity_and_known_role()
    {
        Assert.Throws<ArgumentException>(() => new WorkspaceMember(Guid.Empty, MemberId, WorkspaceRole.MEMBER, Clock));
        Assert.Throws<ArgumentException>(() => new WorkspaceMember(WorkspaceId, Guid.Empty, WorkspaceRole.MEMBER, Clock));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkspaceMember(WorkspaceId, MemberId, (WorkspaceRole)99, Clock));
        Assert.Throws<ArgumentException>(() => new WorkspaceMember(WorkspaceId, MemberId, WorkspaceRole.MEMBER,
            DateTime.SpecifyKind(Clock, DateTimeKind.Unspecified)));
    }

    [Fact]
    public void Member_starts_active_with_version_one()
    {
        var member = new WorkspaceMember(WorkspaceId, MemberId, WorkspaceRole.VIEWER, Clock);
        Assert.Equal(WorkspaceMemberStatus.ACTIVE, member.Status);
        Assert.Equal(WorkspaceRole.VIEWER, member.Role);
        Assert.Equal(1, member.Version);
        Assert.Equal(Clock, member.JoinedAtUtc);
    }

    [Fact]
    public void Last_owner_cannot_be_demoted_even_when_role_is_valid()
    {
        var member = new WorkspaceMember(WorkspaceId, MemberId, WorkspaceRole.OWNER, Clock);
        Assert.Throws<InvalidOperationException>(() => member.ChangeRole(WorkspaceRole.ADMIN, 1));
        Assert.Equal(WorkspaceRole.OWNER, member.Role);
        Assert.Equal(1, member.Version);
        member.ChangeRole(WorkspaceRole.ADMIN, 2);
        Assert.Equal(WorkspaceRole.ADMIN, member.Role);
        Assert.Equal(2, member.Version);
    }

    [Fact]
    public void Owner_cannot_be_suspended_without_prior_demotion()
    {
        var owner = new WorkspaceMember(WorkspaceId, MemberId, WorkspaceRole.OWNER, Clock);
        Assert.Throws<InvalidOperationException>(() => owner.Suspend());
        Assert.Equal(WorkspaceMemberStatus.ACTIVE, owner.Status);
    }

    [Fact]
    public void Suspended_member_cannot_change_role_and_can_be_reactivated()
    {
        var member = new WorkspaceMember(WorkspaceId, MemberId, WorkspaceRole.MEMBER, Clock);
        member.Suspend();
        Assert.Equal(WorkspaceMemberStatus.SUSPENDED, member.Status);
        Assert.Throws<InvalidOperationException>(() => member.ChangeRole(WorkspaceRole.EDITOR, 1));
        member.Reactivate();
        member.ChangeRole(WorkspaceRole.EDITOR, 1);
        Assert.Equal(WorkspaceRole.EDITOR, member.Role);
        Assert.Equal(4, member.Version);
    }

    [Fact]
    public void Member_noop_role_change_does_not_bump_version()
    {
        var member = new WorkspaceMember(WorkspaceId, MemberId, WorkspaceRole.EDITOR, Clock);
        member.ChangeRole(WorkspaceRole.EDITOR, 0);
        Assert.Equal(1, member.Version);
    }

    [Fact]
    public void Invitation_stores_hash_not_raw_token_and_normalizes_email()
    {
        var invite = Invitation();
        Assert.Equal("USER@EXAMPLE.ORG", invite.EmailNormalized);
        Assert.Equal(Digest, invite.TokenHash);
        Assert.Equal(WorkspaceRole.EDITOR, invite.Role);
        Assert.Equal(1, invite.Version);
        Assert.Null(invite.AcceptedAtUtc);
    }

    [Fact]
    public void Invitation_rejects_owner_role_and_malformed_token_hash()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Invitation(role: WorkspaceRole.OWNER));
        Assert.Throws<ArgumentException>(() => Invitation(hash: "raw-secret"));
        Assert.Throws<ArgumentException>(() => Invitation(hash: new string('A', 64)));
    }

    [Fact]
    public void Invitation_rejects_invalid_expiration_and_email()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Invitation(expiration: Clock));
        Assert.Throws<ArgumentOutOfRangeException>(() => Invitation(expiration: Clock.AddDays(15)));
        Assert.Throws<ArgumentException>(() => Invitation(email: "invalid@example. org"));
        Assert.Throws<ArgumentException>(() => Invitation(email: "invalid-email"));
        Assert.Throws<ArgumentException>(() => Invitation(email: "a\nb@demo.org"));
    }

    [Fact]
    public void Invitation_acceptance_requires_matching_verified_user_and_token()
    {
        var invite = Invitation();
        var user = User(verified: true);
        Assert.Throws<InvalidOperationException>(() => invite.Accept(user, new string('b', 64), Clock.AddHours(1)));
        Assert.Null(invite.AcceptedAtUtc);
        invite.Accept(user, Digest, Clock.AddHours(1));
        Assert.Equal(user.Id, invite.AcceptedByUserId);
        Assert.Equal(2, invite.Version);
        Assert.Throws<InvalidOperationException>(() => invite.Accept(user, Digest, Clock.AddHours(2)));
    }

    [Fact]
    public void Unverified_or_different_email_cannot_accept_invitation()
    {
        Assert.Throws<InvalidOperationException>(() => Invitation().Accept(User(verified: false), Digest, Clock.AddHours(1)));
        Assert.Throws<InvalidOperationException>(() => Invitation().Accept(User(email: "other@example.org"), Digest, Clock.AddHours(1)));
    }

    [Fact]
    public void Expired_invitation_cannot_be_accepted()
    {
        Assert.Throws<InvalidOperationException>(() => Invitation().Accept(User(), Digest, Clock.AddDays(7)));
    }

    [Fact]
    public void Revoked_invitation_cannot_be_accepted_or_revoked_again()
    {
        var invite = Invitation();
        invite.Revoke(Clock.AddMinutes(1));
        Assert.Equal(2, invite.Version);
        Assert.Throws<InvalidOperationException>(() => invite.Accept(User(), Digest, Clock.AddMinutes(2)));
        Assert.Throws<InvalidOperationException>(() => invite.Revoke(Clock.AddMinutes(3)));
    }

    private static WorkspaceInvitation Invitation(
        WorkspaceRole role = WorkspaceRole.EDITOR,
        string? hash = null,
        string email = "  user@example.org ",
        DateTime? expiration = null) =>
        new(WorkspaceId, CreatorId, email, role, hash ?? Digest,
            expiration ?? Clock.AddDays(7), Clock);

    private static AppUser User(bool verified = true, string email = "user@example.org")
    {
        var user = new AppUser(new OidcIdentityKey("https://issuer.example.org", "user-1"), "User");
        user.SetProfile("User", email, verified);
        return user;
    }
}
