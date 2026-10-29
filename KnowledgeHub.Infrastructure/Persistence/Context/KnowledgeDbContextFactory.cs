using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace KnowledgeHub.Infrastructure.Persistence.Context;

public class KnowledgeDbContextFactory
    : IDesignTimeDbContextFactory<KnowledgeDbContext>
{
    public KnowledgeDbContext CreateDbContext(
        string[] args)
    {
        var optionsBuilder =
            new DbContextOptionsBuilder<KnowledgeDbContext>();

        optionsBuilder.UseNpgsql(
            "Host=localhost;Port=5432;Database=knowledgehub;Username=postgres;Password=postgres");

        return new KnowledgeDbContext(
            optionsBuilder.Options);
    }
}