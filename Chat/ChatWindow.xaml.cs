using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using WMedia = System.Windows.Media;
using DeskPet.Agent;
using DeskPet.Config;

namespace DeskPet.Chat;

public partial class ChatWindow : Window
{
    private readonly AgentLoop _agent;
    private readonly ChatHistory _history;
    private readonly Settings.AiConfig _config;

    public ChatWindow(Settings.AiConfig aiConfig)
    {
        InitializeComponent();
        _config = aiConfig;
        _history = ChatHistory.Load();
        var tools = new ITool[] { new ReadFileTool() };
        _agent = new AgentLoop(aiConfig, tools);
        _agent.LoadHistory(_history);
        LoadHistoryUI();
    }

    private void LoadHistoryUI()
    {
        foreach (var msg in _history.Messages)
        {
            AddBubble(msg.Content, isUser: msg.Role == "user");
        }
        if (_history.Messages.Count > 0)
            ChatScroll.ScrollToEnd();
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        await SendMessage();
    }

    private async void OnInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            await SendMessage();
    }

    private async Task SendMessage()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        InputBox.Clear();
        InputBox.IsEnabled = false;

        AddBubble(text, isUser: true);
        _history.AddUser(text);

        Border? assistantBubble = null;
        var fullText = "";

        try
        {
            await foreach (var chunk in _agent.RunAsync(text))
            {
                switch (chunk.Kind)
                {
                    case AgentChunkKind.StreamingText:
                    case AgentChunkKind.FinalText:
                        if (assistantBubble == null)
                            assistantBubble = AddBubble("", isUser: false);
                        SetBubbleText(assistantBubble, chunk.Text ?? "");
                        fullText = chunk.Text ?? "";
                        ChatScroll.ScrollToEnd();
                        break;

                    case AgentChunkKind.ToolCallStart:
                        AddToolBubble(chunk.ToolName ?? "tool", "executing");
                        ChatScroll.ScrollToEnd();
                        assistantBubble = null;
                        break;

                    case AgentChunkKind.ToolResult:
                        AddToolBubble(chunk.ToolName ?? "tool", "result", chunk.ToolResult);
                        ChatScroll.ScrollToEnd();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (assistantBubble == null)
                assistantBubble = AddBubble("", isUser: false);
            SetBubbleText(assistantBubble, "出错了: " + ex.Message);
            fullText = "出错了: " + ex.Message;
        }

        if (!string.IsNullOrEmpty(fullText))
            _history.AddAssistant(fullText);
        _history.Save();

        InputBox.IsEnabled = true;
        InputBox.Focus();
    }

    private static WMedia.SolidColorBrush Brush(byte r, byte g, byte b) =>
        new(WMedia.Color.FromRgb(r, g, b));

    private Border AddBubble(string text, bool isUser)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = isUser ? Brush(80, 80, 80) : Brush(60, 60, 60),
            Padding = new Thickness(10, 6, 10, 6),
            MaxWidth = 240
        };

        var border = new Border
        {
            Child = textBlock,
            CornerRadius = new CornerRadius(isUser ? 12 : 2, isUser ? 2 : 12, 12, 12),
            Background = isUser ? Brush(232, 232, 232) : Brush(255, 224, 235),
            Margin = isUser
                ? new Thickness(40, 4, 8, 4)
                : new Thickness(8, 4, 40, 4),
            HorizontalAlignment = isUser
                ? System.Windows.HorizontalAlignment.Right
                : System.Windows.HorizontalAlignment.Left,
            Tag = textBlock
        };

        ChatItems.Items.Add(border);
        return border;
    }

    private void AddToolBubble(string toolName, string status, string? result = null)
    {
        var label = status == "executing"
            ? $"🔧 {toolName}..."
            : $"📄 {toolName}";

        var textBlock = new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Foreground = Brush(120, 100, 60),
            Padding = new Thickness(8, 4, 8, 4),
            MaxWidth = 240
        };

        var border = new Border
        {
            Child = textBlock,
            CornerRadius = new CornerRadius(6),
            Background = Brush(255, 248, 220),
            BorderBrush = Brush(230, 200, 120),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(8, 2, 40, 2),
            HorizontalAlignment = System.Windows.HorizontalAlignment.Left,
            Tag = textBlock
        };

        ChatItems.Items.Add(border);

        if (result != null)
        {
            var truncated = result.Length > 200 ? result[..200] + "..." : result;
            var resultBlock = new TextBlock
            {
                Text = truncated,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10,
                FontFamily = new WMedia.FontFamily("Consolas"),
                Foreground = Brush(100, 100, 100),
                Padding = new Thickness(8, 4, 8, 4),
                MaxWidth = 240,
                MaxHeight = 120
            };

            var resultBorder = new Border
            {
                Child = new ScrollViewer
                {
                    Content = resultBlock,
                    MaxHeight = 120,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                },
                CornerRadius = new CornerRadius(4),
                Background = Brush(245, 245, 245),
                BorderBrush = Brush(220, 220, 220),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(16, 0, 40, 4),
                HorizontalAlignment = System.Windows.HorizontalAlignment.Left
            };

            ChatItems.Items.Add(resultBorder);
        }
    }

    private static void SetBubbleText(Border bubble, string text)
    {
        if (bubble.Tag is TextBlock tb)
            tb.Text = text;
    }
}
