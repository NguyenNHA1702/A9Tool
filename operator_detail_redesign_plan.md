# Operator Detail Page Redesign — Planning Document

> **Mục tiêu**: Thiết kế lại trang `OperatorDetail.razor` theo phong cách UI của game Arknights gốc (ảnh operator ở giữa, UI overlay đè lên background), phục vụ mục đích Wiki (chỉ đọc, có thể chuyển level/elite để xem thay đổi số liệu).

---

## 📸 GitHub Asset URLs Reference

### 1. Ảnh chân dung lớn (Artwork Full — dùng làm background)
Ảnh toàn thân dùng làm background chính, kéo full width/height theo từng Elite.

```
<!-- E1 / Default Art -->
https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/characters/{id}/{id}_1.png

<!-- E2 Art (nếu có) -->
https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/characters/{id}/{id}_2.png

<!-- Fallback: Aceship -->
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/characters/{id}.png
```

### 2. Avatar nhỏ (dùng trong card hoặc thumbnail)
```
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/avatars/{id}.png
```

### 3. Icon Skill
```
<!-- Nguồn chính: ArknightsAssets2 (branch cn, không EscapeDataString) -->
https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/skills/skill_icon_{iconId}.png

<!-- Fallback: Aceship -->
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/skills/skill_icon_{iconId}.png
```

### 4. Icon Class (Profession)
```
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_vanguard.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_guard.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_defender.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_sniper.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_caster.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_medic.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_supporter.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/classes/class_specialist.png
```

### 5. Icon Elite (Promote)
```
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/ui/elite/0.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/ui/elite/1.png
https://raw.githubusercontent.com/Aceship/Arknight-Images/main/ui/elite/2.png
```

### 6. Icon Stat (local assets — đã có sẵn trong wwwroot)
```
/assets/icons/charattrdetail/icon_hp.png
/assets/icons/charattrdetail/icon_atk.png
/assets/icons/charattrdetail/icon_def.png
/assets/icons/charattrdetail/icon_res.png
/assets/icons/charattrdetail/icon_cost.png
/assets/icons/charattrdetail/icon_block.png
/assets/icons/charattrdetail/icon_time.png

<!-- Sub-profession icon (nếu có) -->
/assets/icons/subprofessionicon/sub_{subProfessionId}_icon.png
```

---

## 🏗️ Cấu trúc Layout tổng thể

### Design Concept
- **Toàn màn hình**: Ảnh operator chiếm toàn bộ background (full viewport height).
- **Overlay trái**: Thông tin stats, attributes — bán trong suốt (glassmorphism).
- **Overlay phải**: Thông tin elite/potential/skill — bán trong suốt.
- **Tên operator**: Nằm góc dưới trái, chữ lớn + hiệu ứng shadow như game.

```
┌─────────────────────────────────────────────────────────────┐
│  [Back]                                    [Tabs: Info/Skill]│  ← Top nav bar (glassmorphism)
├────────────────────────────────────────────────────────────── │
│                                                               │
│  [LEFT OVERLAY]         [OPERATOR ART — Full BG]  [RIGHT]    │
│  ┌──────────────┐          centered, full bleed   ┌────────┐ │
│  │ Attributes   │                                 │ Level  │ │
│  │ HP  xxx      │                                 │  80    │ │
│  │ ATK xxx      │                                 │ Elite  │ │
│  │ DEF xxx      │                                 │ Skill  │ │
│  │ RES xx%      │                                 │ Talent │ │
│  │ COST xx      │                                 └────────┘ │
│  │ BLOCK x      │                                            │
│  │ REDEP xxs    │                                            │
│  │              │                                            │
│  │ Trust ──── % │                                            │
│  └──────────────┘                                            │
│                                                               │
│  Operator Name (large, bottom-left)                          │
│  [Class Icon] [SubProf] [Position] [Attack Range]            │
└──────────────────────────────────────────────────────────────┘
```

---

## 📄 HTML Skeleton

> **Lưu ý**: Đây là xương HTML cho `OperatorDetail.razor`. Tất cả `@` binding giữ nguyên theo Blazor. Chưa code logic C#, chỉ là cấu trúc HTML.

```html
@page "/operators/{Id}"
@* ... @using, @inject giữ nguyên ... *@

@if (op is null)
{
    <!-- Loading State -->
    <div class="od-loading">
        <div class="od-loading-spinner"></div>
        <p>Loading Operator Data...</p>
    </div>
}
else
{
    <!-- ============================================================
         ROOT: Full-viewport container, ảnh operator làm background
         ============================================================ -->
    <div class="od-root">

        <!-- BACKGROUND: Ảnh artwork operator full bleed -->
        <!-- Asset: ArknightsAssets2/cn/assets/dyn/arts/characters/{id}/{id}_{elite == 2 ? 2 : 1}.png -->
        <!-- Fallback: Aceship characters/{id}.png -->
        <div class="od-bg-layer" 
             style="background-image: url('@GetArtworkUrl(id)')">
        </div>

        <!-- VIGNETTE OVERLAY: Làm tối 2 bên để text dễ đọc -->
        <div class="od-vignette"></div>

        <!-- ── TOP NAVIGATION BAR ── -->
        <nav class="od-topnav">
            <button class="od-back-btn">← Back</button>

            <!-- Tab selector -->
            <div class="od-tabs">
                <button class="od-tab active">Info</button>
                <button class="od-tab">Skills</button>
                <!-- Có thể mở rộng: Talents, Module, Lore -->
            </div>
        </nav>

        <!-- ================================================================
             CONTENT LAYER: 3 cột (left overlay / center space / right overlay)
             ================================================================ -->
        <div class="od-content-layer">

            <!-- ── LEFT PANEL: Attributes + Trust ── -->
            <aside class="od-panel od-panel-left">

                <!-- ATTRIBUTES SECTION -->
                <div class="od-section od-attributes">
                    <div class="od-section-label">Attributes ▶▶</div>

                    <!-- Elite Selector (E0 / E1 / E2) — thay đổi stats -->
                    <!-- State: selectedElite (int 0/1/2) -->
                    <!-- Logic: GetCurrentStats() trả về Stats từ op.Phases[elite].AttributesKeyFrames.Last().Data -->
                    <!-- Wiki note: Slider level trong 1 phase nếu muốn nội suy stats -->
                    <div class="od-elite-selector">
                        <!-- Mỗi nút gán selectedElite = 0/1/2 -->
                        <button class="od-elite-btn active">E0</button>
                        <button class="od-elite-btn">E1</button>
                        <button class="od-elite-btn">E2</button>
                    </div>

                    <!-- Level Slider (tùy chọn - wiki feature) -->
                    <!-- Khi kéo slider, nội suy stats từ firstKeyFrame đến lastKeyFrame của phase -->
                    <!-- Logic: Lerp(minStats, maxStats, t) với t = (level - minLv) / (maxLv - minLv) -->
                    <div class="od-level-row">
                        <span class="od-lv-label">LV</span>
                        <input class="od-lv-slider" type="range" min="1" max="80" value="80" />
                        <span class="od-lv-value">80</span>
                    </div>

                    <!-- Stat rows -->
                    <!-- Icon: /assets/icons/charattrdetail/icon_hp.png ... -->
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_hp.png" /> 
                        <span>2000</span>  <!-- bind: stats.MaxHp -->
                    </div>
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_atk.png" />
                        <span>700</span>   <!-- bind: stats.Atk -->
                    </div>
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_def.png" />
                        <span>200</span>   <!-- bind: stats.Def -->
                    </div>
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_res.png" />
                        <span>10%</span>   <!-- bind: stats.MagicResistance -->
                    </div>
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_cost.png" />
                        <span>24</span>    <!-- bind: stats.Cost -->
                    </div>
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_block.png" />
                        <span>1</span>     <!-- bind: stats.BlockCnt -->
                    </div>
                    <div class="od-stat-row">
                        <img src="/assets/icons/charattrdetail/icon_time.png" />
                        <span>70s</span>   <!-- bind: stats.RespawnTime -->
                    </div>
                </div>

                <!-- TRUST BONUS SECTION -->
                <!-- Data: op.TrustBonus (List<FavorBonus>) — Level 50 = Max Trust -->
                <!-- Wiki feature: Slider trust 0-200% để xem Trust bonus tại từng mốc -->
                <div class="od-section od-trust">
                    <div class="od-trust-bar-label">Trust</div>

                    <!-- Trust Slider -->
                    <div class="od-trust-row">
                        <div class="od-trust-bar-bg">
                            <div class="od-trust-bar-fill" style="width: 100%;"></div>
                        </div>
                        <span class="od-trust-pct">200%</span>
                    </div>

                    <!-- Trust bonus stats (chỉ hiển thị khi > 0) -->
                    <!-- Logic: Lấy từ op.TrustBonus.FirstOrDefault(x => x.Level == selectedTrustLevel)?.Data -->
                    <div class="od-stat-row od-trust-bonus">
                        <img src="/assets/icons/charattrdetail/icon_hp.png" />
                        <span>+200</span>  <!-- bind: trustBonus.MaxHp nếu != 0 -->
                    </div>
                    <!-- ... ATK, DEF nếu có ... -->
                </div>

                <!-- STAR RATING -->
                <!-- Data: op.Rarity => TIER_6 = 6 sao ... -->
                <div class="od-stars">★★★★★★</div>

            </aside>

            <!-- ── CENTER: Tên operator (bottom-left style), Attack Range ── -->
            <div class="od-center-info">

                <!-- Operator name — large, bottom left like game -->
                <div class="od-op-name-block">
                    <!-- Icon class nhỏ góc trên tên -->
                    <!-- Asset: Aceship/classes/class_{profession}.png -->
                    <img class="od-op-class-mini" src="...class_caster.png" />

                    <h1 class="od-op-name">Amiya</h1>  <!-- bind: op.Name -->

                    <!-- SubProf + Position + Tags -->
                    <div class="od-op-meta">
                        <!-- Icon: /assets/icons/subprofessionicon/sub_{subProfessionId}_icon.png -->
                        <img class="od-subprof-icon" src="..." />
                        <span class="od-position-badge">Ranged</span>  <!-- bind: op.Position -->
                        <span class="od-class-badge">Caster</span>     <!-- bind: FormatProfession -->
                    </div>

                    <!-- Description -->
                    <p class="od-op-desc">...</p>

                    <!-- Tags -->
                    <div class="od-tag-list">
                        <span class="od-tag">DPS</span>
                        <span class="od-tag">Crowd-Control</span>
                    </div>
                </div>

                <!-- Attack Range Grid (tùy chọn nâng cao) -->
                <!-- Data: op.Skills.RangeId → cần load range_table.json -->
                <!-- <div class="od-attack-range"> ... </div> -->

            </div>

            <!-- ── RIGHT PANEL: Elite/Potential/Skill/Talent/Module ── -->
            <aside class="od-panel od-panel-right">

                <!-- ELITE + POTENTIAL ROW -->
                <div class="od-ep-row">
                    <!-- Elite Icons -->
                    <!-- Asset: Aceship/ui/elite/{0/1/2}.png -->
                    <div class="od-elite-icons">
                        <img src="https://raw.githubusercontent.com/Aceship/Arknight-Images/main/ui/elite/0.png" />
                        <img src="https://raw.githubusercontent.com/Aceship/Arknight-Images/main/ui/elite/1.png" />
                        <img src="https://raw.githubusercontent.com/Aceship/Arknight-Images/main/ui/elite/2.png" />
                    </div>

                    <!-- Potential indicator -->
                    <!-- Data: op.PotentialRanks (6 levels) -->
                    <!-- Wiki: Dropdown/click chọn potential 1-6, xem buff text -->
                    <div class="od-potential">
                        <span>Potential</span>
                        <div class="od-potential-dots">
                            <!-- 6 dots, click chọn potential -->
                            <span class="od-pot-dot active"></span>
                            <span class="od-pot-dot"></span>
                            <!-- ... x6 ... -->
                        </div>
                        <!-- Hiển thị buff của potential đang chọn -->
                        <div class="od-pot-desc">DP Cost -1</div>
                    </div>
                </div>

                <!-- SKILL SECTION -->
                <!-- Data: skills (List<SkillsForOperatorResponse>) -->
                <div class="od-skill-section">
                    <div class="od-section-label">▲ Skill</div>

                    <!-- Skill Tabs (chọn skill 1/2/3) -->
                    <div class="od-skill-tabs">
                        <!-- Mỗi tab: icon skill + SP cost hiển thị nhỏ dưới icon -->
                        <!-- Asset: ArknightsAssets2/cn/assets/dyn/arts/skills/skill_icon_{iconId}.png -->
                        <!-- Fallback: Aceship/skills/skill_icon_{iconId}.png -->
                        <div class="od-skill-tab od-skill-tab-active">
                            <img class="od-skill-tab-icon" src="...skill_icon_skchr_xxx_1.png" />
                            <span class="od-skill-sp">▶20 ⚡32</span>  <!-- spCost / initSp tại skillLevel -->
                        </div>
                        <div class="od-skill-tab">
                            <img class="od-skill-tab-icon" src="..." />
                            <span class="od-skill-sp">▶75 ⚡100</span>
                        </div>
                    </div>

                    <!-- Skill Detail Card cho skill đang chọn -->
                    <div class="od-skill-card">
                        <!-- Skill Level Selector (Wiki feature: Level 1-7, Mastery 1-3) -->
                        <!-- Logic: selectedSkillLevel (int 0..9) → skills[selectedSkill].Levels[selectedSkillLevel] -->
                        <!-- SkillLevelResponse: Name, Description, SpCost, InitSp, Duration -->
                        <div class="od-skill-level-row">
                            <span>Lv</span>
                            <div class="od-skill-lvl-dots">
                                <!-- 7 dots Level 1-7 + 3 dots Mastery 1-3 -->
                                <!-- Click để chọn level xem stats/description thay đổi -->
                                <span class="od-lvl-dot active">1</span>
                                <span class="od-lvl-dot">2</span>
                                <span class="od-lvl-dot">3</span>
                                <!-- ... -->
                                <span class="od-lvl-dot mastery">M1</span>
                                <span class="od-lvl-dot mastery">M2</span>
                                <span class="od-lvl-dot mastery">M3</span>
                            </div>
                        </div>

                        <!-- Skill header -->
                        <div class="od-skill-header">
                            <img class="od-skill-icon" src="..." />
                            <div class="od-skill-meta">
                                <span class="od-skill-name">Healing Up γ</span>
                                <!-- SP recovery type badge: Auto Recovery / Manual / Offensive -->
                                <span class="od-sp-type-badge">Auto Recovery</span>
                                <span class="od-sp-type-badge">Manual</span>
                                <!-- Duration badge: icon đồng hồ + giây -->
                                <span class="od-duration-badge">🕐 30 sec</span>
                            </div>
                        </div>

                        <!-- Skill stats: SP cost, Init SP, Duration -->
                        <div class="od-skill-stats">
                            <span>SP: <b>20</b></span>  <!-- bind: Levels[selectedSkillLevel].SpCost -->
                            <span>Init: <b>32</b></span> <!-- bind: Levels[selectedSkillLevel].InitSp -->
                            <span>⏱ 30s</span>           <!-- bind: Levels[selectedSkillLevel].Duration -->
                        </div>

                        <!-- Skill description với highlight số liệu -->
                        <p class="od-skill-desc">
                            ATK <span class="od-highlight">+70%</span>
                            <!-- bind: CleanDescription(Levels[selectedSkillLevel].Description) -->
                        </p>
                    </div>

                    <!-- Rank (Specialization rank hiển thị - wiki only, không unlock) -->
                    <div class="od-skill-rank">RANK 7 ▲ Specialize skill</div>
                </div>

                <!-- TALENT SECTION -->
                <!-- Data: op.Talents[i].Candidates — Candidates theo Elite/Potential -->
                <div class="od-talent-section">
                    <div class="od-section-label">Talents</div>
                    <!-- Wiki feature: hiển thị talent ứng với Elite/Potential đang chọn -->
                    <!-- Logic: candidates.Where(c => c.UnlockCondition.Phase <= selectedElite && c.RequiredPotentialRank <= selectedPotential).LastOrDefault() -->
                    <div class="od-talent-item">
                        <div class="od-talent-name">Skill Aura</div>
                        <p class="od-talent-desc">
                            When deployed, increases SP recovery...
                        </p>
                    </div>
                </div>

                <!-- MODULE SECTION (placeholder - cần thêm module_table.json sau) -->
                <!-- Hiển thị: ORIGINAL module + Module details nếu có data -->
                <div class="od-module-section">
                    <div class="od-section-label">Module</div>
                    <div class="od-module-original">ORIGINAL</div>
                    <div class="od-module-name">Ptilopsis's Badge</div>
                    <!-- Trait text (từ module data) -->
                    <div class="od-trait-text">Restores HP of 3 allies simultaneously.</div>
                </div>

            </aside>
        </div>
        <!-- end .od-content-layer -->

    </div>
    <!-- end .od-root -->
}
```

---

## 🎨 CSS Structure Plan (`app.css`)

### Layout Classes mới cần thêm

```
/* === OPERATOR DETAIL v2 === */

/* Root container — full viewport */
.od-root {
    position: relative;
    width: 100%;
    min-height: 100vh;          /* full screen */
    overflow: hidden;
    background: #0a0a0c;
}

/* Background art layer */
.od-bg-layer {
    position: absolute;
    inset: 0;
    background-size: cover;
    background-position: center 10%;   /* tập trung vào mặt/thân operator */
    background-repeat: no-repeat;
    filter: brightness(0.8);
    z-index: 0;
    transition: background-image 0.4s ease;
}

/* Vignette gradient overlay (tối 2 bên, trong giữa) */
.od-vignette {
    position: absolute;
    inset: 0;
    background: 
        linear-gradient(to right,  rgba(0,0,0,0.75) 0%, transparent 30%, transparent 70%, rgba(0,0,0,0.75) 100%),
        linear-gradient(to bottom, rgba(0,0,0,0.5)  0%, transparent 20%, transparent 70%, rgba(0,0,0,0.85) 100%);
    z-index: 1;
}

/* Top nav bar */
.od-topnav {
    position: absolute;
    top: 0; left: 0; right: 0;
    z-index: 10;
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 12px 20px;
    background: rgba(0,0,0,0.45);
    backdrop-filter: blur(8px);
    border-bottom: 1px solid rgba(255,255,255,0.08);
}

/* Content layer: 3-column flex */
.od-content-layer {
    position: relative;
    z-index: 5;
    display: flex;
    gap: 0;
    height: 100vh;
    padding-top: 52px;       /* compensate for topnav */
    align-items: stretch;
}

/* Panels glassmorphism */
.od-panel {
    background: rgba(10, 10, 15, 0.65);
    backdrop-filter: blur(12px);
    border: 1px solid rgba(255,255,255,0.08);
    overflow-y: auto;
    padding: 16px;
    display: flex;
    flex-direction: column;
    gap: 12px;
    min-width: 220px;
    max-width: 280px;
}

/* Center column expands */
.od-center-info {
    flex: 1;
    display: flex;
    flex-direction: column;
    justify-content: flex-end;  /* đẩy tên operator xuống đáy */
    padding: 24px;
}

/* Operator name block (góc dưới trái, phong cách game) */
.od-op-name { 
    font-size: 3rem;
    font-weight: 900;
    color: #fff;
    text-shadow: 0 2px 20px rgba(0,0,0,0.9), 0 0 40px rgba(0,0,0,0.6);
    line-height: 1;
    margin: 0;
}

/* Stat rows */
.od-stat-row {
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 4px 0;
    border-bottom: 1px solid rgba(255,255,255,0.05);
    font-size: 0.85rem;
    color: #e0e0e0;
}
.od-stat-row img { width: 18px; height: 18px; object-fit: contain; }

/* Trust bar */
.od-trust-bar-bg {
    flex: 1;
    height: 6px;
    background: rgba(255,255,255,0.15);
    border-radius: 3px;
}
.od-trust-bar-fill {
    height: 100%;
    background: linear-gradient(90deg, #a78bfa, #f472b6);
    border-radius: 3px;
    transition: width 0.3s ease;
}

/* Skill tabs (góc phải, icon grid) */
.od-skill-tabs {
    display: flex;
    gap: 6px;
    margin-bottom: 8px;
}
.od-skill-tab {
    position: relative;
    border: 2px solid rgba(255,255,255,0.2);
    border-radius: 6px;
    overflow: hidden;
    cursor: pointer;
    transition: border-color 0.2s;
}
.od-skill-tab.od-skill-tab-active {
    border-color: #60a5fa;
    box-shadow: 0 0 8px rgba(96, 165, 250, 0.5);
}
.od-skill-tab-icon { width: 52px; height: 52px; object-fit: contain; }
.od-skill-sp {
    position: absolute;
    bottom: 0; left: 0; right: 0;
    font-size: 0.6rem;
    background: rgba(0,0,0,0.7);
    text-align: center;
    color: #fff;
    padding: 1px 0;
}

/* Skill level dots */
.od-skill-lvl-dots {
    display: flex;
    gap: 4px;
    flex-wrap: wrap;
}
.od-lvl-dot {
    width: 22px; height: 22px;
    border-radius: 50%;
    background: rgba(255,255,255,0.1);
    border: 1px solid rgba(255,255,255,0.2);
    display: flex; align-items: center; justify-content: center;
    font-size: 0.65rem;
    cursor: pointer;
    transition: all 0.15s;
    color: #aaa;
}
.od-lvl-dot.active {
    background: #60a5fa;
    border-color: #60a5fa;
    color: #fff;
}
.od-lvl-dot.mastery {
    background: rgba(253,185,50,0.15);
    border-color: #fdb932;
    color: #fdb932;
}
.od-lvl-dot.mastery.active {
    background: #fdb932;
    color: #000;
}
```

---

## ⚙️ C# Logic Cần Thêm / Sửa (Ghi chú cho implementation)

### 1. Skill Level Selection
```csharp
// State cần thêm
private int selectedSkillLevel = 0;  // 0-9 (Level 1-7 = index 0-6, Mastery 1-3 = index 7-9)

// Helper lấy SkillLevelResponse tại level đang chọn
private SkillLevelResponse? GetCurrentSkillLevel()
    => (operatorSkillResponse != null && selectedSkillLevel < operatorSkillResponse.Levels.Count)
        ? operatorSkillResponse.Levels[selectedSkillLevel]
        : null;

// Reset về level 0 khi đổi skill
// Thêm: @onclick="() => { selectedSkill = index; selectedSkillLevel = 0; }"
```

### 2. Trust Level Selection
```csharp
// State
private int selectedTrust = 200;  // % 0-200

// Helper: FavorBonus[] có Level = 0, 25, 50 (tương ứng 0%, 100%, 200%)
// Cần map selectedTrust % → level trong TrustBonus
private Stats? GetTrustBonusStats()
{
    // Lấy FavorBonus gần nhất với mức trust đang chọn
    var trustLevel = selectedTrust >= 200 ? 50 : (selectedTrust >= 100 ? 25 : 0);
    return op?.TrustBonus.FirstOrDefault(x => x.Level == trustLevel)?.Data;
}
```

### 3. Potential Selection
```csharp
// State
private int selectedPotential = 0;  // 0-5

// Helper: Lấy talent candidate ứng với elite + potential hiện tại
private TalentCandidate? GetActiveTalentCandidate(Talent talent)
    => talent.Candidates?
        .Where(c => (c.UnlockCondition?.Phase == null || PhaseToInt(c.UnlockCondition.Phase) <= elite)
                 && c.RequiredPotentialRank <= selectedPotential)
        .LastOrDefault();

private int PhaseToInt(string? phase) => phase switch
{
    "PHASE_0" => 0, "PHASE_1" => 1, "PHASE_2" => 2, _ => 0
};
```

### 4. GetArtworkUrl (dùng cho background)
```csharp
private string GetArtworkUrl(string id) => elite == 2
    ? $"https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/characters/{id}/{id}_2.png"
    : $"https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/characters/{id}/{id}_1.png";

// Fallback cần xử lý qua onerror JS
```

### 5. GetSkillImageUrl (sửa lỗi EscapeDataString)
```csharp
// BỎ Uri.EscapeDataString để giữ dấu () trong skill ID như skcom_heal(1)
private string GetSkillImageUrl(string? id) 
    => !string.IsNullOrEmpty(id) 
        ? $"https://raw.githubusercontent.com/ArknightsAssets/ArknightsAssets2/cn/assets/dyn/arts/skills/skill_icon_{id}.png" 
        : "/assets/icons/charattrdetail/sprite_profession.png";
```

---

## 📝 Các phần cần triển khai theo thứ tự

- [ ] **Phase 1** - Layout & CSS: `.od-root`, `.od-bg-layer`, `.od-vignette`, `.od-content-layer`, 2 panels
- [ ] **Phase 2** - Left panel: Stats display, Elite selector gán UI state, Trust bar slider
- [ ] **Phase 3** - Center: Operator name block, description, tags
- [ ] **Phase 4** - Right panel: Elite icons + Potential dots, Skill tabs với SP info, Skill detail card
- [ ] **Phase 5** - Skill Level dots (Level 1-7 + Mastery 1-3), description update theo level
- [ ] **Phase 6** - Talent hiển thị đúng theo Elite+Potential state
- [ ] **Phase 7** - Module section (placeholder), polish animations, responsive

> [!IMPORTANT]
> **Về ảnh background**: Ảnh full artwork (`characters/{id}/{id}_1.png`) **khác** với ảnh portrait card (`charportraits/{id}_1.png`). Ảnh artwork là fullbody có transparent background, phù hợp làm background overlay.

> [!NOTE]
> **Token giới hạn**: Triển khai từng Phase một, mỗi Phase gọi riêng để kiểm soát context. Bắt đầu từ Phase 1 CSS layout trước khi đụng vào Razor markup.

> [!TIP]
> **Level slider cho stats**: Dữ liệu `Phase.AttributesKeyFrames` có 2 keyframe (min level và max level của phase đó). Dùng linear interpolation để tính stats tại level bất kỳ: `stat = min + (max - min) * t` với `t = (level - minLv) / (maxLv - minLv)`.
