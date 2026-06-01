using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DeskPet.Agent;

public class SearchFilesTool : ITool
{
    private const int MaxResults = 50;
    private const int MaxDepth = 6;
    private static readonly JsonElement CachedSchema = JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "Root directory to search. Must be under the user's home directory."
            },
            "pattern": {
                "type": "string",
                "description": "Glob-style filename pattern, e.g. '*.txt', 'report*', '*budget*'."
            }
        },
        "required": ["path", "pattern"]
    }
    """).RootElement.Clone();

    public string Name => "search_files";
    public string Description => "Search for files by name pattern under a directory. Supports '*' and '?' wildcards. Recursive up to 6 levels. Returns at most 50 results.";
    public JsonElement ParametersSchema => CachedSchema;

    public Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var rawPath = arguments.GetProperty("path").GetString();
        var rawPattern = arguments.GetProperty("pattern").GetString();
        if (string.IsNullOrWhiteSpace(rawPath) || string.IsNullOrWhiteSpace(rawPattern))
            return Task.FromResult("Error: path and pattern are required");

        try
        {
            var path = Path.GetFullPath(rawPath.Trim('"', '\''));
            if (!PathGuard.IsAllowed(path))
                return Task.FromResult("Error: access denied. Path must be under your home directory.");
            if (!Directory.Exists(path))
                return Task.FromResult($"Error: directory not found: {path}");

            var regex = GlobToRegex(rawPattern);
            var matches = new List<string>();

            SearchRecursive(path, regex, matches, 0, ct);
            if (matches.Count == 0)
                return Task.FromResult($"No files matching '{rawPattern}' under {path}");
            return Task.FromResult(string.Join("\n", matches.Take(MaxResults))
                + (matches.Count > MaxResults ? $"\n\n... ({matches.Count - MaxResults} more omitted)" : ""));
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error searching: {ex.Message}");
        }
    }

    private static void SearchRecursive(string dir, Regex pattern, List<string> matches,
        int depth, CancellationToken ct)
    {
        if (depth > MaxDepth || matches.Count >= MaxResults) return;
        ct.ThrowIfCancellationRequested();
        try
        {
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                if (matches.Count >= MaxResults) return;
                if (pattern.IsMatch(Path.GetFileName(file)))
                    matches.Add(file);
            }
            foreach (var sub in Directory.EnumerateDirectories(dir))
            {
                if (matches.Count >= MaxResults) return;
                SearchRecursive(sub, pattern, matches, depth + 1, ct);
            }
        }
        catch (UnauthorizedAccessException) { }
    }

    private static Regex GlobToRegex(string glob)
    {
        var pattern = "^" + Regex.Escape(glob).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return new Regex(pattern, RegexOptions.IgnoreCase);
    }
}
