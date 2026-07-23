namespace RPSystem.Core.Services;

/// <summary>
/// Minimal text-completion client interface. Implemented by
/// <see cref="OpenRouterService"/>; stubbed in tests so the
/// authoring-assistant service can be exercised without hitting the network.
/// </summary>
public interface IRpTextCompletionClient
{
    Task<string> GenerateTextAsync(
        string provider,
        string apiKey,
        string model,
        string prompt,
        List<ChatApiMessage>? conversationHistory = null);
}
