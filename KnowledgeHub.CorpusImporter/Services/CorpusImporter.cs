using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.CorpusImporter.Models;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.CorpusImporter.Services;

public class CorpusImporter
{
    private readonly KnowledgeDbContext _context;

    private readonly ITextChunker _chunker;

    public CorpusImporter(
        KnowledgeDbContext context,
        ITextChunker chunker)
    {
        _context = context;
        _chunker = chunker;
    }

    public async Task ImportAsync(
        CorpusRoot corpus)
    {
        Console.WriteLine(
            "Import Categories...");

        foreach (var item in corpus.Categories)
        {
            if (await _context.Categories
                .AnyAsync(x =>
                    x.Name == item.Name))
            {
                continue;
            }

            _context.Categories.Add(
                new Category(item.Name));
        }

        await _context.SaveChangesAsync();

        Console.WriteLine(
            "Import Sources...");

        foreach (var item in corpus.Sources)
        {
            if (await _context.Sources
                .AnyAsync(x =>
                    x.Name == item.Name))
            {
                continue;
            }

            _context.Sources.Add(
                new KnowledgeSource(
                    item.Name,
                    item.Url));
        }

        await _context.SaveChangesAsync();

        Console.WriteLine(
            "Import Tags...");

        foreach (var item in corpus.Tags)
        {
            if (await _context.Tags
                .AnyAsync(x =>
                    x.Name == item.Name))
            {
                continue;
            }

            _context.Tags.Add(
                new Tag(item.Name));
        }

        await _context.SaveChangesAsync();

        Console.WriteLine(
            "Import Documents...");

        foreach (var item in corpus.Documents)
        {
            if (await _context.Documents
                .AnyAsync(x =>
                    x.Title == item.Title))
            {
                continue;
            }


            if (string.IsNullOrWhiteSpace(item.Content))
            {
                Console.WriteLine(
                    $"Document ignoré : {item.Title}");

                continue;
            }

            var document =
                new KnowledgeDocument(
                    item.Title,
                    item.Content);

            document.SetSummary(
                item.Summary);

            document.SetSearchKeywords(
                item.SearchKeywords);

            var category =
                await _context.Categories
                    .FirstOrDefaultAsync(
                        x => x.Name ==
                            item.CategoryName);

            if (category is not null)
            {
                document.AssignCategory(
                    category.Id);
            }

            var source =
                await _context.Sources
                    .FirstOrDefaultAsync(
                        x => x.Name ==
                            item.SourceName);

            if (source is not null)
            {
                document.AssignSource(
                    source.Id);
            }

            foreach (var tagName
                     in item.TagNames)
            {
                var tag =
                    await _context.Tags
                        .FirstOrDefaultAsync(
                            x => x.Name ==
                                tagName);

                if (tag is not null)
                {
                    document.AddTag(tag);
                }
            }

            Console.WriteLine($"Importing document: {item.Title}");
            var chunks =
                _chunker.Chunk(
                    item.Content);

            var index = 0;

            foreach (var chunk in chunks)
            {
                var content =
                    chunk?.Trim();

                if (string.IsNullOrWhiteSpace(content))
                {
                    Console.WriteLine(
                        "Chunk vide ignoré");

                    continue;
                }

                Console.WriteLine(
                    $"Chunk size: {content.Length}");

                document.AddChunk(
                    index++,
                    content.Length,
                    content);
            }

            _context.Documents.Add(
                document);
        }

        await _context.SaveChangesAsync();
    }
}