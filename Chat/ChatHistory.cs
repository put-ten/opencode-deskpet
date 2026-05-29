using System.IO;
using System.Text.Json;

namespace DeskPet.Chat;

public class ChatHistory
{
    private static string HistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskPet", "chat_history.json");

    public List<ChatMessage> Messages { get; set; } = new();

    public class ChatMessage
    {
        public string Role { get; set; } = "";
        public string Content { get; set; } = "";
    }

    public static ChatHistory Load()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var json = File.ReadAllText(HistoryPath);
                var history = JsonSerializer.Deserialize<ChatHistory>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return history ?? new ChatHistory();
            }
        }
        catch { }
        return new ChatHistory();
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(HistoryPath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryPath, json);
        }
        catch { }
    }

    private const int MaxMessages = 50;

    public void AddUser(string content)
    {
        Messages.Add(new ChatMessage { Role = "user", Content = content });
        TrimHistory();
    }

    public void AddAssistant(string content)
    {
        Messages.Add(new ChatMessage { Role = "assistant", Content = content });
        TrimHistory();
    }

    private void TrimHistory()
    {
        if (Messages.Count > MaxMessages)
        {
            var removeCount = Messages.Count - MaxMessages;
            Messages.RemoveRange(0, removeCount);
        }
    }
}
