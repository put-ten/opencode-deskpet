using System.IO;
using System.Text.Json;

namespace DeskPet.Agent;

public class ReadFileTool : ITool
{
    private const int MaxChars = 4000;
    private static readonly string[] AllowedRoots;
    private static readonly JsonElement CachedSchema;

    static ReadFileTool()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        AllowedRoots = new[] { home, desktop, docs };

        var schema = """
        {
            "type": "object",
            "properties": {
                "path": {
                    "type": "string",
                    "description": "The absolute file path to read. Must be under the user's home directory."
                }
            },
            "required": ["path"]
        }
        """;
        CachedSchema = JsonDocument.Parse(schema).RootElement.Clone();
    }

    public string Name => "read_file";
    public string Description => "Read the contents of a file. Limited to user's home directory.";
    public JsonElement ParametersSchema => CachedSchema;

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var raw = arguments.GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(raw))
            return "Error: path is required";

        try
        {
            var path = Path.GetFullPath(raw.Trim('\"', '\''));
            var dir = Path.GetDirectoryName(path);

            if (dir != null && !AllowedRoots.Any(r => path.StartsWith(r, StringComparison.OrdinalIgnoreCase)))
                return $"Error: access denied. File must be under your home directory.";

            if (!File.Exists(path))
                return $"Error: file not found: {path}";

            var content = await File.ReadAllTextAsync(path, ct);

            // Check for binary content
            if (content.Contains('\0'))
                return "Error: file appears to be binary";

            if (content.Length > MaxChars)
                return content[..MaxChars] + $"\n\n... (truncated, {content.Length - MaxChars} chars omitted)";

            return content;
        }
        catch (Exception ex)
        {
            return $"Error reading file: {ex.Message}";
        }
    }
}
