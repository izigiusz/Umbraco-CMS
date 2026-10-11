namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Deterministic keys for the four portal pillar pages.
/// Document type keys are still generated when the migration runs.
/// </summary>
public static class PillarSeed
{
    public static readonly Guid AboutKey = new("c0da7100-0000-4000-8000-000000000005");

    /// <summary>
    /// Pillar pages moved to the recycle bin. Seed steps must not create them again.
    /// </summary>
    public static readonly HashSet<Guid> RetiredKeys = new()
    {
        new Guid("c0da7100-0000-4000-8000-000000000001"),
        new Guid("c0da7100-0000-4000-8000-000000000002"),
        new Guid("c0da7100-0000-4000-8000-000000000003"),
        new Guid("c0da7100-0000-4000-8000-000000000004"),
    };

    public static readonly PillarSeedItem[] Items =
    [
        new(
            "Harness Engineering",
            "Obszar o projektowaniu otoczenia, w którym agenci kodujący pracują.",
            "<p><strong>Strona w przygotowaniu.</strong> Obszar o projektowaniu otoczenia, w którym agenci kodujący pracują: narzędzia, kontekst i pętle weryfikacji.</p>",
            0,
            new Guid("c0da7100-0000-4000-8000-000000000001"),
            null),
        new(
            "Software Factory",
            "Obszar o powtarzalnym wytwarzaniu oprogramowania.",
            "<p><strong>Strona w przygotowaniu.</strong> Obszar o powtarzalnym wytwarzaniu oprogramowania, od specyfikacji przez pracę agentów po kontrolę jakości i wdrożenie.</p>",
            1,
            new Guid("c0da7100-0000-4000-8000-000000000002"),
            null),
        new(
            "Agentic SDLC",
            "Obszar o udziale agentów w cyklu wytwarzania oprogramowania.",
            "<p><strong>Strona w przygotowaniu.</strong> Obszar o udziale agentów w kolejnych etapach cyklu wytwarzania oprogramowania.</p>",
            2,
            new Guid("c0da7100-0000-4000-8000-000000000003"),
            "agentic-sdlc"),
        new(
            "Playbooks",
            "Miejsce na gotowe workflow i checklisty.",
            "<p><strong>Strona w przygotowaniu.</strong> Miejsce na gotowe workflow i checklisty. Treść uzupełni redakcja.</p>",
            3,
            new Guid("c0da7100-0000-4000-8000-000000000004"),
            "playbooks"),
        new(
            "O mnie",
            "Strona w przygotowaniu.",
            "<p><strong>Strona w przygotowaniu.</strong></p>",
            1,
            AboutKey,
            null),
    ];

    public static bool IsRetired(PillarSeedItem item) => RetiredKeys.Contains(item.Key);
}

/// <summary>
/// One pillar page created only when its key is missing.
/// </summary>
/// <param name="Title">Node name and title.</param>
/// <param name="Lead">Short neutral lead.</param>
/// <param name="Body">Draft body. Includes the editor marker.</param>
/// <param name="Order">Display order.</param>
/// <param name="Key">Stable content key.</param>
/// <param name="RelatedCategorySlug">Optional category slug linked only on first create.</param>
public sealed record PillarSeedItem(string Title, string Lead, string Body, int Order, Guid Key, string? RelatedCategorySlug);
