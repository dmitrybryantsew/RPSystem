using ChemCalculationAndManagementApp.RpSystem;
using ChemCalculationAndManagementApp.Services;

namespace ChemCalculationAndManagementApp.Tests.RpSystem;

public sealed class RpFakeLlmClient : IRpLlmClient
{
    private readonly Queue<LlmActionResponse> _responses = new();

    public int CallCount { get; private set; }
    public List<LlmSnapshot> Snapshots { get; } = [];

    public void Enqueue(LlmActionResponse response)
    {
        _responses.Enqueue(response);
    }

    public Task<LlmActionResponse> GetActionAsync(
        LlmSnapshot snapshot,
        string provider,
        string apiKey,
        string model,
        CancellationToken cancellationToken)
    {
        CallCount++;
        Snapshots.Add(snapshot);
        return Task.FromResult(_responses.Count > 0 ? _responses.Dequeue() : RpSimulationService.WaitResponse("Fake wait."));
    }
}
