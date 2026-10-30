
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Npgsql;

namespace KnowledgeHub.Tests.Integration;

public sealed class IndexedDocumentRepositoryIntegrationTests
    : IClassFixture<IsolatedPostgresFixture>
{
    private readonly IsolatedPostgresFixture _fixture;

    public IndexedDocumentRepositoryIntegrationTests(
        IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task AddIndexedAsync_PersistsDocumentChunksAndEmbeddings()
    {
        var document = new KnowledgeDocument(
            "Document initial",
            "Contenu initial");

        var chunk = new DocumentChunk(
            document.Id,
            0,
            document.Content.Length,
            document.Content);

        document.ReplaceChunks([chunk]);

        await using (var context = _fixture.CreateContext())
        {
            var repository =
                new KnowledgeDocumentRepository(context);

            await repository.AddIndexedAsync(
                document,
                [CreateEmbedding(chunk)]);
        }

        await using var verification = _fixture.CreateContext();

        Assert.True(await verification.Documents
            .AsNoTracking()
            .AnyAsync(x => x.Id == document.Id));

        Assert.True(await verification.DocumentChunks
            .AsNoTracking()
            .AnyAsync(x => x.Id == chunk.Id));

        var embedding = await verification.ChunkEmbeddings
            .AsNoTracking()
            .SingleAsync(x => x.ChunkId == chunk.Id);

        Assert.Equal(768, embedding.Embedding.Length);
        Assert.Equal(1f, embedding.Embedding[0]);
    }

    [Fact]
    public async Task UpdateIndexedAsync_ReplacesOldChunksAndEmbeddings()
    {
        var document = new KnowledgeDocument(
            "Document",
            "Ancien contenu");

        var oldChunk = new DocumentChunk(
            document.Id,
            0,
            document.Content.Length,
            document.Content);

        document.ReplaceChunks([oldChunk]);

        await using (var context = _fixture.CreateContext())
        {
            var repository =
                new KnowledgeDocumentRepository(context);

            await repository.AddIndexedAsync(
                document,
                [CreateEmbedding(oldChunk)]);
        }

        Guid newChunkId;

        await using (var context = _fixture.CreateContext())
        {
            var repository =
                new KnowledgeDocumentRepository(context);

            var trackedDocument =
                await repository.GetByIdAsync(document.Id);

            Assert.NotNull(trackedDocument);

            trackedDocument.Update(
                "Document modifié",
                "Nouveau contenu");

            var newChunk = new DocumentChunk(
                trackedDocument.Id,
                0,
                trackedDocument.Content.Length,
                trackedDocument.Content);

            trackedDocument.ReplaceChunks([newChunk]);

		    await repository.UpdateIndexedAsync(
		        trackedDocument,
		        [CreateEmbedding(newChunk, 2f)]);

            newChunkId = newChunk.Id;
        }

        await using var verification = _fixture.CreateContext();

        var persistedDocument = await verification.Documents
            .AsNoTracking()
            .SingleAsync(x => x.Id == document.Id);

        Assert.Equal(
            "Nouveau contenu",
            persistedDocument.Content);

        Assert.False(await verification.DocumentChunks
            .AsNoTracking()
            .AnyAsync(x => x.Id == oldChunk.Id));

        Assert.False(await verification.ChunkEmbeddings
            .AsNoTracking()
            .AnyAsync(x => x.ChunkId == oldChunk.Id));

        Assert.True(await verification.DocumentChunks
            .AsNoTracking()
            .AnyAsync(x => x.Id == newChunkId));

        var embedding = await verification.ChunkEmbeddings
            .AsNoTracking()
            .SingleAsync(x => x.ChunkId == newChunkId);

        Assert.Equal(768, embedding.Embedding.Length);
        Assert.Equal(2f, embedding.Embedding[0]);
    }

    private static ChunkEmbedding CreateEmbedding(
        DocumentChunk chunk,
        float value = 1f)
    {
        var vector = new float[768];
        vector[0] = value;

        return new ChunkEmbedding(
            chunk.Id,
            vector);
    }

	[Fact]
	public async Task UpdateIndexedAsync_WhenEmbeddingInsertFails_RollsBack()
	{
	    var document = new KnowledgeDocument(
	        "Titre initial",
	        "Ancien contenu");

	    var oldChunk = new DocumentChunk(
	        document.Id,
	        0,
	        document.Content.Length,
	        document.Content);

	    document.ReplaceChunks([oldChunk]);

	    await using (var setup = _fixture.CreateContext())
	    {
	        var repository = new KnowledgeDocumentRepository(setup);

	        await repository.AddIndexedAsync(
	            document,
	            [CreateEmbedding(oldChunk)]);

	        // Reject only the sentinel embedding used by this test.
	        await setup.Database.ExecuteSqlRawAsync(
	            """
	            ALTER TABLE "ChunkEmbeddings"
	            ADD CONSTRAINT "CK_KH003_Rollback"
	            CHECK ("Embedding" NOT LIKE '[9999,%');
	            """);
	    }

	    Guid replacementId = Guid.Empty;

	    try
	    {
	        await using (var context = _fixture.CreateContext())
	        {
	            var repository =
	                new KnowledgeDocumentRepository(context);

	            var tracked =
	                await repository.GetByIdAsync(document.Id);

	            Assert.NotNull(tracked);

	            tracked.Update(
	                "Titre modifié",
	                "Nouveau contenu");

	            var replacement = new DocumentChunk(
	                tracked.Id,
	                0,
	                tracked.Content.Length,
	                tracked.Content);

	            replacementId = replacement.Id;

	            tracked.ReplaceChunks([replacement]);

	            var exception =
	                await Assert.ThrowsAsync<DbUpdateException>(
	                    () => repository.UpdateIndexedAsync(
	                        tracked,
	                        [CreateEmbedding(replacement, 9999f)]));

	            var postgresError =
	                Assert.IsType<PostgresException>(
	                    exception.InnerException);

	            // PostgreSQL CHECK constraint violation.
	            Assert.Equal("23514", postgresError.SqlState);
	        }

	        // Verify the database in a fresh DbContext.
	        await using var verification = _fixture.CreateContext();

	        var persisted = await verification.Documents
	            .AsNoTracking()
	            .SingleAsync(x => x.Id == document.Id);

	        Assert.Equal("Titre initial", persisted.Title);
	        Assert.Equal("Ancien contenu", persisted.Content);

	        Assert.True(await verification.DocumentChunks
	            .AnyAsync(x => x.Id == oldChunk.Id));

	        Assert.True(await verification.ChunkEmbeddings
	            .AnyAsync(x => x.ChunkId == oldChunk.Id));

	        Assert.False(await verification.DocumentChunks
	            .AnyAsync(x => x.Id == replacementId));

	        Assert.False(await verification.ChunkEmbeddings
	            .AnyAsync(x => x.ChunkId == replacementId));

	        Assert.Equal(
	            1,
	            await verification.DocumentChunks.CountAsync(
	                x => x.DocumentId == document.Id));
	    }
	    finally
	    {
	        await using var cleanup = _fixture.CreateContext();

	        await cleanup.Database.ExecuteSqlRawAsync(
	            """
	            ALTER TABLE "ChunkEmbeddings"
	            DROP CONSTRAINT IF EXISTS "CK_KH003_Rollback";
	            """);
	    }
	}

	[Fact]
	public async Task UpdateAsync_WhenDocumentIsStale_RejectsSecondWrite()
	{
	    var document = new KnowledgeDocument(
	        "Titre initial",
	        "Contenu stable");

	    var chunk = new DocumentChunk(
	        document.Id,
	        0,
	        document.Content.Length,
	        document.Content);

	    document.ReplaceChunks([chunk]);

	    await using (var setup = _fixture.CreateContext())
	    {
	        var repository = new KnowledgeDocumentRepository(setup);

	        await repository.AddIndexedAsync(
	            document,
	            [CreateEmbedding(chunk)]);
	    }

	    await using (var firstContext = _fixture.CreateContext())
	    await using (var staleContext = _fixture.CreateContext())
	    {
	        var firstRepository =
	            new KnowledgeDocumentRepository(firstContext);

	        var staleRepository =
	            new KnowledgeDocumentRepository(staleContext);

	        var firstDocument =
	            await firstRepository.GetByIdAsync(document.Id);

	        var staleDocument =
	            await staleRepository.GetByIdAsync(document.Id);

	        Assert.NotNull(firstDocument);
	        Assert.NotNull(staleDocument);

	        firstDocument.Update(
	            "Premier titre",
	            firstDocument.Content);

	        await firstRepository.UpdateAsync(firstDocument);

	        staleDocument.Update(
	            "Titre obsolète",
	            staleDocument.Content);

	        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
	            () => staleRepository.UpdateAsync(staleDocument));
	    }

	    await using var verification = _fixture.CreateContext();

	    var persisted = await verification.Documents
	        .AsNoTracking()
	        .SingleAsync(x => x.Id == document.Id);

	    Assert.Equal("Premier titre", persisted.Title);
	    Assert.Equal("Contenu stable", persisted.Content);

	    Assert.True(await verification.DocumentChunks
	        .AnyAsync(x => x.Id == chunk.Id));

	    var embedding = await verification.ChunkEmbeddings
	        .AsNoTracking()
	        .SingleAsync(x => x.ChunkId == chunk.Id);

	    Assert.Equal(768, embedding.Embedding.Length);
	}

	[Fact]
	public async Task DeleteAsync_RemovesDocumentChunksAndEmbeddings()
	{
	    var document = new KnowledgeDocument(
	        "Document à supprimer",
	        "Contenu à supprimer");

	    var firstChunk = new DocumentChunk(
	        document.Id,
	        0,
	        10,
	        "Premier chunk");

	    var secondChunk = new DocumentChunk(
	        document.Id,
	        1,
	        10,
	        "Second chunk");

	    document.ReplaceChunks([firstChunk, secondChunk]);

	    await using (var setup = _fixture.CreateContext())
	    {
	        var repository = new KnowledgeDocumentRepository(setup);

	        await repository.AddIndexedAsync(
	            document,
	            [
	                CreateEmbedding(firstChunk),
	                CreateEmbedding(secondChunk, 2f)
	            ]);
	    }

	    await using (var context = _fixture.CreateContext())
	    {
	        var repository = new KnowledgeDocumentRepository(context);

	        await repository.DeleteAsync(document.Id);
	    }

	    await using var verification = _fixture.CreateContext();

	    Assert.False(await verification.Documents
	        .AnyAsync(x => x.Id == document.Id));

	    Assert.False(await verification.DocumentChunks
	        .AnyAsync(x => x.DocumentId == document.Id));

	    Assert.False(await verification.ChunkEmbeddings
	        .AnyAsync(x => x.ChunkId == firstChunk.Id));

	    Assert.False(await verification.ChunkEmbeddings
	        .AnyAsync(x => x.ChunkId == secondChunk.Id));
	}
}
