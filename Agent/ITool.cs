using System.Text.Json;

namespace DeskPet.Agent;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    JsonElement ParametersSchema { get; }
    Task<string> ExecuteAsync(JsonElement arguments, CancellationToken ct);
}

public enum AgentChunkKind
{
    StreamingText,
    ToolCallStart,
    ToolResult,
    FinalText
}

public record AgentChunk(
    AgentChunkKind Kind,
    string? Text = null,
    string? ToolName = null,
    string? ToolCallId = null,
    string? ToolResult = null
);
