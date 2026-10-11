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
/// Adds the pillar document type, an optional category color, and four draft pillar pages.
/// Existing content, properties and values are left unchanged.
/// </summary>
public class AddPortalPillarsMigration : AsyncMigrationBase
{
    private const string PickerEditorUiAlias = "Umb.PropertyEditorUi.ContentPicker";
    private const string EyeDropperEditorUiAlias = "Umb.PropertyEditorUi.EyeDropper";

    private readonly IContentTypeService _contentTypeService;
    private readonly IDataTypeService _dataTypeService;
    private readonly ITemplateService _templateService;
    private readonly IContentService _contentService;
    private readonly IContentPublishingService _publishingService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _serializer;

    public AddPortalPillarsMigration(
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

        ITemplate template = await EnsureTemplateAsync(BlogAliases.PillarTemplate, "Pillar");
        await EnsureAccentColorAsync(categoryType);

        IDataType relatedPicker = await EnsurePickerAsync(BlogAliases.PillarCategoriesDataTypeName, categoryType.Key);
        IContentType pillarType = await EnsurePillarTypeAsync(template, relatedPicker);

        if (AllowChild(homeType, pillarType))
        {
            await SaveContentTypeAsync(homeType);
            Logger.LogInformation("Allowed {Child} under {Parent}.", BlogAliases.Pillar, BlogAliases.Home);
        }

        await SeedPillarsAsync(pillarType);
    }

    private async Task EnsureAccentColorAsync(IContentType categoryType)
    {
        if (categoryType.PropertyTypeExists(BlogAliases.AccentColor))
        {
            Logger.LogInformation("Property {Alias} already exists on {ContentType}; leaving it unchanged.", BlogAliases.AccentColor, BlogAliases.Category);
            return;
        }

        IDataType color = await EnsureEyeDropperAsync();
        AddProperty(categoryType, color, BlogAliases.AccentColor, "Kolor", 3, false, "Optional. Empty categories use the portal palette.");
        await SaveContentTypeAsync(categoryType);
        Logger.LogInformation("Added property {Alias} to {ContentType}.", BlogAliases.AccentColor, BlogAliases.Category);
    }

    private async Task<IContentType> EnsurePillarTypeAsync(ITemplate template, IDataType relatedPicker)
    {
        IContentType? existing = _contentTypeService.Get(BlogAliases.Pillar);
        if (existing is not null)
        {
            Logger.LogInformation("Document type {Alias} already exists; leaving its properties unchanged.", BlogAliases.Pillar);
            return existing;
        }

        IDataType text = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextstringGuid);
        IDataType area = await RequiredDataTypeAsync(Constants.DataTypes.Guids.TextareaGuid);
        IDataType richText = await RequiredDataTypeAsync(Constants.DataTypes.Guids.RichtextEditorGuid);
        IDataType numeric = await RequiredDataTypeAsync(Constants.DataTypes.Guids.NumericGuid);

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = BlogAliases.Pillar,
            Name = "Pillar",
            Icon = "icon-science",
            Description = "A Codation section page: Harness Engineering, Software Factory, and similar topics.",
            AllowedAsRoot = false,
            IsElement = false,
            AllowedTemplates = [template],
        };
        contentType.SetDefaultTemplate(template);
        AddProperty(contentType, text, BlogAliases.PillarTitle, "Tytuł", 0, false, "Shown as the page title. The node name is used when this is empty.");
        AddProperty(contentType, area, BlogAliases.Lead, "Lead", 1, false, "Short introduction.");
        AddProperty(contentType, richText, BlogAliases.Body, "Treść", 2, false, null);
        AddProperty(contentType, relatedPicker, BlogAliases.RelatedCategories, "Powiązane kategorie", 3, false, "Optional. Posts from these categories are listed on the page.");
        AddProperty(contentType, numeric, BlogAliases.PillarOrder, "Kolejność", 4, false, "Lower numbers appear first.");

        Attempt<ContentTypeOperationStatus> created = await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);
        if (created.Success is false)
        {
            throw new InvalidOperationException($"Creating document type '{BlogAliases.Pillar}' failed: {created.Result}.");
        }

        Logger.LogInformation("Created document type {Alias}.", BlogAliases.Pillar);
        return contentType;
    }

    private async Task SeedPillarsAsync(IContentType pillarType)
    {
        IContent? home = _contentService.GetRootContent()
            .FirstOrDefault(item => item.ContentType.Alias.Equals(BlogAliases.Home, StringComparison.OrdinalIgnoreCase));
        if (home is null)
        {
            Logger.LogWarning("No Home content node was found, so pillar pages were not seeded.");
            return;
        }

        foreach (PillarSeedItem item in PillarSeed.Items)
        {
            if (_contentService.GetById(item.Key) is not null)
            {
                Logger.LogInformation("Pillar {Title} already exists; it was not modified.", item.Title);
                continue;
            }

            var node = _contentService.Create(item.Title, home, pillarType.Alias);
            node.Key = item.Key;
            node.SetValue(BlogAliases.PillarTitle, item.Title);
            node.SetValue(BlogAliases.Lead, item.Lead);
            node.SetValue(BlogAliases.Body, item.Body);
            node.SetValue(BlogAliases.PillarOrder, item.Order);
            if (pillarType.DefaultTemplateId > 0)
            {
                node.TemplateId = pillarType.DefaultTemplateId;
            }

            IContent? related = FindCategoryBySlug(item.RelatedCategorySlug);
            if (related is not null)
            {
                node.SetValue(BlogAliases.RelatedCategories, Udi.Create(Constants.UdiEntityType.Document, related.Key).ToString());
            }

            await SaveAndPublishAsync(node);
            Logger.LogInformation("Published pillar {Title}.", item.Title);
        }
    }

    private IContent? FindCategoryBySlug(string? slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            return null;
        }

        IContentType? categoryType = _contentTypeService.Get(BlogAliases.Category);
        if (categoryType is null)
        {
            return null;
        }

        long page = 0;
        while (true)
        {
            IEnumerable<IContent> batch = _contentService.GetPagedOfTypes([categoryType.Id], page, 100, out long total, null);
            foreach (IContent item in batch)
            {
                var existing = item.GetValue<string>(BlogAliases.Slug)?.Trim();
                if (string.Equals(existing, slug, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }
            }

            page++;
            if (page * 100 >= total)
            {
                return null;
            }
        }
    }

    private async Task<IDataType> EnsureEyeDropperAsync()
    {
        IDataType? existing = await _dataTypeService.GetAsync(BlogAliases.AccentColorDataTypeName);
        if (existing is not null)
        {
            Logger.LogInformation("Data type {Name} already exists; leaving its configuration unchanged.", BlogAliases.AccentColorDataTypeName);
            return existing;
        }

        if (_propertyEditors.TryGet(Constants.PropertyEditors.Aliases.ColorPickerEyeDropper, out IDataEditor? editor) is false || editor is null)
        {
            throw new InvalidOperationException("The eye dropper color picker is not registered.");
        }

        var dataType = new DataType(editor, _serializer)
        {
            Name = BlogAliases.AccentColorDataTypeName,
            EditorUiAlias = EyeDropperEditorUiAlias,
            DatabaseType = ValueStorageType.Nvarchar,
        };

        Attempt<IDataType, DataTypeOperationStatus> created = await _dataTypeService.CreateAsync(dataType, Constants.Security.SuperUserKey);
        if (created.Success is false || created.Result is null)
        {
            throw new InvalidOperationException($"Creating data type '{BlogAliases.AccentColorDataTypeName}' failed: {created.Status}.");
        }

        return created.Result;
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

        var dataType = new DataType(editor, _serializer)
        {
            Name = name,
            EditorUiAlias = PickerEditorUiAlias,
            DatabaseType = ValueStorageType.Ntext,
            ConfigurationData = new Dictionary<string, object>
            {
                ["startNode"] = new Dictionary<string, object> { ["type"] = "content" },
                ["minNumber"] = 0,
                ["maxNumber"] = 0,
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
           ?? throw new InvalidOperationException($"Document type '{alias}' is missing, so portal pillars could not be added.");

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
