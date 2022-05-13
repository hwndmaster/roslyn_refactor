using Ganss.IO;
using Microsoft.Extensions.Configuration;

public class AppSettings
{
    public AppSettings()
    {
        FilesIncludePatternsAsGlob = new Lazy<Glob[]>(() => FilesIncludePatterns.Select(x => new Glob(x)).ToArray());
        FilesExcludePatternsAsGlob = new Lazy<Glob[]>(() => FilesExcludePatterns.Select(x => new Glob(x)).ToArray());
    }

    public string Solution { get; set; } = string.Empty;

    public bool RewriteFiles { get; set; } = false;

    [ConfigurationKeyName("projects.exclude.names")]
    public string[] ProjectsExcludeNames { get; set; } = Array.Empty<string>();

    [ConfigurationKeyName("projects.include.names")]
    public string[] ProjectsIncludeNames { get; set; } = Array.Empty<string>();

    [ConfigurationKeyName("files.exclude.patterns")]
    public string[] FilesExcludePatterns { get; set; } = Array.Empty<string>();

    [ConfigurationKeyName("files.include.patterns")]
    public string[] FilesIncludePatterns { get; set; } = Array.Empty<string>();

    public Lazy<Glob[]> FilesIncludePatternsAsGlob { get; }
    public Lazy<Glob[]> FilesExcludePatternsAsGlob { get; }

    public static AppSettings LoadConfiguration()
    {
        var configuration = new ConfigurationBuilder()
            .AddJsonFile("appconfig.json", optional: true)
            .Build();
        var appSettingsSection = configuration.GetSection("appSettings");
        var appSettings = new AppSettings();
        appSettingsSection.Bind(appSettings);

        if (string.IsNullOrEmpty(appSettings.Solution))
        {
            throw new InvalidOperationException("The 'solution' configuration setting was not found in appconfig.json file.");
        }

        return appSettings;
    }
}
