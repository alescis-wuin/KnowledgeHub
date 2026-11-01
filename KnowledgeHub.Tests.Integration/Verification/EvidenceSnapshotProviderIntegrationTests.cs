using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Verification;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence;
using KnowledgeHub.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KnowledgeHub.Tests.Integration.Verification;

/// <summary>
/// All mutations affect the isolated PostgreSQL Testcontainer, never the
/// developer database. Each test captures and revalidates from separate contexts.
/// </summary>
public sealed class EvidenceSnapshotProviderIntegrationTests
    : IClassFixture<IsolatedPostgresFixture>
{
    private readonly IsolatedPostgresFixture _fixture;

    public EvidenceSnapshotProviderIntegrationTests(IsolatedPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task CaptureAsync_OnlyAuthorizesPersistedSelectedChunk()
    {
        var (document, chunk) = await SeedAsync("Titre", "Ligne 1\r\nLigne 🚀 2");

        await using var context = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(context);
        var capture = await provider.CaptureAsync([Selected(document, chunk)]);

        Assert.Equal(EvidenceCaptureStatus.Captured, capture.Status);
        var snapshot = Assert.IsType<EvidenceRetrievalSnapshot>(capture.Snapshot);
        Assert.Single(snapshot.AllowedChunks);
        Assert.Equal(KnowledgeDocument.ComputeContentSha256(document.Content),
            snapshot.DocumentContentSha256[document.Id]);

        var captured = snapshot.AllowedChunks[chunk.Id];
        Assert.Equal(chunk.Content, captured.Content);
        var quote = "Ligne 🚀";
        var start = captured.Content.IndexOf(quote, StringComparison.Ordinal);
        Assert.Equal(EvidenceAnchorStatus.ValidAnchor,
            EvidenceAnchorVerifier.Verify(
                new EvidencePointer(document.Id, chunk.Id, start,
                    quote.Length, quote, captured.ChunkSha256), snapshot.AllowedChunks));

        Assert.Equal(EvidenceRevalidationStatus.Fresh,
            await provider.RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task CaptureAsync_RejectsContentFromStaleVectorCache()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu original");
        await using var context = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(context);

        var stale = Selected(document, chunk);
        stale.Content = "Contenu falsifié";
        var capture = await provider.CaptureAsync([stale]);

        Assert.Equal(EvidenceCaptureStatus.StaleRetrieval, capture.Status);
        Assert.Null(capture.Snapshot);
    }

    [Fact]
    public async Task CaptureAsync_RejectsUnknownChunkAndWrongDocumentIdentity()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu réel");
        await using var context = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(context);

        var unknownSelection = Selected(document, chunk);
        unknownSelection.ChunkId = Guid.NewGuid();
        var wrongDocumentSelection = Selected(document, chunk);
        wrongDocumentSelection.DocumentId = Guid.NewGuid();
        var unknown = await provider.CaptureAsync([unknownSelection]);
        var wrongDocument = await provider.CaptureAsync([wrongDocumentSelection]);
        Assert.Equal(EvidenceCaptureStatus.StaleRetrieval, unknown.Status);
        Assert.Equal(EvidenceCaptureStatus.StaleRetrieval, wrongDocument.Status);
    }

    [Fact]
    public async Task CaptureAsync_RejectsDuplicatedAndEmptySourceLists()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu réel");
        await using var context = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(context);

        var repeated = Selected(document, chunk);
        var duplicated = await provider.CaptureAsync([repeated, repeated]);
        var empty = await provider.CaptureAsync([]);

        Assert.Equal(EvidenceCaptureStatus.InvalidRetrieval, duplicated.Status);
        Assert.Null(duplicated.Snapshot);
        Assert.Equal(EvidenceCaptureStatus.EmptyRetrieval, empty.Status);
        Assert.Null(empty.Snapshot);
    }

    [Fact]
    public async Task RevalidateAsync_AcceptsMetadataOnlyChanges()
    {
        var (document, chunk) = await SeedAsync("Ancien titre", "Contenu invariable");
        var snapshot = await CaptureAsync(document, chunk);

        await using (var updateContext = _fixture.CreateContext())
        {
            var repository = new KnowledgeDocumentRepository(updateContext);
            var tracked = await repository.GetByIdAsync(document.Id);
            Assert.NotNull(tracked);
            tracked.UpdateMetadata("Nouveau titre", null, null,
                "Résumé éditorial", "Mots-clés");
            await repository.UpdateAsync(tracked);
        }

        await using var validationContext = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(validationContext);
        Assert.Equal(EvidenceRevalidationStatus.Fresh,
            await provider.RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task RevalidateAsync_RejectsReindexedDocumentsAndRemovedChunks()
    {
        var (document, chunk) = await SeedAsync("Titre", "Texte avant indexation");
        var snapshot = await CaptureAsync(document, chunk);

        await using (var updateContext = _fixture.CreateContext())
        {
            var repository = new KnowledgeDocumentRepository(updateContext);
            var tracked = await repository.GetByIdAsync(document.Id);
            Assert.NotNull(tracked);
            tracked.Update("Titre", "Texte après nouvelle indexation");
            var replacement = new DocumentChunk(tracked.Id, 0,
                tracked.Content.Length, tracked.Content);
            tracked.ReplaceChunks([replacement]);
            await repository.UpdateIndexedAsync(tracked,
                [CreateEmbedding(replacement)]);
        }

        await using var validationContext = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(validationContext);
        Assert.Equal(EvidenceRevalidationStatus.Stale,
            await provider.RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task RevalidateAsync_RejectsDocumentContentChangeEvenWhenChunkIsUnchanged()
    {
        var (document, chunk) = await SeedAsync("Titre", "Texte original");
        var snapshot = await CaptureAsync(document, chunk);

        // Simulate a historical or independently written content update that
        // left the chunk untouched. Current content hash must still change.
        await using (var updateContext = _fixture.CreateContext())
        {
            await updateContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Documents\" SET \"Content\" = {"Texte modifié"}, \"ContentSha256\" = NULL WHERE \"Id\" = {document.Id}");
        }

        await using var validationContext = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(validationContext);
        Assert.Equal(EvidenceRevalidationStatus.Stale,
            await provider.RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task CaptureAsync_RecomputesLegacyNullDocumentDigest()
    {
        var (document, chunk) = await SeedAsync("Titre", "Ancien document stable");
        await using (var updateContext = _fixture.CreateContext())
        {
            await updateContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Documents\" SET \"ContentSha256\" = NULL WHERE \"Id\" = {document.Id}");
        }

        var capture = await CaptureResultAsync(document, chunk);
        Assert.Equal(EvidenceCaptureStatus.Captured, capture.Status);
        var snapshot = Assert.IsType<EvidenceRetrievalSnapshot>(capture.Snapshot);
        Assert.Equal(KnowledgeDocument.ComputeContentSha256(document.Content),
            snapshot.DocumentContentSha256[document.Id]);
        await using var validationContext = _fixture.CreateContext();
        Assert.Equal(EvidenceRevalidationStatus.Fresh,
            await new EvidenceSnapshotProvider(validationContext).RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task CaptureAsync_RejectsStoredDigestInconsistentWithCanonicalContent()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu intact");
        await using (var updateContext = _fixture.CreateContext())
        {
            await updateContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"Documents\" SET \"ContentSha256\" = {new string('0', 64)} WHERE \"Id\" = {document.Id}");
        }

        var capture = await CaptureResultAsync(document, chunk);
        Assert.Equal(EvidenceCaptureStatus.InconsistentSource, capture.Status);
        Assert.Null(capture.Snapshot);
    }

    [Fact]
    public async Task RevalidateAsync_RejectsPersistedChunkChangedWithoutChangingIdentity()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu authentique");
        var snapshot = await CaptureAsync(document, chunk);
        await using (var updateContext = _fixture.CreateContext())
        {
            await updateContext.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE \"DocumentChunks\" SET \"Content\" = {"Contenu remplacé"} WHERE \"Id\" = {chunk.Id}");
        }

        await using var validationContext = _fixture.CreateContext();
        Assert.Equal(EvidenceRevalidationStatus.Stale,
            await new EvidenceSnapshotProvider(validationContext).RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task RevalidateAsync_RejectsDeletedDocument()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu à effacer");
        var snapshot = await CaptureAsync(document, chunk);
        await using (var deleteContext = _fixture.CreateContext())
        {
            await new KnowledgeDocumentRepository(deleteContext).DeleteAsync(document.Id);
        }

        await using var validationContext = _fixture.CreateContext();
        Assert.Equal(EvidenceRevalidationStatus.Stale,
            await new EvidenceSnapshotProvider(validationContext).RevalidateAsync(snapshot));
    }

    [Fact]
    public async Task CaptureAsync_HonorsCancellationBeforeAccessingDatabase()
    {
        var (document, chunk) = await SeedAsync("Titre", "Contenu document");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await using var context = _fixture.CreateContext();
        var provider = new EvidenceSnapshotProvider(context);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.CaptureAsync([Selected(document, chunk)], cancelled.Token));
    }

    private async Task<EvidenceRetrievalSnapshot> CaptureAsync(
        KnowledgeDocument document, DocumentChunk chunk)
    {
        var result = await CaptureResultAsync(document, chunk);
        Assert.Equal(EvidenceCaptureStatus.Captured, result.Status);
        return Assert.IsType<EvidenceRetrievalSnapshot>(result.Snapshot);
    }

    private async Task<EvidenceCaptureResult> CaptureResultAsync(
        KnowledgeDocument document, DocumentChunk chunk)
    {
        await using var context = _fixture.CreateContext();
        return await new EvidenceSnapshotProvider(context).CaptureAsync([Selected(document, chunk)]);
    }

    private async Task<(KnowledgeDocument Document, DocumentChunk Chunk)> SeedAsync(
        string title, string content)
    {
        var document = new KnowledgeDocument(title, content);
        var chunk = new DocumentChunk(document.Id, 0, document.Content.Length, document.Content);
        document.ReplaceChunks([chunk]);

        await using var context = _fixture.CreateContext();
        await new KnowledgeDocumentRepository(context).AddIndexedAsync(
            document, [CreateEmbedding(chunk)]);
        return (document, chunk);
    }

    private static VectorSearchResultDto Selected(
        KnowledgeDocument document, DocumentChunk chunk) => new()
        {
            DocumentId = document.Id,
            ChunkId = chunk.Id,
            Title = document.Title,
            Content = chunk.Content,
            Score = 0.9
        };

    private static ChunkEmbedding CreateEmbedding(DocumentChunk chunk)
    {
        var embedding = new float[768];
        embedding[0] = 1f;
        return new ChunkEmbedding(chunk.Id, embedding);
    }
}
