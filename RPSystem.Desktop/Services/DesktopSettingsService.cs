using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using RPSystem.Core.Services;

namespace RPSystem.Desktop.Services;

public sealed partial class DesktopSettingsService : ObservableObject, ISettingsService
{
    private readonly string _path = AppPaths.Combine("settings.json");
    private bool _isLoading;

    [ObservableProperty] private bool debugEnabled;
    [ObservableProperty] private string aiProvider = "OpenRouter";
    [ObservableProperty] private string openRouterApiKey = string.Empty;
    [ObservableProperty] private string openAiProxyApiKey = string.Empty;
    [ObservableProperty] private string openAiProxyBaseUrl = OpenAiProxyEndpoint.DefaultBaseUrl;
    [ObservableProperty] private string zaiApiKey = string.Empty;
    [ObservableProperty] private bool zaiSubscriptionMode;
    [ObservableProperty] private string chutesApiKey = string.Empty;
    [ObservableProperty] private bool compactModelNames = true;
    [ObservableProperty] private bool hideModelsOlderThanMaxAge = true;
    [ObservableProperty] private int modelMaxAgeDays = 365;
    [ObservableProperty] private bool showG4fProxyModels;
    [ObservableProperty] private string hiddenModelIds = string.Empty;

    // Unused properties from old ISettingsService — kept for interface compliance
    [ObservableProperty] private string transcriptionProvider = string.Empty;
    [ObservableProperty] private string transcriptionMultimodalModel = string.Empty;
    [ObservableProperty] private string localWhisperEngine = string.Empty;
    [ObservableProperty] private string transcriptionLanguage = "en";
    [ObservableProperty] private string localWhisperModelPath = string.Empty;
    [ObservableProperty] private string localWhisperGpuAdapter = string.Empty;
    [ObservableProperty] private bool localWhisperFallbackToChutes = true;
    [ObservableProperty] private RPSystem.Core.Models.TextMode transcriptionTextMode = RPSystem.Core.Models.TextMode.Replace;
    [ObservableProperty] private string defaultChatExportFormat = "json";
    [ObservableProperty] private bool showConversationTokenTotal;
    [ObservableProperty] private string chatColorScheme = "classic";

    public DesktopSettingsService() => Load();

    private void Load()
    {
        _isLoading = true;
        try
        {
            if (!File.Exists(_path)) return;
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
            if (data is null) return;

            DebugEnabled = data.DebugEnabled;
            AiProvider = data.AiProvider;
            OpenRouterApiKey = data.OpenRouterApiKey;
            OpenAiProxyApiKey = data.OpenAiProxyApiKey;
            OpenAiProxyBaseUrl = data.OpenAiProxyBaseUrl;
            ZaiApiKey = data.ZaiApiKey;
            ZaiSubscriptionMode = data.ZaiSubscriptionMode;
            ChutesApiKey = data.ChutesApiKey;
            CompactModelNames = data.CompactModelNames;
            HideModelsOlderThanMaxAge = data.HideModelsOlderThanMaxAge;
            ModelMaxAgeDays = data.ModelMaxAgeDays;
            ShowG4fProxyModels = data.ShowG4fProxyModels;
            HiddenModelIds = data.HiddenModelIds;
            TranscriptionProvider = data.TranscriptionProvider;
            TranscriptionMultimodalModel = data.TranscriptionMultimodalModel;
            LocalWhisperEngine = data.LocalWhisperEngine;
            TranscriptionLanguage = data.TranscriptionLanguage;
            LocalWhisperModelPath = data.LocalWhisperModelPath;
            LocalWhisperGpuAdapter = data.LocalWhisperGpuAdapter;
            LocalWhisperFallbackToChutes = data.LocalWhisperFallbackToChutes;
            TranscriptionTextMode = data.TranscriptionTextMode;
            DefaultChatExportFormat = data.DefaultChatExportFormat;
            ShowConversationTokenTotal = data.ShowConversationTokenTotal;
            ChatColorScheme = data.ChatColorScheme;
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Save()
    {
        if (_isLoading) return;
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        var data = new SettingsData
        {
            DebugEnabled = DebugEnabled,
            AiProvider = AiProvider,
            OpenRouterApiKey = OpenRouterApiKey,
            OpenAiProxyApiKey = OpenAiProxyApiKey,
            OpenAiProxyBaseUrl = OpenAiProxyBaseUrl,
            ZaiApiKey = ZaiApiKey,
            ZaiSubscriptionMode = ZaiSubscriptionMode,
            ChutesApiKey = ChutesApiKey,
            CompactModelNames = CompactModelNames,
            HideModelsOlderThanMaxAge = HideModelsOlderThanMaxAge,
            ModelMaxAgeDays = ModelMaxAgeDays,
            ShowG4fProxyModels = ShowG4fProxyModels,
            HiddenModelIds = HiddenModelIds,
            TranscriptionProvider = TranscriptionProvider,
            TranscriptionMultimodalModel = TranscriptionMultimodalModel,
            LocalWhisperEngine = LocalWhisperEngine,
            TranscriptionLanguage = TranscriptionLanguage,
            LocalWhisperModelPath = LocalWhisperModelPath,
            LocalWhisperGpuAdapter = LocalWhisperGpuAdapter,
            LocalWhisperFallbackToChutes = LocalWhisperFallbackToChutes,
            TranscriptionTextMode = TranscriptionTextMode,
            DefaultChatExportFormat = DefaultChatExportFormat,
            ShowConversationTokenTotal = ShowConversationTokenTotal,
            ChatColorScheme = ChatColorScheme
        };
        File.WriteAllText(_path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    partial void OnDebugEnabledChanged(bool value) => Save();
    partial void OnAiProviderChanged(string value) => Save();
    partial void OnOpenRouterApiKeyChanged(string value) => Save();
    partial void OnOpenAiProxyApiKeyChanged(string value) => Save();
    partial void OnOpenAiProxyBaseUrlChanged(string value) => Save();
    partial void OnZaiApiKeyChanged(string value) => Save();
    partial void OnZaiSubscriptionModeChanged(bool value) => Save();
    partial void OnChutesApiKeyChanged(string value) => Save();
    partial void OnCompactModelNamesChanged(bool value) => Save();
    partial void OnHideModelsOlderThanMaxAgeChanged(bool value) => Save();
    partial void OnModelMaxAgeDaysChanged(int value) => Save();
    partial void OnShowG4fProxyModelsChanged(bool value) => Save();
    partial void OnHiddenModelIdsChanged(string value) => Save();
    partial void OnTranscriptionProviderChanged(string value) => Save();
    partial void OnTranscriptionMultimodalModelChanged(string value) => Save();
    partial void OnLocalWhisperEngineChanged(string value) => Save();
    partial void OnTranscriptionLanguageChanged(string value) => Save();
    partial void OnLocalWhisperModelPathChanged(string value) => Save();
    partial void OnLocalWhisperGpuAdapterChanged(string value) => Save();
    partial void OnLocalWhisperFallbackToChutesChanged(bool value) => Save();
    partial void OnTranscriptionTextModeChanged(RPSystem.Core.Models.TextMode value) => Save();
    partial void OnDefaultChatExportFormatChanged(string value) => Save();
    partial void OnShowConversationTokenTotalChanged(bool value) => Save();
    partial void OnChatColorSchemeChanged(string value) => Save();

    public bool GetBool(string key, bool fallback = false)
    {
        if (!File.Exists(_path)) return fallback;
        try
        {
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
            if (data is not null && data.CustomBooleans.TryGetValue(key, out var value))
                return value;
        }
        catch { }
        return fallback;
    }

    public void SetBool(string key, bool value)
    {
        EnsureLoaded();
        var data = LoadData() ?? new SettingsData();
        data.CustomBooleans[key] = value;
        SaveData(data);
    }

    public int GetInt(string key, int fallback = 0)
    {
        if (!File.Exists(_path)) return fallback;
        try
        {
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
            if (data is not null && data.CustomIntegers.TryGetValue(key, out var value))
                return value;
        }
        catch { }
        return fallback;
    }

    public void SetInt(string key, int value)
    {
        EnsureLoaded();
        var data = LoadData() ?? new SettingsData();
        data.CustomIntegers[key] = value;
        SaveData(data);
    }

    public string GetString(string key, string fallback = "")
    {
        if (!File.Exists(_path)) return fallback;
        try
        {
            var data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
            if (data is not null && data.CustomStrings.TryGetValue(key, out var value))
                return value;
        }
        catch { }
        return fallback;
    }

    public void SetString(string key, string value)
    {
        EnsureLoaded();
        var data = LoadData() ?? new SettingsData();
        data.CustomStrings[key] = value;
        SaveData(data);
    }

    private void EnsureLoaded()
    {
        if (!_isLoading)
        {
            Load();
        }
    }

    private SettingsData? LoadData()
    {
        if (!File.Exists(_path)) return null;
        return JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path));
    }

    private void SaveData(SettingsData data)
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        File.WriteAllText(_path, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class SettingsData
    {
        public bool DebugEnabled { get; set; }
        public string AiProvider { get; set; } = "OpenRouter";
        public string OpenRouterApiKey { get; set; } = string.Empty;
        public string OpenAiProxyApiKey { get; set; } = string.Empty;
        public string OpenAiProxyBaseUrl { get; set; } = OpenAiProxyEndpoint.DefaultBaseUrl;
        public string ZaiApiKey { get; set; } = string.Empty;
        public bool ZaiSubscriptionMode { get; set; }
        public string ChutesApiKey { get; set; } = string.Empty;
        public bool CompactModelNames { get; set; } = true;
        public bool HideModelsOlderThanMaxAge { get; set; } = true;
        public int ModelMaxAgeDays { get; set; } = 365;
        public bool ShowG4fProxyModels { get; set; }
        public string HiddenModelIds { get; set; } = string.Empty;
        public string TranscriptionProvider { get; set; } = string.Empty;
        public string TranscriptionMultimodalModel { get; set; } = string.Empty;
        public string LocalWhisperEngine { get; set; } = string.Empty;
        public string TranscriptionLanguage { get; set; } = "en";
        public string LocalWhisperModelPath { get; set; } = string.Empty;
        public string LocalWhisperGpuAdapter { get; set; } = string.Empty;
        public bool LocalWhisperFallbackToChutes { get; set; } = true;
        public RPSystem.Core.Models.TextMode TranscriptionTextMode { get; set; } = RPSystem.Core.Models.TextMode.Replace;
        public string DefaultChatExportFormat { get; set; } = "json";
        public bool ShowConversationTokenTotal { get; set; }
        public string ChatColorScheme { get; set; } = "classic";

        // Custom key-value stores for viewmodel preferences
        public Dictionary<string, bool> CustomBooleans { get; set; } = new();
        public Dictionary<string, int> CustomIntegers { get; set; } = new();
        public Dictionary<string, string> CustomStrings { get; set; } = new();
    }
}
