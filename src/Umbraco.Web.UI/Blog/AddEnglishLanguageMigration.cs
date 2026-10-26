using System.Net;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentEditing;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Extensions;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Adds Polish as the default language and English as a second language, turns on
/// culture variation for editorial text, and publishes English variants.
/// Shared values stay invariant: URLs, pickers, slugs, order, dates, tags and images.
/// URL segments follow the culture name, so Polish keeps /kursy/ and English is /en/courses/.
/// The English hostname is the path /en, which matches any host. Polish is the wildcard default, so it has no prefix.
/// </summary>
public class AddEnglishLanguageMigration : AsyncMigrationBase
{
    private const string AboutLead =
        "My name is Grzegorz Sulowski. I work as a programmer and application designer. For years I have been building systems in .NET and Java, and today I work in an AI-first model. On Codation I share what I learn about harness engineering in practice.";

    private const string AboutBody =
        "<h2>Who I am</h2><p>I graduated in Computer Science from AGH University of Science and Technology (MSc Eng., 1990–1995). Since July 2007 I have worked at SoftSystem sp. z o.o. as a programmer and application designer, developing a monolithic system for the medical industry.</p>"
        + "<h2>What I do</h2><ul><li><p>I modernize and extend a legacy system with an AI workflow: adding features and fixing defects.</p></li><li><p>I diagnose and resolve service tickets using an AI workflow.</p></li><li><p>I designed, implemented and deployed .NET Web API servers: Open Files API and Formatter for Report Distribution System.</p></li><li><p>I fixed memory leaks in .NET apps, removed performance bottlenecks and optimized SQL queries.</p></li><li><p>I worked on reporting engines in Active Reports and DevExpress.</p></li></ul>"
        + "<h2>Stack</h2><p>.NET (WinForms, Web API), Java Spring Boot, Oracle PL/SQL, Git. I work in an AI-first and Spec-driven way.</p>"
        + "<h2>My AI workflow</h2><p>Day to day I use Herdr (running in ZED), Claude Code CLI, MCP, OpenWiki, Serena and Neo4j. I experiment with agent frameworks: LangChain, DeepAgents and the Claude SDK. I am building my own AI workflow with a knowledge base based on GraphRAG, a wiki and a vector database, plus a set of useful MCPs, skills and plugins. Privately I experiment with and build my own Software Factory, which is why this site exists — to automatically deliver up-to-date news from this domain. Of course this site itself will also be developed by the Software Factory, and its content will be created by an agentic system.</p>"
        + "<h2>Courses and certificates (2025/2026)</h2><ul><li><p>Architekt Jutra</p></li><li><p>10xdevs</p></li><li><p>AI_devs3 AGENTS</p></li><li><p>Fundamentals of MCP</p></li><li><p>Cloud Computing Software Architecture Patterns</p></li></ul>"
        + "<h2>Why this portal</h2><p>Codation is where I collect and organize knowledge about harness engineering — how to build software together with AI agents.</p>";

    private const string UnderConstruction = "Page under construction.";
    private const string UnderConstructionBody = "<p><strong>Page under construction.</strong></p>";

    private static readonly Dictionary<string, string[]> VariantProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        [BlogAliases.Home] = [BlogAliases.Intro],
        [BlogAliases.Blog] = [BlogAliases.Intro],
        [BlogAliases.Post] = [BlogAliases.Summary, BlogAliases.Body],
        [BlogAliases.Category] = [BlogAliases.CategoryName, BlogAliases.Description],
        [BlogAliases.Categories] = [],
        [BlogAliases.Pillar] = [BlogAliases.PillarTitle, BlogAliases.Lead, BlogAliases.Body],
        [BlogAliases.ToolsCatalog] = [],
        [BlogAliases.Tool] = [BlogAliases.ShortDescription],
    };

    private static readonly Dictionary<string, string> ToolDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Neo4j"] = "Graph database with the Cypher query language. A foundation for GraphRAG and knowledge graphs for AI agents.",
        ["Claude Code"] = "Anthropic's AI coding agent for the terminal, IDE and browser. It plans changes, writes code, runs tests and opens pull requests.",
        ["Cursor"] = "VS Code–based editor with built-in AI agents, including cloud agents. Lets you delegate code changes and review them as pull requests.",
        ["Pi"] = "Minimalist terminal coding agent, extended with extensions, skills and prompt templates. Supports multiple model providers, including local ones via Ollama.",
        ["LangChain"] = "Open-source ecosystem for LLM apps and agents: LangChain (components and model integrations), LangGraph (stateful long-running agent workflows), DeepAgents (agents with planning, subagents and a filesystem), LangSmith (tracing and evaluations) and OpenWiki (an agent that maintains repo docs for coding agents).",
        ["Herdr"] = "Runtime for coding agents: a background terminal server where each agent (Claude Code, Codex, Cursor, Pi and others) has its own pane and keeps working after you disconnect. Shows which agent is working, waiting for a decision or done; via CLI and socket API agents can start and supervise each other, including across machines over SSH.",
        ["T3 Code"] = "Open-source control panel for coding agents from T3 (Theo): one app (web, desktop and mobile) drives Claude Code, Codex, Cursor, Grok Build, OpenCode and Antigravity under your own subscriptions. Diff preview, terminal, separate git worktrees per task and one-click pull requests.",
        ["Cezar"] = "Open-source (MIT) coding-agent orchestrator from Polish company Open Mercato. Runs parallel tasks in Claude Code, Codex, OpenCode, Cursor, Junie, Copilot CLI and Pi, each in its own git worktree, locally or 24/7 on your own VPS.",
    };

    private static readonly Dictionary<string, string> CategoryDescriptions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["t3-code"] = "Open-source control panel for coding agents from T3 (Theo, ping.gg): one app (web, desktop and mobile) drives Claude Code, Codex, Cursor, Grok Build, OpenCode and Antigravity under your own subscriptions, with diffs, a terminal, worktrees and one-click pull requests. t3.codes",
    };

    private readonly ILanguageService _languageService;
    private readonly IDictionaryItemService _dictionaryItemService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;
    private readonly IDomainService _domainService;

    public AddEnglishLanguageMigration(
        IMigrationContext context,
        ILanguageService languageService,
        IDictionaryItemService dictionaryItemService,
        IContentTypeService contentTypeService,
        IContentService contentService,
        IContentPublishingService publishingService,
        IDomainService domainService)
        : base(context)
    {
        _languageService = languageService;
        _dictionaryItemService = dictionaryItemService;
        _contentTypeService = contentTypeService;
        _contentService = contentService;
        _publishingService = publishingService;
        _domainService = domainService;
    }

    protected override async Task MigrateAsync()
    {
        RebuildCache = true;

        await EnsureLanguagesAsync();
        await EnsureDictionaryAsync();

        IContent? home = FindHome();
        List<ContentSnapshot> snapshots = home is null ? [] : Snapshot(home);
        await EnableVariationAsync();

        if (home is null)
        {
            Logger.LogWarning("No Home content node was found, so English content and hostnames were not seeded.");
            return;
        }

        await PublishEnglishAsync(home, snapshots);
        await EnsureDomainsAsync(home);
    }

    private async Task EnsureLanguagesAsync()
    {
        ILanguage? polish = await _languageService.GetAsync(SiteText.Polish);
        if (polish is null)
        {
            var created = new Language(SiteText.Polish, "Polish")
            {
                IsDefault = true,
                IsMandatory = true,
            };
            Attempt<ILanguage, LanguageOperationStatus> result = await _languageService.CreateAsync(created, Constants.Security.SuperUserKey);
            if (result.Success is false)
            {
                throw new InvalidOperationException($"Creating language '{SiteText.Polish}' failed: {result.Status}.");
            }

            Logger.LogInformation("Created Polish as the default language.");
        }
        else if (polish.IsDefault is false || polish.IsMandatory is false)
        {
            polish.IsDefault = true;
            polish.IsMandatory = true;
            Attempt<ILanguage, LanguageOperationStatus> result = await _languageService.UpdateAsync(polish, Constants.Security.SuperUserKey);
            if (result.Success is false)
            {
                throw new InvalidOperationException($"Updating language '{SiteText.Polish}' failed: {result.Status}.");
            }

            Logger.LogInformation("Marked Polish as the default mandatory language.");
        }
        else
        {
            Logger.LogInformation("Polish is already the default language.");
        }

        ILanguage? english = await _languageService.GetAsync(SiteText.English);
        if (english is null)
        {
            var created = new Language(SiteText.English, "English")
            {
                IsDefault = false,
                IsMandatory = false,
                FallbackIsoCode = SiteText.Polish,
            };
            Attempt<ILanguage, LanguageOperationStatus> result = await _languageService.CreateAsync(created, Constants.Security.SuperUserKey);
            if (result.Success is false)
            {
                throw new InvalidOperationException($"Creating language '{SiteText.English}' failed: {result.Status}.");
            }

            Logger.LogInformation("Created English with a fallback to Polish.");
            return;
        }

        if (string.Equals(english.FallbackIsoCode, SiteText.Polish, StringComparison.OrdinalIgnoreCase) is false)
        {
            english.FallbackIsoCode = SiteText.Polish;
            english.IsDefault = false;
            Attempt<ILanguage, LanguageOperationStatus> result = await _languageService.UpdateAsync(english, Constants.Security.SuperUserKey);
            if (result.Success is false)
            {
                throw new InvalidOperationException($"Updating language '{SiteText.English}' failed: {result.Status}.");
            }

            Logger.LogInformation("Set the English fallback to Polish.");
            return;
        }

        Logger.LogInformation("English already falls back to Polish.");
    }

    private async Task EnsureDictionaryAsync()
    {
        ILanguage polish = await _languageService.GetAsync(SiteText.Polish)
            ?? throw new InvalidOperationException("Polish must exist before dictionary items are seeded.");
        ILanguage english = await _languageService.GetAsync(SiteText.English)
            ?? throw new InvalidOperationException("English must exist before dictionary items are seeded.");

        foreach ((string key, string polishValue, string englishValue) in SiteText.DictionaryEntries())
        {
            if (await _dictionaryItemService.ExistsAsync(key))
            {
                IDictionaryItem? existing = await _dictionaryItemService.GetAsync(key);
                if (existing is null)
                {
                    continue;
                }

                var changed = false;
                if (HasTranslation(existing, SiteText.Polish) is false)
                {
                    existing.AddOrUpdateDictionaryValue(polish, polishValue);
                    changed = true;
                }

                if (HasTranslation(existing, SiteText.English) is false)
                {
                    existing.AddOrUpdateDictionaryValue(english, englishValue);
                    changed = true;
                }

                if (changed)
                {
                    Attempt<IDictionaryItem, DictionaryItemOperationStatus> updated = await _dictionaryItemService.UpdateAsync(existing, Constants.Security.SuperUserKey);
                    if (updated.Success is false)
                    {
                        throw new InvalidOperationException($"Updating dictionary item '{key}' failed: {updated.Status}.");
                    }
                }

                continue;
            }

            var item = new DictionaryItem(key);
            item.AddOrUpdateDictionaryValue(polish, polishValue);
            item.AddOrUpdateDictionaryValue(english, englishValue);
            Attempt<IDictionaryItem, DictionaryItemOperationStatus> created = await _dictionaryItemService.CreateAsync(item, Constants.Security.SuperUserKey);
            if (created.Success is false)
            {
                throw new InvalidOperationException($"Creating dictionary item '{key}' failed: {created.Status}.");
            }
        }

        Logger.LogInformation("Seeded Codation dictionary items for Polish and English.");
    }

    private async Task EnableVariationAsync()
    {
        foreach (KeyValuePair<string, string[]> entry in VariantProperties)
        {
            IContentType? contentType = _contentTypeService.Get(entry.Key);
            if (contentType is null)
            {
                Logger.LogWarning("Document type {Alias} is missing, so it was not marked as culture variant.", entry.Key);
                continue;
            }

            var changed = false;
            if (contentType.VariesByCulture() is false)
            {
                contentType.Variations = ContentVariation.Culture;
                changed = true;
            }

            foreach (var alias in entry.Value)
            {
                IPropertyType? property = contentType.PropertyTypes.FirstOrDefault(item => item.Alias.Equals(alias, StringComparison.OrdinalIgnoreCase));
                if (property is null)
                {
                    Logger.LogWarning("Property {Alias} is missing on {ContentType}.", alias, contentType.Alias);
                    continue;
                }

                if (property.VariesByCulture())
                {
                    continue;
                }

                property.Variations = ContentVariation.Culture;
                changed = true;
            }

            if (changed is false)
            {
                Logger.LogInformation("Document type {Alias} already varies by culture.", contentType.Alias);
                continue;
            }

            Attempt<ContentTypeOperationStatus> saved = await _contentTypeService.UpdateAsync(contentType, Constants.Security.SuperUserKey);
            if (saved.Success is false)
            {
                throw new InvalidOperationException($"Updating document type '{contentType.Alias}' failed: {saved.Result}.");
            }

            Logger.LogInformation("Enabled culture variation on {Alias}. Invariant values are kept for the default language.", contentType.Alias);
        }
    }

    private async Task PublishEnglishAsync(IContent home, List<ContentSnapshot> snapshots)
    {
        Dictionary<string, EnglishPostCopy> posts = new(EnglishPostCatalog.Load(), StringComparer.Ordinal);
        var published = 0;
        var copied = 0;

        foreach (ContentSnapshot snapshot in snapshots)
        {
            IContent? content = _contentService.GetById(snapshot.Key);
            if (content is null || content.Trashed)
            {
                continue;
            }

            if (content.ContentType.VariesByCulture() is false)
            {
                throw new InvalidOperationException($"'{content.Name}' is still invariant after the schema update.");
            }

            var dirty = RestorePolish(content, snapshot);
            dirty |= ApplyEnglish(content, snapshot, posts, ref copied);
            if (dirty is false && content.IsCulturePublished(SiteText.English))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(content.GetCultureName(SiteText.English)))
            {
                if (dirty)
                {
                    OperationResult restored = _contentService.Save(content);
                    if (restored.Success is false)
                    {
                        throw new InvalidOperationException($"Saving '{content.Name}' failed: {restored.Result}.");
                    }
                }

                continue;
            }

            await SaveAndPublishAsync(content);
            published++;
        }

        Logger.LogInformation("Published {Count} English variants. {Copied} posts had no prepared translation and kept their Polish text in English.", published, copied);
    }

    private bool ApplyEnglish(IContent content, ContentSnapshot snapshot, Dictionary<string, EnglishPostCopy> posts, ref int copied)
    {
        var alias = content.ContentType.Alias;
        if (alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase))
        {
            return SetCultureNameIfNeeded(content, PolishName(content, snapshot));
        }

        if (alias.Equals(BlogAliases.Blog, StringComparison.OrdinalIgnoreCase))
        {
            var changed = SetCultureNameIfNeeded(content, SiteText.ValuesFor(SiteText.NavNews).English);
            var intro = Text(content, snapshot, BlogAliases.Intro);
            if (string.Equals(intro, "Newest posts first.", StringComparison.Ordinal))
            {
                changed |= SetText(content, BlogAliases.Intro, intro);
            }

            return changed;
        }

        if (alias.Equals(BlogAliases.ToolsCatalog, StringComparison.OrdinalIgnoreCase))
        {
            return SetCultureNameIfNeeded(content, SiteText.ValuesFor(SiteText.NavTools).English);
        }

        if (alias.Equals(BlogAliases.Categories, StringComparison.OrdinalIgnoreCase))
        {
            return SetCultureNameIfNeeded(content, SiteText.ValuesFor(SiteText.NavCategories).English);
        }

        if (alias.Equals(BlogAliases.Tool, StringComparison.OrdinalIgnoreCase))
        {
            var name = PolishName(content, snapshot);
            var changed = SetCultureNameIfNeeded(content, name);
            if (ToolDescriptions.TryGetValue(name, out var description))
            {
                changed |= SetText(content, BlogAliases.ShortDescription, description);
            }
            else
            {
                Logger.LogWarning("No English description was prepared for tool {Name}.", name);
            }

            return changed;
        }

        if (alias.Equals(BlogAliases.Category, StringComparison.OrdinalIgnoreCase))
        {
            var name = PolishName(content, snapshot);
            var changed = SetCultureNameIfNeeded(content, name);
            var categoryName = Text(content, snapshot, BlogAliases.CategoryName);
            changed |= SetText(content, BlogAliases.CategoryName, string.IsNullOrWhiteSpace(categoryName) ? name : categoryName);
            var slug = content.GetValue<string>(BlogAliases.Slug)?.Trim();
            if ((string.IsNullOrWhiteSpace(slug) is false && CategoryDescriptions.TryGetValue(slug, out var description))
                || (string.Equals(name, "T3 Code", StringComparison.OrdinalIgnoreCase) && CategoryDescriptions.TryGetValue("t3-code", out description)))
            {
                changed |= SetText(content, BlogAliases.Description, description);
            }

            return changed;
        }

        if (alias.Equals(BlogAliases.Pillar, StringComparison.OrdinalIgnoreCase))
        {
            if (content.Key == PillarSeed.AboutKey)
            {
                var changed = SetCultureNameIfNeeded(content, SiteText.ValuesFor(SiteText.NavAbout).English);
                changed |= SetText(content, BlogAliases.PillarTitle, SiteText.ValuesFor(SiteText.NavAbout).English);
                changed |= SetText(content, BlogAliases.Lead, AboutLead);
                changed |= SetText(content, BlogAliases.Body, AboutBody);
                return changed;
            }

            if (content.Key == AddCoursesPageMigration.PageKey)
            {
                var changed = SetCultureNameIfNeeded(content, SiteText.ValuesFor(SiteText.NavCourses).English);
                changed |= SetText(content, BlogAliases.PillarTitle, SiteText.ValuesFor(SiteText.NavCourses).English);
                changed |= SetText(content, BlogAliases.Lead, UnderConstruction);
                changed |= SetText(content, BlogAliases.Body, UnderConstructionBody);
                return changed;
            }

            return SetCultureNameIfNeeded(content, PolishName(content, snapshot));
        }

        if (alias.Equals(BlogAliases.Post, StringComparison.OrdinalIgnoreCase))
        {
            var polishTitle = PolishName(content, snapshot);
            if (posts.TryGetValue(polishTitle, out EnglishPostCopy? copy) && copy is not null)
            {
                var changed = SetCultureNameIfNeeded(content, copy.Title.Trim());
                changed |= SetText(content, BlogAliases.Summary, copy.Summary.Trim());
                changed |= SetText(content, BlogAliases.Body, EnglishBody(copy.Summary.Trim(), Text(content, snapshot, BlogAliases.Body)));
                return changed;
            }

            copied++;
            Logger.LogWarning("No English translation was prepared for post {Name}. The Polish text was published for English.", polishTitle);
            var fallback = SetCultureNameIfNeeded(content, polishTitle);
            var summary = Text(content, snapshot, BlogAliases.Summary);
            var body = Text(content, snapshot, BlogAliases.Body);
            if (string.IsNullOrWhiteSpace(summary) is false)
            {
                fallback |= SetText(content, BlogAliases.Summary, summary);
            }

            if (string.IsNullOrWhiteSpace(body) is false)
            {
                fallback |= SetText(content, BlogAliases.Body, body);
            }

            return fallback;
        }

        return false;
    }

    private async Task EnsureDomainsAsync(IContent home)
    {
        Attempt<DomainUpdateResult, DomainOperationStatus> result = await _domainService.UpdateDomainsAsync(
            home.Key,
            new DomainsUpdateModel
            {
                DefaultIsoCode = SiteText.Polish,
                Domains =
                [
                    new DomainModel
                    {
                        DomainName = SiteText.EnglishDomain,
                        IsoCode = SiteText.English,
                    },
                ],
            });
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Assigning cultures and hostnames on Home failed: {result.Status}.");
        }

        Logger.LogInformation("Assigned the wildcard default to Polish and {Domain} to English.", SiteText.EnglishDomain);
    }

    private List<ContentSnapshot> Snapshot(IContent home)
    {
        var snapshots = new List<ContentSnapshot> { Capture(home) };
        foreach (IContent child in Children(home.Id))
        {
            snapshots.Add(Capture(child));
            if (child.ContentType.Alias.Equals(BlogAliases.Blog, StringComparison.OrdinalIgnoreCase)
                || child.ContentType.Alias.Equals(BlogAliases.Categories, StringComparison.OrdinalIgnoreCase)
                || child.ContentType.Alias.Equals(BlogAliases.ToolsCatalog, StringComparison.OrdinalIgnoreCase))
            {
                foreach (IContent grandchild in Children(child.Id))
                {
                    snapshots.Add(Capture(grandchild));
                }
            }
        }

        return snapshots;
    }

    private ContentSnapshot Capture(IContent content)
    {
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (VariantProperties.TryGetValue(content.ContentType.Alias, out string[]? aliases))
        {
            foreach (var alias in aliases)
            {
                if (content.HasProperty(alias) is false)
                {
                    continue;
                }

                var culture = content.ContentType.VariesByCulture() && content.Properties[alias]!.PropertyType.VariesByCulture()
                    ? SiteText.Polish
                    : null;
                values[alias] = content.GetValue<string>(alias, culture);
            }
        }

        return new ContentSnapshot(content.Key, content.Name, values);
    }

    private bool RestorePolish(IContent content, ContentSnapshot snapshot)
    {
        var changed = false;
        if (string.IsNullOrWhiteSpace(content.GetCultureName(SiteText.Polish)) && string.IsNullOrWhiteSpace(snapshot.Name) is false)
        {
            content.SetCultureName(snapshot.Name, SiteText.Polish);
            changed = true;
        }

        foreach (KeyValuePair<string, string?> pair in snapshot.Values)
        {
            if (string.IsNullOrWhiteSpace(pair.Value))
            {
                continue;
            }

            if (content.HasProperty(pair.Key) is false || content.Properties[pair.Key]!.PropertyType.VariesByCulture() is false)
            {
                continue;
            }

            var current = content.GetValue<string>(pair.Key, SiteText.Polish);
            if (string.IsNullOrWhiteSpace(current) is false)
            {
                continue;
            }

            content.SetValue(pair.Key, pair.Value, SiteText.Polish);
            changed = true;
        }

        return changed;
    }

    private IContent? FindHome()
        => _contentService.GetRootContent()
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase));

    private List<IContent> Children(int parentId)
    {
        var children = new List<IContent>();
        const int pageSize = 100;
        long page = 0;
        while (true)
        {
            IEnumerable<IContent> batch = _contentService.GetPagedChildren(parentId, page, pageSize, out long total);
            children.AddRange(batch.Where(item => item.Trashed is false));
            page++;
            if (page * pageSize >= total)
            {
                return children;
            }
        }
    }

    private async Task SaveAndPublishAsync(IContent content)
    {
        if (string.IsNullOrWhiteSpace(content.GetCultureName(SiteText.Polish)))
        {
            throw new InvalidOperationException($"'{content.Name}' has no Polish name, so it was not published.");
        }

        OperationResult saved = _contentService.Save(content);
        if (saved.Success is false)
        {
            throw new InvalidOperationException($"Saving '{content.Name}' failed: {saved.Result}.");
        }

        Attempt<ContentPublishingResult, ContentPublishingOperationStatus> published = await _publishingService.PublishAsync(
            content.Key,
            [
                new CulturePublishScheduleModel { Culture = SiteText.Polish },
                new CulturePublishScheduleModel { Culture = SiteText.English },
            ],
            Constants.Security.SuperUserKey);
        if (published.Success is false)
        {
            throw new InvalidOperationException($"Publishing '{content.Name}' failed: {published.Status}.");
        }
    }

    private static bool SetCultureNameIfNeeded(IContent content, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        var trimmed = name.Trim();
        if (string.Equals(content.GetCultureName(SiteText.English), trimmed, StringComparison.Ordinal))
        {
            return false;
        }

        content.SetCultureName(trimmed, SiteText.English);
        return true;
    }

    private static bool SetText(IContent content, string alias, string? value)
    {
        if (content.HasProperty(alias) is false || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var current = content.GetValue<string>(alias, SiteText.English);
        if (string.Equals(current, value, StringComparison.Ordinal))
        {
            return false;
        }

        content.SetValue(alias, value, SiteText.English);
        return true;
    }

    private static string PolishName(IContent content, ContentSnapshot snapshot)
    {
        var cultured = content.GetCultureName(SiteText.Polish);
        if (string.IsNullOrWhiteSpace(cultured) is false)
        {
            return cultured.Trim();
        }

        if (string.IsNullOrWhiteSpace(snapshot.Name) is false)
        {
            return snapshot.Name.Trim();
        }

        return content.Name?.Trim() ?? string.Empty;
    }

    private static string? Text(IContent content, ContentSnapshot snapshot, string alias)
    {
        if (snapshot.Values.TryGetValue(alias, out var stored) && string.IsNullOrWhiteSpace(stored) is false)
        {
            return stored;
        }

        if (content.HasProperty(alias) is false)
        {
            return null;
        }

        var culture = content.Properties[alias]!.PropertyType.VariesByCulture() ? SiteText.Polish : null;
        return content.GetValue<string>(alias, culture);
    }

    private static string EnglishBody(string summary, string? polishBody)
    {
        var html = "<p>" + WebUtility.HtmlEncode(summary) + "</p>";
        var href = FirstHttpHref(polishBody);
            if (href is not null)
            {
                html += "<p><a href=\"" + WebUtility.HtmlEncode(href) + "\">Source</a></p>";
            }

        return html;
    }

    private static string? FirstHttpHref(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return null;
        }

        const string marker = "href=\"";
        var start = html.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return null;
        }

        start += marker.Length;
        var end = html.IndexOf('"', start);
        if (end <= start)
        {
            return null;
        }

        var href = html[start..end];
        if (href.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || href.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            return href;
        }

        return null;
    }

    private static bool HasTranslation(IDictionaryItem item, string isoCode)
        => item.Translations.Any(translation =>
            translation.LanguageIsoCode.Equals(isoCode, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(translation.Value) is false);

    private sealed record ContentSnapshot(Guid Key, string? Name, Dictionary<string, string?> Values);
}
