using System.Windows;
using System.Windows.Controls;
using DeskPet.Config;

namespace DeskPet.Chat;

public partial class SettingsWindow : Window
{
    private readonly Settings _settings;
    private readonly List<Settings.ModelEntry> _models;

    public SettingsWindow()
    {
        InitializeComponent();
        _settings = Settings.Load();
        _models = Settings.LoadModels();

        foreach (var m in _models)
        {
            var label = m.ModelId;
            if (!string.IsNullOrWhiteSpace(m.DisplayVariant))
                label += $" [{m.DisplayVariant}]";
            if (!m.HasKey)
                label += " (无密钥)";
            ModelList.Items.Add(label);
        }

        var currentIdx = _models.FindIndex(m => m.ModelId == _settings.SelectedModel);
        if (currentIdx < 0)
            currentIdx = _models.FindIndex(m => m.HasKey && m.ProviderName == "opencode-go");
        if (currentIdx < 0)
            currentIdx = _models.FindIndex(m => m.HasKey);
        ModelList.SelectedIndex = currentIdx >= 0 ? currentIdx : 0;

        AiPrompt.Text = _settings.SystemPrompt;
        IdleInterval.Text = _settings.Behavior.IdleInterval.ToString();
        WalkSpeed.Text = _settings.Behavior.WalkSpeed.ToString();
    }

    private void OnModelSelected(object sender, SelectionChangedEventArgs e)
    {
        var idx = ModelList.SelectedIndex;
        if (idx < 0 || idx >= _models.Count) return;
        var m = _models[idx];
        ModelDetail.Text = $"提供者: {m.ProviderName}  |  密钥: {(m.HasKey ? "已配置" : "缺失")}";
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var idx = ModelList.SelectedIndex;
        if (idx >= 0 && idx < _models.Count)
            _settings.SelectedModel = _models[idx].ModelId;

        _settings.SystemPrompt = AiPrompt.Text.Trim();
        if (int.TryParse(IdleInterval.Text, out var idle)) _settings.Behavior.IdleInterval = idle;
        if (double.TryParse(WalkSpeed.Text, out var speed)) _settings.Behavior.WalkSpeed = speed;
        _settings.Save();
        Close();
    }
}
