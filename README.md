# 📑 GIỚI THIỆU DỰ ÁN & LỘ TRÌNH PHÁT TRIỂN A9TOOL

> [!TIP]
> **Cách cập nhật bảng kế hoạch này:**
> 1. **Tích chọn:** Bạn chỉ cần đổi `[ ]` thành `[x]` (và ngược lại) ở cột **Tích** để đánh dấu hoàn thành.
> 2. **Trạng thái:** Bạn có thể sửa trực tiếp cột **Trạng thái** thành: `ToDo` ➡️ `In Progress` ➡️ `Done` (hoặc `Backlog` cho các tính năng dài hạn).
> 3. Bạn có thể mở file này trực tiếp trong Visual Studio / VS Code để chỉnh sửa bằng tay, hoặc yêu cầu AI cập nhật hộ mỗi khi hoàn thành xong một task.

---

## 1. Giới thiệu Dự án
* **Tên dự án:** A9Tool (Arknights Helper Tool)
* **Công nghệ:** .NET MAUI + Blazor Hybrid (.NET 8/9, C#)
* **Mục tiêu:** Xây dựng một **Wiki** và **bộ công cụ tính toán (calculation tools)** hoàn chỉnh dành cho người chơi Arknights, hoạt động đa nền tảng (Windows/Mobile/Desktop) tương tự như các web-tools nổi tiếng (Aceship, Gamepress, PRTS).

---

## 2. Cấu trúc Thư mục Dự án

```
A9Tools/
├── A9Tool.slnx                     # Cấu hình Solution tổng thể
├── Core/                           # Tầng nghiệp vụ chính (Domain & Business Logic)
│   ├── DTOs/                       # DTOs truyền tải dữ liệu
│   ├── Interfaces/                 # Interfaces (IGameDataRepository...)
│   ├── Models/                     # Entity mô tả dữ liệu game (Character, Stats, Skill...)
│   └── Services/                   # CharacterService...
├── Infrastructure/                 # Tầng hạ tầng (Data Fetching & Storage)
│   └── Github/                     # Gọi API và quản lý cache offline từ GitHub GameData
├── A9Tool/                         # Tầng Giao diện hiển thị (Blazor Hybrid Client App)
│   ├── Components/                 # Components tái sử dụng
│   │   └── Pages/                  # Pages chính (Home, Operators, OperatorDetail)
│   └── wwwroot/                    # CSS, JS, Assets tĩnh
└── A9Tool.API/                     # [Kế hoạch Epic 10] Backend ASP.NET Core Web API & Sync Server
    ├── Controllers/                # AuthController (JWT), RosterSyncController...
    ├── Data/                       # AppDbContext (EF Core + SQL Server / PostgreSQL)
    ├── Services/                   # AuthService, RosterCloudService...
    └── Dockerfile                  # Containerize API Server & Database
```

---

## 3. Kế hoạch Chi tiết

---

### PHẦN I: TÍNH NĂNG CORE (CORE FUNCTIONALITIES)

#### 📋 Epic 0: Các tính năng đã hoàn thành (Done)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-001 | **Tìm kiếm & Bộ lọc Operator:** Tìm kiếm theo tên và lọc theo Rarity (1★ - 6★) hoặc Class. | - | 🔥 Rất cao | `Done` |
| [x] | A9-002 | **Trang chi tiết Operator cơ bản:** Hiển thị tên, mã số, mô tả, sub-class, position và danh sách tiềm năng (Potential 1 - 6). | - | 🔥 Rất cao | `Done` |
| [x] | A9-003 | **Hiển thị chỉ số động theo Elite:** Tự động đổi chỉ số HP, ATK, DEF, RES, Cost, Block, Redeploy khi chuyển E0/E1/E2. | - | 🔥 Rất cao | `Done` |
| [x] | A9-004 | **Thiên phú & Chỉ số tin cậy:** Hiển thị thiên phú mở khóa theo Elite và chỉ số cộng thêm từ điểm tin cậy. | - | 🔥 Rất cao | `Done` |
| [x] | A9-005 | **Lưu cache offline dữ liệu JSON:** Tự động lưu cache offline các file JSON trong 24 giờ. | - | ⚡ Cao | `Done` |

#### 📋 Epic 1: Hoàn thiện Wiki Skills (Thời gian ước lượng: ~6h còn lại)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-101 | **Tải và Parse dữ liệu `skill_table.json`:** Đã triển khai hàm `GetSkillTableAsync()` trong repo. | 2h | 🔥 Rất cao | `Done` |
| [x] | A9-102 | **Ánh xạ Skill vào Character Detail:** Viết logic trong `CharacterService` để lấy chi tiết kỹ năng dựa trên danh sách `skillId` của Operator. | 2h | 🔥 Rất cao | `Done` |
| [x] | A9-103 | **Giao diện hiển thị danh sách kỹ năng:** Hiển thị tên, icon, loại SP (Auto/Manual/Passive) và cơ chế sạc trong trang chi tiết. | 2h | 🔥 Rất cao | `Done` |
| [x] | A9-104 | **Slider/Dropdown tương tác cấp độ kỹ năng:** Thiết kế slider Level 1 -> Mastery 3 để cập nhật động mô tả kỹ năng và chỉ số SP. | 2h | 🔥 Rất cao | `Done` |
| [x] | A9-105 | **Tích hợp Range Table cho Operator & Skill:** Parse `range_table.json`, hiển thị lưới tầm đánh mặc định của Operator và tầm đánh riêng của Skill (nếu có `rangeId` khác null). | 2.5h | 🛠️ Trung bình | `Done` |

#### 📋 Epic 2: Hiển thị Hệ thống Module (Thời gian ước lượng: ~6h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-201 | **Nạp dữ liệu Module từ GitHub:** Tải và parse dữ liệu module từ `uniequip_table.json` và `battle_equip_table.json`. | 2h | ⚡ Cao | `Done` |
| [x] | A9-202 | **Giao diện chi tiết Module:** Hiển thị icon module, tên module, điều kiện mở khóa. | 2h | ⚡ Cao | `Done` |
| [x] | A9-203 | **Tương tác cấp độ Module (Stage 1/2/3):** Cho phép click chọn Stage 1/2/3 để xem các chỉ số cộng thêm động và sự thay đổi Trait/Talent. | 2h | ⚡ Cao | `Done` |
| [x] | A9-204 | **Nhiệm vụ mở khóa Module (Module Missions):** Trích xuất `missionList` từ `uniequip_table`, format mô tả nhiệm vụ và hiển thị thẻ nhiệm vụ trực quan kèm Stage badge. | 2h | ⚡ Cao | `Done` |

#### 📋 Epic 3: Bộ tính Tag Tuyển chọn - Recruitment Calculator (Thời gian ước lượng: ~5h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-301 | **Thuật toán lọc tổ hợp Tag:** Viết thuật toán tổ hợp $C(n, k)$ nhận đầu vào là các Tag được chọn và trả về các Operator thỏa mãn (áp dụng luật 6★ Top Operator). | 2h | ⚡ Cao | `Done` |
| [x] | A9-302 | **Xác định tổ hợp cam kết & tính xác suất (Guaranteed & Probability):** Đánh dấu các tổ hợp bảo đảm 6★, 5★, 4★+, tính tỉ lệ 100% Độc quyền (Sole Target) và tỉ lệ % cày 4★ thuần. | 1.5h | ⚡ Cao | `Done` |
| [x] | A9-303 | **Giao diện ma trận Tag & Toolbar tính toán:** Thiết kế bảng chọn 28 Tag trực quan, thanh lọc tab nhanh, dropdown sắp xếp xác suất, ô tìm kiếm Săn tướng (Target Hunter) và hiển thị avatar icon nhỏ chuẩn Aceship. | 1.5h | ⚡ Cao | `Done` |

#### 📋 Epic 4: Bảng tính Nguyên liệu - Elite & Mastery Planner (Thời gian ước lượng: ~10h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-401 | **Cấu trúc hóa nguyên liệu nâng cấp:** Parse thông tin chi tiết nguyên liệu nâng cấp (độ hiếm, tên, icon) từ `item_table.json`, tạo component `<ItemBadge.razor />`. | 3h | 🛠️ Trung bình | `Done` |
| [x] | A9-402 | **Bộ tính chi phí Leveling & Thăng cấp từng tướng (Modal):** Tích hợp `LevelingCostService`, popup tính LMD, EXP và danh sách nguyên liệu thăng cấp E0/E1/E2 trong Operator Detail. | 4h | 🛠️ Trung bình | `Done` |
| [x] | A9-403 | **Giao diện lập kế hoạch Planner tổng hợp (Multi-Operator Planner):** Trang `/calculator` với bộ tính tài nguyên nâng cấp Operator và Spark Planner tích lũy Orundum/Originite Prime. | 3h | 🛠️ Trung bình | `Done` |

#### 📋 Epic 4.5: Hồ sơ Nhân vật & Đa phương tiện - Character Biography & Media Hub (Thời gian ước lượng: ~10h)

> [!NOTE]
> **Thứ tự ưu tiên triển khai:** Nút chuyển tab `Bio` đã có sẵn placeholder tại [OperatorDetail.razor](file:///d:/VS_Studio/A9Tools/A9Tool/A9Tool/Components/Pages/OperatorDetail.razor#L38).

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-451 | **Trang phục & Trình xem Ảnh (Skins & Art Gallery):** Parse dữ liệu từ `skin_table.json`, hiển thị danh sách trang phục theo thương hiệu (Coral Coast, EPOQUE...), xem tranh minh họa đầy đủ (Splash Art, Chibi, Live2D). | 3.5h | 🎨 Thẩm mỹ | `ToDo` |
| [ ] | A9-452 | **Lồng tiếng & Lời thoại (Voice Lines & Audio Player):** Nạp `charword_table.json`, hỗ trợ đa ngôn ngữ lồng tiếng (CV JP/EN/CN/KR), tích hợp trình phát audio thoại trực tuyến và hiển thị transcript lời thoại. | 3.5h | 🎧 Âm thanh | `ToDo` |
| [ ] | A9-453 | **Hồ sơ Cá nhân & Cốt truyện (Handbook & Archive Stories):** Nạp `handbook_info_table.json`, hiển thị chi tiết kiểm tra thể chất (Physical Exam), bệnh án Oripathy (Clinical Analysis), 4 tệp hồ sơ lưu trữ và ghi chép thăng cấp (Promotion Record). | 3h | 📖 Cốt truyện | `ToDo` |

#### 📋 Epic 4.8: Cây Cốt Truyện & Trình Đọc Visual Novel - Storyline Tree & Story Reader (Thời gian ước lượng: ~12h)

> [!TIP]
> **Nguồn dữ liệu:** Khai thác từ `story_review_table.json`, `zone_table.json` và kho kịch bản thoại text Arknights (ArknightsStoryJson / Kengxxiao).

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-481 | **Cây Niên Biểu & Bản Đồ Dòng Thời Gian (Storyline Timeline Tree):** Phân chia cốt truyện theo các chương: Main Theme (Chương 0 - 14), Intermezzi (Darknights Memoir, Under Tides...), Side Story (Near Light, Il Siracusano...) và Story Collection. Thiết kế cây sơ đồ tương tác (Interactive Timeline Tree) theo chuẩn phong cách Terra. | 4h | 📖 Cốt truyện | `Done` |
| [x] | A9-482 | **Trích xuất & Xử lý Kịch bản Hội thoại:** Parse dữ liệu script đối thoại (tên nhân vật, avatar cảm xúc, đoạn thoại, câu dẫn người dẫn chuyện, phông nền Background CG và hiệu ứng âm thanh/nhạc nền BGM). | 3.5h | ⚙️ Kỹ thuật | `Done` |
| [x] | A9-483 | **Trình Đọc Visual Novel & Light Novel (Dual-Mode Story Reader):** Giao diện đọc truyện toàn màn hình chuẩn phong cách game: đổi chế độ 1-click giữa VN và LN cuộn mượt, portrait đổi biểu cảm, nền CG, tự động stream BGM Monster Siren, hiệu ứng rung CameraShake, Auto-play, và xem lại Log lịch sử. | 4.5h | 🌟 Đỉnh cao | `Done` |

#### 📋 Epic 4.9: Hệ thống Giao diện PRTS & Cài đặt - PRTS Terminal UI & Settings (Thời gian ước lượng: ~8h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-491 | **App Shell & Header Điều Hướng PRTS Terminal:** Thiết kế component `<PrtsTerminalHeader />` sắc cạnh, vạch đo tọa độ, nút Back nhanh và thanh Breadcrumb phong cách hệ thống tác chiến Rhodes Island. | 2.5h | ⚡ Cao | `Done` |
| [x] | A9-492 | **Màn hình Tải Dữ liệu Chống Đơ (Loading Overlay):** Xây dựng component `<PrtsLoadingOverlay />` với Rhombus Spinner vàng kim xoay 360°, thanh Laser quét % tiến độ và Faint Tactical Console Logger. | 2.5h | ⚡ Cao | `Done` |
| [x] | A9-493 | **Trang Cài đặt Hệ thống & Bộ chọn Theme (`Settings.razor`):** Hỗ trợ đổi Theme Canon Light, Night Dark, Sui's Lotus (Hồng hoa sen), công tắc âm thanh ambient và công cụ xóa bộ nhớ đệm Cache. | 3h | ⚡ Cao | `Done` |

---

### PHẦN II: TỐI ƯU HÓA TRƯỚC KHI RELEASE (RELEASE OPTIMIZATIONS)

#### 📋 Epic 5: Tải và Quản lý Tài nguyên Offline (Thời gian ước lượng: ~4.5h còn lại)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [x] | A9-501 | **Background Downloader cho Avatar:** Đã có hàm tải avatar lưu offline trong Repo. | - | ⚡ Cao | `Done` |
| [x] | A9-502 | **Tải offline Icons (Skill & Module):** Viết thêm hàm tải các icon skill và module về lưu trữ local. | 3h | ⚡ Cao | `Done` |
| [x] | A9-503 | **Cơ chế fallback hình ảnh thông minh:** Ưu tiên đọc ảnh local, nếu không có mới gọi online và tự động lưu lại (lazy cache). | 1.5h | ⚡ Cao | `Done` |
| [x] | A9-504 | **Chuẩn hóa Error Handling cho GameDataRepository:** Áp dụng try-catch/TryGetValue nhất quán khi tra cứu dữ liệu (skill, item, module...), tránh crash khi thiếu key hoặc field null; log cảnh báo thay vì throw exception làm sập luồng hiển thị. | 2h | ⚡ Cao | `Done` |

#### 📋 Epic 6: Tối ưu bộ nhớ & Tốc độ khởi động (Thời gian ước lượng: ~6h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-601 | **Tối ưu dung lượng file Cache JSON:** Loại bỏ các trường dữ liệu thừa trong JSON trước khi lưu cache để giảm dung lượng file. | 2h | 🛠️ Trung bình | `ToDo` |
| [ ] | A9-602 | **Tải lười dữ liệu (Lazy Loading):** Chỉ load danh sách tóm tắt lúc khởi động app, click vào Operator nào mới load chi tiết của Operator đó. | 2h | 🛠️ Trung bình | `ToDo` |
| [ ] | A9-603 | **Chỉ mục tìm kiếm (Indexing):** Tăng tốc tìm kiếm bằng cách tạo chỉ mục sẵn thay vì duyệt qua danh sách thô trên giao diện. | 2h | 🛠️ Trung bình | `ToDo` |

#### 📋 Epic 6.1: Rà soát & Sửa Lại Chức Năng AI Chưa Đạt (Code Audit & AI Refinement - Thời gian ước lượng: ~6h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-611 | **Xử lý triệt để Cảnh báo Build & Null Reference:** Khắc phục toàn bộ các cảnh báo biên dịch CS8602, CS8604, CS8601 (dereference of a possibly null reference) trong `OperatorDetail.razor` và `Calculator.razor` do AI sinh code tự động. | 2h | 🔥 Rất cao | `ToDo` |
| [ ] | A9-612 | **Chuẩn hóa Logic Tính toán & State Binding:** Rà soát lại logic tính chỉ số động theo cấp độ, đồng bộ Potential/Elite/Module không để bị lệch công thức game; sửa các biến thừa chưa dùng (`showLightbox`...). | 2h | ⚡ Cao | `ToDo` |
| [ ] | A9-613 | **Tối ưu UX & Responsive Layout:** Khắc phục tình trạng tràn chữ, co giật layout hoặc nút bấm quá nhỏ trên màn hình bé; kiểm tra luồng điều hướng giữa các trang không bị delay hay đơ giao diện. | 2h | ⚡ Cao | `ToDo` |

#### 📋 Epic 6.2: Vẽ Lại CSS & Tích Hợp Asset Game Gốc (Game Asset & CSS Remaster - Thời gian ước lượng: ~8h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-621 | **Thay thế Khung Thẻ & Đèn Độ Hiếm Bằng Asset Gốc:** Sử dụng trực tiếp sprite từ `ArknightsAssets2` (`sprite_rartylight_1..6.png`, `sprite_upperhub_1..6.png`, `sprite_star_1..6.png`) thay thế cho hiệu ứng border CSS tự vẽ thô sơ. | 2.5h | 🎨 Thẩm mỹ | `ToDo` |
| [ ] | A9-622 | **Tích hợp Bộ Icon Chỉ Số & Nút Bấm In-game:** Áp dụng bộ sprite in-game chính thức cho các chỉ số ATK, DEF, HP, RES, Block, Cost (`panel_chara_one/icon_*.png`) và nút quay lại (`image_btn_back.png`). | 2h | 🎨 Thẩm mỹ | `ToDo` |
| [ ] | A9-623 | **Watermark Mờ Phe Phái & Biểu Tượng Class Khổng Lồ:** Đưa Logo Tháp Rhodes Island (`logo_rhodes.png`), 44 Camp Logos (`camplogo/logo_*.png`) và Profession large icons (`icon_*_large.png`) in chìm mờ (`opacity: 0.05 - 0.08`) làm nền canvas trang chi tiết. | 2h | 🎨 Thẩm mỹ | `ToDo` |
| [ ] | A9-624 | **Dọn dẹp & Tinh gọn `app.css`:** Tái cấu trúc file CSS, loại bỏ các rule thừa trùng lặp, áp dụng triệt để Design Tokens chuẩn `--prts-*`, tuân thủ nghiêm ngặt `border-radius: 0px` và font số cố định `tabular-nums`. | 1.5h | 🛠️ Trung bình | `ToDo` |

---

### PHẦN III: TÍNH NĂNG MỞ RỘNG THEO MOOD (INSPIRATIONAL BACKLOG)

#### 📋 Epic 7: Trình phát nhạc - Music Player (Thời gian ước lượng: ~8h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-701 | **Kết nối dữ liệu Monster Siren Records:** Gọi API hoặc cào danh sách album/nhạc từ trang chủ phát hành nhạc của game. | 3h | 🎵 Mood (Cao) | `Backlog` |
| [ ] | A9-702 | **Xây dựng trình phát nhạc Blazor:** Giao diện phát nhạc trực tiếp link âm thanh (Play, Pause, Volume, Seekbar). | 5h | 🎵 Mood (Cao) | `Backlog` |

#### 📋 Epic 8: Mô phỏng Gacha - Gacha Simulator (Thời gian ước lượng: ~6h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-801 | **Thuật toán giả lập Gacha:** Giả lập tỉ lệ ban đầu và cơ chế tăng tỉ lệ tích lũy (Pity) sau roll 50 của banner game. | 3h | 🎲 Mood (Cao) | `Backlog` |
| [ ] | A9-802 | **Bảng thống kê lịch sử & độ may mắn:** Lưu lại lịch sử quay giả lập và vẽ biểu đồ trực quan độ may mắn của người chơi. | 3h | 🎲 Mood (Cao) | `Backlog` |

#### 📋 Epic 9: Công cụ bổ trợ nâng cao (Thời gian ước lượng: ~16h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-901 | **So sánh Operator (Compare Tool):** Đặt song song các Operator để đối chiếu chỉ số và kỹ năng. | 4h | 💤 Thấp | `Backlog` |
| [ ] | A9-902 | **Xây dựng Đội hình - Squad Builder:** Tạo đội hình 12 nhân vật, đưa ra các đánh giá phân bổ class và gợi ý synergy. | 6h | 💤 Thấp | `Backlog` |
| [ ] | A9-903 | **Trình tối ưu map farm:** Tích hợp API tỉ lệ rơi đồ cộng đồng từ Penguin Stats để gợi ý map tối ưu nhất. | 6h | 💤 Thấp | `Backlog` |

---

### PHẦN IV: HỆ THỐNG CÁ NHÂN HÓA & ĐỒNG BỘ TÀI KHOẢN (USER ROSTER & ACCOUNT SYNC)

#### 📋 Epic 10: Quản lý Roster & Đồng bộ Tài khoản (Krooster System & Web API Backend - Thời gian ước lượng: ~35h)

| Tích | Mã Task | Tên & Chi tiết Task | Thời gian | Độ quan trọng | Trạng thái |
| :---: | :---: | :--- | :---: | :---: | :---: |
| [ ] | A9-1001 | **(Cấp 1) Quản lý Roster thủ công & Lưu Local:** Tạo UI chọn nhanh Operator sở hữu (Level, Elite 0/1/2, Skill M1-M3, Module). Lưu trữ dữ liệu vào SQLite/JSON local. | 8h | ⚡ Cao | `Backlog` |
| [ ] | A9-1002 | **(Cấp 2) Import / Export chuẩn JSON Krooster & MAA:** Đọc và xuất dữ liệu Roster chuẩn file `.json` tương thích 100% với Krooster và MeoArknightsAssistant. | 5h | ⚡ Cao | `Backlog` |
| [ ] | A9-1003 | **Xây dựng Backend ASP.NET Core Web API (`A9Tool.API`):** Tạo service Backend quản lý User & Cloud Sync Roster. Tích hợp JWT Auth (`/api/v1/auth/login`), EF Core + SQL Server/PostgreSQL, Swagger OpenAPI & đóng gói Docker Container. | 12h | ⚡ Cao | `Backlog` |
| [ ] | A9-1004 | **(Cấp 3) Tự động Kéo dữ liệu từ Server Yostar/Hypergryph:** Kết nối Auth Token game, giải mã Protobuf để tự động kéo Roster + Kho nguyên liệu thực tế về app. | 10h | 🛠️ Nâng cao | `Backlog` |
| [ ] | A9-1005 | **Tích hợp Roster cá nhân vào Material Planner:** Tự động lấy Roster đang có ở Epic 10 đưa vào Bảng tính nguyên liệu (Epic 4) để trừ ra số lượng item cần farm thực tế. | 5h | ⚡ Cao | `Backlog` |

---

## 4. Tài liệu Tham khảo & Schema Cần dùng (References & Schemas)

1. **Dữ liệu Game & Repo đang sử dụng trực tiếp trong Codebase:**
   * **ArknightsAssets / ArknightsGamedata:** [GitHub Repo](https://github.com/ArknightsAssets/ArknightsGamedata) *(Nguồn JSON `character_table.json`, `skill_table.json`... đang dùng chính trong `GithubGameDataRepository.cs`)*
   * **ArknightsAssets / ArknightsAssets2:** [GitHub Repo](https://github.com/ArknightsAssets/ArknightsAssets2) *(Nguồn tải ảnh Avatar & Character Arts `.png`)*
   * **Kengxxiao / ArknightsGameData:** [GitHub Repo](https://github.com/Kengxxiao/ArknightsGameData) *(Nguồn dữ liệu game bản Server CN dự phòng)*

2. **Roster Tracker & Schema tham khảo:**
   * **Krooster Website:** [https://krooster.com](https://krooster.com) *(Tham khảo UI/UX và cấu trúc Export JSON Roster)*
   * **MAA Copilot Schema:** [MaaAssistantArknights GitHub](https://github.com/MaaAssistantArknights/MaaAssistantArknights) *(Chuẩn JSON Import/Export dữ liệu tài khoản Arknights)*
   * **Penguin Stats API:** [Penguin Stats Docs](https://penguin-stats.io/) *(API Tỉ lệ rơi nguyên liệu các map farm)*

3. **Giao thức Kết nối Game Server (Cho Cấp 3 - Auto Sync):**
   * **Arknights Protocol / Protobuf Schemas:** [Arknights Protocol Schemas](https://github.com/freshtuna/Arknights-Protocol) *(Tham khảo cấu trúc Protobuf để deserialize dữ liệu tài khoản game)*


