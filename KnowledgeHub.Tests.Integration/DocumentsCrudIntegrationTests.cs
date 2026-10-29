using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Tests.Integration;

public class DocumentsCrudIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DocumentsCrudIntegrationTests(
        CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
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
}