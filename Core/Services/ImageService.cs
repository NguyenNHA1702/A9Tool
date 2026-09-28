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

        // 6. Global concurrency throttle for network downloads to prevent socket pool starvation and timeouts
        private readonly SemaphoreSlim _networkThrottle = new(6, 6);

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
        /// Attempts to synchronously retrieve a cached Base64 Data URI from L1 (RAM) or L2 (Disk).
        /// Returns true if found in cache, false otherwise.
        /// </summary>
        /// <param name="url">Image URL to query.</param>
        /// <param name="category">Sub-folder category.</param>
        /// <param name="dataUri">The resolved Base64 Data URI if found.</param>
        /// <returns>True if cached, false if cache miss.</returns>
        public bool TryGetCachedImage(string? url, string category, out string dataUri)
        {
            dataUri = string.Empty;
            if (string.IsNullOrWhiteSpace(url)) return false;

            if (_memoryCache.TryGetValue(url, out var ramUri))
            {
                dataUri = ramUri;
                return true;
            }

            string categoryFolder = Path.Combine(_baseCachePath, category);
            string fileName = GetHashedFileName(url);
            string filePath = Path.Combine(categoryFolder, fileName);

            if (File.Exists(filePath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(filePath);
                    dataUri = ToDataUri(bytes);
                    _memoryCache[url] = dataUri;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ImageCache] Error reading cached file: {Path}", filePath);
                }
            }

            return false;
        }

        /// <summary>
        /// Safely downloads raw byte array from the given URL with concurrency throttling and a timeout safeguard.
        /// </summary>
        /// <param name="url">Target resource URL.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="timeoutSeconds">Network request timeout in seconds.</param>
        /// <returns>Downloaded byte array, or null if the request failed or timed out.</returns>
        private async Task<byte[]?> DownloadBytesSafeAsync(string url, CancellationToken cancellationToken = default, int timeoutSeconds = 25)
        {
            // Acquire a slot from the global network throttle before starting the timeout timer
            await _networkThrottle.WaitAsync(cancellationToken);
            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);
                return await _httpClient.GetByteArrayAsync(url, linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Request was cancelled by caller (e.g., component unmounted)
                return null;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("[ImageCache] Download timed out ({Timeout}s) for {Url}", timeoutSeconds, url);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ImageCache] Download error for {Url}: {Message}", url, ex.Message);
                return null;
            }
            finally
            {
                _networkThrottle.Release();
            }
        }

        /// <summary>
        /// Retrieves an image as a Base64 Data URI through a 2-tier caching hierarchy:
        /// L1 RAM (0ms) -> L2 Disk Cache -> Thread-safe Network Download -> Fallback URL.
        /// </summary>
        /// <param name="url">Primary image source URL.</param>
        /// <param name="fallbackUrl">Optional fallback URL if primary fails.</param>
        /// <param name="category">Sub-folder category on disk (e.g., "characters", "avatars", "skills", "modules").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>Base64 Data URI string, or fallback/empty string on failure.</returns>
        public async Task<string> GetImageAsync(
            string? url,
            string? fallbackUrl = null,
            string category = "general",
            CancellationToken cancellationToken = default)
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
                    byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
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
            await sem.WaitAsync(cancellationToken);

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
                    byte[] bytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
                    string dataUri = ToDataUri(bytes);
                    _memoryCache[url] = dataUri;
                    return dataUri;
                }

                // Download from primary URL with concurrency throttling
                byte[]? downloadedBytes = await DownloadBytesSafeAsync(url, cancellationToken);

                // Attempt fallback URL if primary download failed
                if (downloadedBytes == null && !string.IsNullOrEmpty(fallbackUrl) && fallbackUrl != url)
                {
                    _logger.LogWarning("[ImageCache] Primary URL failed, trying fallback: {Url}", fallbackUrl);
                    downloadedBytes = await DownloadBytesSafeAsync(fallbackUrl, cancellationToken);
                }

                if (downloadedBytes != null && downloadedBytes.Length > 0)
                {
                    await File.WriteAllBytesAsync(filePath, downloadedBytes, cancellationToken);
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
