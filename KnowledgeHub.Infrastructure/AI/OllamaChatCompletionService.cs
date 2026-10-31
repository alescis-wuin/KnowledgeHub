using KnowledgeHub.Application.Telemetry;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using KnowledgeHub.Application.Interfaces;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class OllamaChatCompletionService
    : IChatCompletionService
{
    private readonly HttpClient
        _httpClient;

    public OllamaChatCompletionService(
        HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string>
        GenerateAnswerAsync(
            string question,
            string context,
            CancellationToken cancellationToken =
                default)
    {
        return await KnowledgeHubActivities.TraceAsync("ollama.chat", async () =>
        {
        var request =
            new OllamaChatRequest
            {
                Model = "qwen3:4b-instruct",

                Stream = false,

                Messages =
                [
                    new OllamaChatMessage
                    {
                        Role = "system",

                        Content =
"""
Règles :

- réponds directement
- ne montre jamais ton raisonnement
- ne réfléchis pas à voix haute
- utilise uniquement les informations du contexte
- synthétise les informations
- ne copie pas le contexte
- maximum 150 mots
- ajoute à la fin :

Sources utilisées :
- titre 1
- titre 2
"""
                    },

                    new OllamaChatMessage
                    {
                        Role = "user",

                        Content =
$"""
CONTEXTE

{context}

QUESTION

{question}
"""
                    }
                ],

                Options =
                    new OllamaOptions
                    {
                        NumPredict = 800,
                        NumCtx = 4096
                    }
            };

        using var response =
            await _httpClient.PostAsJsonAsync(
                "/api/chat",
                request,
                cancellationToken);

        response.EnsureSuccessStatusCode();

        // Never print or persist the raw response: it may contain reasoning,
        // private document excerpts or model-generated confidential content.
        OllamaChatResponse? result;
        try
        {
            result = await response.Content
                .ReadFromJsonAsync<OllamaChatResponse>(
                    cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            // Do not expose the parser's original diagnostic or payload.
            throw new InvalidDataException(
                "Ollama chat returned invalid JSON.");
        }

        if (string.IsNullOrWhiteSpace(result?.Message?.Content))
        {
            // A reasoning-only answer is not a user-facing answer.
            throw new InvalidDataException(
                "Ollama chat did not return a usable answer.");
        }

        return result.Message.Content;
        }, cancellationToken);
    }

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]
        public string Model
        {
            get;
            set;
        }
        = string.Empty;

        [JsonPropertyName("stream")]
        public bool Stream
        {
            get;
            set;
        }

        [JsonPropertyName("messages")]
        public List<OllamaChatMessage>
            Messages
        {
            get;
            set;
        }
        = [];

        [JsonPropertyName("options")]
        public OllamaOptions
            Options
        {
            get;
            set;
        }
        = new();
    }

    private sealed class OllamaChatMessage
    {
        [JsonPropertyName("role")]
        public string Role
        {
            get;
            set;
        }
        = string.Empty;

        [JsonPropertyName("content")]
        public string Content
        {
            get;
            set;
        }
        = string.Empty;

    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("num_predict")]
        public int NumPredict
        {
            get;
            set;
        } = 800;

        [JsonPropertyName("num_ctx")]
        public int NumCtx
        {
            get;
            set;
        } = 4096;
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")]
        public OllamaChatMessage
            Message
        {
            get;
            set;
        }
        = new();
    }
}
