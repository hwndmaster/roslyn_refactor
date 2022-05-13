using Ganss.IO;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

MSBuildLocator.RegisterDefaults();

//MSBuildLocator.RegisterMSBuildPath(@"c:\Windows\Microsoft.NET\Framework64\v4.0.30319\");

Console.Write("Loading configuration... ");
var appSettings = AppSettings.LoadConfiguration();
Console.WriteLine("Done.");

Console.WriteLine("Warming up MSBuild and loading a solution... ");
var workspace = MSBuildWorkspace.Create();
workspace.WorkspaceFailed += Workspace_WorkspaceFailed;
var solution = await workspace.OpenSolutionAsync(appSettings.Solution);
Console.WriteLine("Done.");

var projects = FilterProject(solution.Projects, appSettings);

var sourceVisitors = new ISourceVisitor[] {
    new ResourcesExSourceVisitor()
};

foreach (var project in projects)
{
    Console.WriteLine("Project: " + project.Name);

    var documents = FilterDocuments(project.Documents, appSettings);

    foreach (var document in documents)
    {
        Console.WriteLine("File: " + document.Name);

        var updatedDocument = document;
        foreach (var sourceVisitor in sourceVisitors)
        {
            updatedDocument = await sourceVisitor.VisitAsync(updatedDocument);
        }

        if (!ReferenceEquals(updatedDocument, document))
        {
            var root = await updatedDocument.GetSyntaxRootAsync();
            if (root is null)
            {
                WriteError("Root cannot be found.");
                continue;
            }
            var updatedSourceCode = root.ToFullString();

            var targetFileName = document.FilePath;
            if (targetFileName is null)
            {
                WriteError("document.FilePath is null.");
                continue;
            }
            if (!appSettings.RewriteFiles)
            {
                targetFileName += ".upd";
            }
            File.WriteAllText(targetFileName, updatedSourceCode);
        }
    }
}

Console.WriteLine("Finished.");
Console.ReadKey();



static void Workspace_WorkspaceFailed(object? sender, WorkspaceDiagnosticEventArgs e)
{
    var originalColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.DarkYellow;
    Console.WriteLine(e.Diagnostic.Message);
    Console.ForegroundColor = originalColor;
}

static void WriteError(string message)
{
    var originalColor = Console.ForegroundColor;
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine(message);
    Console.ForegroundColor = originalColor;
}

static IEnumerable<Project> FilterProject(IEnumerable<Project> projects, AppSettings appSettings)
{
    if (appSettings.ProjectsIncludeNames?.Any() == true)
    {
        return projects.Where(p => appSettings.ProjectsIncludeNames.Contains(p.Name));
    }
    else if (appSettings.ProjectsExcludeNames?.Any() == true)
    {
        return projects.Where(p => !appSettings.ProjectsExcludeNames.Contains(p.Name));
    }

    return projects;
}

static IEnumerable<Document> FilterDocuments(IEnumerable<Document> documents, AppSettings appSettings)
{
    if (appSettings.FilesIncludePatterns?.Any() == true)
    {
        return documents.Where(doc => doc.FilePath is not null
            && appSettings.FilesIncludePatternsAsGlob.Value.Any(glob => glob.IsMatch(doc.Name) || glob.IsMatch(doc.FilePath)));
    }
    else if (appSettings.FilesExcludePatterns?.Any() == true)
    {
        return documents.Where(doc => doc.FilePath is null
            || !appSettings.FilesExcludePatternsAsGlob.Value.Any(glob => glob.IsMatch(doc.Name) || glob.IsMatch(doc.FilePath)));
    }

    return documents;
}
