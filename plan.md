# 📑 Hướng Dẫn & Kế Hoạch Triển Khai Toàn Bộ Epic 5 (Tự Code)

> [!TIP]
> Bản kế hoạch này bao gồm toàn bộ các task còn lại của **Epic 5: Tải và Quản lý Tài nguyên Offline**:
> * **A9-502:** Tải offline Icons (Skill & Module)
> * **A9-503:** Cơ chế fallback hình ảnh thông minh (On-Demand Lazy Cache 2 lớp)
> * **A9-504:** Chuẩn hóa Error Handling cho `GameDataRepository` & `CharacterService`

---

## 1. 📋 Bảng Tổng Hợp Các File Cần Tạo & Sửa

| STT | File / Đường Dẫn | Thao Tác | Task | Mục Đích |
| :---: | :--- | :---: | :---: | :--- |
| 1 | [ImageService.cs](file:///d:/VS_Studio/A9Tools/A9Tool/Core/Services/ImageService.cs) | `[SỬA/HOÀN THIỆN]` | A9-503, A9-502 | Service quản lý cache ảnh 2 lớp (L1 RAM + L2 Disk), Base64 Data URI, SemaphoreSlim lock chống race condition, prefetch. |
| 2 | [MauiProgram.cs](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/MauiProgram.cs) | `[SỬA]` | A9-503 | Đăng ký `ImageService` bằng Factory truyền `FileSystem.CacheDirectory`, xóa dòng đăng ký trùng. |
| 3 | [LazyImage.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Common/LazyImage.razor) | `[NEW]` | A9-503 | Component Razor hiển thị ảnh mượt mà, có placeholder loading và nạp Data URI từ `ImageService`. |
| 4 | [Operators.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Pages/Operators.razor) | `[SỬA]` | A9-503 | Đổi thẻ `<img>` portrait sang dùng `<LazyImage />` (cuộn tới đâu tải & cache tới đó). |
| 5 | [OperatorDetail.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Pages/OperatorDetail.razor) | `[SỬA]` | A9-503, A9-502 | Cache Art E0/E1/E2, Skill Icons, Module Icons qua `ImageService`. Giữ nguyên icon tĩnh `/assets/...`. |
| 6 | [CharacterService.cs](file:///d:/VS_Studio/A9Tools/A9Tool/Core/Services/CharacterService.cs) | `[SỬA]` | A9-504 | Null-check an toàn cho `skillTable`, `uniEquipData`, tránh văng `NullReferenceException`. |
| 7 | [GithubGameDataRepository .cs](file:///d:/VS_Studio/A9Tools/A9Tool/Infrastructure/Github/GithubGameDataRepository%20.cs) | `[SỬA]` | A9-504 | Try-catch an toàn trong `GetUniEquipTableAsync`, `GetBattleEquipTableAsync`, ưu tiên đọc local cache đĩa nếu lỗi mạng. |
| 8 | [README.md](file:///d:/VS_Studio/A9Tools/A9Tool/README.md) | `[SỬA]` | Epic 5 | Đánh dấu hoàn thành `[x]` cho toàn bộ Epic 5. |

---

## 2. Chi Tiết Phần I: Chuẩn Hóa `ImageService.cs`

> **Vị trí file:** `Core/Services/ImageService.cs`  
> **Namespace:** `Core.Services`

### 2.1. Mã Nguồn Đầy Đủ & Chuẩn Xác Cho `ImageService.cs`

```csharp
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
    public class ImageService
    {
        // 1. HttpClient tải mảng byte ảnh từ CDN
        private readonly HttpClient _httpClient;

        // 2. Logger theo dõi thông tin cache trên console
        private readonly ILogger<ImageService> _logger;

        // 3. Đường dẫn thư mục gốc lưu ảnh trên ổ cứng
        private readonly string _baseCachePath;

        // 4. L1 RAM Cache: Lưu Base64 Data URI trong bộ nhớ (0ms)
        private readonly ConcurrentDictionary<string, string> _memoryCache = new();

        // 5. Khóa SemaphoreSlim cho từng URL để chống Race Condition khi nhiều component tải cùng lúc
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public ImageService(HttpClient httpClient, ILogger<ImageService> logger, string baseCachePath)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseCachePath = baseCachePath;

            if (!Directory.Exists(_baseCachePath))
            {
                Directory.CreateDirectory(_baseCachePath);
            }
        }

        /// <summary>
        /// Băm URL thành chuỗi hex MD5 32 ký tự để làm tên file an toàn trên hệ điều hành.
        /// </summary>
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
        /// Chuyển byte[] thành Base64 Data URI để thẻ <img> hiển thị trực tiếp mà không bị WebView chặn.
        /// </summary>
        private static string ToDataUri(byte[] bytes)
        {
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }

        /// <summary>
        /// Tải byte[] từ mạng an toàn có Timeout (15s) tránh treo app.
        /// </summary>
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
        /// Phương thức chính: Lấy ảnh theo quy trình RAM (L1) -> Disk (L2) -> Semaphore Lock -> Network Download -> Fallback.
        /// </summary>
        public async Task<string> GetImageAsync(string? url, string? fallbackUrl = null, string category = "general")
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return fallbackUrl ?? "";
            }

            // 1. Kiểm tra L1 RAM Cache (0ms)
            if (_memoryCache.TryGetValue(url, out var memoryDataUri))
            {
                return memoryDataUri;
            }

            // 2. Xác định đường dẫn file L2 Disk: _baseCachePath/{category}/{fileName}
            string categoryFolder = Path.Combine(_baseCachePath, category);
            if (!Directory.Exists(categoryFolder))
            {
                Directory.CreateDirectory(categoryFolder);
            }

            string fileName = GetHashedFileName(url);
            string filePath = Path.Combine(categoryFolder, fileName);

            // Kiểm tra L2 Disk Cache (Lưu ý: Phải là File.Exists, KHÔNG có dấu !)
            if (File.Exists(filePath))
            {
                try
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath);
                    string dataUri = ToDataUri(bytes);
                    _memoryCache[url] = dataUri;
                    return dataUri;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[ImageCache] Error reading cached file: {Path}", filePath);
                }
            }

            // 3. Cache Miss: Khóa Semaphore chống tải trùng cho URL này
            var sem = _locks.GetOrAdd(url, _ => new SemaphoreSlim(1, 1));
            await sem.WaitAsync();
            try
            {
                // Double-check sau khi vào lock
                if (_memoryCache.TryGetValue(url, out memoryDataUri))
                {
                    return memoryDataUri;
                }

                if (File.Exists(filePath))
                {
                    byte[] bytes = await File.ReadAllBytesAsync(filePath);
                    string dataUri = ToDataUri(bytes);
                    _memoryCache[url] = dataUri;
                    return dataUri;
                }

                // Tải từ URL chính
                byte[]? downloadedBytes = await DownloadBytesSafeAsync(url);

                // Nếu URL chính lỗi, thử fallbackUrl
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

            // Nếu thất bại hoàn toàn, trả về fallbackUrl hoặc url gốc
            return fallbackUrl ?? url;
        }

        /// <summary>
        /// A9-502: Tải trước icon/ảnh chạy ngầm (fire-and-forget), không chặn luồng UI.
        /// </summary>
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
        /// Tính tổng dung lượng thư mục cache ảnh (bytes).
        /// </summary>
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
        /// Dọn dẹp toàn bộ cache RAM và thư mục ổ đĩa.
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
```

---

## 3. Chi Tiết Phần II: Tầng Giao Diện Blazor & Đăng Ký DI

### 3.1. Đăng ký DI trong `MauiProgram.cs`

Mở file [MauiProgram.cs](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/MauiProgram.cs):
> [!IMPORTANT]
> Vì `ImageService` cần tham số constructor `baseCachePath`, ta **phải dùng Factory Delegate** để truyền `FileSystem.CacheDirectory`. **Xóa bỏ** bất kỳ dòng `builder.Services.AddSingleton<ImageService>();` nào bị khai báo dư.

```csharp
// Đăng ký ImageService dạng Singleton với đường dẫn thư mục cache
builder.Services.AddSingleton<ImageService>(sp =>
{
    var http = sp.GetRequiredService<HttpClient>();
    var logger = sp.GetRequiredService<ILogger<ImageService>>();
    string cachePath = Path.Combine(FileSystem.CacheDirectory, "A9ToolImages");
    return new ImageService(http, logger, cachePath);
});
```

---

### 3.2. Tạo Component `LazyImage.razor`

Tạo file mới tại: `A9Tool/Components/Common/LazyImage.razor`  
*(Nếu chưa có thư mục `Common`, hãy tạo thư mục `A9Tool/Components/Common`)*

```razor
@using Core.Services
@inject ImageService ImageService

@if (string.IsNullOrEmpty(currentSrc))
{
    <!-- Hiển thị khung xám placeholder khi ảnh đang tải -->
    <div class="lazy-image-placeholder @Class" style="@Style"></div>
}
else
{
    <img src="@currentSrc"
         class="@Class"
         style="@Style"
         alt="@Alt"
         @attributes="AdditionalAttributes" />
}

@code {
    [Parameter] public string? Src { get; set; }
    [Parameter] public string? FallbackSrc { get; set; }
    [Parameter] public string Category { get; set; } = "general";
    [Parameter] public string? Class { get; set; }
    [Parameter] public string? Style { get; set; }
    [Parameter] public string? Alt { get; set; }

    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string? currentSrc;
    private string? lastRequestedSrc;

    protected override async Task OnParametersSetAsync()
    {
        // Tránh gọi lại khi component re-render nhưng Src không đổi
        if (Src == lastRequestedSrc && !string.IsNullOrEmpty(currentSrc))
        {
            return;
        }

        lastRequestedSrc = Src;

        if (string.IsNullOrWhiteSpace(Src))
        {
            currentSrc = FallbackSrc;
            return;
        }

        // Lấy Base64 Data URI từ ImageService (RAM -> Disk -> Tải mạng)
        currentSrc = await ImageService.GetImageAsync(Src, FallbackSrc, Category);
        StateHasChanged();
    }
}
```

---

### 3.3. Áp Dụng Vào `Operators.razor`

Mở file [Operators.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Pages/Operators.razor):
Thay thế thẻ `<img>` portrait bằng `<LazyImage />`:

```razor
<A9Tool.Components.Common.LazyImage 
    Class="op-card-img"
    Src="@($"https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/charportraits/{id}_1.png")"
    FallbackSrc="@($"https://raw.githubusercontent.com/Aceship/Arknight-Images/main/characters/{id}.png")"
    Category="avatars"
    Alt="@op.Name" />
```

---

### 3.4. Áp Dụng Vào `OperatorDetail.razor` (A9-502 & A9-503)

Mở file [OperatorDetail.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Pages/OperatorDetail.razor):
1. **Inject Service ở đầu file:**
   ```razor
   @inject Core.Services.ImageService ImageService
   ```

2. **Cache Art E0/E1/E2:**
   Khi đổi sang E0/E1/E2 hoặc lúc nạp trang, lấy ảnh qua `ImageService`:
   ```csharp
   currentBgUrl = await ImageService.GetImageAsync(remoteUrl, fallbackUrl, "characters");
   ```

3. **Prefetch Skill & Module Icons (Task A9-502):**
   Ngay trong sự kiện nạp Operator (`OnInitializedAsync` hoặc sau khi lấy xong dữ liệu Operator), kích hoạt tải trước icon ngầm để khi bấm sang tab Skill/Module icon đã có sẵn:
   ```csharp
   if (operatorData?.Skills != null)
   {
       foreach (var skill in operatorData.Skills)
       {
           string skillIconUrl = GetSkillImageUrl(skill.IconId);
           ImageService.PrefetchImage(skillIconUrl, category: "skills");
       }
   }
   ```

4. **Skill Icons & Module Icons trên giao diện:**
   Thay vì dùng `<img src="..." />`, dùng `<LazyImage />`:
   ```razor
   <A9Tool.Components.Common.LazyImage 
       Src="@GetSkillImageUrl(s.IconId)" 
       Category="skills" 
       Class="skill-icon" 
       Alt="@s.Name" />
   ```

5. **Icon tĩnh nội bộ:**
   Giữ nguyên toàn bộ các icon tĩnh `/assets/icons/...` (HP, ATK, DEF, icon Class) vì nằm sẵn trong bundle của app.

---

## 4. Chi Tiết Phần III: Chuẩn Hóa Error Handling (A9-504)

### 4.1. Sửa `Core/Services/CharacterService.cs`

Trong hàm `GetSkillsForOperatorAsync`, tránh crash `NullReferenceException` khi `skillTable` bị null:

```csharp
var skillTable = await _gameDataRepository.GetSkillTableAsync();
if (skillTable == null || op.Skills == null)
{
    _logger.LogWarning("[GetSkillsForOperatorAsync] SkillTable is null or operator has no skills.");
    return list;
}

foreach (var skill in op.Skills)
{
    if (string.IsNullOrEmpty(skill.SkillId) || !skillTable.TryGetValue(skill.SkillId, out var returnSkill))
    {
        _logger.LogWarning("Skill {SkillId} not found in skill table", skill.SkillId);
        continue;
    }
    // Xử lý nạp skill bình thường...
}
```

---

### 4.2. Sửa `Infrastructure/Github/GithubGameDataRepository .cs`

Bọc `try-catch` và fallback đọc file cache local cho `GetUniEquipTableAsync` và `GetBattleEquipTableAsync`:

```csharp
public async Task<Dictionary<string, UniEquipData>?> GetUniEquipTableAsync()
{
    if (_cachedUniEquipData != null) return _cachedUniEquipData;

    string localpath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "uniequip_table.json");

    try
    {
        var response = await LoadTableAsync<UniEquipData>(localpath, "uniequip_table", "uniequip_table.fbs");
        _logger.LogInformation("[GetUniEquipTableAsync] Loaded {Count} records.", response?.Count ?? 0);
        _cachedUniEquipData = response;
        return response;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "[GetUniEquipTableAsync] Failed to load uniequip_table. Checking local disk fallback...");
        if (File.Exists(localpath))
        {
            try
            {
                var json = await File.ReadAllTextAsync(localpath, Encoding.UTF8);
                var dict = ParseFlatBuffersJsonToDictionary<UniEquipData>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                _cachedUniEquipData = dict;
                return dict;
            }
            catch { }
        }
        return new Dictionary<string, UniEquipData>();
    }
}
```

---

## 5. 📋 Checklist Các Điểm Cần Sửa Ngay (Khắc Phục Chỗ Lệch)

- [ ] **Trong `Core/Services/ImageService.cs`:**
  - Sửa dòng 90: Đổi `Path.Combine(_baseCachePath, url)` thành `Path.Combine(_baseCachePath, category)`.
  - Sửa dòng 99: Đổi `if (!File.Exists(filePath))` thành `if (File.Exists(filePath))`.
  - Thêm phương thức `PrefetchImage(...)`, `GetCacheSizeBytesAsync()`, `ClearCacheAsync()`.
- [ ] **Trong `A9Tool/MauiProgram.cs`:**
  - Xóa bỏ dòng `builder.Services.AddSingleton<ImageService>();` bị lặp ở dòng 61 (chỉ giữ lại Factory ở dòng 52).
- [ ] **Tạo file `A9Tool/Components/Common/LazyImage.razor`** và dùng `@inject ImageService ImageService`.
- [ ] **Tích hợp vào `Operators.razor` và `OperatorDetail.razor`**.
- [ ] **Bổ sung null-check trong `CharacterService.cs` và try-catch trong `GithubGameDataRepository .cs`**.
