using Core.Interfaces;
using Core.Services;
using Infrastructure.Github;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage; // Thêm để dùng FileSystem
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

            // 1. Đăng ký HttpClient riêng lẻ
            builder.Services.AddScoped(sp => new HttpClient());

            // 2. Đăng ký Repository thủ công để truyền path
            builder.Services.AddSingleton<IGameDataRepository>(sp =>
            {
                var httpClient = sp.GetRequiredService<HttpClient>();
                var logger = sp.GetRequiredService<ILogger<GithubGameDataRepository>>();

                // Lấy đường dẫn thư mục dữ liệu của App trên từng nền tảng (Win/Android/iOS)
                string localPath = Path.Combine(FileSystem.AppDataDirectory, "operators.json");

                return new GithubGameDataRepository(httpClient, logger, localPath);
            });

            builder.Services.AddScoped<CharacterService>();

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