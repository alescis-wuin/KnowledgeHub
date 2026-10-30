using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KnowledgeHub.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Tests.Integration;

public class DocumentsCrudIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>,
      IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DocumentsCrudIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _factory = factory;

        _client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });
    }

    public Task InitializeAsync()
    {
        return _factory.ResetDocumentsAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Get_All_Should_Return_200()
    {
        var response =
            await _client.GetAsync(
                "/api/documents");

        response.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Create_Document_Should_Return_201()
    {
        var request =
            new CreateDocumentDto
            {
                Title = "Integration Test",
                Content = "Content"
            };

        var response =
            await _client.PostAsJsonAsync(
                "/api/documents",
                request);

        response.StatusCode
            .Should()
            .Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Invalid_Document_Should_Return_400()
    {
        var request =
            new CreateDocumentDto
            {
                Title = "",
                Content = ""
            };

        var response =
            await _client.PostAsJsonAsync(
                "/api/documents",
                request);

        response.StatusCode
            .Should()
            .Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Get_By_Id_Should_Return_200()
    {
        var createRequest =
            new CreateDocumentDto
            {
                Title = "Document GetById",
                Content = "Contenu"
            };

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/documents",
                createRequest);

        createResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.Created);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    ApiResponse<DocumentDto>>();

        created.Should().NotBeNull();

        var id =
            created!.Data!.Id;

        var getResponse =
            await _client.GetAsync(
                $"/api/documents/{id}");

        getResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        var document =
            await getResponse.Content
                .ReadFromJsonAsync<
                    ApiResponse<DocumentDto>>();

        document.Should().NotBeNull();

        document!.Success
            .Should()
            .BeTrue();

        document.Data!.Id
            .Should()
            .Be(id);

        document.Data.Title
            .Should()
            .Be("Document GetById");
    }

    [Fact]
    public async Task Update_Should_Return_200()
    {
        var createRequest =
            new CreateDocumentDto
            {
                Title = "Ancien titre",
                Content = "Ancien contenu"
            };

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/documents",
                createRequest);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    ApiResponse<DocumentDto>>();

        var updateRequest =
            new UpdateDocumentDto
            {
                Title = "Nouveau titre",
                Content = "Nouveau contenu"
            };

        var response =
            await _client.PutAsJsonAsync(
                $"/api/documents/{created!.Data!.Id}",
                updateRequest);

        response.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        var updated =
            await response.Content
                .ReadFromJsonAsync<
                    ApiResponse<DocumentDto>>();

        updated.Should().NotBeNull();

        updated!.Success
            .Should()
            .BeTrue();

        updated.Data!.Title
            .Should()
            .Be("Nouveau titre");

        updated.Data.Content
            .Should()
            .Be("Nouveau contenu");
    }

    [Fact]
    public async Task Delete_Should_Return_200()
    {
        var createRequest =
            new CreateDocumentDto
            {
                Title = "Document à supprimer",
                Content = "Contenu"
            };

        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/documents",
                createRequest);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    ApiResponse<DocumentDto>>();

        var id =
            created!.Data!.Id;

        var deleteResponse =
            await _client.DeleteAsync(
                $"/api/documents/{id}");

        deleteResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        var getResponse =
            await _client.GetAsync(
                $"/api/documents/{id}");

        getResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Full_Crud_Lifecycle_Should_Work()
    {
        // CREATE
        var createResponse =
            await _client.PostAsJsonAsync(
                "/api/documents",
                new CreateDocumentDto
                {
                    Title = "Titre initial",
                    Content = "Contenu initial"
                });

        createResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.Created);

        var created =
            await createResponse.Content
                .ReadFromJsonAsync<
                    ApiResponse<DocumentDto>>();

        var id =
            created!.Data!.Id;

        // READ
        var readResponse =
            await _client.GetAsync(
                $"/api/documents/{id}");

        readResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        // UPDATE
        var updateResponse =
            await _client.PutAsJsonAsync(
                $"/api/documents/{id}",
                new UpdateDocumentDto
                {
                    Title = "Titre modifié",
                    Content = "Contenu modifié"
                });

        updateResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        // DELETE
        var deleteResponse =
            await _client.DeleteAsync(
                $"/api/documents/{id}");

        deleteResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.OK);

        // READ AFTER DELETE
        var notFoundResponse =
            await _client.GetAsync(
                $"/api/documents/{id}");

        notFoundResponse.StatusCode
            .Should()
            .Be(HttpStatusCode.NotFound);
    }

	[Fact]
	public async Task Http_Reindexing_KeepsChunksAndEmbeddingsConsistent()
	{
	    var initialContent = new string('A', 620);
	    var updatedContent = new string('B', 1030);
	    var fake = new DeterministicEmbeddingService();

	    // 1. Create the document through the real HTTP API.
	    using var createResponse = await _client.PostAsJsonAsync(
	        "/api/documents",
	        new CreateDocumentDto
	        {
	            Title = "Titre initial",
	            Content = initialContent
	        });

	    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

	    var created = await createResponse.Content
	        .ReadFromJsonAsync<ApiResponse<DocumentDto>>();

	    Assert.NotNull(created);
	    Assert.NotNull(created.Data);

	    var documentId = created.Data.Id;
	    Guid[] originalChunkIds;

	    // 2. Verify the original chunks and their embeddings.
	    using (var scope = _factory.Services.CreateScope())
	    {
	        var db = scope.ServiceProvider
	            .GetRequiredService<KnowledgeDbContext>();

	        var chunks = await db.DocumentChunks
	            .AsNoTracking()
	            .Where(x => x.DocumentId == documentId)
	            .OrderBy(x => x.ChunkIndex)
	            .ToArrayAsync();

	        Assert.Equal(2, chunks.Length);
	        Assert.Equal(
	            initialContent,
	            string.Concat(chunks.Select(x => x.Content)));

	        originalChunkIds = chunks.Select(x => x.Id).ToArray();

	        foreach (var chunk in chunks)
	        {
	            var embedding = await db.ChunkEmbeddings
	                .AsNoTracking()
	                .SingleAsync(x => x.ChunkId == chunk.Id);

	            var expected = await fake.GenerateEmbeddingAsync(
	                chunk.Content);

	            Assert.Equal(768, embedding.Embedding.Length);
	            Assert.True(expected.SequenceEqual(embedding.Embedding));
	        }
	    }

	    // 3. Change the content through HTTP.
	    using var updateResponse = await _client.PutAsJsonAsync(
	        $"/api/documents/{documentId}",
	        new UpdateDocumentDto
	        {
	            Title = "Titre modifié",
	            Content = updatedContent
	        });

	    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

	    Guid[] replacementChunkIds;
	    Guid[] replacementEmbeddingIds;

	    // 4. Confirm complete replacement of the old index.
	    using (var scope = _factory.Services.CreateScope())
	    {
	        var db = scope.ServiceProvider
	            .GetRequiredService<KnowledgeDbContext>();

	        Assert.False(await db.DocumentChunks
	            .AnyAsync(x => originalChunkIds.Contains(x.Id)));

	        Assert.False(await db.ChunkEmbeddings
	            .AnyAsync(x => originalChunkIds.Contains(x.ChunkId)));

	        var document = await db.Documents
	            .AsNoTracking()
	            .SingleAsync(x => x.Id == documentId);

	        Assert.Equal("Titre modifié", document.Title);
	        Assert.Equal(updatedContent, document.Content);

	        var chunks = await db.DocumentChunks
	            .AsNoTracking()
	            .Where(x => x.DocumentId == documentId)
	            .OrderBy(x => x.ChunkIndex)
	            .ToArrayAsync();

	        Assert.Equal(3, chunks.Length);
	        Assert.Equal(new[] { 0, 1, 2 },
	            chunks.Select(x => x.ChunkIndex).ToArray());

	        Assert.Equal(
	            updatedContent,
	            string.Concat(chunks.Select(x => x.Content)));

	        replacementChunkIds = chunks.Select(x => x.Id).ToArray();

	        var embeddings = await db.ChunkEmbeddings
	            .AsNoTracking()
	            .Where(x => replacementChunkIds.Contains(x.ChunkId))
	            .ToArrayAsync();

	        Assert.Equal(3, embeddings.Length);

	        foreach (var chunk in chunks)
	        {
				var embedding = Assert.Single(
				    embeddings,
				    x => x.ChunkId == chunk.Id);

	            var expected = await fake.GenerateEmbeddingAsync(
	                chunk.Content);

	            Assert.True(expected.SequenceEqual(embedding.Embedding));
	        }

	        replacementEmbeddingIds = embeddings
	            .Select(x => x.Id)
	            .OrderBy(x => x)
	            .ToArray();
	    }

	    // 5. Changing only the title must preserve the index.
	    using var titleResponse = await _client.PutAsJsonAsync(
	        $"/api/documents/{documentId}",
	        new UpdateDocumentDto
	        {
	            Title = "Titre uniquement",
	            Content = updatedContent
	        });

	    Assert.Equal(HttpStatusCode.OK, titleResponse.StatusCode);

	    using (var scope = _factory.Services.CreateScope())
	    {
	        var db = scope.ServiceProvider
	            .GetRequiredService<KnowledgeDbContext>();

	        var chunkIds = await db.DocumentChunks
	            .AsNoTracking()
	            .Where(x => x.DocumentId == documentId)
	            .OrderBy(x => x.ChunkIndex)
	            .Select(x => x.Id)
	            .ToArrayAsync();

	        Assert.Equal(replacementChunkIds, chunkIds);

	        var embeddingIds = await db.ChunkEmbeddings
	            .AsNoTracking()
	            .Where(x => replacementChunkIds.Contains(x.ChunkId))
	            .Select(x => x.Id)
	            .ToArrayAsync();

	        Assert.Equal(
	            replacementEmbeddingIds,
	            embeddingIds.OrderBy(x => x).ToArray());

	        var title = await db.Documents
	            .AsNoTracking()
	            .Where(x => x.Id == documentId)
	            .Select(x => x.Title)
	            .SingleAsync();

	        Assert.Equal("Titre uniquement", title);
	    }
	}
}
