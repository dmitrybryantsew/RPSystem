// MauiProgram.cs
using CommunityToolkit.Maui;
using RPSystem.Services;
using RPSystem.Core.Services;
using RPSystem.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace RPSystem
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseSkiaSharp()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                });

#if DEBUG
            builder.Logging.AddDebug();
#endif

            // --- 1. SETTINGS ---
            builder.Services.AddSingleton<SettingsService>();
            builder.Services.AddSingleton<ISettingsService>(sp => sp.GetRequiredService<SettingsService>());

            // --- 2. LLM / MODEL SERVICES ---
            builder.Services.AddSingleton<OpenRouterService>(sp =>
            {
                var handler = CreateHttpHandler();
                var httpClient = new HttpClient(handler);
                var settings = sp.GetRequiredService<ISettingsService>();
                return new OpenRouterService(httpClient, settings);
            });

            builder.Services.AddHttpClient<AiModelService>()
                .ConfigurePrimaryHttpMessageHandler(CreateHttpHandler);

            // --- 3. RP SERVICES (instance-based only — factory classes are static) ---
            builder.Services.AddSingleton<IRpLlmClient, RpLlmClient>();
            builder.Services.AddSingleton<RpSimulationService>();
            builder.Services.AddSingleton<RpWorldSaveService>();
            builder.Services.AddSingleton<RpWorldInspectionService>();
            builder.Services.AddSingleton<RpInventoryService>();
            builder.Services.AddSingleton<RpMarkdownImportService>();
            builder.Services.AddSingleton<RpInteractionService>();
            builder.Services.AddSingleton<RpAbilityService>();
            builder.Services.AddSingleton<RpCaveMapGenerator>();
            builder.Services.AddSingleton<RpMapRenderProjectionService>();
            builder.Services.AddSingleton<RpFlowFieldService>();
            builder.Services.AddSingleton<RpPathfindingService>();
            builder.Services.AddSingleton<RpCharacterCompositionService>();
            builder.Services.AddSingleton<RpWorldContextEditorService>();
            builder.Services.AddSingleton<RpJobService>();
            // Static factory classes — called directly, not registered in DI:
            //   RpWorldFactory, RpBodyFactory, RpCreatureService, RpMovementCostService
            builder.Services.AddSingleton<IClipboardService, ClipboardService>();

            // --- 4. PAGES & VIEWMODELS ---
            builder.Services.AddSingleton<MainPage>();
            builder.Services.AddSingleton<MainViewModel>();

            builder.Services.AddSingleton<SettingsPage>();
            builder.Services.AddSingleton<SettingsViewModel>(sp =>
                new SettingsViewModel(
                    sp.GetRequiredService<SettingsService>(),
                    sp.GetRequiredService<AiModelService>()));

            builder.Services.AddTransient<RpSystemPage>();
            builder.Services.AddTransient<RpSystemViewModel>();

            var app = builder.Build();

            // --- 5. WIRE DEBUG LOGGING GATE ---
            var settings = app.Services.GetRequiredService<SettingsService>();
            RpSimulationService.DebugLoggingEnabled = settings.DebugEnabled;
            settings.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SettingsService.DebugEnabled))
                {
                    RpSimulationService.DebugLoggingEnabled = settings.DebugEnabled;
                }
            };

            return app;
        }

        private static HttpMessageHandler CreateHttpHandler()
        {
#if ANDROID
            return new Xamarin.Android.Net.AndroidMessageHandler();
#else
            return new HttpClientHandler();
#endif
        }
    }
}
