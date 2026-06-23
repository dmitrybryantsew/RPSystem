// MauiProgram.cs
using CommunityToolkit.Maui;
using ChemCalculationAndManagementApp.Services;
using ChemCalculationAndManagementApp.ViewModels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using SkiaSharp.Views.Maui.Controls.Hosting;

namespace ChemCalculationAndManagementApp
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
                var handler = GetInsecureHandler();
                var httpClient = new HttpClient(handler);
                var settings = sp.GetRequiredService<ISettingsService>();
                return new OpenRouterService(httpClient, settings);
            });

            builder.Services.AddHttpClient<AiModelService>()
                .ConfigurePrimaryHttpMessageHandler(GetInsecureHandler);

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

            return builder.Build();
        }

        private static HttpMessageHandler GetInsecureHandler()
        {
#if ANDROID
            var handler = new Xamarin.Android.Net.AndroidMessageHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            return handler;
#else
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
            return handler;
#endif
        }
    }
}
