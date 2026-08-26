# Kế Hoạch & Tài Liệu Kỹ Thuật: Chuyển Đổi Sang Repo ArknightsAssets2 (FlatBuffers Binary)

> **Mục tiêu**: Chuyển đổi toàn bộ nguồn dữ liệu Game Data từ repository cũ (`ArknightsGamedata` - đã ngưng cập nhật) sang repository mới (`ArknightsAssets2` - nhánh `en`).
> 
> **Thách thức chính**: 
> 1. Repo mới không còn lưu dạng JSON trực tiếp mà lưu định dạng FlatBuffers Binary (`.bytes`).
> 2. Tên file có đuôi hash ngẫu nhiên (ví dụ `character_tabled88efb.bytes`).
> 3. File binary có chứa 128-byte RSA signature header cần loại bỏ trước khi parse.
> 4. GitHub REST API có giới hạn `HTTP 403 Rate Limit Exceeded` (60 req/giờ).

---

## 1. 🏗 Kiến Trúc Luồng Dữ Liệu & Bộ Nhớ Cache (Multi-Tier Architecture)

```
[UI Components (Operators.razor, OperatorDetail.razor)]
                       │
                       ▼
[CharacterService / IGameDataRepository]
                       │
          ┌────────────┴────────────┐
          ▼                         ▼
   [Tầng 1: RAM Cache]     [Tầng 2: Disk Cache JSON]
   (Lưu Dictionary)        (character_table.json, ...)
   -> Tốc độ 0ms           -> Tốc độ ~1ms
          │                         │
          └────────────┬────────────┘
                       │ (Cache Miss / Expired > 1 ngày)
                       ▼
 [Tầng 3: GitHub Contents API Directory Cache]
 (github_excel_contents_cache.json -> Tránh lỗi 403 Rate Limit)
                       │
                       ▼
  [Download Binary .bytes Raw -> Strip 128-byte Header]
                       │
                       ▼
    [flatc Process (D:\flatbuffers\flatc.exe) + FBS Schema]
                       │
                       ▼
   [Parse JSON Wrapper -> Chuẩn hóa Dictionary<string, T>]
```

---

## 2. 📁 Chi Tiết Các Thay Đổi Code (File Changes)

### 1. Schema & FlatBuffers Code
- **`Infrastructure/FlatBuffers/Schemas/character_table.fbs`**: Schema lấy từ OpenArknightsFBS.
- **`Infrastructure/FlatBuffers/Schemas/skill_table.fbs`**: Schema lấy từ OpenArknightsFBS.
- **`Infrastructure/FlatBuffers/Schemas/range_table.fbs`**: Schema tùy chỉnh ma trận phạm vi tấn công.
- **`Infrastructure/FlatBuffers/Generated/*.cs`**: C# Accessor Structs được generate từ `flatc`.

### 2. Service Chuyển Đổi FlatBuffers
- **`Infrastructure/FlatBuffers/FlatBuffersConverter.cs`**:
  - `EnsureHeaderStripped(byte[])`: Tự động nhận diện và bóc tách 128-byte RSA header.
  - `FindFlatcExecutablePath()`: Tự động quét và lấy đường dẫn thực thi của `flatc.exe` trong máy (App Base Directory, PATH, `D:\flatbuffers\flatc.exe`,...).
  - `ConvertToJsonAsync()`: Thực thi lệnh `flatc --json --raw-binary` chuyển file `.bytes` thành JSON.

### 3. Repository Quản Lý Dữ Liệu
- **`Infrastructure/Github/GithubGameDataRepository.cs`**:
  - Tích hợp comment lưu vết các URL repo JSON cũ (`ArknightsGamedata`, `Kengxxiao`).
  - `GetExcelDirectoryContentsAsync()`: Cache danh sách file từ GitHub API (RAM 30 phút, Disk 1 giờ) để không bao giờ bị chặn 403.
  - `ParseFlatBuffersJsonToDictionary<T>()`: Tự động nhận diện cấu trúc wrapper `{ "characters": [...] }` từ `flatc` và chuyển đổi thành `Dictionary<string, T>` chuẩn.
  - `GetRangeTableAsync()`: Đọc trực tiếp file ma trận tầm đánh local `range_table.json` (72 entries) mà không cần gọi qua mạng.

### 4. Sửa Lỗi Core Models & Services
- **`Core/Services/CharacterService.cs`**:
  - Sửa lỗi crash `InvalidOperationException` ở hàm `ParseBlackboard` khi gặp thuộc tính null `bb.Value` (giúp hiển thị mượt mà nhân vật Rope `char_236_rope` và các operator khác).

---

## 3. 📜 Ý Nghĩa Chi Tiết Các Thẻ Logger (Log Tag Documentation)

Khi bật ứng dụng hoặc xem trong cửa sổ Output / Console Log:

| Thẻ Log | Ý nghĩa & Luồng xử lý |
| :--- | :--- |
| **`[Cache Hit]`** | Đọc dữ liệu JSON chuẩn trực tiếp từ đĩa cứng local (thời gian nạp ~1ms). |
| **`[Cache Miss/Expired]`** | File JSON local chưa có hoặc đã quá 1 ngày -> Bắt đầu kết nối GitHub lấy bản `.bytes` mới nhất. |
| **`[GitHub API]`** | Gọi GitHub API để quét danh sách các file `.bytes` hiện có trên server. |
| **`[GitHub API Cache Hit]`** | Sử dụng lại danh sách file đã lưu tạm trên RAM/Disk (tránh gọi API thừa gây dính lỗi 403). |
| **`[Download]`** | Đang tải file FlatBuffers binary (`.bytes`) từ GitHub về máy. |
| **`[FlatBuffers]`** | Đang loại bỏ 128-byte header và gọi `flatc.exe` giải mã `.bytes` sang JSON. |
| **`[flatc]`** | Vị trí thực thi của công cụ `flatc.exe` tìm thấy trên hệ thống. |
| **`[Success]`** | Nạp và chuẩn hóa dữ liệu vào C# models hoàn tất. |
| **`[Fallback]`** | Tự động đọc dữ liệu offline local khi bị rớt mạng hoặc gặp sự cố tải file (giúp app luôn chạy ổn định). |
| **`[Network Error]` / `[Error]`** | Thông báo lỗi khi không thể kết nối mạng hoặc lỗi phát sinh trong quá trình xử lý. |

---

## 4. 📌 Đánh Giá Trạng Thái & Kiểm Thứ (Status & Verification)

- [x] Tích hợp repository mới `ArknightsAssets2` nhánh `en`.
- [x] Tự động giải mã FlatBuffers `.bytes` bằng `flatc`.
- [x] Khắc phục lỗi `403 Rate Limit` từ GitHub API bằng Directory Cache.
- [x] Khắc phục lỗi crash Nullable ở `CharacterService.cs`.
- [x] Đã kiểm tra build solution thành công (`0 Error(s)`).
