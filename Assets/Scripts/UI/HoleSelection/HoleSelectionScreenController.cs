using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using GolfinRedux.UI;
using Golfin.Gameplay.Session;
using Golfin.Roster;
using Golfin.UI.GameplayTransition;
using Golfin.Utilities;

namespace GolfinRedux.UI.HoleSelection
{
    public enum CourseFilter { Lomond, Yaita }
    public enum TeeFilter    { Ladies, Front, Regular, Back }

    [System.Serializable]
    public class FilterPill
    {
        public Button button;
        public TextMeshProUGUI label;
        public Image background;
        public GameObject lockIcon; // shown only when locked == true
        public bool locked; // can't be selected
        public string filterId; // "Lomond" / "Yaita" / "Ladies" / "Front" / "Regular" / "Back"
    }

    /// <summary>
    /// Full-screen controller for the Hole Selection screen.
    /// Instantiates one HoleCard per HoleData in the database, wires up
    /// tap events, enforces the single-expanded invariant, and centres
    /// the expanded card in the scroll viewport.
    /// Also manages course/tee filter pill state and dynamic counts.
    /// </summary>
    public class HoleSelectionScreenController : MonoBehaviour
    {
        [Header("Filters")]
        [SerializeField] private GameObject filtersContainer;
        [SerializeField] private List<FilterPill> coursePills = new List<FilterPill>();
        [SerializeField] private List<FilterPill> teePills    = new List<FilterPill>();

        [Header("Cards List")]
        [SerializeField] private ScrollRect cardsScrollRect;
        [SerializeField] private RectTransform cardsContent;
        [SerializeField] private HoleCardController cardPrefab;

        [Header("Hole Database")]
        [SerializeField] private HoleDatabase holeDatabase;

        private readonly List<HoleCardController> _cards = new List<HoleCardController>();

        private CourseFilter _activeCourse = CourseFilter.Lomond;
        private TeeFilter    _activeTee    = TeeFilter.Ladies;

        // Pill colour rules per Figma + project convention (mirrors ClubFilterBar / InventoryScreenController):
        //   active selectable → gold gradient (TextGradients.ApplyGold)
        //   inactive selectable → silver gradient (TextGradients.ApplySilver)
        //   locked → silver gradient + lock icon visible
        // The two LADIES/FRONT pills in row 2 are independent toggles per spec, so "white active"
        // doesn't apply — they share the gold/silver convention with everything else.

        private void OnEnable()
        {
            // NOTE: We deliberately do NOT inject runtime dividers anymore. Cesar baked
            // the dividers into the filter row background sprites
            // (Background - Filter 1.png / Background - Filter 2.png) during his polish pass.
            // Injecting them at runtime here was duplicating those bars and visually fighting them.
            BuildFilterPillListeners();
            RebuildCards();
        }

        private void BuildFilterPillListeners()
        {
            foreach (var p in coursePills)
            {
                if (p == null || p.button == null) continue;
                p.button.onClick.RemoveAllListeners();
                if (!p.locked)
                {
                    var captured = p;
                    p.button.onClick.AddListener(() => OnCoursePillClicked(captured));
                }
                p.button.interactable = !p.locked;
            }

            foreach (var p in teePills)
            {
                if (p == null || p.button == null) continue;
                p.button.onClick.RemoveAllListeners();
                if (!p.locked)
                {
                    var captured = p;
                    p.button.onClick.AddListener(() => OnTeePillClicked(captured));
                }
                p.button.interactable = !p.locked;
            }

            UpdatePillVisuals();
            UpdatePillCounts();
        }

        private void OnCoursePillClicked(FilterPill pill)
        {
            if (pill == null || pill.locked) return;
            if (System.Enum.TryParse<CourseFilter>(pill.filterId, true, out var result))
                _activeCourse = result;
            UpdatePillVisuals();
            RebuildCards();
        }

        private void OnTeePillClicked(FilterPill pill)
        {
            if (pill == null || pill.locked) return;
            if (System.Enum.TryParse<TeeFilter>(pill.filterId, true, out var result))
                _activeTee = result;
            UpdatePillVisuals();
            // Tees don't filter the card list in this task (no per-tee data); just visual swap.
        }

        private void UpdatePillVisuals()
        {
            // IMPORTANT: do NOT overwrite the active pill's text colour at runtime.
            // Cesar's polish pass set the active text manually in the prefab/scene
            // (yellow per Figma). Calling TextGradients.ApplyGold here was clobbering it.
            // Only the locked / inactive states get a runtime restyle, and only via the
            // Silver gradient that Cesar explicitly approved. Active = leave the prefab
            // colour untouched. Word-wrap is also no longer overridden here — Cesar set
            // textWrappingMode = NoWrap on each label in the prefab.
            foreach (var p in coursePills)
            {
                if (p == null || p.label == null) continue;
                if (p.lockIcon != null) p.lockIcon.SetActive(p.locked);
                if (p.locked) { TextGradients.ApplySilver(p.label); continue; }
                bool isActive = string.Equals(p.filterId, _activeCourse.ToString(), System.StringComparison.OrdinalIgnoreCase);
                if (!isActive) TextGradients.ApplySilver(p.label);
                // active: keep prefab colour
            }
            foreach (var p in teePills)
            {
                if (p == null || p.label == null) continue;
                if (p.lockIcon != null) p.lockIcon.SetActive(p.locked);
                if (p.locked) { TextGradients.ApplySilver(p.label); continue; }
                bool isActive = string.Equals(p.filterId, _activeTee.ToString(), System.StringComparison.OrdinalIgnoreCase);
                if (!isActive) TextGradients.ApplySilver(p.label);
                // active: keep prefab colour
            }
        }

        /// <summary>
        /// Update the dynamic count fragments on each pill (e.g. "LOMOND 28/72").
        /// </summary>
        private void UpdatePillCounts()
        {
            // Per spec: filter count fragments are hardcoded literal strings (visual placeholders).
            // No dynamic calculation in this task — that's a future filter-wiring spec.
            foreach (var p in coursePills)
            {
                if (p == null || p.label == null) continue;
                if      (string.Equals(p.filterId, "Lomond", System.StringComparison.OrdinalIgnoreCase)) p.label.text = "LOMOND 28/72";
                else if (string.Equals(p.filterId, "Yaita",  System.StringComparison.OrdinalIgnoreCase)) p.label.text = "YAITA - KIKYOU";
            }

            foreach (var p in teePills)
            {
                if (p == null || p.label == null) continue;
                if      (p.filterId == "Ladies")  p.label.text = "LADIES 18/18";
                else if (p.filterId == "Front")   p.label.text = "FRONT 10/18";
                else if (p.filterId == "Regular") p.label.text = "REGULAR 0/18";
                else if (p.filterId == "Back")    p.label.text = "BACK 0/18";
            }
        }

        private void RebuildCards()
        {
            HoleDatabase db = holeDatabase;
            if (db == null) db = HoleDatabaseLoader.RuntimeDatabase;

            if (db == null)
            {
                Debug.LogWarning("[HoleSelection] No hole database available — screen will be empty.");
                return;
            }

            // Clear prior children and event subscriptions
            foreach (var c in _cards)
            {
                if (c == null) continue;
                c.OnCardTapped -= HandleCardTapped;
                c.OnActionButtonClicked -= HandleActionClicked;
            }
            foreach (Transform child in cardsContent) Destroy(child.gameObject);
            _cards.Clear();

            // For Yaita filter: no holes (no Yaita data exists)
            if (_activeCourse == CourseFilter.Yaita)
            {
                if (cardsScrollRect != null) cardsScrollRect.verticalNormalizedPosition = 1f;
                return;
            }

            // Lomond filter: all 18 holes
            var holes = new List<HoleData>(db.holes);
            holes.Sort((a, b) => a.holeNumber.CompareTo(b.holeNumber));

            HoleCardController nextCard = null;

            foreach (var hole in holes)
            {
                if (cardPrefab == null)
                {
                    Debug.LogError("[HoleSelection] cardPrefab is null — cannot instantiate cards.");
                    break;
                }

                HoleCardController card = Instantiate(cardPrefab, cardsContent);

                HoleCardMode mode = HoleProgressionService.Instance.HasPlayed(hole.holeNumber)
                    ? HoleCardMode.Replay
                    : HoleCardMode.Play;

                HoleCardState state = !HoleProgressionService.Instance.IsUnlocked(hole.holeNumber)
                    ? HoleCardState.Locked
                    : HoleCardState.Collapsed;

                card.Bind(hole, mode, state);
                card.OnCardTapped += HandleCardTapped;
                card.OnActionButtonClicked += HandleActionClicked;

                _cards.Add(card);

                // "Next" hole = first unlocked, not-yet-played card. Remember it so we
                // can auto-expand it after the layout pass settles.
                if (nextCard == null && mode == HoleCardMode.Play && state == HoleCardState.Collapsed)
                    nextCard = card;
            }

            if (nextCard != null)
            {
                nextCard.SetState(HoleCardState.Expanded);
                StartCoroutine(CentreCardNextFrame(nextCard));
            }
            else if (cardsScrollRect != null)
            {
                cardsScrollRect.verticalNormalizedPosition = 1f;
            }
        }

        private void OnDisable()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.OnCardTapped -= HandleCardTapped;
                card.OnActionButtonClicked -= HandleActionClicked;
            }
            _cards.Clear();
        }

        private void HandleCardTapped(HoleCardController card)
        {
            if (card == null) return;

            // Belt-and-suspenders: ignore locked cards
            if (card.State == HoleCardState.Locked) return;

            if (card.State == HoleCardState.Expanded)
            {
                card.SetState(HoleCardState.Collapsed);
                return;
            }

            // Collapse the currently-expanded card (if any)
            foreach (var c in _cards)
            {
                if (c != null && c != card && c.State == HoleCardState.Expanded)
                    c.SetState(HoleCardState.Collapsed);
            }

            card.SetState(HoleCardState.Expanded);
            StartCoroutine(CentreCardNextFrame(card));
        }

        private void HandleActionClicked(HoleCardController card)
        {
            // Practice path: seed the session directly and launch gameplay — no matchmaking modal.
            // Explicitly clear the versus flag so the solo HUD is used.
            // holeNumber is 1-based; GameSession.SeedSession expects the 1-based hole number.
            GameSession.IsVersus = false;

            int holeNumber = card.HoleNumber;

            string charId = CharacterManager.Instance != null
                ? CharacterManager.Instance.GetSelectedCharacterId()
                : string.Empty;
            int bagSlot = BagManager.Instance != null
                ? BagManager.Instance.EquippedBagSlot
                : 0;

            GameSession.SeedSession(holeNumber, charId, bagSlot);
#if UNITY_EDITOR
            Debug.Log($"[Practice] GameSession seeded — Hole={holeNumber}, CharacterId='{charId}', BagSlot={bagSlot}");
#endif

            var loader = GameplaySceneLoader.Instance;
            if (loader != null)
            {
                loader.BeginGameplayLoad(holeNumber);
            }
            else
            {
                Debug.LogError("[HoleSelection] GameplaySceneLoader.Instance is null — " +
                               "gameplay scene will not load. Verify GameplaySceneLoader is wired in ShellScene.");
            }
        }

        private IEnumerator CentreCardNextFrame(HoleCardController card)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            if (cardsScrollRect == null || card == null || card.rootRect == null) yield break;

            var content  = cardsScrollRect.content;
            var viewport = cardsScrollRect.viewport;
            var cardRt   = card.rootRect;

            // Position of card's centre in content-local space, measured from content top.
            float cardCentreFromTop = -cardRt.anchoredPosition.y + cardRt.rect.height * 0.5f;
            float scrollableHeight  = content.rect.height - viewport.rect.height;
            if (scrollableHeight <= 0f) yield break;

            float targetCentreFromTop = cardCentreFromTop - viewport.rect.height * 0.5f;
            float normalized = Mathf.Clamp01(1f - targetCentreFromTop / scrollableHeight);
            cardsScrollRect.verticalNormalizedPosition = normalized;
        }
    }
}
