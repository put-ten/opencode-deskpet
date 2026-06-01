using System.IO;
using System.Text.Json;

namespace DeskPet.Agent;

public class ReadFileTool : ITool
{
    private const int MaxChars = 4000;
    private static readonly JsonElement CachedSchema = JsonDocument.Parse("""
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
    """).RootElement.Clone();

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
            var path = Path.GetFullPath(raw.Trim('"', '\''));
            if (!PathGuard.IsAllowed(path))
                return "Error: access denied. Path must be under your home directory.";
            if (!File.Exists(path))
                return $"Error: file not found: {path}";

            var content = await File.ReadAllTextAsync(path, ct);
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
