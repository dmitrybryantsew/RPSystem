using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RPSystem.Services;
using RPSystem.Core.Services;
using Microsoft.Maui.Storage;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

#if WINDOWS
#endif

namespace RPSystem.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly SettingsService _settingsService;
        private readonly AiModelService _aiModelService;
        private readonly ObservableCollection<AiModel> _multimodalTranscriptionModels = new();

        [ObservableProperty]
        private string _modelCacheStatus = string.Empty;

        private string _androidClientId = string.Empty;
        private string _iosClientId = string.Empty;
        private string _windowsClientId = string.Empty;
        private string _windowsClientSecret = string.Empty;
        private AiModel? _selectedMultimodalTranscriptionModel;

        public SettingsViewModel(SettingsService settingsService, AiModelService aiModelService)
        {
            _settingsService = settingsService;
            _aiModelService = aiModelService;
            _selectedTabIndex = 0;
#if WINDOWS
#endif

            // Subscribe to property changes from the service
            _settingsService.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SettingsService.DebugEnabled))
                {
                    OnPropertyChanged(nameof(DebugEnabled));
                }
                if (e.PropertyName == nameof(SettingsService.AiProvider))
                {
                    OnPropertyChanged(nameof(IsOpenRouterSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxySelected));
                    OnPropertyChanged(nameof(IsZaiSelected));
                    OnPropertyChanged(nameof(IsChutesSelected));
                }
                if (e.PropertyName == nameof(SettingsService.OpenRouterApiKey))
                {
                    OnPropertyChanged(nameof(OpenRouterApiKey));
                }
                if (e.PropertyName == nameof(SettingsService.OpenAiProxyApiKey))
                {
                    OnPropertyChanged(nameof(OpenAiProxyApiKey));
                }
                if (e.PropertyName == nameof(SettingsService.OpenAiProxyBaseUrl))
                {
                    OnPropertyChanged(nameof(OpenAiProxyBaseUrl));
                }
                if (e.PropertyName == nameof(SettingsService.ZaiApiKey))
                {
                    OnPropertyChanged(nameof(ZaiApiKey));
                }
                if (e.PropertyName == nameof(SettingsService.ZaiSubscriptionMode))
                {
                    OnPropertyChanged(nameof(ZaiSubscriptionMode));
                }
                if (e.PropertyName == nameof(SettingsService.PhotoQuality))
                {
                    OnPropertyChanged(nameof(PhotoQuality));
                }
                if (e.PropertyName == nameof(SettingsService.MaxResolution))
                {
                    OnPropertyChanged(nameof(MaxResolution));
                }
                if (e.PropertyName == nameof(SettingsService.ChutesApiKey))
                {
                    OnPropertyChanged(nameof(ChutesApiKey));
                }
                if (e.PropertyName == nameof(SettingsService.CompactModelNames))
                {
                    OnPropertyChanged(nameof(CompactModelNames));
                }
                if (e.PropertyName == nameof(SettingsService.HideModelsOlderThanMaxAge))
                {
                    OnPropertyChanged(nameof(HideModelsOlderThanMaxAge));
                }
                if (e.PropertyName == nameof(SettingsService.ModelMaxAgeDays))
                {
                    OnPropertyChanged(nameof(ModelMaxAgeDays));
                }
                if (e.PropertyName == nameof(SettingsService.ShowG4fProxyModels))
                {
                    OnPropertyChanged(nameof(ShowG4fProxyModels));
                }
                if (e.PropertyName == nameof(SettingsService.HiddenModelIds))
                {
                    OnPropertyChanged(nameof(HiddenModelIds));
                }
                if (e.PropertyName == nameof(SettingsService.TranscriptionProvider))
                {
                    OnPropertyChanged(nameof(IsTranscriptionProviderChutesSelected));
                    OnPropertyChanged(nameof(IsTranscriptionProviderOpenAiProxyMultimodalSelected));
                    OnPropertyChanged(nameof(IsTranscriptionProviderLocalSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxyMultimodalTranscriptionSelected));
                }
                if (e.PropertyName == nameof(SettingsService.TranscriptionMultimodalModel))
                {
                    OnPropertyChanged(nameof(TranscriptionMultimodalModel));
                    SyncSelectedMultimodalTranscriptionModel();
                }

                if (e.PropertyName == nameof(SettingsService.TranscriptionLanguage))
                {
                    OnPropertyChanged(nameof(TranscriptionLanguage));
                }



                if (e.PropertyName == nameof(SettingsService.DefaultChatExportFormat))
                {
                    OnPropertyChanged(nameof(DefaultChatExportFormat));
                    OnPropertyChanged(nameof(IsExportFormatJson));
                    OnPropertyChanged(nameof(IsExportFormatMarkdown));
                }
                if (e.PropertyName == nameof(SettingsService.ShowConversationTokenTotal))
                {
                    OnPropertyChanged(nameof(ShowConversationTokenTotal));
                }
                if (e.PropertyName == nameof(SettingsService.ChatColorScheme))
                {
                    OnPropertyChanged(nameof(ChatColorScheme));
                    OnPropertyChanged(nameof(IsColorSchemeClassic));
                    OnPropertyChanged(nameof(IsColorSchemeForest));
                    OnPropertyChanged(nameof(IsColorSchemeSunset));
                    OnPropertyChanged(nameof(IsColorSchemeMono));
                }
            };
        }

        [ObservableProperty]
        private int _selectedTabIndex;

        public bool IsGeneralTabSelected => SelectedTabIndex == 0;
        public bool IsCameraTabSelected => SelectedTabIndex == 1;
        public bool IsAiTabSelected => SelectedTabIndex == 2;
        public bool IsIntegrationsTabSelected => SelectedTabIndex == 3;

        partial void OnSelectedTabIndexChanged(int value)
        {
            OnPropertyChanged(nameof(IsGeneralTabSelected));
            OnPropertyChanged(nameof(IsCameraTabSelected));
            OnPropertyChanged(nameof(IsAiTabSelected));
            OnPropertyChanged(nameof(IsIntegrationsTabSelected));
        }

        [RelayCommand]
        private void SelectTab(string? parameter)
        {
            if (!int.TryParse(parameter, out var index))
            {
                return;
            }

            if (index < 0 || index > 3)
            {
                return;
            }

            SelectedTabIndex = index;
        }

        public async Task InitializeAsync()
        {
            AndroidClientId = await _settingsService.GetGoogleOAuthAndroidClientIdAsync();
            IosClientId = await _settingsService.GetGoogleOAuthIosClientIdAsync();
            WindowsClientId = await _settingsService.GetGoogleOAuthWindowsClientIdAsync();
            WindowsClientSecret = await _settingsService.GetGoogleOAuthWindowsClientSecretAsync();
            await RefreshMultimodalTranscriptionModelsAsync(forceRefresh: false);
        }

        public bool DebugEnabled
        {
            get => _settingsService.DebugEnabled;
            set => _settingsService.DebugEnabled = value;
        }

        public string ZaiApiKey
        {
            get => _settingsService.ZaiApiKey;
            set => _settingsService.ZaiApiKey = value;
        }

        public string OpenRouterApiKey
        {
            get => _settingsService.OpenRouterApiKey;
            set => _settingsService.OpenRouterApiKey = value;
        }

        public string OpenAiProxyApiKey
        {
            get => _settingsService.OpenAiProxyApiKey;
            set => _settingsService.OpenAiProxyApiKey = value;
        }

        public string OpenAiProxyBaseUrl
        {
            get => _settingsService.OpenAiProxyBaseUrl;
            set => _settingsService.OpenAiProxyBaseUrl = value;
        }

        public bool ZaiSubscriptionMode
        {
            get => _settingsService.ZaiSubscriptionMode;
            set => _settingsService.ZaiSubscriptionMode = value;
        }

        public int PhotoQuality
        {
            get => _settingsService.PhotoQuality;
            set => _settingsService.PhotoQuality = value;
        }

        public int MaxResolution
        {
            get => _settingsService.MaxResolution;
            set => _settingsService.MaxResolution = value;
        }

        public string ChutesApiKey
        {
            get => _settingsService.ChutesApiKey;
            set => _settingsService.ChutesApiKey = value;
        }

        public bool CompactModelNames
        {
            get => _settingsService.CompactModelNames;
            set => _settingsService.CompactModelNames = value;
        }

        public bool HideModelsOlderThanMaxAge
        {
            get => _settingsService.HideModelsOlderThanMaxAge;
            set => _settingsService.HideModelsOlderThanMaxAge = value;
        }

        public int ModelMaxAgeDays
        {
            get => _settingsService.ModelMaxAgeDays;
            set => _settingsService.ModelMaxAgeDays = Math.Max(1, value);
        }

        public bool ShowG4fProxyModels
        {
            get => _settingsService.ShowG4fProxyModels;
            set => _settingsService.ShowG4fProxyModels = value;
        }

        public string HiddenModelIds
        {
            get => _settingsService.HiddenModelIds;
            set => _settingsService.HiddenModelIds = value ?? string.Empty;
        }

        public string TranscriptionLanguage
        {
            get => _settingsService.TranscriptionLanguage;
            set => _settingsService.TranscriptionLanguage = (value ?? "en").Trim().ToLowerInvariant();
        }

        public string TranscriptionMultimodalModel
        {
            get => _settingsService.TranscriptionMultimodalModel;
            set => _settingsService.TranscriptionMultimodalModel = value ?? string.Empty;
        }

        public ObservableCollection<AiModel> MultimodalTranscriptionModels => _multimodalTranscriptionModels;

        public AiModel? SelectedMultimodalTranscriptionModel
        {
            get => _selectedMultimodalTranscriptionModel;
            set
            {
                if (SetProperty(ref _selectedMultimodalTranscriptionModel, value) && value != null)
                {
                    TranscriptionMultimodalModel = value.Id;
                }
            }
        }

        public bool IsTranscriptionProviderChutesSelected
        {
            get => _settingsService.TranscriptionProvider == "Chutes";
            set
            {
                if (value)
                {
                    _settingsService.TranscriptionProvider = "Chutes";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTranscriptionProviderOpenAiProxyMultimodalSelected));
                    OnPropertyChanged(nameof(IsTranscriptionProviderLocalSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxyMultimodalTranscriptionSelected));
                }
            }
        }

        public bool IsTranscriptionProviderOpenAiProxyMultimodalSelected
        {
            get => _settingsService.TranscriptionProvider == "OpenAIProxyMultimodal";
            set
            {
                if (value)
                {
                    _settingsService.TranscriptionProvider = "OpenAIProxyMultimodal";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTranscriptionProviderChutesSelected));
                    OnPropertyChanged(nameof(IsTranscriptionProviderLocalSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxyMultimodalTranscriptionSelected));
                }
            }
        }

        public bool IsTranscriptionProviderLocalSelected
        {
            get => _settingsService.TranscriptionProvider == "LocalWhisper";
            set
            {
                if (value)
                {
                    _settingsService.TranscriptionProvider = "LocalWhisper";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsTranscriptionProviderChutesSelected));
                    OnPropertyChanged(nameof(IsTranscriptionProviderOpenAiProxyMultimodalSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxyMultimodalTranscriptionSelected));
                }
            }
        }

        public bool IsOpenAiProxyMultimodalTranscriptionSelected => _settingsService.TranscriptionProvider == "OpenAIProxyMultimodal";

        public string DefaultChatExportFormat
        {
            get => _settingsService.DefaultChatExportFormat;
            set
            {
                var normalized = (value ?? "json").Trim().ToLowerInvariant();
                if (normalized != "json" && normalized != "md")
                {
                    normalized = "json";
                }

                _settingsService.DefaultChatExportFormat = normalized;
                OnPropertyChanged(nameof(IsExportFormatJson));
                OnPropertyChanged(nameof(IsExportFormatMarkdown));
            }
        }

        public bool IsExportFormatJson
        {
            get => DefaultChatExportFormat == "json";
            set
            {
                if (value)
                {
                    DefaultChatExportFormat = "json";
                }
            }
        }

        public bool IsExportFormatMarkdown
        {
            get => DefaultChatExportFormat == "md";
            set
            {
                if (value)
                {
                    DefaultChatExportFormat = "md";
                }
            }
        }

        public bool ShowConversationTokenTotal
        {
            get => _settingsService.ShowConversationTokenTotal;
            set => _settingsService.ShowConversationTokenTotal = value;
        }

        public string ChatColorScheme
        {
            get => _settingsService.ChatColorScheme;
            set
            {
                var normalized = (value ?? "classic").Trim().ToLowerInvariant();
                if (normalized != "classic" && normalized != "forest" && normalized != "sunset" && normalized != "mono")
                {
                    normalized = "classic";
                }

                _settingsService.ChatColorScheme = normalized;
                OnPropertyChanged(nameof(IsColorSchemeClassic));
                OnPropertyChanged(nameof(IsColorSchemeForest));
                OnPropertyChanged(nameof(IsColorSchemeSunset));
                OnPropertyChanged(nameof(IsColorSchemeMono));
            }
        }

        public bool IsColorSchemeClassic
        {
            get => ChatColorScheme == "classic";
            set { if (value) ChatColorScheme = "classic"; }
        }

        public bool IsColorSchemeForest
        {
            get => ChatColorScheme == "forest";
            set { if (value) ChatColorScheme = "forest"; }
        }

        public bool IsColorSchemeSunset
        {
            get => ChatColorScheme == "sunset";
            set { if (value) ChatColorScheme = "sunset"; }
        }

        public bool IsColorSchemeMono
        {
            get => ChatColorScheme == "mono";
            set { if (value) ChatColorScheme = "mono"; }
        }

        public string AndroidClientId
        {
            get => _androidClientId;
            set
            {
                if (SetProperty(ref _androidClientId, value))
                {
                    _ = _settingsService.SetGoogleOAuthAndroidClientIdAsync(value);
                }
            }
        }

        public string IosClientId
        {
            get => _iosClientId;
            set
            {
                if (SetProperty(ref _iosClientId, value))
                {
                    _ = _settingsService.SetGoogleOAuthIosClientIdAsync(value);
                }
            }
        }

        public string WindowsClientId
        {
            get => _windowsClientId;
            set
            {
                if (SetProperty(ref _windowsClientId, value))
                {
                    _ = _settingsService.SetGoogleOAuthWindowsClientIdAsync(value);
                }
            }
        }

        public string WindowsClientSecret
        {
            get => _windowsClientSecret;
            set
            {
                if (SetProperty(ref _windowsClientSecret, value))
                {
                    _ = _settingsService.SetGoogleOAuthWindowsClientSecretAsync(value);
                }
            }
        }

        public bool IsOpenRouterSelected
        {
            get => _settingsService.AiProvider == "OpenRouter";
            set
            {
                if (value)
                {
                    _settingsService.AiProvider = "OpenRouter";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOpenAiProxySelected));
                    OnPropertyChanged(nameof(IsZaiSelected));
                    OnPropertyChanged(nameof(IsChutesSelected));
                }
            }
        }

        public bool IsOpenAiProxySelected
        {
            get => _settingsService.AiProvider == "OpenAIProxy";
            set
            {
                if (value)
                {
                    _settingsService.AiProvider = "OpenAIProxy";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOpenRouterSelected));
                    OnPropertyChanged(nameof(IsZaiSelected));
                    OnPropertyChanged(nameof(IsChutesSelected));
                }
            }
        }

        public bool IsZaiSelected
        {
            get => _settingsService.AiProvider == "ZAI";
            set
            {
                if (value)
                {
                    _settingsService.AiProvider = "ZAI";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOpenRouterSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxySelected));
                    OnPropertyChanged(nameof(IsChutesSelected));
                }
            }
        }

        public bool IsChutesSelected
        {
            get => _settingsService.AiProvider == "Chutes";
            set
            {
                if (value)
                {
                    _settingsService.AiProvider = "Chutes";
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsOpenRouterSelected));
                    OnPropertyChanged(nameof(IsOpenAiProxySelected));
                    OnPropertyChanged(nameof(IsZaiSelected));
                }
            }
        }

        [RelayCommand]
        public async Task PreloadModels()
        {
            ModelCacheStatus = "Refreshing models...";
            var result = await _aiModelService.PreloadModelsAsync();
            ModelCacheStatus = result.success
                ? $"OK: {result.message}"
                : $"Warning: {result.message}";
        }

        [RelayCommand]
        private Task RefreshMultimodalTranscriptionModels()
            => RefreshMultimodalTranscriptionModelsAsync(forceRefresh: true);

        private async Task RefreshMultimodalTranscriptionModelsAsync(bool forceRefresh)
        {
            try
            {
                var models = await _aiModelService.GetModelsForProviderAsync("OpenAIProxy", forceRefresh);
                var multimodalModels = models
                    .Where(AiModelCapabilities.IsMultimodalInputCandidate)
                    .GroupBy(model => model.Id, StringComparer.OrdinalIgnoreCase)
                    .Select(group => group.First())
                    .OrderBy(model => model.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (multimodalModels.Count == 0 && !string.IsNullOrWhiteSpace(TranscriptionMultimodalModel))
                {
                    multimodalModels.Add(new AiModel
                    {
                        Id = TranscriptionMultimodalModel,
                        Name = TranscriptionMultimodalModel,
                        Provider = "OpenAIProxy"
                    });
                }

                _multimodalTranscriptionModels.Clear();
                foreach (var model in multimodalModels)
                {
                    _multimodalTranscriptionModels.Add(model);
                }

                SyncSelectedMultimodalTranscriptionModel();
                ModelCacheStatus = $"Loaded {_multimodalTranscriptionModels.Count} multimodal proxy model candidate(s).";
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(TranscriptionMultimodalModel) &&
                    !_multimodalTranscriptionModels.Any(model => string.Equals(model.Id, TranscriptionMultimodalModel, StringComparison.OrdinalIgnoreCase)))
                {
                    _multimodalTranscriptionModels.Add(new AiModel
                    {
                        Id = TranscriptionMultimodalModel,
                        Name = TranscriptionMultimodalModel,
                        Provider = "OpenAIProxy"
                    });
                    SyncSelectedMultimodalTranscriptionModel();
                }

                ModelCacheStatus = $"Warning: Failed to load proxy multimodal models ({ex.Message})";
            }
        }

        private void SyncSelectedMultimodalTranscriptionModel()
        {
            var selected = _multimodalTranscriptionModels.FirstOrDefault(model =>
                string.Equals(model.Id, TranscriptionMultimodalModel, StringComparison.OrdinalIgnoreCase));

            if (selected == null && !string.IsNullOrWhiteSpace(TranscriptionMultimodalModel))
            {
                selected = new AiModel
                {
                    Id = TranscriptionMultimodalModel,
                    Name = TranscriptionMultimodalModel,
                    Provider = "OpenAIProxy"
                };
                _multimodalTranscriptionModels.Add(selected);
            }

            _selectedMultimodalTranscriptionModel = selected;
            OnPropertyChanged(nameof(SelectedMultimodalTranscriptionModel));
        }
    }
}
