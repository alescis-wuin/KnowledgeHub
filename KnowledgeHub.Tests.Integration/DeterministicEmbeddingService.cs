using System.Security.Cryptography;
using System.Text;
using KnowledgeHub.Application.Interfaces;

namespace KnowledgeHub.Tests.Integration;

// Replaces Ollama only inside the HTTP integration-test host.
public sealed class DeterministicEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        var vector = new float[768];

        for (var i = 0; i < digest.Length; i++)
        {
            vector[i] = digest[i] / 255f;
        }

        vector[digest.Length] = 1f;
        return Task.FromResult(vector);
    }
}
