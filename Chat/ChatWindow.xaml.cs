using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskPet.Config;

namespace DeskPet.Chat;

public partial class ChatWindow : Window
{
    private readonly ChatService _chat;

    public ChatWindow(Settings.AiConfig aiConfig)
    {
        InitializeComponent();
        _chat = new ChatService(aiConfig);
    }

    private async void OnSendClick(object sender, RoutedEventArgs e)
    {
        await SendMessage();
    }

    private async void OnInputKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter)
            await SendMessage();
    }

    private async Task SendMessage()
    {
        var text = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;
        InputBox.Clear();
        InputBox.IsEnabled = false;

        AddBubble(text, isUser: true);
        var bubble = AddBubble("...", isUser: false);

        try
        {
            await foreach (var content in _chat.SendAsync(text))
            {
                SetBubbleText(bubble, content);
                ChatScroll.ScrollToBottom();
            }
        }
        catch (Exception ex)
        {
            SetBubbleText(bubble, "出错了: " + ex.Message);
        }

        InputBox.IsEnabled = true;
        InputBox.Focus();
    }

    private Border AddBubble(string text, bool isUser)
    {
        var textBlock = new TextBlock
        {
            Text = text,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            Foreground = isUser
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(80, 80, 80))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(60, 60, 60)),
            Padding = new Thickness(10, 6, 10, 6),
            MaxWidth = 240
        };

        var border = new Border
        {
            Child = textBlock,
            CornerRadius = new CornerRadius(isUser ? 12 : 2, isUser ? 2 : 12, 12, 12),
            Background = isUser
                ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(232, 232, 232))
                : new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 224, 235)),
            Margin = isUser
                ? new Thickness(40, 4, 8, 4)
                : new Thickness(8, 4, 40, 4),
            HorizontalAlignment = isUser ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left,
            Tag = textBlock
        };

        ChatItems.Items.Add(border);
        return border;
    }

    private static void SetBubbleText(Border bubble, string text)
    {
        if (bubble.Tag is TextBlock tb)
            tb.Text = text;
    }
}
