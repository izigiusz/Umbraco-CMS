using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.ContentPublishing;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Services.OperationStatus;
using Umbraco.Cms.Core.Strings;
using Umbraco.Cms.Infrastructure.Migrations;

namespace Umbraco.Cms.Web.UI.Blog;

/// <summary>
/// Adds the Narzędzia list and the tool document type, then publishes one list node under Home.
/// Existing content, properties and values are left unchanged.
/// </summary>
public class AddToolsCatalogMigration : AsyncMigrationBase
{
    public const string CatalogName = "Narzędzia";

    /// <summary>
    /// Stable key of the seeded list node. A later boot creates its public URL.
    /// </summary>
    public static readonly Guid CatalogKey = new("c0de4110-0000-4000-8000-000000000020");

    private const string PickerEditorUiAlias = "Umb.PropertyEditorUi.ContentPicker";
    private const string ToolUrlPattern = @"^https?://\S+$";
    private const string ToolUrlPatternMessage = "Podaj adres zaczynający się od http:// lub https://.";

    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly ITemplateService _templateService;
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;

    public AddToolsCatalogMigration(
        IMigrationContext context,
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        ITemplateService templateService,
        IContentService contentService,
        IContentPublishingService publishingService,
        IShortStringHelper shortStringHelper,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer serializer)
        : base(context)
    {
        _contentTypeService = contentTypeService;
        _dataTypeService = dataTypeService;
        _templateService = templateService;
        _contentService = contentService;
        _publishingService = publishingService;
        _shortStringHelper = shortStringHelper;
        _propertyEditors = propertyEditors;
        _serializer = serializer;
    }

    protected override async Task MigrateAsync()
    {
        RebuildCache = true;

        IContentType homeType = RequiredContentType(BlogAliases.Home);
        IContentType categoryType = RequiredContentType(BlogAliases.Category);

        ITemplate template = await EnsureTemplateAsync(BlogAliases.ToolsCatalogTemplate, "Tools catalog");
        IDataType categoryPicker = await EnsurePickerAsync(BlogAliases.ToolCategoryDataTypeName, categoryType.Key);
        IContentType toolType = await EnsureToolTypeAsync(categoryPicker);
        IContentType catalogType = await EnsureCatalogTypeAsync(template, toolType);

        if (AllowChild(homeType, catalogType))
        {
            await SaveContentTypeAsync(homeType);
            Logger.LogInformation("Allowed {Child} under {Parent}.", BlogAliases.ToolsCatalog, BlogAliases.Home);
        }

        await SeedCatalogAsync(catalogType);
    }

    private async Task<IContentType> EnsureToolTypeAsync(IDataType categoryPicker)
    {
        IContentType? existing = _contentTypeService.Get(BlogAliases.Tool);
        if (existing is not null)
        {
            await AddMissingToolPropertiesAsync(existing, categoryPicker);
            return _contentTypeService.Get(BlogAliases.Tool) ?? existing;
        }

        IDataType text = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextstringGuid);
        IDataType area = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextareaGuid);
        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = BlogAliases.Tool,
            Name = "Narzędzie",
            Icon = "icon-link",
            Description = "A tool listed on Narzędzia. The node name is the tool name. It has no page of its own.",
            AllowedAsRoot = false,
            IsElement = false,
        };
        AddToolProperties(contentType, text, area, categoryPicker);

        Attempt<ContentTypeOperationStatus> created = await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);
        if (created.Success is false)
        {
            throw new InvalidOperationException($"Creating document type '{BlogAliases.Tool}' failed: {created.Result}.");
        }

        Logger.LogInformation("Created document type {Alias}.", BlogAliases.Tool);
        return contentType;
    }

    private async Task AddMissingToolPropertiesAsync(IContentType contentType, IDataType categoryPicker)
    {
        var changed = false;
        IDataType text = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextstringGuid);
        IDataType area = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextareaGuid);
        if (contentType.PropertyTypeExists(BlogAliases.ToolUrl) is false)
        {
            AddUrlProperty(contentType, text, 0);
            changed = true;
        }

        if (contentType.PropertyTypeExists(BlogAliases.ShortDescription) is false)
        {
            AddProperty(contentType, area, BlogAliases.ShortDescription, "Krótki opis", 1, false, "Krótki opis na liście.");
            changed = true;
        }

        if (contentType.PropertyTypeExists(BlogAliases.ToolCategory) is false)
        {
            AddProperty(contentType, categoryPicker, BlogAliases.ToolCategory, "Kategoria", 2, false, "Jedna kategoria z katalogu /kategorie/.");
            changed = true;
        }

        if (changed)
        {
            await SaveContentTypeAsync(contentType);
            Logger.LogInformation("Added missing properties on {ContentType}.", BlogAliases.Tool);
            return;
        }

        Logger.LogInformation("Document type {Alias} already exists; leaving its properties unchanged.", BlogAliases.Tool);
    }

    private async Task<IContentType> EnsureCatalogTypeAsync(ITemplate template, IContentType toolType)
    {
        IContentType? existing = _contentTypeService.Get(BlogAliases.ToolsCatalog);
        if (existing is not null)
        {
            var changed = AllowChild(existing, toolType);
            if (existing.DefaultTemplateId == 0)
            {
                existing.AllowedTemplates = [template];
                existing.SetDefaultTemplate(template);
                changed = true;
            }

            if (changed)
            {
                await SaveContentTypeAsync(existing);
                Logger.LogInformation("Updated allowed children or template on {Alias} without changing its properties.", BlogAliases.ToolsCatalog);
            }
            else
            {
                Logger.LogInformation("Document type {Alias} already exists; leaving its properties unchanged.", BlogAliases.ToolsCatalog);
            }

            return existing;
        }

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = BlogAliases.ToolsCatalog,
            Name = CatalogName,
            Icon = "icon-wrench",
            Description = "Lists published tools by name.",
            AllowedAsRoot = false,
            IsElement = false,
            ListView = Constants.DataTypes.Guids.ListViewContentGuid,
            AllowedTemplates = [template],
            AllowedContentTypes = [new ContentTypeSort(toolType.Key, 0, toolType.Alias)],
        };
        contentType.SetDefaultTemplate(template);

        Attempt<ContentTypeOperationStatus> created = await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);
        if (created.Success is false)
        {
            throw new InvalidOperationException($"Creating document type '{BlogAliases.ToolsCatalog}' failed: {created.Result}.");
        }

        Logger.LogInformation("Created document type {Alias}.", BlogAliases.ToolsCatalog);
        return contentType;
    }

    private async Task SeedCatalogAsync(IContentType catalogType)
    {
        if (_contentService.GetById(CatalogKey) is not null)
        {
            Logger.LogInformation("{Name} already exists; it was not modified.", CatalogName);
            return;
        }

        IContent? home = _contentService.GetRootContent()
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase));
        if (home is null)
        {
            Logger.LogWarning("No Home content node was found, so {Name} was not seeded.", CatalogName);
            return;
        }

        IContent? existing = _contentService.GetPagedChildren(home.Id, 0, 100, out _)
            .FirstOrDefault(item => item.Trashed is false && item.ContentType.Alias.Equals(BlogAliases.ToolsCatalog, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            Logger.LogInformation("A {Name} node already exists under Home; it was not modified.", CatalogName);
            return;
        }

        var node = _contentService.Create(CatalogName, home, catalogType.Alias);
        node.Key = CatalogKey;
        if (catalogType.DefaultTemplateId > 0)
        {
            node.TemplateId = catalogType.DefaultTemplateId;
        }

        await SaveAndPublishAsync(node);
        Logger.LogInformation("Published {Name}.", CatalogName);
    }

    private async Task<IDataType> EnsurePickerAsync(string name, Guid allowedContentTypeKey)
    {
        IDataType? existing = await _dataTypeService.GetAsync(name);
        if (existing is not null)
        {
            Logger.LogInformation("Data type {Name} already exists; leaving its configuration unchanged.", name);
            return existing;
        }

        if (_propertyEditors.TryGet(Constants.PropertyEditors.Aliases.MultiNodeTreePicker, out IDataEditor? editor) is false || editor is null)
        {
            throw new InvalidOperationException("The Multi Node Tree Picker editor is not registered.");
        }

        var startNode = new Dictionary<string, object> { ["type"] = "content" };
        if (_contentService.GetById(CategorySeed.ContainerKey) is { Trashed: false })
        {
            startNode["id"] = CategorySeed.ContainerKey.ToString();
        }

        var dataType = new DataType(editor, _serializer)
        {
            Name = name,
            EditorUiAlias = PickerEditorUiAlias,
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = new Dictionary<string, object>
            {
                ["startNode"] = startNode,
                ["minNumber"] = 0,
                ["maxNumber"] = 1,
                ["filter"] = allowedContentTypeKey.ToString(),
                ["ignoreUserStartNodes"] = false,
            },
        };

        Attempt<IDataType, DataTypeOperationStatus> created = await _dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        if (created.Success is false || created.Result is null)
        {
            throw new InvalidOperationException($"Creating data type '{name}' failed: {created.Status}.");
        }

        return created.Result;
    }

    private async Task<ITemplate> EnsureTemplateAsync(string alias, string name)
    {
        ITemplate? existing = await _templateService.GetAsync(alias);
        if (existing is not null)
        {
            return existing;
        }

        Attempt<ITemplate, TemplateOperationStatus> created = await _templateService.CreateAsync(
            name,
            alias,
            content: null,
            Constants.Security.SuperUserKey);
        if (created.Success is false || created.Result is null)
        {
            throw new InvalidOperationException($"Creating template '{alias}' failed: {created.Status}.");
        }

        return created.Result;
    }

    private async Task SaveAndPublishAsync(IContent content)
    {
        OperationResult saved = _contentService.Save(content);
        if (saved.Success is false)
        {
            throw new InvalidOperationException($"Saving '{content.Name}' failed: {saved.Result}.");
        }

        Attempt<ContentPublishingResult, ContentPublishingOperationStatus> published = await _publishingService.PublishAsync(
            content.Key,
            [new CulturePublishScheduleModel()],
            Constants.Security.SuperUserKey);
        if (published.Success is false)
        {
            throw new InvalidOperationException($"Publishing '{content.Name}' failed: {published.Status}.");
        }
    }

    private async Task SaveContentTypeAsync(IContentType contentType)
    {
        Attempt<ContentTypeOperationStatus> result = await _contentTypeService.UpdateAsync(contentType, Constants.Security.SuperUserKey);
        if (result.Success is false)
        {
            throw new InvalidOperationException($"Updating document type '{contentType.Alias}' failed: {result.Result}.");
        }
    }

    private static bool AllowChild(IContentType parent, IContentType child)
    {
        List<ContentTypeSort> allowed = parent.AllowedContentTypes?.ToList() ?? [];
        if (allowed.Any(item => item.Alias.Equals(child.Alias, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        allowed.Add(new ContentTypeSort(child.Key, allowed.Count, child.Alias));
        parent.AllowedContentTypes = allowed;
        return true;
    }

    private async Task<IDataType> RequiredDataTypeAsync(Guid key)
        => await _dataTypeService.GetAsync(key)
           ?? throw new InvalidOperationException($"Required Umbraco data type {key} was not found.");

    private IContentType RequiredContentType(string alias)
        => _contentTypeService.Get(alias)
           ?? throw new InvalidOperationException($"Document type '{alias}' is missing, so the tools catalog could not be added.");

    private void AddToolProperties(IContentType contentType, IDataType text, IDataType area, IDataType categoryPicker)
    {
        AddUrlProperty(contentType, text, 0);
        AddProperty(contentType, area, BlogAliases.ShortDescription, "Krótki opis", 1, false, "Krótki opis na liście.");
        AddProperty(contentType, categoryPicker, BlogAliases.ToolCategory, "Kategoria", 2, false, "Jedna kategoria z katalogu /kategorie/.");
    }

    private void AddUrlProperty(IContentType contentType, IDataType dataType, int sortOrder)
    {
        var property = new PropertyType(_shortStringHelper, dataType, BlogAliases.ToolUrl)
        {
            Name = "Adres",
            SortOrder = sortOrder,
            Mandatory = true,
            Description = "Pełny adres strony narzędzia.",
            ValidationRegExp = ToolUrlPattern,
            ValidationRegExpMessage = ToolUrlPatternMessage,
            DataTypeKey = dataType.Key,
        };

        if (contentType.AddPropertyType(property, BlogAliases.PropertyGroup, "Content") is false)
        {
            throw new InvalidOperationException($"Could not add property '{BlogAliases.ToolUrl}' to document type '{contentType.Alias}'.");
        }
    }

    private void AddProperty(
        IContentType contentType,
        IDataType dataType,
        string alias,
        string name,
        int sortOrder,
        bool mandatory,
        string? description)
    {
        var property = new PropertyType(_shortStringHelper, dataType, alias)
        {
            Name = name,
            SortOrder = sortOrder,
            Mandatory = mandatory,
            Description = description,
            DataTypeKey = dataType.Key,
        };

        if (contentType.AddPropertyType(property, BlogAliases.PropertyGroup, "Content") is false)
        {
            throw new InvalidOperationException($"Could not add property '{alias}' to document type '{contentType.Alias}'.");
        }
    }
}
