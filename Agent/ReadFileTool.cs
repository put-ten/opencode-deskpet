using System.IO;
using System.Text.Json;

namespace DeskPet.Agent;

public class ReadFileTool : ITool
{
    private const int MaxChars = 4000;

    public string Name => "read_file";

    public string Description => "Read the contents of a file at the given path. Returns up to 4000 characters.";

    public JsonElement ParametersSchema
    {
        get
        {
            var schema = """
            {
                "type": "object",
                "properties": {
                    "path": {
                        "type": "string",
                        "description": "The absolute or relative file path to read"
                    }
                },
                "required": ["path"]
            }
            """;
            return JsonDocument.Parse(schema).RootElement.Clone();
        }
    }

    public async Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var path = arguments.GetProperty("path").GetString();
        if (string.IsNullOrWhiteSpace(path))
            return "Error: path is required";

        try
        {
            if (!File.Exists(path))
                return $"Error: file not found: {path}";

            var content = await File.ReadAllTextAsync(path, ct);
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
