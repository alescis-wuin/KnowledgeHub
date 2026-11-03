using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

public sealed class AppUserConfiguration :
    IEntityTypeConfiguration<AppUser>, IWorkspaceStagedConfiguration
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers", table =>
        {
            table.HasCheckConstraint("CK_AppUsers_IdentityNotBlank",
                "length(btrim(\"Issuer\")) > 0 AND length(btrim(\"Subject\")) > 0");
            table.HasCheckConstraint("CK_AppUsers_VerifiedEmail",
                "NOT \"EmailVerified\" OR \"EmailNormalized\" IS NOT NULL");
            table.HasCheckConstraint("CK_AppUsers_DisplayNameNotBlank",
                "length(btrim(\"DisplayName\")) > 0");
        });
        builder.HasKey(user => user.Id);
        builder.Property(user => user.Issuer)
            .HasMaxLength(300).UseCollation("C").IsRequired();
        builder.Property(user => user.Subject)
            .HasMaxLength(300).UseCollation("C").IsRequired();
        builder.Property(user => user.DisplayName)
            .HasMaxLength(150).IsRequired();
        builder.Property(user => user.EmailNormalized)
            .HasMaxLength(320);
        builder.Property(user => user.EmailVerified).IsRequired();
        builder.Property(user => user.CreatedAtUtc)
            .HasColumnType("timestamp with time zone").IsRequired();
        builder.HasIndex(user => new { user.Issuer, user.Subject })
            .IsUnique().HasDatabaseName("UX_AppUsers_OidcIdentity");
        // Email is deliberately not unique: never use it as account identity.
    }
}
