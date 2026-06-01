using System.Text.Json;

namespace DeskPet.Agent;

public class GetDateTimeTool : ITool
{
    private static readonly JsonElement CachedSchema = JsonDocument.Parse("""
    {
        "type": "object",
        "properties": {}
    }
    """).RootElement.Clone();

    public string Name => "get_date_time";
    public string Description => "Returns the current local date and time. Use when the user asks about the time, today's date, or scheduling.";
    public JsonElement ParametersSchema => CachedSchema;

    public Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct)
    {
        var now = DateTime.Now;
        var day = now.ToString("dddd", new System.Globalization.CultureInfo("zh-CN"));
        return Task.FromResult($"{now:yyyy-MM-dd HH:mm:ss} ({day})");
    }
}
