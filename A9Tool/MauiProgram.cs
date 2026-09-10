using Core.Interfaces;
using Core.Services;
using Infrastructure.Github;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices; // Required for AllocConsole

namespace A9Tool
{
    public static class MauiProgram
    {
        // Import the native Windows API to allocate a command prompt window
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool AllocConsole();

        public static MauiApp CreateMauiApp()
        {
#if DEBUG && WINDOWS
            // This forces Windows to spin up a command prompt window side-by-side with your app UI
            AllocConsole();
#endif

            var builder = MauiApp.CreateBuilder();
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
            builder.Services.AddSingleton<ImageService>();

#if DEBUG
            builder.Services.AddBlazorWebViewDeveloperTools();
            builder.Logging.AddDebug();

            // Configures console logging formatting identically to a standard .NET Web API
            builder.Logging.AddConsole(options =>
            {
                options.TimestampFormat = "[HH:mm:ss] ";
            });
#endif

            return builder.Build();
        }
    }
}