using System.IO;
using System.Text.Json;

namespace DeskPet.Agent;

public class ListDirectoryTool : ITool
{
    private const int MaxEntries = 200;
    private static readonly JsonElement CachedSchema = JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {
            "path": {
                "type": "string",
                "description": "Absolute directory path. Must be under the user's home directory."
            }
        },
        "required": ["path"]
    }
    """).RootElement.Clone();

    public string Name => "list_directory";
    public string Description => "List files and subdirectories in a directory. Limited to user's home directory. Returns at most 200 entries.";
    public JsonElement ParametersSchema => CachedSchema;

    public Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var raw = arguments.GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return Task.FromResult("Error: path is required");

        try
        {
            var path = Path.GetFullPath(raw.Trim('"', '\''));
            if (!PathGuard.IsAllowed(path))
                return Task.FromResult("Error: access denied. Path must be under your home directory.");
            if (!Directory.Exists(path))
                return Task.FromResult($"Error: directory not found: {path}");

            var entries = Directory.EnumerateFileSystemEntries(path)
                .Take(MaxEntries)
                .Select(p =>
                {
                    var name = Path.GetFileName(p);
                    var isDir = Directory.Exists(p);
                    return isDir ? $"[dir]  {name}" : $"[file] {name}";
                });

            var result = entries.Any()
                ? string.Join("\n", entries)
                : "(empty directory)";
            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            return Task.FromResult($"Error listing directory: {ex.Message}");
        }
    }
}
