using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Storage;
using RPSystem.Core.Services;
using RPSystem.Core.Models;
using System;
using System.Threading.Tasks;

namespace RPSystem.Services
{
    public partial class SettingsService : ObservableObject, ISettingsService
    {
        private static readonly string DebugEnabledKey = "DebugEnabled";
        private static readonly string AiProviderKey = "AiProvider";
        private static readonly string OpenRouterApiKeyKey = "OpenRouterApiKey";
        private static readonly string OpenAiProxyApiKeyKey = "OpenAiProxyApiKey";
        private static readonly string OpenAiProxyBaseUrlKey = "OpenAiProxyBaseUrl";
        private static readonly string ZaiApiKeyKey = "ZaiApiKey";
        private static readonly string ZaiSubscriptionModeKey = "ZaiSubscriptionMode";
        private static readonly string ChutesApiKeyKey = "ChutesApiKey";
        private static readonly string PhotoQualityKey = "PhotoQuality";
        private static readonly string MaxResolutionKey = "MaxResolution";
        private static readonly string CompactModelNamesKey = "CompactModelNames";
        private static readonly string HideModelsOlderThanMaxAgeKey = "HideModelsOlderThanMaxAge";
        private static readonly string ModelMaxAgeDaysKey = "ModelMaxAgeDays";
        private static readonly string ShowG4fProxyModelsKey = "ShowG4fProxyModels";
        private static readonly string HiddenModelIdsKey = "HiddenModelIds";
        private static readonly string TranscriptionProviderKey = "TranscriptionProvider";
        private static readonly string TranscriptionMultimodalModelKey = "TranscriptionMultimodalModel";
        private static readonly string LocalWhisperEngineKey = "LocalWhisperEngine";
        private static readonly string TranscriptionLanguageKey = "TranscriptionLanguage";
        private static readonly string LocalWhisperModelPathKey = "LocalWhisperModelPath";
        private static readonly string LocalWhisperGpuAdapterKey = "LocalWhisperGpuAdapter";
        private static readonly string LocalWhisperFallbackToChutesKey = "LocalWhisperFallbackToChutes";
        private static readonly string TranscriptionTextModeKey = "TranscriptionTextMode";
        private static readonly string DefaultChatExportFormatKey = "DefaultChatExportFormat";
        private static readonly string ShowConversationTokenTotalKey = "ShowConversationTokenTotal";
        private static readonly string ChatColorSchemeKey = "ChatColorScheme";

        private const string GoogleOAuthAndroidClientIdKey = "google_oauth_android_client_id";
        private const string GoogleOAuthIosClientIdKey = "google_oauth_ios_client_id";
        private const string GoogleOAuthWindowsClientIdKey = "google_oauth_windows_client_id";
        private const string GoogleOAuthWindowsClientSecretKey = "google_oauth_windows_client_secret";

        private const string GoogleOAuthAndroidClientIdEnv = "GOOGLE_OAUTH_ANDROID_CLIENT_ID";
        private const string GoogleOAuthIosClientIdEnv = "GOOGLE_OAUTH_IOS_CLIENT_ID";
        private const string GoogleOAuthWindowsClientIdEnv = "GOOGLE_OAUTH_WINDOWS_CLIENT_ID";
        private const string GoogleOAuthWindowsClientSecretEnv = "GOOGLE_OAUTH_WINDOWS_CLIENT_SECRET";

        private static SettingsService? _instance;
        public static SettingsService Instance => _instance ??= new SettingsService();

        public SettingsService()
        {
            // Load saved preference on initialization
            _debugEnabled = Preferences.Get(DebugEnabledKey, false);
            // Default to OpenRouter if not set
            _aiProvider = Preferences.Get(AiProviderKey, "OpenRouter");
            _openRouterApiKey = Preferences.Get(OpenRouterApiKeyKey, Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? string.Empty);
            _openAiProxyApiKey = Preferences.Get(OpenAiProxyApiKeyKey, Environment.GetEnvironmentVariable("OPENAI_PROXY_API_KEY") ?? Environment.GetEnvironmentVariable("PROXY_API_KEY") ?? string.Empty);
            _openAiProxyBaseUrl = Preferences.Get(OpenAiProxyBaseUrlKey, Environment.GetEnvironmentVariable("OPENAI_PROXY_BASE_URL") ?? OpenAiProxyEndpoint.DefaultBaseUrl);
            _zaiApiKey = Preferences.Get(ZaiApiKeyKey, string.Empty);
            _zaiSubscriptionMode = Preferences.Get(ZaiSubscriptionModeKey, false);
            _chutesApiKey = Preferences.Get(ChutesApiKeyKey, string.Empty);
            // Default: 75% quality, 1280px width/height (good balance for text readability)
            _photoQuality = Preferences.Get(PhotoQualityKey, 75);
            _maxResolution = Preferences.Get(MaxResolutionKey, 1280);
            _compactModelNames = Preferences.Get(CompactModelNamesKey, true);
            _hideModelsOlderThanMaxAge = Preferences.Get(HideModelsOlderThanMaxAgeKey, true);
            _modelMaxAgeDays = Preferences.Get(ModelMaxAgeDaysKey, 365);
            _showG4fProxyModels = Preferences.Get(ShowG4fProxyModelsKey, false);
            _hiddenModelIds = Preferences.Get(HiddenModelIdsKey, string.Empty);
            var defaultWhisperEngine = DeviceInfo.Platform == DevicePlatform.Android
                ? "AndroidWhisperCpp"
                : "CpuWhisperNet";
            _transcriptionProvider = Preferences.Get(TranscriptionProviderKey, "Chutes");
            _transcriptionMultimodalModel = Preferences.Get(TranscriptionMultimodalModelKey, "nim:nvidia/nemotron-3-nano-omni-30b-a3b-reasoning");
            _localWhisperEngine = Preferences.Get(LocalWhisperEngineKey, defaultWhisperEngine);
            _transcriptionLanguage = Preferences.Get(TranscriptionLanguageKey, "en");
            _localWhisperModelPath = Preferences.Get(LocalWhisperModelPathKey, string.Empty);
            _localWhisperGpuAdapter = Preferences.Get(LocalWhisperGpuAdapterKey, string.Empty);
            _localWhisperFallbackToChutes = Preferences.Get(LocalWhisperFallbackToChutesKey, true);
            _transcriptionTextMode = (RPSystem.Core.Models.TextMode)Preferences.Get(TranscriptionTextModeKey, (int)RPSystem.Core.Models.TextMode.Replace);
            _defaultChatExportFormat = Preferences.Get(DefaultChatExportFormatKey, "json");
            _showConversationTokenTotal = Preferences.Get(ShowConversationTokenTotalKey, false);
            _chatColorScheme = Preferences.Get(ChatColorSchemeKey, "classic");
        }

        [ObservableProperty]
        private bool _debugEnabled;

        [ObservableProperty]
        private string _aiProvider; // Values: "OpenRouter", "OpenAIProxy", "ZAI", or "Chutes"

        [ObservableProperty]
        private string _openRouterApiKey;

        [ObservableProperty]
        private string _openAiProxyApiKey;

        [ObservableProperty]
        private string _openAiProxyBaseUrl;

        [ObservableProperty]
        private string _zaiApiKey;

        [ObservableProperty]
        private bool _zaiSubscriptionMode; // True = Coding Plan, False = Standard Pay-As-You-Go

        [ObservableProperty]
        private string _chutesApiKey;

        [ObservableProperty]
        private int _photoQuality; // 1-100, default 75

        [ObservableProperty]
        private int _maxResolution; // Max dimension in pixels, default 1280

        [ObservableProperty]
        private bool _compactModelNames;

        [ObservableProperty]
        private bool _hideModelsOlderThanMaxAge;

        [ObservableProperty]
        private int _modelMaxAgeDays;

        [ObservableProperty]
        private bool _showG4fProxyModels;

        [ObservableProperty]
        private string _hiddenModelIds;

        [ObservableProperty]
        private string _transcriptionProvider;

        [ObservableProperty]
        private string _transcriptionMultimodalModel;

        [ObservableProperty]
        private string _localWhisperEngine;

        [ObservableProperty]
        private string _transcriptionLanguage;

        [ObservableProperty]
        private string _localWhisperModelPath;

        [ObservableProperty]
        private string _localWhisperGpuAdapter;

        [ObservableProperty]
        private bool _localWhisperFallbackToChutes;

        [ObservableProperty]
        private RPSystem.Core.Models.TextMode _transcriptionTextMode;

        [ObservableProperty]
        private string _defaultChatExportFormat;

        [ObservableProperty]
        private bool _showConversationTokenTotal;

        [ObservableProperty]
        private string _chatColorScheme;

        partial void OnDebugEnabledChanged(bool value)
        {
            // Persist the setting whenever it changes
            Preferences.Set(DebugEnabledKey, value);
        }

        partial void OnAiProviderChanged(string value) => Preferences.Set(AiProviderKey, value);
        partial void OnOpenRouterApiKeyChanged(string value) => Preferences.Set(OpenRouterApiKeyKey, value);
        partial void OnOpenAiProxyApiKeyChanged(string value) => Preferences.Set(OpenAiProxyApiKeyKey, value);
        partial void OnOpenAiProxyBaseUrlChanged(string value) => Preferences.Set(OpenAiProxyBaseUrlKey, value);
        partial void OnZaiApiKeyChanged(string value) => Preferences.Set(ZaiApiKeyKey, value);
        partial void OnZaiSubscriptionModeChanged(bool value) => Preferences.Set(ZaiSubscriptionModeKey, value);
        partial void OnChutesApiKeyChanged(string value) => Preferences.Set(ChutesApiKeyKey, value);
        partial void OnPhotoQualityChanged(int value) => Preferences.Set(PhotoQualityKey, value);
        partial void OnMaxResolutionChanged(int value) => Preferences.Set(MaxResolutionKey, value);
        partial void OnCompactModelNamesChanged(bool value) => Preferences.Set(CompactModelNamesKey, value);
        partial void OnHideModelsOlderThanMaxAgeChanged(bool value) => Preferences.Set(HideModelsOlderThanMaxAgeKey, value);
        partial void OnModelMaxAgeDaysChanged(int value) => Preferences.Set(ModelMaxAgeDaysKey, Math.Max(1, value));
        partial void OnShowG4fProxyModelsChanged(bool value) => Preferences.Set(ShowG4fProxyModelsKey, value);
        partial void OnHiddenModelIdsChanged(string value) => Preferences.Set(HiddenModelIdsKey, value ?? string.Empty);
        partial void OnTranscriptionProviderChanged(string value) => Preferences.Set(TranscriptionProviderKey, value);
        partial void OnTranscriptionMultimodalModelChanged(string value) => Preferences.Set(TranscriptionMultimodalModelKey, value ?? string.Empty);
        partial void OnLocalWhisperEngineChanged(string value) => Preferences.Set(LocalWhisperEngineKey, value);
        partial void OnTranscriptionLanguageChanged(string value) => Preferences.Set(TranscriptionLanguageKey, value);
        partial void OnLocalWhisperModelPathChanged(string value) => Preferences.Set(LocalWhisperModelPathKey, value);
        partial void OnLocalWhisperGpuAdapterChanged(string value) => Preferences.Set(LocalWhisperGpuAdapterKey, value);
        partial void OnLocalWhisperFallbackToChutesChanged(bool value) => Preferences.Set(LocalWhisperFallbackToChutesKey, value);
        partial void OnTranscriptionTextModeChanged(RPSystem.Core.Models.TextMode value) => Preferences.Set(TranscriptionTextModeKey, (int)value);
        partial void OnDefaultChatExportFormatChanged(string value) => Preferences.Set(DefaultChatExportFormatKey, value);
        partial void OnShowConversationTokenTotalChanged(bool value) => Preferences.Set(ShowConversationTokenTotalKey, value);
        partial void OnChatColorSchemeChanged(string value) => Preferences.Set(ChatColorSchemeKey, value);

        public Task<string> GetGoogleOAuthAndroidClientIdAsync() =>
            GetSecretOrEnvAsync(GoogleOAuthAndroidClientIdKey, GoogleOAuthAndroidClientIdEnv);

        public Task<string> GetGoogleOAuthIosClientIdAsync() =>
            GetSecretOrEnvAsync(GoogleOAuthIosClientIdKey, GoogleOAuthIosClientIdEnv);

        public Task<string> GetGoogleOAuthWindowsClientIdAsync() =>
            GetSecretOrEnvAsync(GoogleOAuthWindowsClientIdKey, GoogleOAuthWindowsClientIdEnv);

        public Task<string> GetGoogleOAuthWindowsClientSecretAsync() =>
            GetSecretOrEnvAsync(GoogleOAuthWindowsClientSecretKey, GoogleOAuthWindowsClientSecretEnv);

        public Task SetGoogleOAuthAndroidClientIdAsync(string value) =>
            SetSecretAsync(GoogleOAuthAndroidClientIdKey, value);

        public Task SetGoogleOAuthIosClientIdAsync(string value) =>
            SetSecretAsync(GoogleOAuthIosClientIdKey, value);

        public Task SetGoogleOAuthWindowsClientIdAsync(string value) =>
            SetSecretAsync(GoogleOAuthWindowsClientIdKey, value);

        public Task SetGoogleOAuthWindowsClientSecretAsync(string value) =>
            SetSecretAsync(GoogleOAuthWindowsClientSecretKey, value);

        private static async Task<string> GetSecretOrEnvAsync(string storageKey, string envVar)
        {
            var value = await SecureStorage.Default.GetAsync(storageKey);
            if (string.IsNullOrWhiteSpace(value))
            {
                value = Environment.GetEnvironmentVariable(envVar);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    await SecureStorage.Default.SetAsync(storageKey, value);
                }
            }

            return value ?? string.Empty;
        }

        private static Task SetSecretAsync(string storageKey, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                SecureStorage.Default.Remove(storageKey);
                return Task.CompletedTask;
            }

            return SecureStorage.Default.SetAsync(storageKey, value.Trim());
        }

        // ISettingsService typed accessors (delegate to MAUI Preferences)
        public bool GetBool(string key, bool fallback = false) => Preferences.Get(key, fallback);
        public void SetBool(string key, bool value) => Preferences.Set(key, value);
        public int GetInt(string key, int fallback = 0) => Preferences.Get(key, fallback);
        public void SetInt(string key, int value) => Preferences.Set(key, value);
        public string GetString(string key, string fallback = "") => Preferences.Get(key, fallback);
        public void SetString(string key, string value) => Preferences.Set(key, value);
    }
}
