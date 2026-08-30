#nullable enable
using System.Collections.Generic;
using Golfin.Gameplay.Missions;
using Golfin.Utilities;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.MissionSelection
{
    public enum MissionCardState { Collapsed, Expanded, Locked }
    public enum MissionCardMode  { Play, Replay, Daily }

    /// <summary>
    /// One mission card. Spec: missions_v1 §C2/§C3.
    ///
    /// CLONED FROM <c>HoleCardController</c> and deliberately shaped the same: the same three
    /// states, the same collapsed/expanded container swap, the same reward-slot arrays, the
    /// same "parent enforces the single-expanded invariant" contract. A mission card and a hole
    /// card are the same object with different words on it, and two implementations of that
    /// would drift the moment either was polished.
    ///
    /// WHAT IT ADDS over the hole card, and why each one is here:
    ///
    ///   THE GOAL BULLETS. A hole card describes a hole; a mission card describes a RULE, and
    ///   the rule is the entire reason to play it. One line per goal, plus the two the mockup
    ///   does not have — wind and clubs — because those are what actually set the difficulty
    ///   and a player who cannot see them is being asked to guess.
    ///
    ///   THE WARNING STATE (§C3). A mission whose bag cannot be assembled — an `own:` mask that
    ///   empties the player's bag, a `supplied:` loadout naming a club nobody makes — renders
    ///   with PLAY DISABLED and the reason on the card. The standing invariant is that a client
    ///   missing information never shows a broken card; this is the half of it that only the
    ///   client can enforce, because whether YOUR bag survives a ban mask is not something a
    ///   publish validator can know.
    ///
    /// Every string is resolved imperatively at Bind() and re-resolved on the language-changed
    /// event, for the reason HoleCardController spells out: the Settings OVERLAY leaves the
    /// screen enabled underneath, so nothing re-enables the card and stale text would survive a
    /// language switch until the screen was re-entered.
    /// </summary>
    public class MissionCardController : MonoBehaviour
    {
        // ── Layout ──────────────────────────────────────────────────────────────
        [Header("Layout")]
        [SerializeField] public RectTransform rootRect = null!;
        [SerializeField] private GameObject? collapsedContainer;
        [SerializeField] private GameObject? expandedContainer;

        // ── Text ────────────────────────────────────────────────────────────────
        [Header("Text — Collapsed")]
        [SerializeField] private TextMeshProUGUI? titleTextCollapsed;     // NEXT / REPLAY / LOCKED pill
        [SerializeField] private TextMeshProUGUI? subtitleTextCollapsed;  // "9 - Sand Save - Hole 8"

        [Header("Text — Expanded")]
        [SerializeField] private TextMeshProUGUI? titleTextExpanded;
        [SerializeField] private TextMeshProUGUI? subtitleTextExpanded;
        [SerializeField] private TextMeshProUGUI? courseLineText;         // "Lomond Country Club - Hole 8"

        // ── Hole map + the start marker (NEW) ───────────────────────────────────
        [Header("Hole Map")]
        [SerializeField] private Image? holeImage;
        [SerializeField] private RectTransform? startMarker;

        // ── Goal / wind / loadout lines (NEW) ───────────────────────────────────
        [Header("Goals")]
        [SerializeField] private TextMeshProUGUI[] goalLines = new TextMeshProUGUI[3];
        [SerializeField] private TextMeshProUGUI? windLine;
        [SerializeField] private TextMeshProUGUI? loadoutLine;
        [SerializeField] private TextMeshProUGUI? startLine;

        // ── Rewards ─────────────────────────────────────────────────────────────
        [Header("Rewards — Collapsed")]
        [SerializeField] private GameObject[] collapsedRewardSlots = new GameObject[4];
        [SerializeField] private Image[] collapsedRewardIcons = new Image[4];
        [SerializeField] private TextMeshProUGUI[] collapsedRewardAmounts = new TextMeshProUGUI[4];

        [Header("Rewards — Expanded")]
        [SerializeField] private GameObject[] expandedRewardSlots = new GameObject[4];
        [SerializeField] private Image[] expandedRewardIcons = new Image[4];
        [SerializeField] private TextMeshProUGUI[] expandedRewardAmounts = new TextMeshProUGUI[4];

        [Header("Reward Icons")]
        [SerializeField] private Sprite? pointsIcon;
        [SerializeField] private Sprite? repairKitIcon;
        [SerializeField] private Sprite? ballIcon;
        [SerializeField] private Sprite? ticketIcon;

        // ── Action button ───────────────────────────────────────────────────────
        [Header("Action Button")]
        [SerializeField] private Button? actionButton;
        [SerializeField] private TextMeshProUGUI? actionButtonLabel;
        [SerializeField] private Sprite? playButtonSprite;
        [SerializeField] private Sprite? replayButtonSprite;

        // ── Interaction / overlays ──────────────────────────────────────────────
        [Header("Interaction")]
        [SerializeField] private Button? cardTapButton;
        [SerializeField] private GameObject? lockedOverlay;
        [SerializeField] private GameObject? chevronCollapsed;
        [SerializeField] private GameObject? chevronExpanded;
        [SerializeField] private GameObject? lockIconCollapsed;

        // ── Warning (§C3) ───────────────────────────────────────────────────────
        [Header("Warning")]
        [SerializeField] private GameObject? warningContainer;
        [SerializeField] private TextMeshProUGUI? warningText;

        // ── Daily (§C2) ─────────────────────────────────────────────────────────
        [Header("Daily")]
        [SerializeField] private GameObject? dailyHeaderTint;
        [SerializeField] private TextMeshProUGUI? dailyCountdownText;
        /// <summary>
        /// The streak, drawn as the SHARED flame badge (daily_mission_home_pill §3) rather than
        /// the sentence "{0} day streak" it used to be. One prefab, so the Home pill and this
        /// card can never render the same number two different ways.
        /// </summary>
        [SerializeField] private Golfin.UI.Common.StreakFlameView? streakFlame;
        [SerializeField] private Golfin.UI.Common.StreakFlameView? streakFlameExp;
        [SerializeField] private TextMeshProUGUI? dailyCountdownTextExp;
        [SerializeField] private TextMeshProUGUI? rulesHeader;

        /// <summary>Last streak we were told about, so expand/collapse cannot disagree with it.</summary>
        private int _dailyStreak;

        // ── State ───────────────────────────────────────────────────────────────
        public MissionDefinition? Mission { get; private set; }
        public MissionCardMode Mode { get; private set; }
        public MissionCardState State { get; private set; }

        /// <summary>Un-playable: the bag could not be assembled, or the start is not baked.
        /// PLAY is disabled and <see cref="warningText"/> says why (§C3).</summary>
        public bool IsPlayable { get; private set; } = true;

        public event System.Action<MissionCardController>? OnCardTapped;
        public event System.Action<MissionCardController>? OnActionButtonClicked;

        private string _warning = "";

        private void Awake()
        {
            if (cardTapButton != null)
            {
                // Same fix, same reason, as HoleCardController: the tap button is saved as the
                // LAST child, so it renders and raycasts ON TOP of the action button and eats
                // every PLAY tap. Push it to the bottom of the sibling stack.
                cardTapButton.transform.SetAsFirstSibling();
                cardTapButton.onClick.AddListener(() => OnCardTapped?.Invoke(this));
            }
            if (actionButton != null)
                actionButton.onClick.AddListener(() => OnActionButtonClicked?.Invoke(this));
        }

        private void OnEnable()  => LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

        // ── Bind ────────────────────────────────────────────────────────────────

        public void Bind(MissionDefinition mission, MissionCardMode mode, MissionCardState state,
                         string warning = "")
        {
            if (mission == null) return;
            Mission = mission;
            Mode = mode;
            _warning = warning ?? "";
            IsPlayable = string.IsNullOrEmpty(_warning);

            RefreshLocalizedText();
            BindHoleMap(mission);
            BindRewards(mission, mode);
            SetState(state);
        }

        /// <summary>
        /// Re-resolve every string against the current language, IN PLACE. Deliberately not
        /// Bind(): Bind ends in SetState with the state it was first given, which would snap an
        /// expanded card shut on every language switch.
        /// </summary>
        private void RefreshLocalizedText()
        {
            var m = Mission;
            if (m == null) return;

            // Header pill — the three states plus the daily's own.
            string titleStr;
            bool silver;
            if (Mode == MissionCardMode.Daily)         { titleStr = LocalizationManager.Get("MISSION_PILL_DAILY");  silver = false; }
            else if (State == MissionCardState.Locked) { titleStr = LocalizationManager.Get("MISSION_PILL_LOCKED"); silver = true;  }
            else if (Mode == MissionCardMode.Replay)   { titleStr = LocalizationManager.Get("MISSION_PILL_REPLAY"); silver = true;  }
            else                                       { titleStr = LocalizationManager.Get("MISSION_PILL_NEXT");   silver = false; }

            ApplyTitle(titleTextCollapsed, titleStr, silver);
            ApplyTitle(titleTextExpanded,  titleStr, silver);

            // "{order} - {name} - Hole {n}" per the Figma fidelity table.
            string name = string.IsNullOrEmpty(m.NameKey) ? "" : LocalizationManager.Get(m.NameKey);
            string subtitle = Mode == MissionCardMode.Daily
                ? $"{LocalizationManager.Get("MISSION_COURSE_LOMOND")} - {LocalizationManager.Get("MISSION_HOLE")} {m.HoleNumber}"
                : $"{m.Order} - {name} - {LocalizationManager.Get("MISSION_HOLE")} {m.HoleNumber}";
            if (subtitleTextCollapsed != null) subtitleTextCollapsed.text = subtitle;
            if (subtitleTextExpanded  != null) subtitleTextExpanded.text  = subtitle;

            // The daily card's SUBTITLE is already "<course> - Hole n" (there is no mission order
            // or name to show), so drawing the course line under it repeats the same sentence
            // twice. Campaign cards keep it: their subtitle is "3 - Chip and Run - Hole 6", which
            // never names the course.
            if (courseLineText != null)
            {
                bool duplicate = Mode == MissionCardMode.Daily;
                courseLineText.gameObject.SetActive(!duplicate);
                if (!duplicate)
                    courseLineText.text = $"{LocalizationManager.Get("MISSION_COURSE_LOMOND")} - " +
                                          $"{LocalizationManager.Get("MISSION_HOLE")} {m.HoleNumber}";
            }

            BindGoalLines(m);

            if (actionButtonLabel != null)
                actionButtonLabel.text = Mode == MissionCardMode.Replay
                    ? LocalizationManager.Get("MISSION_REPLAY")
                    : LocalizationManager.Get("MISSION_PLAY");

            if (warningText != null && !IsPlayable) warningText.text = _warning;
        }

        /// <summary>
        /// The goal bullets, plus the wind and clubs lines the mockup does not have.
        ///
        /// Goal text is TEMPLATED from (type, param) — `GOAL_SHOTS` = "Hole out in {0} strokes
        /// or fewer" — so the copy can never disagree with the rule the evaluator actually
        /// applies. SCORE is the one type with a key per value, because "Score par or better"
        /// reads nothing like "Score bogey or better".
        /// </summary>
        private void BindGoalLines(MissionDefinition m)
        {
            for (int i = 0; i < goalLines.Length; i++)
            {
                var line = goalLines[i];
                if (line == null) continue;
                if (i >= m.Goals.Count)
                {
                    line.gameObject.SetActive(false);
                    continue;
                }
                line.gameObject.SetActive(true);
                line.text = Bullet(GoalLineText(m.Goals[i]));
            }

            // A heading, so the block reads as the mission's terms rather than loose sentences.
            if (rulesHeader != null) rulesHeader.text = LocalizationManager.Get("MISSION_RULES");

            // The last three lines are CONDITIONS, not objectives, and were indistinguishable
            // from the goals above them: "Ladies tee" and "Light tailwind" read like things to
            // achieve. Naming the axis fixes that in three words.
            if (windLine != null)
                windLine.text = Labelled("MISSION_LABEL_WIND", m.WindKey);

            if (loadoutLine != null)
                loadoutLine.text = Labelled("MISSION_LABEL_GEAR", m.LoadoutKey);

            // WHERE the ball starts, in words. This carries the information the start MARKER
            // was meant to — and carries it exactly, which the marker could not (see
            // MissionSelectionScreenController.PlaceStartMarker for why it is off).
            if (startLine != null)
                startLine.text = Labelled("MISSION_LABEL_START", m.StartAreaKey);
        }

        /// <summary>
        /// Re-dress a bound card as the VERDICT on a mission just played, for the Hole Complete
        /// modal. The modal shows this same card rather than a result card of its own, so a player
        /// reads the mission they attempted in the layout they chose it from.
        ///
        /// Only the pill and the rule marks change: the hole, the map, the wind, the gear and the
        /// reward are the mission's, and they are as true after the round as before it.
        /// </summary>
        public void ShowResult(MissionResult result)
        {
            if (result == null) return;

            bool cleared = result.Cleared;
            string pill = LocalizationManager.Get(cleared ? "MISSION_PILL_SUCCESS" : "MISSION_PILL_FAILED");
            var colour = cleared ? new Color32(0x50, 0xC8, 0x78, 0xFF)    // the modal's own SUCCESS green
                                 : new Color32(0xD1, 0x6A, 0x47, 0xFF);  // ...and its FAILED red
            foreach (var label in new[] { titleTextCollapsed, titleTextExpanded })
            {
                if (label == null) continue;
                label.text = pill;
                label.color = colour;
            }

            // Each rule gets the verdict it earned, in place of its bullet. A goal the evaluator
            // never decided (Met == null, when the hole ended before it could settle) reads as
            // unmet: the mission was not cleared, and a bare line would look like a lost record.
            for (int i = 0; i < goalLines.Length && i < result.Goals.Count; i++)
            {
                var line = goalLines[i];
                if (line == null || !line.gameObject.activeSelf) continue;
                bool met = result.Goals[i].Met == true;
                line.text = (met ? "\u2713  " : "\u2717  ") + GoalLineText(result.Goals[i]);
                line.color = met ? new Color32(0x50, 0xC8, 0x78, 0xFF)
                                 : new Color32(0xD1, 0x6A, 0x47, 0xFF);
            }
        }

        /// <summary>A rule line. The bullet is text, not a layout child: the lines are a plain
        /// VerticalLayoutGroup of TMP fields and a glyph column would have to be kept in step
        /// with which of them are active.</summary>
        private static string Bullet(string body) =>
            string.IsNullOrEmpty(body) ? "" : "\u2022  " + body;

        /// <summary>A rule line that names its axis — "Start: Ladies tee".</summary>
        private static string Labelled(string labelKey, string valueKey) =>
            string.IsNullOrEmpty(valueKey)
                ? ""
                : Bullet(LocalizationManager.Get(labelKey) + ": " + LocalizationManager.Get(valueKey));

        /// <summary>One goal as a sentence. The param is substituted, and a SURFACE or CLUB
        /// param is itself localized — "Never land in the bunker", not "…in the Bunker".</summary>
        public static string GoalLineText(MissionGoal goal)
        {
            string template = LocalizationManager.Get(goal.TextKey);
            if (string.IsNullOrEmpty(goal.Param)) return template;

            string param = goal.Param;
            string key = ParamKey(goal);
            if (!string.IsNullOrEmpty(key))
            {
                string localized = LocalizationManager.Get(key);
                // Get() returns the KEY when it does not know one; falling back to the raw
                // param beats printing "SURFACE_FAIRWAY" on a card.
                if (localized != key) param = localized;
            }
            return template.Replace("{0}", param);
        }

        private static string ParamKey(MissionGoal goal)
        {
            switch (goal.Type)
            {
                case MissionGoalType.AVOID:
                case MissionGoalType.LAND_TEE:
                case MissionGoalType.LAND_ANY:
                    return "SURFACE_" + goal.Param.Replace(".", "").Replace("&", "_AND_").ToUpperInvariant();
                case MissionGoalType.USE_CLUB:
                case MissionGoalType.AVOID_CLUB:
                    return "CLUBTYPE_" + goal.Param.ToUpperInvariant();
                default:
                    return "";
            }
        }

        private void BindHoleMap(MissionDefinition m)
        {
            if (holeImage == null) return;
            Sprite? img = Resources.Load<Sprite>($"HoleImages/lomond-country-club/Hole_{m.HoleNumber:D2}");
            if (img == null) img = Resources.Load<Sprite>("HoleImages/Missing");
            if (img != null) holeImage.sprite = img;

            // The marker is hidden by the screen controller — there is no calibration that
            // maps a world point onto these pre-rendered thumbnails. The start is on the card
            // in words instead.
            if (startMarker != null) startMarker.gameObject.SetActive(false);
        }

        // ── Rewards ─────────────────────────────────────────────────────────────

        /// <summary>
        /// RP first, then the item rewards. An UNCLEARED mission advertises its first-clear
        /// amount; a cleared one advertises the replay amount, because that is what another run
        /// is now worth — showing the first-clear number on a card that can no longer pay it
        /// would be the card lying about money.
        /// </summary>
        private void BindRewards(MissionDefinition m, MissionCardMode mode)
        {
            var rewards = new List<(Sprite? icon, string amount)>();
            int rp = mode == MissionCardMode.Replay ? m.ReplayRP : m.FirstClearRP;
            if (rp > 0) rewards.Add((pointsIcon, $"x{rp}"));

            // Item rewards only ever land on a FIRST clear — a replay pays RP alone.
            if (mode != MissionCardMode.Replay)
                foreach (var (kind, qty) in ParseItemRewards(m.ItemRewards))
                    rewards.Add((IconFor(kind), $"x{qty}"));

            Fill(collapsedRewardSlots, collapsedRewardIcons, collapsedRewardAmounts, rewards);
            Fill(expandedRewardSlots,  expandedRewardIcons,  expandedRewardAmounts,  rewards);
        }

        /// <summary>`"RepairKit x1"` / `"GoldTicket x2"` → (kind, qty). Blank yields nothing.</summary>
        public static List<(string kind, int qty)> ParseItemRewards(string raw)
        {
            var outList = new List<(string, int)>();
            if (string.IsNullOrWhiteSpace(raw)) return outList;
            foreach (string part in raw.Split(','))
            {
                string[] bits = part.Trim().Split('x');
                if (bits.Length < 2) continue;
                string kind = bits[0].Trim();
                if (kind.Length == 0) continue;
                if (!int.TryParse(bits[bits.Length - 1].Trim(), out int qty)) qty = 1;
                outList.Add((kind, qty));
            }
            return outList;
        }

        private Sprite? IconFor(string kind)
        {
            if (kind.IndexOf("Ticket", System.StringComparison.OrdinalIgnoreCase) >= 0) return ticketIcon;
            if (kind.IndexOf("Repair", System.StringComparison.OrdinalIgnoreCase) >= 0) return repairKitIcon;
            if (kind.IndexOf("Ball",   System.StringComparison.OrdinalIgnoreCase) >= 0) return ballIcon;
            return pointsIcon;
        }

        private static void Fill(GameObject[] slots, Image[] icons, TextMeshProUGUI[] amounts,
                                 List<(Sprite? icon, string amount)> rewards)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                bool used = i < rewards.Count;
                if (slots[i] != null) slots[i].SetActive(used);
                if (!used) continue;
                if (i < icons.Length && icons[i] != null && rewards[i].icon != null)
                    icons[i].sprite = rewards[i].icon;
                if (i < amounts.Length && amounts[i] != null)
                    amounts[i].text = rewards[i].amount;
            }
        }

        // ── State ───────────────────────────────────────────────────────────────

        public void SetState(MissionCardState state)
        {
            State = state;
            bool isLocked   = state == MissionCardState.Locked;
            bool isExpanded = state == MissionCardState.Expanded;

            if (collapsedContainer != null) collapsedContainer.SetActive(!isExpanded);
            if (expandedContainer  != null) expandedContainer.SetActive(isExpanded);
            if (lockedOverlay      != null) lockedOverlay.SetActive(isLocked);
            if (cardTapButton      != null) cardTapButton.interactable = !isLocked;

            float alpha = isLocked ? 0.4f : 1f;
            ApplyAlpha(collapsedRewardIcons, collapsedRewardAmounts, alpha);
            ApplyAlpha(expandedRewardIcons,  expandedRewardAmounts,  alpha);

            if (chevronCollapsed  != null) chevronCollapsed.SetActive(!isLocked);
            if (lockIconCollapsed != null) lockIconCollapsed.SetActive(isLocked);

            // §C3 — a card that cannot be played says so, and its PLAY is dead. Never a card
            // that looks live and drops the player into a hole with no clubs.
            bool warn = !IsPlayable && !isLocked;
            if (warningContainer != null) warningContainer.SetActive(warn);
            if (actionButton != null) actionButton.interactable = !isLocked && IsPlayable;

            // Daily chrome — off for every campaign card.
            //
            // The streak used to be decided in TWO places that disagreed: here it was shown for
            // any daily card, and in SetDailyStatus it was shown only when streak > 0. Expanding
            // re-runs this method, so the chip blinked on and off as the card opened and closed.
            // One rule now, applied from one place, to both the collapsed and expanded copies.
            bool daily = Mode == MissionCardMode.Daily;
            if (dailyHeaderTint != null) dailyHeaderTint.SetActive(daily);
            ApplyDailyChrome(daily);

            RefreshLocalizedText();

            if (rootRect != null) LayoutRebuilder.ForceRebuildLayoutImmediate(rootRect);
        }

        /// <summary>
        /// The one place that decides whether the daily chrome is on screen, so expanding a card
        /// can never contradict what binding it decided.
        ///
        /// The countdown and the streak are shown on BOTH the collapsed and the expanded copy.
        /// They used to exist only on the collapsed one, so opening the card threw away the two
        /// pieces of information that are unique to the daily.
        ///
        /// A streak of zero is not a streak, so it is hidden rather than reading "0 day streak".
        /// </summary>
        private void ApplyDailyChrome(bool daily)
        {
            if (dailyCountdownText    != null) dailyCountdownText.gameObject.SetActive(daily);
            if (dailyCountdownTextExp != null) dailyCountdownTextExp.gameObject.SetActive(daily);

            // The badge owns the zero rule (StreakFlameView): passing 0 hides it. Passing the
            // streak only when this is a daily keeps a campaign card free of daily chrome.
            int shown = daily ? _dailyStreak : 0;
            if (streakFlame    != null) streakFlame.SetStreak(shown);
            if (streakFlameExp != null) streakFlameExp.SetStreak(shown);
        }

        /// <summary>Countdown to UTC midnight + the streak, on the daily card only.</summary>
        public void SetDailyStatus(System.TimeSpan untilReset, int streak, bool claimed)
        {
            string resetLine = LocalizationManager.Get("MISSION_DAILY_RESETS")
                .Replace("{0}", $"{(int)untilReset.TotalHours:00}:{untilReset.Minutes:00}:{untilReset.Seconds:00}");
            if (dailyCountdownText    != null) dailyCountdownText.text    = resetLine;
            if (dailyCountdownTextExp != null) dailyCountdownTextExp.text = resetLine;

            _dailyStreak = streak;
            ApplyDailyChrome(Mode == MissionCardMode.Daily);

            if (claimed && actionButtonLabel != null)
            {
                actionButtonLabel.text = LocalizationManager.Get("MISSION_DAILY_CLEARED");
                if (actionButton != null) actionButton.interactable = false;
            }
        }

        /// <summary>
        /// Place the start marker over the hole thumbnail, at a 0..1 position IN SPRITE SPACE.
        ///
        /// Sprite space, not rect space — the two are not the same and assuming they were put
        /// the first marker in open water beside the hole. `holeImage` is 300×280 with
        /// preserveAspect on, and the art is 103×400: the drawing is letterboxed to a 72×280
        /// column down the middle, so it only occupies u 0.38–0.62 of the rect. A caller that
        /// says "69% across the hole" means 69% across the DRAWING.
        /// </summary>
        public void SetStartMarkerNormalised(Vector2 normalised)
        {
            if (startMarker == null || holeImage == null) return;

            Vector2 uv = normalised;
            var sprite = holeImage.sprite;
            if (holeImage.preserveAspect && sprite != null)
            {
                Rect  r  = holeImage.rectTransform.rect;
                float sa = sprite.rect.width / sprite.rect.height;   // sprite aspect
                float ra = r.width / r.height;                       // rect aspect
                float dw, dh;
                if (sa < ra) { dh = r.height; dw = dh * sa; }        // tall art: height-limited
                else         { dw = r.width;  dh = dw / sa; }
                // The fitted drawing is centred, so scale about the middle.
                uv = new Vector2(
                    0.5f + (normalised.x - 0.5f) * (dw / r.width),
                    0.5f + (normalised.y - 0.5f) * (dh / r.height));
            }

            startMarker.gameObject.SetActive(true);
            startMarker.anchorMin = startMarker.anchorMax = uv;
            startMarker.anchoredPosition = Vector2.zero;
        }

        public void HideStartMarker()
        {
            if (startMarker != null) startMarker.gameObject.SetActive(false);
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        private static void ApplyTitle(TextMeshProUGUI? label, string text, bool silver)
        {
            if (label == null) return;
            label.text = text;
            if (silver) TextGradients.ApplySilver(label);
        }

        private static void ApplyAlpha(Image[] icons, TextMeshProUGUI[] amounts, float alpha)
        {
            foreach (var i in icons)
                if (i != null) { var c = i.color; c.a = alpha; i.color = c; }
            foreach (var t in amounts)
                if (t != null) { var c = t.color; c.a = alpha; t.color = c; }
        }
    }
}
