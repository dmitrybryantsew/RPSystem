using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using RPSystem.Core.Services;
using RPSystem.Desktop.Services;
using RPSystem.Desktop.ViewModels;
using System.ComponentModel;

namespace RPSystem.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppPaths.AppDataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RPSystem");

        var services = new ServiceCollection();

        // Platform-specific implementations (this project)
        services.AddSingleton<ISettingsService, DesktopSettingsService>();
        services.AddSingleton<IClipboardService, AvaloniaClipboardService>();
        services.AddSingleton<IFilePickerService, AvaloniaFilePickerService>();

        // Plain HttpClient — no certificate bypass handler
        services.AddSingleton(_ => new HttpClient());

        // Core RP services
        services.AddSingleton<OpenRouterService>();
        services.AddSingleton<AiModelService>();
        services.AddSingleton<IRpLlmClient, RpLlmClient>();
        services.AddSingleton<RpSimulationService>();
        services.AddSingleton<RpWorldSaveService>();
        services.AddSingleton<RpWorldInspectionService>();
        services.AddSingleton<RpInventoryService>();
        services.AddSingleton<RpMarkdownImportService>();
        services.AddSingleton<RpInteractionService>();
        services.AddSingleton<RpAbilityService>();
        services.AddSingleton<IRpTextSummarizer, RpRuleBasedTextSummarizer>();
        services.AddSingleton<RpConversationService>();
        services.AddSingleton<RpCaveMapGenerator>();
        services.AddSingleton<RpMapRenderProjectionService>();
        services.AddSingleton<RpFlowFieldService>();
        services.AddSingleton<RpPathfindingService>();
        services.AddSingleton<RpCharacterCompositionService>();
        services.AddSingleton<RpWorldContextEditorService>();
        services.AddSingleton<RpJobService>();

        // ViewModels — registered in dependency order (leaves first)
        services.AddSingleton<WorldSimulationViewModel>();
        services.AddSingleton<PlayerControlViewModel>();
        services.AddSingleton<WorldMapViewModel>();
        services.AddSingleton<TestMapsViewModel>();
        services.AddSingleton<ContextAbilityEditorViewModel>();
        services.AddSingleton<RelationshipRuleEditorViewModel>();
        services.AddSingleton<ContextCharacterEditorViewModel>();
        services.AddSingleton<ContextModuleEditorViewModel>();
        services.AddSingleton<FactionProfileEditorViewModel>();
        services.AddSingleton<SpeciesTemplateEditorViewModel>();
        services.AddSingleton<SceneEnvironmentContinuityEditorViewModel>();
        services.AddSingleton<WorldContextEditorViewModel>();
        services.AddSingleton<MainShellViewModel>();

        var provider = services.BuildServiceProvider();

        var settings = provider.GetRequiredService<ISettingsService>();
        RpSimulationService.DebugLoggingEnabled = settings.DebugEnabled;
        if (settings is INotifyPropertyChanged notifying)
        {
            notifying.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(ISettingsService.DebugEnabled))
                {
                    RpSimulationService.DebugLoggingEnabled = settings.DebugEnabled;
                }
            };
        }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = provider.GetRequiredService<MainShellViewModel>()
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
