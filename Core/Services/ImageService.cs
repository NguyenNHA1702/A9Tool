using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Core.Services
{
    /// <summary>
    /// High-performance 2-tier image caching service (L1 RAM + L2 Disk).
    /// Provides Base64 Data URI strings for Blazor WebViews, thread-safe network downloads, and prefetching.
    /// </summary>
    public class ImageService
    {
        // 1. HTTP client for downloading image assets from remote CDN
        private readonly HttpClient _httpClient;

        // 2. Logger for tracking cache hits, misses, and download diagnostics
        private readonly ILogger<ImageService> _logger;

        // 3. Root directory path on local disk storage for cached images
        private readonly string _baseCachePath;

        // 4. L1 In-Memory Cache: Maps URL to Base64 Data URI string for instantaneous retrieval (0ms)
        private readonly ConcurrentDictionary<string, string> _memoryCache = new();

        // 5. Per-URL Semaphore locks to prevent duplicate concurrent network downloads (anti-race condition)
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        /// <summary>
        /// Initializes a new instance of <see cref="ImageService"/> with required dependencies and initializes the cache directory.
        /// </summary>
        /// <param name="httpClient">HTTP client instance.</param>
        /// <param name="logger">Logger instance.</param>
        /// <param name="baseCachePath">Absolute path to local disk cache directory.</param>
        public ImageService(HttpClient httpClient, ILogger<ImageService> logger, string baseCachePath)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseCachePath = baseCachePath;

            // Ensure the root cache directory exists on startup
            if (!Directory.Exists(_baseCachePath))
            {
                Directory.CreateDirectory(_baseCachePath);
            }
        }

        /// <summary>
        /// Computes an MD5 hexadecimal hash (32 characters) for the given URL to use as a safe, filesystem-compatible filename.
        /// </summary>
        /// <param name="url">The image source URL.</param>
        /// <returns>A unique hashed file name preserving the image extension (defaulting to .png).</returns>
        private static string GetHashedFileName(string url)
        {
            using var md5 = MD5.Create();
            byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes(url));
            var sb = new StringBuilder();
            foreach (byte b in hash) sb.Append(b.ToString("x2"));

            string ext = Path.GetExtension(url.Split('?')[0]);
            if (string.IsNullOrEmpty(ext) || ext.Length > 5) ext = ".png";
            return sb.ToString() + ext;
        }

        /// <summary>
        /// Converts raw image bytes into a Base64 Data URI string compatible with HTML &lt;img&gt; tags inside WebViews.
        /// </summary>
        /// <param name="bytes">Raw image byte array.</param>
        /// <returns>A formatted data URI string (e.g., data:image/png;base64,...).</returns>
        private static string ToDataUri(byte[] bytes)
        {
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }

        /// <summary>
        /// Safely downloads raw byte array from the given URL with a timeout safeguard.
        /// </summary>
        /// <param name="url">Target resource URL.</param>
        /// <param name="timeoutSeconds">Network request timeout in seconds.</param>
        /// <returns>Downloaded byte array, or null if the request failed or timed out.</returns>
        private async Task<byte[]?> DownloadBytesSafeAsync(string url, int timeoutSeconds = 15)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                return await _httpClient.GetByteArrayAsync(url, cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ImageCache] Download error for {Url}: {Message}", url, ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Retrieves an image as a Base64 Data URI through a 2-tier caching hierarchy:
        /// L1 RAM (0ms) -> L2 Disk Cache -> Thread-safe Network Download -> Fallback URL.
        /// </summary>
        /// <param name="url">Primary image source URL.</param>
        /// <param name="fallbackUrl">Optional fallback URL if primary fails.</param>
        /// <param name="category">Sub-folder category on disk (e.g., "characters", "avatars", "skills", "modules").</param>
        /// <returns>Base64 Data URI string, or fallback/empty string on failure.</returns>
        public async Task<string> GetImageAsync(string? url, string? fallbackUrl = null, string category = "general")
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return fallbackUrl ?? "";
            }

            // 1. Check L1 Memory Cache (RAM)
            if (_memoryCache.TryGetValue(url, out var memoryDataUri))
            {
                return memoryDataUri;
            }

            // 2. Check L2 Disk Cache
            string categoryFolder = Path.Combine(_baseCachePath, category);
            if (!Directory.Exists(categoryFolder))
            {
                Directory.CreateDirectory(categoryFolder);
            }

            string fileName = GetHashedFileName(url);
            string filePath = Path.Combine(categoryFolder, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath);
                    string dataUri = ToDataUri(bytes);
                    _memoryCache[url] = dataUri;
                    return dataUri;
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "[ImageCache] Error reading cached file: {Path}", filePath);
                }
            }

            // 3. Cache Miss: Acquire URL-specific Semaphore lock to prevent duplicate downloads
            var sem = _locks.GetOrAdd(url, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();

            try
            {
                // Double-check RAM cache after acquiring lock
                if (_memoryCache.TryGetValue(url, out memoryDataUri))
                {
                    return memoryDataUri;
                }

                // Double-check disk cache after acquiring lock
                if (File.Exists(filePath))
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath);
                    string dataUri = ToDataUri(bytes);
                    _memoryCache[url] = dataUri;
                    return dataUri;
                }

                // Download from primary URL
                byte[]? downloadedBytes = await DownloadBytesSafeAsync(url);

                // Attempt fallback URL if primary download failed
                if (downloadedBytes == null && !string.IsNullOrEmpty(fallbackUrl) && fallbackUrl != url)
                {
                    _logger.LogWarning("[ImageCache] Primary URL failed, trying fallback: {Url}", fallbackUrl);
                    downloadedBytes = await DownloadBytesSafeAsync(fallbackUrl);
                }

                if (downloadedBytes != null && downloadedBytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(filePath, downloadedBytes);
                    string dataUri = ToDataUri(downloadedBytes);
                    _memoryCache[url] = dataUri;
                    _logger.LogInformation("[ImageCache Saved] {Category}/{File} ({Size} KB)", category, fileName, downloadedBytes.Length / 1024);
                    return dataUri;
                }
            }
            finally
            {
                sem.Release();
            }

            // Return fallback URL or original URL if all caching and downloading attempts fail
            return fallbackUrl ?? url;
        }

        /// <summary>
        /// Asynchronously pre-fetches and caches an image in the background without blocking the UI thread (fire-and-forget).
        /// </summary>
        /// <param name="url">Image URL to pre-fetch.</param>
        /// <param name="fallbackUrl">Optional fallback URL.</param>
        /// <param name="category">Sub-folder category.</param>
        public void PrefetchImage(string? url, string? fallbackUrl = null, string category = "general")
        {
            if (string.IsNullOrWhiteSpace(url)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await GetImageAsync(url, fallbackUrl, category);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ImageCache] Prefetch failed for: {Url}", url);
                }
            });
        }

        /// <summary>
        /// Calculates the total disk space usage (in bytes) of all cached image files.
        /// </summary>
        /// <returns>Total size in bytes.</returns>
        public Task<long> GetCacheSizeBytesAsync()
        {
            return Task.Run(() =>
            {
                if (!Directory.Exists(_baseCachePath)) return 0L;
                var dirInfo = new DirectoryInfo(_baseCachePath);
                long size = 0;
                foreach (var file in dirInfo.GetFiles("*", SearchOption.AllDirectories))
                {
                    size += file.Length;
                }
                return size;
            });
        }

        /// <summary>
        /// Purges both the L1 in-memory cache and the L2 on-disk cache directory.
        /// </summary>
        public Task ClearCacheAsync()
        {
            return Task.Run(() =>
            {
                _memoryCache.Clear();
                if (Directory.Exists(_baseCachePath))
                {
                    try
                    {
                        Directory.Delete(_baseCachePath, true);
                        Directory.CreateDirectory(_baseCachePath);
                        _logger.LogInformation("[ImageCache] Successfully cleared all cached images.");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "[ImageCache] Failed to delete cache folder: {Message}", ex.Message);
                    }
                }
            });
        }
    }
}
