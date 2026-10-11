namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Deterministic keys for the category container and the seeded categories.
/// Document type and data type keys are still generated when the migration runs.
/// </summary>
public static class CategorySeed
{
    public static readonly Guid ContainerKey = new("c0de4110-0000-4000-8000-000000000001");
    public static readonly Guid RagKey = new("c0de4110-0000-4000-8000-000000000014");
    public static readonly Guid Neo4jKey = new("c0de4110-0000-4000-8000-00000000001a");

    /// <summary>
    /// Categories moved to the recycle bin. Seed steps must not create them again.
    /// </summary>
    public static readonly HashSet<Guid> RetiredKeys = new()
    {
        new Guid("c0de4110-0000-4000-8000-000000000011"),
        new Guid("c0de4110-0000-4000-8000-000000000012"),
        new Guid("c0de4110-0000-4000-8000-000000000013"),
        new Guid("c0de4110-0000-4000-8000-000000000015"),
        new Guid("c0de4110-0000-4000-8000-000000000016"),
        new Guid("c0de4110-0000-4000-8000-000000000017"),
        new Guid("c0de4110-0000-4000-8000-000000000018"),
        new Guid("c0de4110-0000-4000-8000-000000000019"),
    };

    public static readonly CategorySeedItem[] Items =
    [
        new("Agenci kodowania", "agenci-kodowania", null, new Guid("c0de4110-0000-4000-8000-000000000011")),
        new("Modele", "modele", null, new Guid("c0de4110-0000-4000-8000-000000000012")),
        new("Orkiestracja", "orkiestracja", null, new Guid("c0de4110-0000-4000-8000-000000000013")),
        new("RAG", "rag", null, RagKey),
        new("Evaluation i reliability", "evaluation-i-reliability", "testy agentów, benchmarki, pomiary jakości", new Guid("c0de4110-0000-4000-8000-000000000015")),
        new("Agentic SDLC", "agentic-sdlc", "agenci na kolejnych etapach cyklu wytwarzania oprogramowania, od wymagań po deployment", new Guid("c0de4110-0000-4000-8000-000000000016")),
        new("CI/CD i pipeline'y", "ci-cd-i-pipeliney", "automatyczne budowanie, testowanie i wdrażanie oprogramowania", new Guid("c0de4110-0000-4000-8000-000000000017")),
        new("Observability i monitoring", "observability-i-monitoring", "logi, metryki, tracing i alerty w systemach agentowych", new Guid("c0de4110-0000-4000-8000-000000000018")),
        new("Playbooks", "playbooks", "gotowe workflow i checklisty do wdrożenia", new Guid("c0de4110-0000-4000-8000-000000000019")),
        new("Neo4j", "neo4j", "Kategoria poświęcona Neo4j.", Neo4jKey),
    ];

    public static bool IsRetired(CategorySeedItem item) => RetiredKeys.Contains(item.Key);
}

public sealed record CategorySeedItem(string Name, string Slug, string? Description, Guid Key);
