# Phase H — Balls Inventory Screen

> Spec written by Claude (Architect) for Claude Code (Implementer).
> Follows the Clubs Inventory pattern: CSV database → Manager singleton → Carousel → Detail Panel.
> Balls are simpler than clubs: no rarity, no level, no durability, no equip/repair/level-up.
> Key novelty: segmented stat bars that show positive (blue) or negative (red) values.

---

## Changelog

- 2026-03-27: Initial spec
- 2026-03-27: Balls have NO rarity — removed rarity badge from thumbnail cards, use neutral/default card background
- 2026-03-27: Stat range confirmed as -10 to +10 (expanded from original -5 to +5 in confluence doc)

---

## Overview

The Balls tab (index 2 in `InventoryScreenController.tabPanels`) shows a paginated carousel of
owned balls and a detail panel with ball image, info text, and 5 segmented stat bars.

**Reference mockup:** The uploaded Balls mockup shows:
- Thumbnail cards with quantity badge (top-right, e.g. `x99` or `∞`), ball image, name. NO rarity badge.
- Detail panel: ball name → "OWNED  x99" → 5 stat rows with segmented bars → COMPARE button
- Left panel: ball full image (top) + INFO section (bottom)

**Stat range:** -10 to +10 (confirmed, expanded from original -5 to +5 in confluence).

---

## Sub-task H1: Data Layer

### H1a: BallData.cs

**File:** `Assets/Scripts/UI/Inventory/BallData.cs`
**Namespace:** `Golfin.Inventory`

```csharp
#nullable enable
using UnityEngine;

namespace Golfin.Inventory
{
    // ── Template data (loaded from Balls.csv) ──────────────────────────────────

    /// <summary>
    /// Read-only ball template loaded from Balls.csv.
    /// One instance per ball definition shared across all players.
    /// </summary>
    public class BallDataRuntime
    {
        public string ballId            = "";
        public string name              = "";
        public string brand             = "";

        // Stats — range: -10 to +10
        public int power          = 0;
        public int rebound        = 0;
        public int windResistance = 0;
        public int roll           = 0;
        public int spin           = 0;

        // Sprites (loaded from Resources/Balls/)
        public string  thumbnailSpriteName = "";
        public Sprite? thumbnailSprite     = null;
        public string  fullSpriteName      = "";
        public Sprite? fullSprite          = null;

        public string info = "";

        public override string ToString() =>
            $"{name}: PWR={power} REB={rebound} WIND={windResistance} ROLL={roll} SPIN={spin}";
    }

    // ── Player instance data (owned ball state) ─────────────────────────────────

    /// <summary>
    /// Mutable per-player ball state — just a quantity count.
    /// Balls stack up to 99. No level, no durability, no equip state.
    /// </summary>
    public class PlayerBallData
    {
        public string ballId   = "";
        public int    quantity = 0;   // 0 = not owned, max stacking = 99 (∞ for default ball)

        /// <summary>True if this is the default unlimited ball (Golfin ball).</summary>
        public bool IsUnlimited => quantity < 0;  // -1 = unlimited (shown as ∞)
    }
}
```

**Key differences from ClubData.cs:**
- No `ClubType` enum equivalent — balls are a flat list (no type filter bar for now)
- No rarity field — balls have no rarity system
- No level, no durability, no equip slot
- Stats are simple ints (-10 to +10) not computed from base + SP
- `PlayerBallData` only tracks quantity (stacks to 99, or -1 for unlimited default ball)

---

### H1b: BallDatabaseCSV.cs

**File:** `Assets/Scripts/UI/Inventory/BallDatabaseCSV.cs`
**Namespace:** `Golfin.Inventory`

Mirrors `ClubDatabaseCSV.cs` pattern. Singleton, loads from TextAsset.

```csharp
#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Golfin.Inventory
{
    /// <summary>
    /// CSV-driven ball database — mirrors ClubDatabaseCSV pattern.
    /// Loads Balls.csv from a TextAsset assigned in Inspector and resolves
    /// sprites from Resources/Balls/Thumbnails/ and Resources/Balls/Full/.
    ///
    /// Execution order: runs before BallManager so data is ready for it.
    /// </summary>
    public class BallDatabaseCSV : MonoBehaviour
    {
        public static BallDatabaseCSV? Instance { get; private set; }

        [Header("CSV File")]
        [SerializeField] private TextAsset ballsCSV = null!;

        private const string ThumbnailPath = "Balls/Thumbnails";
        private const string FullPath      = "Balls/Full";

        private readonly Dictionary<string, BallDataRuntime> ballMap  = new();
        private readonly List<BallDataRuntime>                allBalls = new();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadCSV();
        }

        private void LoadCSV()
        {
            if (ballsCSV == null)
            {
                Debug.LogError("[BallDatabaseCSV] ballsCSV not assigned — drag Balls.csv into Inspector.");
                return;
            }

            ballMap.Clear();
            allBalls.Clear();

            string[] lines = ballsCSV.text.Split('\n');
            if (lines.Length < 2) { Debug.LogError("[BallDatabaseCSV] Balls.csv is empty."); return; }

            var headerIndex = BuildHeaderIndex(ParseCSVLine(lines[0]));

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                var ball = ParseRow(ParseCSVLine(line), headerIndex);
                if (ball == null) continue;

                ballMap[ball.ballId] = ball;
                allBalls.Add(ball);
            }

            Debug.Log($"[BallDatabaseCSV] Loaded {allBalls.Count} balls.");
        }

        private Dictionary<string, int> BuildHeaderIndex(List<string> headers)
        {
            var idx = new Dictionary<string, int>();
            for (int i = 0; i < headers.Count; i++)
                idx[headers[i].Trim()] = i;
            return idx;
        }

        private BallDataRuntime? ParseRow(List<string> fields, Dictionary<string, int> idx)
        {
            try
            {
                string Get(string col, string def = "")
                    => idx.TryGetValue(col, out int i) && i < fields.Count ? fields[i].Trim() : def;
                int GetInt(string col, int def = 0)
                    => int.TryParse(Get(col), out int v) ? v : def;

                var ball = new BallDataRuntime
                {
                    ballId             = Get("id"),
                    name               = Get("name"),
                    brand              = Get("brand"),
                    power              = GetInt("power"),
                    rebound            = GetInt("rebound"),
                    windResistance     = GetInt("windResistance"),
                    roll               = GetInt("roll"),
                    spin               = GetInt("spin"),
                    thumbnailSpriteName = Get("thumbnailSprite"),
                    fullSpriteName     = Get("fullSprite"),
                    info               = Get("info"),
                };

                if (string.IsNullOrEmpty(ball.ballId)) return null;

                ball.thumbnailSprite = LoadSprite(ThumbnailPath, ball.thumbnailSpriteName);
                ball.fullSprite      = LoadSprite(FullPath,      ball.fullSpriteName);

                return ball;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[BallDatabaseCSV] Row parse error: {e.Message}");
                return null;
            }
        }

        private static Sprite? LoadSprite(string folder, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            var sprite = Resources.Load<Sprite>($"{folder}/{name}");
            if (sprite == null)
                Debug.LogWarning($"[BallDatabaseCSV] Sprite not found: Resources/{folder}/{name}");
            return sprite;
        }

        // Reuse the same CSV parser as ClubDatabaseCSV
        private static List<string> ParseCSVLine(string line)
        {
            var fields  = new List<string>();
            var current = new System.Text.StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    { current.Append('"'); i++; }
                    else
                    { inQuotes = !inQuotes; }
                }
                else if (c == ',' && !inQuotes)
                { fields.Add(current.ToString()); current.Clear(); }
                else
                { current.Append(c); }
            }

            fields.Add(current.ToString());
            return fields;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public BallDataRuntime? GetBall(string ballId)
        {
            if (ballMap.TryGetValue(ballId, out var data)) return data;
            Debug.LogWarning($"[BallDatabaseCSV] Ball '{ballId}' not found.");
            return null;
        }

        public List<BallDataRuntime> GetAllBalls() => allBalls.ToList();
    }
}
```

---

### H1c: BallManager.cs

**File:** `Assets/Scripts/BallManager.cs` (top-level, matches ClubManager.cs location)

```csharp
#nullable enable
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Golfin.Inventory;

/// <summary>
/// Singleton — owns all player ball data (quantities).
/// Mirrors ClubManager pattern but much simpler (no equip, no level, no durability).
///
/// Execution order: after BallDatabaseCSV (set in Project Settings > Script Execution Order).
/// </summary>
public class BallManager : MonoBehaviour
{
    public static BallManager Instance { get; private set; } = null!;

    /// <summary>Fired when the owned-ball list or any quantity changes.</summary>
    public event System.Action? OnInventoryChanged;

    private readonly Dictionary<string, PlayerBallData> ownedBalls = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitializeBalls();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null!;
    }

    /// <summary>
    /// Seeds PlayerBallData for every ball in the database.
    /// First ball (Golfin) gets unlimited quantity (-1), others get test quantity.
    /// </summary>
    private void InitializeBalls()
    {
        var db = BallDatabaseCSV.Instance;
        if (db == null)
        {
            Debug.LogError("[BallManager] BallDatabaseCSV.Instance is null — check Script Execution Order.");
            return;
        }

        ownedBalls.Clear();
        bool first = true;

        foreach (var template in db.GetAllBalls())
        {
            var playerBall = new PlayerBallData
            {
                ballId   = template.ballId,
                quantity = first ? -1 : 99,  // first ball = unlimited (∞), rest = test qty
            };
            ownedBalls[template.ballId] = playerBall;
            first = false;
        }

        Debug.Log($"[BallManager] Initialized {ownedBalls.Count} balls.");
    }

    // ── Public API ────────────────────────────────────────────────────────

    public PlayerBallData? GetBallData(string ballId)
        => ownedBalls.TryGetValue(ballId, out var data) ? data : null;

    public List<string> GetAllOwnedBallIds()
        => ownedBalls.Where(kvp => kvp.Value.quantity != 0)
                     .Select(kvp => kvp.Key)
                     .ToList();

    public int GetQuantity(string ballId)
        => ownedBalls.TryGetValue(ballId, out var data) ? data.quantity : 0;

    /// <summary>Returns display string: "∞" for unlimited, "x99" for normal.</summary>
    public string GetQuantityDisplay(string ballId)
    {
        if (!ownedBalls.TryGetValue(ballId, out var data)) return "x0";
        return data.IsUnlimited ? "∞" : $"x{data.quantity}";
    }
}
```

---

### H1d: Balls.csv

**File:** `Assets/Data/Balls.csv`

```csv
id,name,brand,power,rebound,windResistance,roll,spin,thumbnailSprite,fullSprite,info
ball_golfin,Golfin,Golfin,0,0,0,0,0,Golfin,Golfin,"The standard Golfin ball. Perfectly balanced with no stat bonuses or penalties—reliable in any situation."
ball_putt_ace,Putt Ace,Putt Ace,10,-6,0,5,-4,PuttAce,PuttAce,"Designed by PUTT ACE, a name synonymous with short-game mastery, this ball delivers exceptional spin, subtle roll, and balanced power—tailored for precision play in any condition."
```

**Sprite paths resolved to:**
- `Resources/Balls/Thumbnails/Golfin` → already exists
- `Resources/Balls/Thumbnails/PuttAce` → already exists
- `Resources/Balls/Full/Golfin` → verify exists
- `Resources/Balls/Full/PuttAce` → verify exists

---

## Sub-task H2: BallThumbnailCard

**File:** `Assets/Scripts/UI/Inventory/BallThumbnailCard.cs`
**Namespace:** `Golfin.Inventory`

Derived from `ClubThumbnailCard.cs`. Key changes:
- **No rarity badge** — balls have no rarity. Remove `rarityBadgeImage` and `rarityLabelText`.
- **Quantity badge** instead of level badge — shows `∞` or `x99` in the top-right corner
- **No status icons** (no equipped, no durability low)
- **Background** — use a neutral/default card background (no rarity-colored backgrounds)

```csharp
#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace Golfin.Inventory
{
    /// <summary>
    /// Individual ball card in the Ball Inventory carousel.
    /// Simplified from ClubThumbnailCard — no rarity, shows quantity instead of level.
    /// </summary>
    public class BallThumbnailCard : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private Image           portraitImage      = null!;
        [SerializeField] private TextMeshProUGUI nameText           = null!;
        [SerializeField] private TextMeshProUGUI quantityText       = null!;   // top-right: "x99" or "∞"
        [SerializeField] private Image           selectionHighlight = null!;
        [SerializeField] private Image           backgroundImage    = null!;
        [SerializeField] private Button          cardButton         = null!;

        private string ballId    = "";
        private bool   isSelected = false;
        private Coroutine? scaleCoroutine;

        public System.Action? OnClicked;

        public void Initialize(string id)
        {
            ballId = id;

            var playerBall = BallManager.Instance?.GetBallData(ballId);
            if (playerBall == null) { Debug.LogError($"[BallThumbnailCard] PlayerBallData for '{id}' not found."); return; }

            var template = BallDatabaseCSV.Instance?.GetBall(ballId);
            if (template == null) { Debug.LogError($"[BallThumbnailCard] BallDataRuntime for '{id}' not found."); return; }

            // Portrait
            if (portraitImage != null && template.thumbnailSprite != null)
                portraitImage.sprite = template.thumbnailSprite;

            // Name
            if (nameText != null)
                nameText.text = template.name.ToUpper();

            // Quantity badge (replaces level badge position)
            if (quantityText != null)
                quantityText.text = BallManager.Instance?.GetQuantityDisplay(ballId) ?? "x0";

            // Background — neutral/default (no rarity coloring)
            // NOTE: The builder should set a default card background. If using the
            // character card prefab as base, consider using Common sprite or a neutral one.
            if (backgroundImage != null)
            {
                var bgSprite = Resources.Load<Sprite>("Rarities/Common");
                if (bgSprite != null)
                {
                    backgroundImage.sprite = bgSprite;
                    backgroundImage.color  = Color.white;
                }
            }

            // Button
            if (cardButton != null)
                cardButton.onClick.AddListener(() => OnClicked?.Invoke());

            Debug.Log($"[BallThumbnailCard] Initialized: {template.name}");
        }

        // ── Selection (identical to ClubThumbnailCard) ────────────────────────

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (selectionHighlight != null)
                selectionHighlight.enabled = selected;

            float target = selected ? 1.05f : 1f;
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(AnimateScale(target));
        }

        private IEnumerator AnimateScale(float target)
        {
            float start    = transform.localScale.x;
            float duration = 0.3f;
            float elapsed  = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t    = elapsed / duration;
                float ease = 1f - Mathf.Pow(2f, -10f * t) * Mathf.Cos(t * Mathf.PI * 3f);
                transform.localScale = Vector3.one * Mathf.LerpUnclamped(start, target, ease);
                yield return null;
            }
            transform.localScale = Vector3.one * target;
            scaleCoroutine = null;
        }

        public string GetBallId()  => ballId;
        public bool   IsSelected() => isSelected;
    }
}
```

---

## Sub-task H3: BallCarouselController

**File:** `Assets/Scripts/UI/Inventory/BallCarouselController.cs`
**Namespace:** `Golfin.Inventory`

Nearly identical to `ClubCarouselController.cs` but:
- Reads ball IDs from `BallManager.Instance.GetAllOwnedBallIds()`
- No filter bar (no ball types for now)
- Uses `BallThumbnailCard` prefab instead of `ClubThumbnailCard`
- Fires `OnBallSelected(string ballId)` event

Copy `ClubCarouselController.cs` and do a find-replace:
- `Club` → `Ball` (class names, method names, comments)
- `ClubManager` → `BallManager`
- `ClubThumbnailCard` → `BallThumbnailCard`
- `ClubFilterBar` references → remove entirely (no filter bar)
- `_currentFilter` / filter-related code → remove

The `PopulateCarousel` method should call:
```csharp
var ballIds = BallManager.Instance?.GetAllOwnedBallIds() ?? new List<string>();
```

Keep: pagination, arrow buttons, scroll animation, card selection, `OnBallSelected` event.
Remove: all filter-related code (`ClubFilterBar`, `_currentFilter`, `OnFilterChanged`).

---

## Sub-task H4: BallDetailPanel

**File:** `Assets/Scripts/UI/Inventory/BallDetailPanel.cs`
**Namespace:** `Golfin.Inventory`

Derived from `ClubDetailPanel.cs`. Major simplifications:

### Fields:
```csharp
[Header("Left Panel")]
[SerializeField] private Image           ballImage = null!;
[SerializeField] private TextMeshProUGUI infoHeader = null!;
[SerializeField] private TextMeshProUGUI infoText   = null!;

[Header("Right Panel — Name & Quantity")]
[SerializeField] private TextMeshProUGUI ballNameText  = null!;
[SerializeField] private TextMeshProUGUI ownedLabel    = null!;    // "OWNED"
[SerializeField] private TextMeshProUGUI quantityText  = null!;    // "x99" or "∞"

[Header("Stat — Power")]
[SerializeField] private TextMeshProUGUI powerName     = null!;
[SerializeField] private Image           powerBar      = null!;
[SerializeField] private TextMeshProUGUI powerNumber   = null!;

[Header("Stat — Rebound")]
[SerializeField] private TextMeshProUGUI reboundName   = null!;
[SerializeField] private Image           reboundBar    = null!;
[SerializeField] private TextMeshProUGUI reboundNumber = null!;

[Header("Stat — Wind Resistance")]
[SerializeField] private TextMeshProUGUI windResistanceName   = null!;
[SerializeField] private Image           windResistanceBar    = null!;
[SerializeField] private TextMeshProUGUI windResistanceNumber = null!;

[Header("Stat — Roll")]
[SerializeField] private TextMeshProUGUI rollName   = null!;
[SerializeField] private Image           rollBar    = null!;
[SerializeField] private TextMeshProUGUI rollNumber = null!;

[Header("Stat — Spin")]
[SerializeField] private TextMeshProUGUI spinName   = null!;
[SerializeField] private Image           spinBar    = null!;
[SerializeField] private TextMeshProUGUI spinNumber = null!;

[Header("Buttons")]
[SerializeField] private Button compareButton = null!;

[Header("Carousel")]
[SerializeField] private BallCarouselController? carousel;
```

### Fields REMOVED (vs ClubDetailPanel):
- `rarityLabel`, `currentLevelText`, `maxLevelText` — replaced by `ownedLabel` + `quantityText`
- `durabilityName/Bar/Number`, `distanceName/Value` — balls don't have these
- `levelUpButton`, `repairButton`, `equipButton`, `equipButtonText`, `bagLabel` — no actions
- `equippedIcon` — no equip state
- `compareController` — defer to H8
- `levelUpModal`, `bagSelectionModal` — not needed

### Stat bar update logic:

```csharp
private const int BALL_STAT_MAX = 10;

private static readonly Color StatPositiveColor = new(0.2f, 0.5f, 0.9f, 1f);  // blue
private static readonly Color StatNegativeColor = new(0.9f, 0.3f, 0.15f, 1f); // orange-red

private void UpdateBallStatBar(TextMeshProUGUI? nameField, Image? bar,
    TextMeshProUGUI? numberField, string label, int value)
{
    if (nameField != null) nameField.text = label;

    // Number shows +/- prefix
    if (numberField != null)
    {
        if (value > 0)      numberField.text = $"+{value}";
        else if (value < 0) numberField.text = $"{value}";
        else                numberField.text = "0";
    }

    // Bar fill — absolute value / max, colored by sign
    if (bar != null)
    {
        bar.fillAmount = BALL_STAT_MAX > 0 ? (float)Mathf.Abs(value) / BALL_STAT_MAX : 0f;
        bar.color = value >= 0 ? StatPositiveColor : StatNegativeColor;
    }
}
```

### UpdatePanel method:

```csharp
private void UpdatePanel(string ballId)
{
    currentBallId = ballId;

    var playerBall = BallManager.Instance?.GetBallData(ballId);
    if (playerBall == null) return;

    var template = BallDatabaseCSV.Instance?.GetBall(ballId);
    if (template == null) return;

    // Ball image
    if (ballImage != null)
    {
        if (template.fullSprite != null)
            ballImage.sprite = template.fullSprite;
        else if (template.thumbnailSprite != null)
            ballImage.sprite = template.thumbnailSprite;
    }

    // Name
    if (ballNameText != null) ballNameText.text = template.name.ToUpper();

    // Owned + quantity
    if (ownedLabel != null) ownedLabel.text = LocalizationManager.Get("BALL_OWNED");
    if (quantityText != null)
        quantityText.text = BallManager.Instance?.GetQuantityDisplay(ballId) ?? "x0";

    // Info
    if (infoHeader != null) infoHeader.text = LocalizationManager.Get("BALL_INFO");
    if (infoText != null) infoText.text = template.info;

    // Stat bars
    UpdateBallStatBar(powerName, powerBar, powerNumber,
        LocalizationManager.Get("BALL_POWER"), template.power);
    UpdateBallStatBar(reboundName, reboundBar, reboundNumber,
        LocalizationManager.Get("BALL_REBOUND"), template.rebound);
    UpdateBallStatBar(windResistanceName, windResistanceBar, windResistanceNumber,
        LocalizationManager.Get("BALL_WIND_RESISTANCE"), template.windResistance);
    UpdateBallStatBar(rollName, rollBar, rollNumber,
        LocalizationManager.Get("BALL_ROLL"), template.roll);
    UpdateBallStatBar(spinName, spinBar, spinNumber,
        LocalizationManager.Get("BALL_SPIN"), template.spin);
}
```

---

## Sub-task H5: Editor Scripts

### H5a: BallThumbnailCardBuilder.cs

**File:** `Assets/Scripts/UI/Inventory/Editor/BallThumbnailCardBuilder.cs`
**MenuItem:** `GOLFIN/Setup/Ball Thumbnail Card Prefab`

Duplicates `CharacterThumbnailCardGlowUp.prefab` → `Assets/Prefabs/UI/Inventory/BallThumbnailCard.prefab`.
Same approach as `ClubThumbnailCardBuilder.cs`:
1. Copy the source prefab
2. Remove old component
3. Add `BallThumbnailCard` component
4. Wire shared fields: `portraitImage`, `nameText`, `selectionHighlight`, `backgroundImage`, `cardButton`
5. Wire `quantityText` to the existing level text GO (repurposed)
6. Hide/remove rarity badge GO entirely (or leave unwired)
7. Remove/hide status icon GOs (equipped, durability low) — or just leave them unwired

### H5b: BallDetailPanelAutoWire.cs

**File:** `Assets/Scripts/UI/Inventory/Editor/BallDetailPanelAutoWire.cs`
**MenuItem:** `GOLFIN/Wire/Ball Detail Panel`

Mirrors `ClubDetailPanelAutoWire.cs` but wires the ball-specific fields.

**NOTE:** The exact Transform paths depend on how the BallsContent panel is built.
Claude Code should check the actual hierarchy after the builder runs and adjust paths accordingly.

### H5c: BallManagerSetup.cs

**File:** `Assets/Scripts/UI/Inventory/Editor/BallManagerSetup.cs`
**MenuItem:** `GOLFIN/Setup/Ball Manager`

Mirrors `ClubManagerSetup.cs`. Creates a `BallManager` GO + a `BallDatabaseCSV` GO
in the scene, wires the Balls.csv TextAsset.

---

## Sub-task H6: Localization Keys

Add to the localization CSV:

| Key | EN | JP |
|-----|----|----|
| `BALL_OWNED` | `OWNED` | `所持` |
| `BALL_INFO` | `INFO` | `情報` |
| `BALL_POWER` | `POWER` | `パワー` |
| `BALL_REBOUND` | `REBOUND` | `リバウンド` |
| `BALL_WIND_RESISTANCE` | `WIND RESISTANCE` | `耐風` |
| `BALL_ROLL` | `ROLL` | `ロール` |
| `BALL_SPIN` | `SPIN` | `スピン` |

---

## Sub-task H7: Segmented Stat Bars (Visual Polish — can defer)

The mockup shows segmented/block-style bars rather than smooth fills. For initial implementation,
**use the same smooth fill bars as clubs** with just color changes (blue/red based on sign).
The segmented look can be added later as a visual polish pass — either by:
1. Overlaying a mask image with gaps (simple approach)
2. Using a HorizontalLayoutGroup with individual block Images
3. A custom UI shader

**Recommendation:** Start with smooth bars (H4 implementation above). Add segmented overlay mask
as a follow-up task once the functionality is working.

---

## Execution Order

1. **H1a–H1d** — Create `BallData.cs`, `BallDatabaseCSV.cs`, `BallManager.cs`, `Balls.csv`
2. **H5c** — Run `GOLFIN/Setup/Ball Manager` to create scene GOs
3. **H2** — Create `BallThumbnailCard.cs`
4. **H5a** — Run `GOLFIN/Setup/Ball Thumbnail Card Prefab`
5. **H3** — Create `BallCarouselController.cs`
6. **H4** — Create `BallDetailPanel.cs`
7. **H5b** — Create auto-wire script + run
8. **H6** — Add localization keys
9. Set Script Execution Order: `BallDatabaseCSV` before `BallManager`
10. Test: switch to Balls tab in Inventory, verify carousel + detail panel

---

## Script Execution Order

Add to Project Settings > Script Execution Order:
- `BallDatabaseCSV` — before `BallManager` (same gap as ClubDatabaseCSV before ClubManager)
- `BallManager` — default

---

## Open Questions

1. **Ball types** — The confluence doc mentions "Ball Types" as a filter concept but doesn't define any
   specific types. For M2, skip the filter bar. Can add later if ball categories emerge.
2. **Segmented bars** — Defer the visual segmentation to a polish pass (H7). Smooth fill + color is
   functional and correct for now.
3. **Ball Compare** — Defer to Phase H8. The COMPARE button can be wired but show a "Coming Soon" log
   for now.
4. **Default ball** — The Golfin ball has quantity `∞` (unlimited). Modeled as `quantity = -1`
   in `PlayerBallData`.

---

## Reminders

- Balls have NO rarity — no rarity badge, no rarity-colored backgrounds
- Balls have NO level — the level badge position is repurposed for quantity display
- Balls have NO equip/bag system — they're chosen in-game, not from inventory
- Stat values are on the TEMPLATE (BallDataRuntime), not computed from base + SP
- Stat range is -10 to +10
- The stat number display uses +/- prefix: "+10", "-6", "0"
- Bar color: blue for positive/zero, orange-red for negative
- Push to GitHub after completing
