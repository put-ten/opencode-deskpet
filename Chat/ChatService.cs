using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using DeskPet.Config;

namespace DeskPet.Chat;

public class ChatService
{
    private readonly HttpClient _http = new();
    private readonly Settings.AiConfig _config;
    private readonly List<object> _messages = new();
    private readonly ChatHistory _history;

    public ChatService(Settings.AiConfig config)
    {
        _config = config;
        _history = ChatHistory.Load();
        _messages.Add(new { role = "system", content = "你是一只住在主人电脑桌面上的像素小猫。说话带喵，语气傲娇又粘人，句子简短。" });
        foreach (var msg in _history.Messages)
        {
            _messages.Add(new { role = msg.Role, content = msg.Content });
        }
    }

    public ChatHistory History => _history;

    public async IAsyncEnumerable<string> SendAsync(string text)
    {
        _messages.Add(new { role = "user", content = text });
        _history.AddUser(text);

        if (string.IsNullOrWhiteSpace(_config.Endpoint) || string.IsNullOrWhiteSpace(_config.ApiKey))
        {
            var fallback = text switch
            {
                string s when s.Contains("你好") || s.Contains("嗨") || s.Contains("hi") => "嗨~ 我在呢！",
                string s when s.Contains("可爱") => "嘿嘿，谢谢！你也很棒~",
                _ => "喵？"
            };
            _history.AddAssistant(fallback);
            _history.Save();
            yield return fallback;
            yield break;
        }

        var body = new
        {
            model = _config.Model,
            messages = _messages,
            stream = true
        };

        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, _config.Endpoint.TrimEnd('/'))
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("Authorization", $"Bearer {_config.ApiKey}");

        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream);

        var fullContent = "";
        while (!reader.EndOfStream)
        {
            var line = await reader.ReadLineAsync();
            if (string.IsNullOrEmpty(line)) continue;
            if (!line.StartsWith("data: ")) continue;
            var data = line[6..];
            if (data == "[DONE]") break;

            using var doc = JsonDocument.Parse(data);
            var choice = doc.RootElement.GetProperty("choices")[0];
            if (choice.TryGetProperty("delta", out var delta) &&
                delta.TryGetProperty("content", out var content))
            {
                fullContent += content.GetString();
                yield return fullContent;
            }
        }

        _messages.Add(new { role = "assistant", content = fullContent });
        _history.AddAssistant(fullContent);
        _history.Save();
    }
}
