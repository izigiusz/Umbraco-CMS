using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using Swashbuckle.AspNetCore.SwaggerUI;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Hosting;
using Umbraco.Cms.Web.Common.ApplicationBuilder;
using Umbraco.Extensions;
using IHostingEnvironment = Umbraco.Cms.Core.Hosting.IHostingEnvironment;

namespace Umbraco.Cms.Api.Common.OpenApi;

public class SwaggerRouteTemplatePipelineFilter : UmbracoPipelineFilter
{
    public SwaggerRouteTemplatePipelineFilter(string name)
        : base(name)
        => PostPipeline = PostPipelineAction;

    private void PostPipelineAction(IApplicationBuilder applicationBuilder)
    {
        if (SwaggerIsEnabled(applicationBuilder) is false)
        {
            return;
        }

        IOptions<SwaggerGenOptions> swaggerGenOptions = applicationBuilder.ApplicationServices.GetRequiredService<IOptions<SwaggerGenOptions>>();

        applicationBuilder.UseSwagger(swaggerOptions =>
        {
            swaggerOptions.RouteTemplate = SwaggerRouteTemplate(applicationBuilder);
        });

        applicationBuilder.UseSwaggerUI(swaggerUiOptions => SwaggerUiConfiguration(swaggerUiOptions, swaggerGenOptions.Value, applicationBuilder));
    }

    protected virtual bool SwaggerIsEnabled(IApplicationBuilder applicationBuilder)
        => applicationBuilder.ApplicationServices.GetRequiredService<IWebHostEnvironment>().IsProduction() is false;

    protected virtual string SwaggerRouteTemplate(IApplicationBuilder applicationBuilder)
        => $"{GetBackOfficePath(applicationBuilder).TrimStart(Constants.CharArrays.ForwardSlash)}/swagger/{{documentName}}/swagger.json";

    protected virtual string SwaggerUiRoutePrefix(IApplicationBuilder applicationBuilder)
        => $"{GetBackOfficePath(applicationBuilder).TrimStart(Constants.CharArrays.ForwardSlash)}/swagger";

    protected virtual void SwaggerUiConfiguration(
        SwaggerUIOptions swaggerUiOptions,
        SwaggerGenOptions swaggerGenOptions,
        IApplicationBuilder applicationBuilder)
    {
        swaggerUiOptions.RoutePrefix = SwaggerUiRoutePrefix(applicationBuilder);

        foreach ((var name, OpenApiInfo? apiInfo) in swaggerGenOptions.SwaggerGeneratorOptions.SwaggerDocs.OrderBy(x => x.Value.Title))
        {
            swaggerUiOptions.SwaggerEndpoint($"{name}/swagger.json", $"{apiInfo.Title}");
        }

        // Add custom configuration from https://swagger.io/docs/open-source-tools/swagger-ui/usage/configuration/
        swaggerUiOptions.ConfigObject.PersistAuthorization = true; // persists authorization data so it would not be lost on browser close/refresh
        swaggerUiOptions.ConfigObject.Filter = string.Empty; // Enable the filter with an empty string as default filter.

        swaggerUiOptions.OAuthClientId(Constants.OAuthClientIds.Swagger);
        swaggerUiOptions.OAuthUsePkce();
    }

    private string GetBackOfficePath(IApplicationBuilder applicationBuilder)
        => applicationBuilder.ApplicationServices.GetRequiredService<IHostingEnvironment>().GetBackOfficePath();
}
