# Kế Hoạch Thiết Kế & Triển Khai Task A9-105: Range Table (Tầm Đánh Operator & Skill)

> **Mục tiêu Task A9-105**: Nạp dữ liệu `range_table.json`, tính toán ma trận tọa độ hiển thị lưới Tầm đánh (Attack Range Grid) trực quan theo phong cách Arknights cho cả Operator (mặc định) và Skill (khi Skill thay đổi tầm đánh).
> 
> **Lưu ý**: Tài liệu này chứa đầy đủ thông số CSS, cấu trúc HTML Skeleton và thuật toán ma trận C# để sẵn sàng triển khai code ở bước tiếp theo.

---

## 1. 📐 Nguyên lý Ma trận Tầm đánh Arknights

Trong game Arknights:
- Mỗi Tầm đánh (`RangeId`, ví dụ: `"3-1"`, `"2-1"`, `"0-1"`) chứa danh sách ô vuông tọa độ tương đối `grids` với `row` (dòng) và `col` (cột).
- Ô vị trí Operator luôn là gốc tọa độ **`[row: 0, col: 0]`** (Origin).
- **Hướng quay mặt**: Mặc định Operator quay mặt sang bên **phải** (`col > 0` là phía trước Operator, `row < 0` là bên trên, `row > 0` là bên dưới).

### Cấu trúc JSON `range_table.json`:
```json
{
  "3-1": {
    "id": "3-1",
    "grids": [
      { "row": 0, "col": 0 },
      { "row": 0, "col": 1 },
      { "row": 0, "col": 2 },
      { "row": -1, "col": 1 },
      { "row": 1, "col": 1 }
    ]
  }
}
```

---

## 2. 🎨 CSS Design System (`app.css`)

Dưới đây là toàn bộ bộ CSS định hình giao diện **Attack Range Grid**:

```css
/* ============================================================
   ATTACK RANGE GRID (Task A9-105)
   ============================================================ */

/* Khung bọc tổng thể của Tầm đánh */
.od-attack-range {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 10px 12px;
    background: rgba(10, 10, 15, 0.65);
    backdrop-filter: blur(10px);
    border: 1px solid rgba(255, 255, 255, 0.08);
    border-radius: 6px;
    width: fit-content;
    box-shadow: 0 4px 16px rgba(0, 0, 0, 0.4);
}

/* Header tiêu đề của Attack Range */
.od-range-header {
    display: flex;
    align-items: center;
    justify-content: space-between;
    gap: 12px;
    font-size: 0.68rem;
    font-weight: 800;
    color: #888;
    letter-spacing: 0.8px;
    text-transform: uppercase;
}

.od-range-title {
    display: flex;
    align-items: center;
    gap: 4px;
    color: #aaa;
}

.od-range-id-badge {
    font-size: 0.62rem;
    font-weight: 700;
    padding: 1px 6px;
    border-radius: 3px;
    background: rgba(255, 255, 255, 0.08);
    color: #60a5fa;
    border: 1px solid rgba(96, 165, 250, 0.2);
}

/* Lưới hiển thị các ô tầm đánh (CSS Grid động) */
.od-range-grid-container {
    display: inline-grid;
    gap: 3px;
    padding: 6px;
    background: rgba(0, 0, 0, 0.5);
    border-radius: 4px;
    border: 1px solid rgba(255, 255, 255, 0.05);
    align-self: flex-start;
}

/* Kiểu dáng từng ô vuông (Tile) */
.od-range-tile {
    width: 16px;
    height: 16px;
    border-radius: 2px;
    background: transparent;
    border: 1px solid transparent;
    transition: all 0.2s ease;
}

/* Ô nằm ngoài tầm đánh (trống) */
.od-range-tile.empty {
    background: rgba(255, 255, 255, 0.02);
    border-color: rgba(255, 255, 255, 0.03);
}

/* Ô trong tầm đánh (Attack range cell) */
.od-range-tile.active {
    background: rgba(255, 255, 255, 0.85);
    border-color: #ffffff;
    box-shadow: 0 0 6px rgba(255, 255, 255, 0.4);
}

/* Ô vị trí đứng của Operator (Origin cell [0,0]) */
.od-range-tile.origin {
    background: #09090b;
    border: 2px solid #60a5fa;
    box-shadow: 0 0 8px rgba(96, 165, 250, 0.6);
    position: relative;
}

/* Chấm xanh trung tâm ô Operator */
.od-range-tile.origin::after {
    content: '';
    position: absolute;
    top: 50%;
    left: 50%;
    transform: translate(-50%, -50%);
    width: 6px;
    height: 6px;
    background: #60a5fa;
    border-radius: 1px;
}
```

---

## 3. 📄 HTML Skeleton Component

Cấu trúc Blazor HTML để nhúng vào trang `OperatorDetail.razor` (có thể đặt ở Left Panel hoặc cạnh tên Operator ở Center Panel):

```html
<!-- ============================================================
     ATTACK RANGE COMPONENT (Task A9-105)
     ============================================================ -->
@if (activeRangeData != null)
{
    <div class="od-attack-range">
        <!-- Header tiêu đề -->
        <div class="od-range-header">
            <span class="od-range-title">🎯 Range</span>
            <span class="od-range-id-badge">@activeRangeData.Id</span>
        </div>

        <!-- Lưới ma trận các ô (Dynamic CSS Grid Columns/Rows) -->
        <div class="od-range-grid-container"
             style="grid-template-columns: repeat(@activeRangeMatrix.ColumnCount, 16px);
                    grid-template-rows: repeat(@activeRangeMatrix.RowCount, 16px);">

            @for (int r = 0; r < activeRangeMatrix.RowCount; r++)
            {
                @for (int c = 0; c < activeRangeMatrix.ColumnCount; c++)
                {
                    var cellType = activeRangeMatrix.GetCellType(r, c);
                    <div class="od-range-tile @cellType"
                         title="[@activeRangeMatrix.GetRowCoord(r), @activeRangeMatrix.GetColCoord(c)]">
                    </div>
                }
            }
        </div>
    </div>
}
```

---

## 4. ⚙️ Thuật Toán & Cấu Trúc Data C# (Planning)

### 1. DTO Model `RangeData.cs`
```csharp
namespace Core.Models.Entities
{
    public class RangeData
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }

        [JsonPropertyName("grids")]
        public List<RangeGridCell> Grids { get; set; } = new();
    }

    public class RangeGridCell
    {
        [JsonPropertyName("row")]
        public int Row { get; set; }

        [JsonPropertyName("col")]
        public int Col { get; set; }
    }
}
```

### 2. Class Tính Toán Ma Trận `RangeMatrixCalculator`
Để biến danh sách ô tọa độ tương đối thành ma trận hình chữ nhật 2D (Rows x Cols) cho CSS Grid:

```csharp
public class RangeMatrix
{
    public int MinRow { get; set; }
    public int MaxRow { get; set; }
    public int MinCol { get; set; }
    public int MaxCol { get; set; }

    public int RowCount => MaxRow - MinRow + 1;
    public int ColumnCount => MaxCol - MinCol + 1;

    private HashSet<(int row, int col)> activeGrids = new();

    public static RangeMatrix Create(RangeData data)
    {
        var matrix = new RangeMatrix();
        
        // Luôn đảm bảo ô gốc (0,0) nằm trong Bounding Box
        int minR = 0, maxR = 0, minC = 0, maxC = 0;
        
        foreach (var cell in data.Grids)
        {
            matrix.activeGrids.Add((cell.Row, cell.Col));
            if (cell.Row < minR) minR = cell.Row;
            if (cell.Row > maxR) maxR = cell.Row;
            if (cell.Col < minC) minC = cell.Col;
            if (cell.Col > maxC) maxC = cell.Col;
        }

        matrix.MinRow = minR;
        matrix.MaxRow = maxR;
        matrix.MinCol = minC;
        matrix.MaxCol = maxC;

        return matrix;
    }

    public string GetCellType(int gridRowIndex, int gridColIndex)
    {
        int actualRow = MinRow + gridRowIndex;
        int actualCol = MinCol + gridColIndex;

        if (actualRow == 0 && actualCol == 0)
            return "origin";

        if (activeGrids.Contains((actualRow, actualCol)))
            return "active";

        return "empty";
    }

    public int GetRowCoord(int gridRowIndex) => MinRow + gridRowIndex;
    public int GetColCoord(int gridColIndex) => MinCol + gridColIndex;
}
```

---

## 5. 🔄 Luồng Tương Tác & Chuyển Đổi Range (Operator vs Skill)

1. **Mặc định**: Khi xem thông tin Operator ở Elite hiện tại, hiển thị Range mặc định của Phase (`op.Phases[elite].RangeId`).
2. **Khi chọn Skill**:
   - Nếu `skillLevel.RangeId` khác `null` hoặc khác rỗng: tự động chuyển Attack Range Grid hiển thị theo `rangeId` riêng của Skill đó.
   - Nếu `skillLevel.RangeId` bằng `null`: giữ nguyên Range mặc định của Operator.

---

## 6. 📝 Các Bước Tiến Hành Khi Bắt Đầu Task A9-105

- [ ] **Bước 1**: Tạo DTO `RangeData.cs` trong `Core/Models/Entities`.
- [ ] **Bước 2**: Thêm hàm `GetRangeTableAsync()` trong `IGameDataRepository` và `CharacterService` để fetch `range_table.json` từ URL:
  `https://raw.githubusercontent.com/ArknightsAssets/ArknightsGamedata/refs/heads/master/en/gamedata/excel/range_table.json`
  *(Hoặc dự phòng CN: `https://raw.githubusercontent.com/Kengxxiao/ArknightsGameData/master/zh_CN/gamedata/excel/range_table.json`)*
- [ ] **Bước 3**: Thêm các lớp CSS `.od-attack-range`, `.od-range-tile` vào `app.css`.
- [ ] **Bước 4**: Nhúng HTML Component Range Grid vào [OperatorDetail.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Pages/OperatorDetail.razor).
- [ ] **Bước 5**: Thêm logic cập nhật Range linh hoạt khi chuyển đổi Elite hoặc chọn Skill.
