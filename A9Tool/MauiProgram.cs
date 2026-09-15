using Core.Interfaces;
using Core.Services;
using Infrastructure.Github;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;

namespace A9Tool
{
    public static class MauiProgram
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        private const int STD_OUTPUT_HANDLE = -11;
        private const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;

        private static void EnableVirtualTerminal()
        {
            var handle = GetStdHandle(STD_OUTPUT_HANDLE);
            if (GetConsoleMode(handle, out uint mode))
            {
                SetConsoleMode(handle, mode | ENABLE_VIRTUAL_TERMINAL_PROCESSING);
            }
        }

        public static MauiApp CreateMauiApp()
        {
#if DEBUG && WINDOWS
            // Force Windows to allocate command prompt window side-by-side with app UI
            AllocConsole();
            EnableVirtualTerminal();
#endif

            // Initialize Serilog with ANSI Console theme for colored, structured logging
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console(
                    theme: AnsiConsoleTheme.Code,
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            var builder = MauiApp.CreateBuilder();

            // Register Serilog as the primary logging provider
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog(dispose: true);

            builder
                .UseMauiApp<App>()
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                });

            builder.Services.AddMauiBlazorWebView();

            // 1. Register standalone HttpClient instance
            builder.Services.AddScoped(sp => new HttpClient());

            // 2. Register repository singleton with platform-specific persistent storage path
            builder.Services.AddSingleton<IGameDataRepository>(sp =>
            {
                var httpClient = sp.GetRequiredService<HttpClient>();
                var logger = sp.GetRequiredService<ILogger<GithubGameDataRepository>>();

                // Resolve platform-specific application data directory (Windows / Android / iOS)
                string localPath = Path.Combine(FileSystem.AppDataDirectory, "operators.json");

                return new GithubGameDataRepository(httpClient, logger, localPath);
            });

            builder.Services.AddSingleton(sp =>
            {
                var http = sp.GetRequiredService<HttpClient>();
                var logger = sp.GetRequiredService<ILogger<ImageService>>();
                string cachePath = Path.Combine(FileSystem.CacheDirectory, "A9ToolImages");
                return new ImageService(http, logger, cachePath);
            });

            builder.Services.AddScoped<CharacterService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
#endif

            return builder.Build();
        }
    }
}