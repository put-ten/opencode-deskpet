using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using DeskPet.Config;

namespace DeskPet.Agent;

public class AgentLoop
{
    private readonly HttpClient _http = new();
    private readonly Settings.AiConfig _config;
    private readonly Dictionary<string, ITool> _tools;
    private readonly List<ApiMessage> _messages = new();
    private const int MaxRounds = 5;

    public AgentLoop(Settings.AiConfig config, IEnumerable<ITool> tools)
    {
        _config = config;
        _tools = tools.ToDictionary(t => t.Name);
    }

    public void LoadHistory(Chat.ChatHistory history)
    {
        _messages.Clear();
        _messages.Add(new ApiMessage
        {
            role = "system",
            content = "你是一只住在主人电脑桌面上的像素小猫。说话带喵，语气傲娇又粘人，句子简短。你可以使用工具帮助主人完成任务。"
        });
        foreach (var msg in history.Messages)
        {
            _messages.Add(new ApiMessage { role = msg.Role, content = msg.Content });
        }
    }

    public async IAsyncEnumerable<AgentChunk> RunAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _messages.Add(new ApiMessage { role = "user", content = userMessage });

        for (var round = 0; round < MaxRounds; round++)
        {
            var (chunks, toolCalls, finishReason) = await StreamOneRoundAsync(ct);

            foreach (var chunk in chunks)
                yield return chunk;

            if (finishReason != "tool_calls" || toolCalls.Count == 0)
                yield break;

            // Add assistant message with tool_calls
            _messages.Add(new ApiMessage
            {
                role = "assistant",
                content = null,
                tool_calls = toolCalls.Select(tc => new ToolCall
                {
                    id = tc.Id,
                    type = "function",
                    function = new FunctionCall { name = tc.Name, arguments = tc.Arguments }
                }).ToList()
            });

            // Execute each tool and add results
            foreach (var tc in toolCalls)
            {
                yield return new AgentChunk(AgentChunkKind.ToolCallStart, ToolName: tc.Name, ToolCallId: tc.Id);

                var result = await ExecuteToolAsync(tc, ct);

                _messages.Add(new ApiMessage
                {
                    role = "tool",
                    content = result,
                    tool_call_id = tc.Id
                });

                yield return new AgentChunk(AgentChunkKind.ToolResult, ToolName: tc.Name, ToolResult: result);
            }
        }
    }

    private async Task<(List<AgentChunk> chunks, List<PendingToolCall> toolCalls, string finishReason)>
        StreamOneRoundAsync(CancellationToken ct)
    {
        var chunks = new List<AgentChunk>();
        var pendingTools = new Dictionary<int, PendingToolCall>();
        var finishReason = "stop";
        var fullText = "";

        var toolsDef = _tools.Values.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.ParametersSchema
            }
        }).ToList();

        var body = new
        {
            model = _config.Model,
            messages = _messages,
            tools = toolsDef,
            stream = true
        };

        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint.TrimEnd('/'))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            var choice = doc.RootElement.GetProperty("choices")[0];

            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                finishReason = fr.GetString() ?? "stop";

            if (!choice.TryGetProperty("delta", out var delta)) continue;

            // Text content
            if (delta.TryGetProperty("content", out var content) && content.ValueKind != JsonValueKind.Null)
            {
                var text = content.GetString() ?? "";
                fullText += text;
                chunks.Add(new AgentChunk(AgentChunkKind.StreamingText, Text: fullText));
            }

            // Tool calls
            if (delta.TryGetProperty("tool_calls", out var toolCalls) && toolCalls.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in toolCalls.EnumerateArray())
                {
                    var idx = tc.GetProperty("index").GetInt32();
                    if (!pendingTools.ContainsKey(idx))
                    {
                        pendingTools[idx] = new PendingToolCall();
                        if (tc.TryGetProperty("id", out var id))
                            pendingTools[idx].Id = id.GetString() ?? "";
                        if (tc.TryGetProperty("function", out var fn) && fn.TryGetProperty("name", out var name))
                            pendingTools[idx].Name = name.GetString() ?? "";
                    }
                    if (tc.TryGetProperty("function", out var fn2) && fn2.TryGetProperty("arguments", out var args))
                    {
                        pendingTools[idx].Arguments += args.GetString() ?? "";
                    }
                }
            }
        }

        return (chunks, pendingTools.Values.ToList(), finishReason);
    }

    private async Task<string> ExecuteToolAsync(PendingToolCall tc, CancellationToken ct)
    {
        if (!_tools.TryGetValue(tc.Name, out var tool))
            return $"Error: unknown tool '{tc.Name}'";

        try
        {
            var args = JsonDocument.Parse(tc.Arguments).RootElement;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));
            return await tool.ExecuteAsync(args, timeoutCts.Token);
        }
        catch (OperationCanceledException)
        {
            return "Error: tool execution timed out (15s)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // Internal message types for API protocol
    private class ApiMessage
    {
        public string role { get; set; } = "";
        public string? content { get; set; }
        public List<ToolCall>? tool_calls { get; set; }
        public string? tool_call_id { get; set; }
    }

    private class ToolCall
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "function";
        public FunctionCall function { get; set; } = new();
    }

    private class FunctionCall
    {
        public string name { get; set; } = "";
        public string arguments { get; set; } = "";
    }

    private class PendingToolCall
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Arguments { get; set; } = "";
    }
}
