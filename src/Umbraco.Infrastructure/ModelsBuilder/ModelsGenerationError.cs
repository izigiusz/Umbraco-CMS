using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Extensions;

namespace Umbraco.Cms.Infrastructure.ModelsBuilder;

public sealed class ModelsGenerationError
{
    private readonly IHostEnvironment _hostEnvironment;
    private ModelsBuilderSettings _config;

    public ModelsGenerationError(IOptionsMonitor<ModelsBuilderSettings> config, IHostEnvironment hostEnvironment)
    {
        _config = config.CurrentValue;
        _hostEnvironment = hostEnvironment;
        config.OnChange(x => _config = x);
    }

    public void Clear()
    {
        var errFile = GetErrFile();
        if (errFile == null)
        {
            return;
        }

        // "If the file to be deleted does not exist, no exception is thrown."
        File.Delete(errFile);
    }

    public void Report(string message, Exception e)
    {
        var errFile = GetErrFile();
        if (errFile == null)
        {
            return;
        }

        var sb = new StringBuilder();
        sb.Append(message);
        sb.Append("\r\n");
        sb.Append(e.Message);
        sb.Append("\r\n\r\n");
        sb.Append(e.StackTrace);
        sb.Append("\r\n");

        File.WriteAllText(errFile, sb.ToString());
    }

    public string? GetLastError()
    {
        var errFile = GetErrFile();
        if (errFile == null)
        {
            return null;
        }

        try
        {
            return File.ReadAllText(errFile);
        }
        catch
        {
            // accepted
            return null;
        }
    }

    private string? GetErrFile()
    {
        var modelsDirectory = _config.ModelsDirectoryAbsolute(_hostEnvironment);
        if (!Directory.Exists(modelsDirectory))
        {
            return null;
        }

        return Path.Combine(modelsDirectory, "models.err");
    }
}
