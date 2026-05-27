using System.IO;
using System.Text.Json;

namespace DeskPet.Config;

public class Settings
{
    public WindowSettings Window { get; set; } = new();
    public BehaviorSettings Behavior { get; set; } = new();
    public string SystemPrompt { get; set; } = "";
    public string SelectedModel { get; set; } = "";

    public class WindowSettings
    {
        public int Scale { get; set; } = 3;
        public double Opacity { get; set; } = 1.0;
        public bool AlwaysOnTop { get; set; } = true;
        public bool AutoHide { get; set; } = false;
    }

    public class BehaviorSettings
    {
        public int IdleInterval { get; set; } = 5;
        public double WalkSpeed { get; set; } = 1.0;
        public bool Bounce { get; set; } = true;
    }

    public class AiConfig
    {
        public string Endpoint { get; set; } = "";
        public string ApiKey { get; set; } = "";
        public string Model { get; set; } = "";
    }

    public class ProviderEntry
    {
        public string ProviderName { get; set; } = "";
        public string Endpoint { get; set; } = "";
        public string ApiKey { get; set; } = "";
    }

    public class ModelEntry
    {
        public string ModelId { get; set; } = "";
        public string ProviderName { get; set; } = "";
        public string DisplayVariant { get; set; } = "";
        public bool HasKey { get; set; }
    }

    // ---- Live opencode data ----

    public static List<ModelEntry> LoadModels()
    {
        var models = new List<ModelEntry>();
        try
        {
            var auth = LoadAuth();
            var epMap = LoadProviderEndpoints();
            var dbModels = LoadSessionModels();

            foreach (var (modelId, providerId, variant) in dbModels)
            {
                var key = ResolveKey(providerId, auth);
                models.Add(new ModelEntry
                {
                    ModelId = modelId,
                    ProviderName = providerId,
                    DisplayVariant = variant,
                    HasKey = !string.IsNullOrWhiteSpace(key)
                });
            }

            foreach (var (providerId, modelId) in LoadJsoncModels())
            {
                if (!models.Any(m => m.ModelId == modelId))
                {
                    var key = ResolveKey(providerId, auth);
                    models.Add(new ModelEntry
                    {
                        ModelId = modelId,
                        ProviderName = providerId,
                        HasKey = !string.IsNullOrWhiteSpace(key)
                    });
                }
            }
        }
        catch { }

        if (models.Count == 0)
            models.Add(new ModelEntry { ModelId = "deepseek-v4-pro", ProviderName = "opencode-go", HasKey = true });

        return models;
    }

    public static AiConfig ResolveAi(string? preferredModel = null)
    {
        var models = LoadModels();
        var pick = preferredModel ?? "";
        var selected = models.FirstOrDefault(m => m.ModelId == pick && m.HasKey)
                    ?? models.FirstOrDefault(m => m.HasKey && m.ProviderName == "opencode-go")
                    ?? models.FirstOrDefault(m => m.HasKey)
                    ?? models.FirstOrDefault();

        if (selected == null)
            return new AiConfig { Model = "deepseek-v4-pro" };

        var prov = GetProvider(selected.ProviderName);
        return new AiConfig
        {
            Model = selected.ModelId,
            Endpoint = prov?.Endpoint ?? "https://opencode.ai/zen/go/v1/chat/completions",
            ApiKey = prov?.ApiKey ?? ""
        };
    }

    private static ProviderEntry? GetProvider(string providerId)
    {
        try
        {
            var auth = LoadAuth();
            var epMap = LoadProviderEndpoints();
            var ep = ResolveEndpoint(providerId, epMap);
            var key = ResolveKey(providerId, auth);
            if (string.IsNullOrWhiteSpace(key)) return null;
            return new ProviderEntry { ProviderName = providerId, Endpoint = ep, ApiKey = key };
        }
        catch { return null; }
    }

    // ---- Internal data readers ----

    private static Dictionary<string, string> LoadAuth()
    {
        var result = new Dictionary<string, string>();
        var authPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "opencode", "auth.json");
        if (!File.Exists(authPath)) return result;
        using var doc = JsonDocument.Parse(File.ReadAllText(authPath));
        foreach (var p in doc.RootElement.EnumerateObject())
        {
            if (p.Value.TryGetProperty("key", out var k))
                result[p.Name] = k.GetString() ?? "";
        }
        return result;
    }

    private static Dictionary<string, string> LoadProviderEndpoints()
    {
        var result = new Dictionary<string, string>();
        var jsoncPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "opencode", "opencode.jsonc");
        if (!File.Exists(jsoncPath)) return result;
        var text = File.ReadAllText(jsoncPath);
        text = JsoncStrip(text);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("provider", out var providers)) return result;
        foreach (var p in providers.EnumerateObject())
        {
            if (p.Value.TryGetProperty("options", out var opts) &&
                opts.TryGetProperty("baseURL", out var url))
                result[p.Name] = (url.GetString() ?? "").TrimEnd('/');
        }
        return result;
    }

    private static List<(string modelId, string providerId, string variant)> LoadSessionModels()
    {
        var result = new List<(string, string, string)>();
        var dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".local", "share", "opencode", "opencode.db");
        if (!File.Exists(dbPath)) return result;
        try
        {
            using var db = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={dbPath}");
            db.Open();
            var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT DISTINCT model FROM session WHERE model IS NOT NULL";
            using var reader = cmd.ExecuteReader();
            var seen = new HashSet<string>();
            while (reader.Read())
            {
                var raw = reader.GetString(0);
                try
                {
                    using var d = JsonDocument.Parse(raw);
                    var mid = d.RootElement.GetProperty("id").GetString() ?? "";
                    var pid = d.RootElement.GetProperty("providerID").GetString() ?? "";
                    var variant = "";
                    if (d.RootElement.TryGetProperty("variant", out var v))
                        variant = v.GetString() ?? "";
                    if (seen.Add(mid + "|" + pid))
                        result.Add((mid, pid, variant));
                }
                catch { }
            }
        }
        catch { }
        return result;
    }

    private static List<(string providerId, string modelId)> LoadJsoncModels()
    {
        var result = new List<(string, string)>();
        var jsoncPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".config", "opencode", "opencode.jsonc");
        if (!File.Exists(jsoncPath)) return result;
        var text = File.ReadAllText(jsoncPath);
        text = JsoncStrip(text);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("provider", out var providers)) return result;
        foreach (var p in providers.EnumerateObject())
        {
            if (p.Value.TryGetProperty("models", out var mods))
            {
                foreach (var m in mods.EnumerateObject())
                    result.Add((p.Name, m.Name));
            }
        }
        return result;
    }

    private static string ResolveEndpoint(string providerId, Dictionary<string, string> epMap)
    {
        if (epMap.TryGetValue(providerId, out var ep) && ep.StartsWith("http"))
            return ep + "/chat/completions";
        return "https://opencode.ai/zen/go/v1/chat/completions";
    }

    private static string ResolveKey(string providerId, Dictionary<string, string> auth)
    {
        if (auth.TryGetValue(providerId, out var key) && !string.IsNullOrWhiteSpace(key))
            return key;
        if (providerId == "opencode" && auth.TryGetValue("opencode-go", out var fallback))
            return fallback;
        return "";
    }

    private static string JsoncStrip(string jsonc)
    {
        var lines = jsonc.Split('\n');
        var clean = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("//")) continue;
            var idx = line.IndexOf("//");
            if (idx > 0)
            {
                var before = line[..idx];
                if (before.TrimEnd().EndsWith(":"))
                    clean.Add(line);
                else
                    clean.Add(before);
            }
            else
                clean.Add(line);
        }
        return string.Join("\n", clean);
    }

    // ---- Persistence (no AI credentials) ----

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DeskPet", "settings.json");

    public static Settings Load()
    {
        Settings s;
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                s = JsonSerializer.Deserialize<Settings>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Settings();
            }
            else
                s = new Settings();
        }
        catch
        {
            s = new Settings();
        }

        if (string.IsNullOrEmpty(s.SystemPrompt))
            s.SystemPrompt = "你是一只住在主人电脑桌面上的像素小猫。说话带喵，语气傲娇又粘人，句子简短。会用小爪子在屏幕上走，偶尔打瞌睡伸懒腰。喜欢吃鱼干，讨厌被无视。";
        return s;
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(ConfigPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(ConfigPath, json);
    }
}
