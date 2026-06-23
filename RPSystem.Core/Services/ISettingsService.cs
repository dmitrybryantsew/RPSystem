namespace RPSystem.Core.Services
{
    /// <summary>
    /// Interface for application settings.
    /// Enables dependency injection and testing without concrete dependencies.
    /// </summary>
    public interface ISettingsService
    {
        bool DebugEnabled { get; set; }
        string AiProvider { get; set; }
        string OpenRouterApiKey { get; set; }
        string OpenAiProxyApiKey { get; set; }
        string OpenAiProxyBaseUrl { get; set; }
        string ZaiApiKey { get; set; }
        bool ZaiSubscriptionMode { get; set; }
        string ChutesApiKey { get; set; }
        bool CompactModelNames { get; set; }
        bool HideModelsOlderThanMaxAge { get; set; }
        int ModelMaxAgeDays { get; set; }
        bool ShowG4fProxyModels { get; set; }
        string HiddenModelIds { get; set; }
        string TranscriptionProvider { get; set; }
        string TranscriptionMultimodalModel { get; set; }
        string LocalWhisperEngine { get; set; }
        string TranscriptionLanguage { get; set; }
        string LocalWhisperModelPath { get; set; }
        string LocalWhisperGpuAdapter { get; set; }
        bool LocalWhisperFallbackToChutes { get; set; }
        RPSystem.Core.Models.TextMode TranscriptionTextMode { get; set; }
        string DefaultChatExportFormat { get; set; }
        bool ShowConversationTokenTotal { get; set; }
        string ChatColorScheme { get; set; }
    }
}
