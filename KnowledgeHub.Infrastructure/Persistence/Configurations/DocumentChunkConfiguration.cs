using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations;

public class DocumentChunkConfiguration
	: IEntityTypeConfiguration<DocumentChunk>
{
	public void Configure(
		EntityTypeBuilder<DocumentChunk> builder)
	{
		builder.ToTable("DocumentChunks");

		builder.HasKey(x => x.Id);

		builder.Property(x => x.Content)
			.IsRequired();

		builder.Property(x => x.CreatedAt)
			.IsRequired();

		builder.HasIndex(x => x.DocumentId);
	}
}