using System.Text.Json;
using KnowledgeHub.CorpusImporter.Models;

namespace KnowledgeHub.CorpusImporter.Services;

public class CorpusReader
{
    public async Task<CorpusRoot> ReadAsync(
        string filePath)
    {
        var json =
            await File.ReadAllTextAsync(
                filePath);

        var options =
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive =
                    true
            };

        return JsonSerializer.Deserialize<
            CorpusRoot>(
                json,
                options)
            ?? new CorpusRoot();
    }
}