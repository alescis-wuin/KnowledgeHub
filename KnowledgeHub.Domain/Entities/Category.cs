namespace KnowledgeHub.Domain.Entities;

public class Category
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } =
        string.Empty;

    private Category()
    {
    }

    public Category(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Category name is required");

        Id = Guid.NewGuid();

        Name = name.Trim();
    }
}