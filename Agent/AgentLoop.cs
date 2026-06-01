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
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public AgentLoop(Settings.AiConfig config, IEnumerable<ITool> tools)
    {
        _config = config;
        _tools = tools.ToDictionary(t => t.Name);
    }

    public void LoadHistory(Chat.ChatHistory history)
    {
        _messages.Clear();
        var settings = Config.Settings.Load();
        var prompt = string.IsNullOrWhiteSpace(settings.SystemPrompt)
            ? "你是一只住在主人电脑桌面上的像素小猫。说话带喵，语气傲娇又粘人，句子简短。你可以使用工具帮助主人完成任务。"
            : settings.SystemPrompt;
        _messages.Add(new ApiMessage { role = "system", content = prompt });
        foreach (var msg in history.Messages)
            _messages.Add(new ApiMessage { role = msg.Role, content = msg.Content });
    }

    public async IAsyncEnumerable<AgentChunk> RunAsync(
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _messages.Add(new ApiMessage { role = "user", content = userMessage });

        for (var round = 0; round < MaxRounds; round++)
        {
            var (fullText, toolCalls, finishReason, reasoning) = await CallApiAsync(ct);

            if (!string.IsNullOrEmpty(fullText))
                yield return new AgentChunk(AgentChunkKind.StreamingText, Text: fullText);

            if (finishReason == "tool_calls" && toolCalls.Count > 0)
            {
                _messages.Add(new ApiMessage
                {
                    role = "assistant",
                    content = fullText,
                    reasoning_content = reasoning,
                    tool_calls = toolCalls.Select(tc => new ToolCallObj
                    {
                        id = tc.Id,
                        type = "function",
                        function = new FunctionCallObj { name = tc.Name, arguments = tc.Arguments }
                    }).ToList()
                });

                foreach (var tc in toolCalls)
                {
                    yield return new AgentChunk(AgentChunkKind.ToolCallStart, ToolName: tc.Name, ToolCallId: tc.Id);
                    var result = await ExecuteToolAsync(tc, ct);
                    _messages.Add(new ApiMessage { role = "tool", content = result, tool_call_id = tc.Id });
                    yield return new AgentChunk(AgentChunkKind.ToolResult, ToolName: tc.Name, ToolResult: result);
                }
            }
            else
            {
                _messages.Add(new ApiMessage { role = "assistant", content = fullText, reasoning_content = reasoning });
                yield break;
            }
        }
    }

    private async Task<(string, List<PendingToolCall>, string, string)> CallApiAsync(CancellationToken ct)
    {
        var toolDefs = _tools.Values.Select(t => new
        {
            type = "function",
            function = new
            {
                name = t.Name,
                description = t.Description,
                parameters = t.ParametersSchema
            }
        }).ToList();

        var body = new { model = _config.Model, messages = _messages, tools = toolDefs, stream = true };
        var json = JsonSerializer.Serialize(body, JsonOpts);

        var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint.TrimEnd('/'))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            var errBody = await response.Content.ReadAsStringAsync(ct);
            File.WriteAllText(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "deskpet_api_error.log"),
                $"Status: {response.StatusCode}\nResponse: {errBody}\n\nRequest: {json}"
            );
            throw new HttpRequestException($"API {response.StatusCode}: {errBody}");
        }

        using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var pendingTools = new Dictionary<int, PendingToolCall>();
        var fullText = "";
        var reasoning = "";
        var finish = "stop";

        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync(ct);
            if (string.IsNullOrEmpty(line) || !line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            var choice = doc.RootElement.GetProperty("choices")[0];
            if (choice.TryGetProperty("finish_reason", out var fr) && fr.ValueKind != JsonValueKind.Null)
                finish = fr.GetString() ?? "stop";
            if (!choice.TryGetProperty("delta", out var delta)) continue;

            if (delta.TryGetProperty("content", out var c) && c.ValueKind != JsonValueKind.Null)
            {
                fullText += c.GetString() ?? "";
            }

            if (delta.TryGetProperty("reasoning_content", out var rc) && rc.ValueKind != JsonValueKind.Null)
            {
                reasoning += rc.GetString() ?? "";
            }

            if (delta.TryGetProperty("tool_calls", out var tcs) && tcs.ValueKind == JsonValueKind.Array)
            {
                foreach (var tc in tcs.EnumerateArray())
                {
                    var idx = tc.GetProperty("index").GetInt32();
                    if (!pendingTools.ContainsKey(idx))
                    {
                        pendingTools[idx] = new PendingToolCall();
                        if (tc.TryGetProperty("id", out var id))
                            pendingTools[idx].Id = id.GetString() ?? "";
                        if (tc.TryGetProperty("function", out var fn) && fn.TryGetProperty("name", out var n))
                            pendingTools[idx].Name = n.GetString() ?? "";
                    }
                    if (tc.TryGetProperty("function", out var fn2) && fn2.TryGetProperty("arguments", out var a))
                        pendingTools[idx].Arguments += a.GetString() ?? "";
                }
            }
        }

        return (fullText, pendingTools.Values.ToList(), finish, reasoning);
    }

    private async Task<string> ExecuteToolAsync(PendingToolCall tc, CancellationToken ct)
    {
        if (!_tools.TryGetValue(tc.Name, out var tool))
            return $"Error: unknown tool '{tc.Name}'";
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            var args = JsonDocument.Parse(tc.Arguments).RootElement;
            return await tool.ExecuteAsync(args, linked.Token);
        }
        catch (OperationCanceledException)
        {
            return "Error: timeout (15s)";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    // ---- API message types ----
    private class ApiMessage
    {
        public string role { get; set; } = "";
        public string? content { get; set; }
        public List<ToolCallObj>? tool_calls { get; set; }
        public string? tool_call_id { get; set; }
        public string? reasoning_content { get; set; }
    }

    private class ToolCallObj
    {
        public string id { get; set; } = "";
        public string type { get; set; } = "function";
        public FunctionCallObj function { get; set; } = new();
    }

    private class FunctionCallObj
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
