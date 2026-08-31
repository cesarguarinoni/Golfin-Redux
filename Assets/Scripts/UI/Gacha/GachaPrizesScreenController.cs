// Assets/Scripts/UI/Gacha/GachaPrizesScreenController.cs
// gacha_prizes Stage 1 — Controller for the Gacha Prizes screen.
// Dual mode (x1 / x10) parameterised by the pending pull RESULT (set before ShowScreen).
// gacha_reveal_animation §1/§3 — the screen now binds the exact prize list the reveal modal
// just showed (SetPendingResult), PULL means "pull again" through GachaPullFlow, and the cards
// stagger in when the screen was opened by a pull (never on a BACK-navigation return).
// x10: shows 4/4/2 card grid with all 10 prizes of the result.
// x1:  hides the three prize rows, shows the x1CardSlot with ONE centred card.
// BACK → ScreenId.GeneralShop (history-aware). PULL → pull again via GachaPullFlow.
#nullable enable
using System.Collections;
using System.Collections.Generic;
using Golfin.Inventory;
using Golfin.Roster;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Attached to the root of GachaPrizesScreen.prefab (scene instance).
    /// Binds 10 mock prize cards via BagClubCard.Initialize(), mirrors the
    /// GachaHistoryRow binding pattern (display-only, all buttons non-interactable).
    /// </summary>
    public class GachaPrizesScreenController : MonoBehaviour
    {
        // ── Static pending context (set by caller before ShowScreen) ───────────

        // The last pull's prizes. NOT cleared on read: a BACK-navigation return to this screen
        // must re-bind the SAME result it showed before, not a freshly rolled one.
        // Empty until a pull happens. It used to be seeded with ten mock prizes so the screen
        // rendered something when opened directly; there is no mock any more, and a screen showing
        // prizes nobody won is exactly what this task removed.
        private static IReadOnlyList<PrizeRecord> s_result = System.Array.Empty<PrizeRecord>();

        // Consumed once by the next OnEnable — only a real pull earns the entrance animation.
        private static bool s_pendingEntrance;

        /// <summary>
        /// Set this BEFORE calling ScreenManager.ShowScreen(ScreenId.GachaPrizes).
        /// The list length picks the mode: 1 → x1 (single centred card), otherwise x10 (4/4/2 grid).
        /// Also arms the staggered card entrance for the next open.
        /// </summary>
        public static void SetPendingResult(IReadOnlyList<PrizeRecord> result)
        {
            if (result == null || result.Count == 0)
            {
                Debug.LogWarning("[GachaPrizesScreenController] SetPendingResult got an empty result — keeping the previous one.");
                return;
            }

            s_result = result;
            s_pendingEntrance = true;
        }

        // ── Inspector refs ─────────────────────────────────────────────────────

        [Header("Grid rows (x10 mode)")]
        [SerializeField] private GameObject? _prizeRow1;   // 4 cards
        [SerializeField] private GameObject? _prizeRow2;   // 4 cards
        [SerializeField] private GameObject? _prizeRow3;   // 2 cards

        [Header("x1 mode — centred single card")]
        [SerializeField] private GameObject? _x1CardSlot;  // container with LayoutElement.preferredHeight=1170
        [SerializeField] private BagClubCard? _x1Card;     // the single centred BagClubCard

        [Header("Labels")]
        [SerializeField] private TMP_Text? _costMultiLabel;   // "x10" / "x1"  (CostRow > x10Label)
        [SerializeField] private TMP_Text? _pullButtonLabel;  // "PULL x10" / "PULL x1" (PullButton > PlayLable)

        [Header("Buttons")]
        [SerializeField] private Button? _pullButton;
        [SerializeField] private Button? _backButton;

        // ── Runtime ───────────────────────────────────────────────────────────

        /// <summary>
        /// The ten authored grid SLOTS — the parent transform of each card, not the card itself.
        ///
        /// <para>
        /// The prefab ships ten <c>BagClubCard</c> children, one per slot, and that was fine while
        /// every prize was a club. A pull can now pay a ball, an item or a ticket, which is a
        /// different prefab family — so the authored card is kept as the slot's CLUB card and a
        /// non-club prize gets its own card instantiated INTO the same slot transform. The ten
        /// slot rects, their positions and the row layout are untouched (SPEC §4.3: keep the slot
        /// transforms, parent the right prefab under each).
        /// </para>
        /// </summary>
        private Transform[] _gridSlots = System.Array.Empty<Transform>();

        /// <summary>The authored club card of each slot, hidden when that slot shows another kind.</summary>
        private BagClubCard[] _gridClubCards = System.Array.Empty<BagClubCard>();

        /// <summary>The non-club card spawned into each slot, if any. Destroyed on re-bind.</summary>
        private readonly Dictionary<int, GameObject> _spawnedCards = new Dictionary<int, GameObject>();

        // ── Lifecycle ──────────────────────────────────────────────────────────

        private void Awake()
        {
            CollectGridCards();

            if (_backButton != null)
            {
                _backButton.onClick.RemoveAllListeners();
                _backButton.onClick.AddListener(OnBack);
            }

            if (_pullButton != null)
            {
                _pullButton.onClick.RemoveAllListeners();
                _pullButton.onClick.AddListener(OnPull);
            }
        }

        private void OnEnable()
        {
            // The mode is DERIVED from the result rather than carried alongside it, so the
            // labels and the grid can never disagree about what was pulled.
            int pullCount = s_result.Count == 1 ? 1 : 10;
            bool playEntrance = s_pendingEntrance;
            s_pendingEntrance = false;

            _pullCount = pullCount;
            ApplyMode(pullCount);

            if (playEntrance) _entranceRoutine = StartCoroutine(PlayEntrance());

            // The PULL label is resolved imperatively here, so — unlike a LocalizedText label —
            // nothing repaints it when the language changes. The toggle lives in the Settings
            // OVERLAY, which leaves this screen enabled, so OnEnable never re-ran and the label
            // kept the old language until the screen was re-entered.
            LocalizationManager.OnLanguageChanged += RefreshLocalizedText;
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= RefreshLocalizedText;

            // Leaving mid-entrance would strand cards at scale 0 / alpha 0; ApplyMode resets
            // them on the next open, but stop the routine so it cannot fight that reset.
            if (_entranceRoutine != null) { StopCoroutine(_entranceRoutine); _entranceRoutine = null; }
        }

        // Runs the staggered card entrance; tracked so a screen change can stop it.
        private Coroutine? _entranceRoutine;

        // The pull count OnEnable derived from the result. RefreshLocalizedText (a language
        // change while the screen is already open) needs it, and it must not be re-derived from
        // a result that a "pull again" may have replaced in the meantime.
        private int _pullCount = 10;

        private void RefreshLocalizedText()
        {
            if (_pullButtonLabel != null)
                _pullButtonLabel.text = LocalizationManager.Get(_pullCount != 1 ? "GACHA_PULL_X10" : "GACHA_PULL_X1");
        }

        // ── Mode logic ─────────────────────────────────────────────────────────

        private void ApplyMode(int pullCount)
        {
            bool isX10 = pullCount != 1;

            // Always start from fully-visible cards: PlayEntrance primes them to 0 and a
            // mid-animation screen change would otherwise leave them invisible forever.
            ResetCardVisuals();

            // Toggle row visibility
            SetActive(_prizeRow1, isX10);
            SetActive(_prizeRow2, isX10);
            SetActive(_prizeRow3, isX10);
            SetActive(_x1CardSlot, !isX10);

            // Update labels
            string multiplier = isX10 ? "x10" : "x1";
            if (_costMultiLabel != null) _costMultiLabel.text = multiplier;
            if (_pullButtonLabel != null)
                _pullButtonLabel.text = LocalizationManager.Get(isX10 ? "GACHA_PULL_X10" : "GACHA_PULL_X1");

            // Bind cards
            if (isX10)
                BindGridCards();
            else
                BindX1Card();
        }

        // ── Card binding ───────────────────────────────────────────────────────

        private void BindGridCards()
        {
            for (int i = 0; i < _gridSlots.Length; i++)
            {
                // A result shorter than the grid (a refused pull, or a server that paid fewer
                // slots than the grid holds) hides the surplus rather than leaving stale prizes on
                // screen.
                bool hasPrize = i < s_result.Count;
                BindSlot(i, hasPrize ? (PrizeRecord?)s_result[i] : null);
            }
        }

        /// <summary>
        /// Put ONE prize in slot <paramref name="index"/>: the authored club card when it is a
        /// club, a freshly instantiated shop card when it is not, and nothing when the slot has no
        /// prize. Exactly one of the two is ever active in a slot.
        /// </summary>
        private void BindSlot(int index, PrizeRecord? prize)
        {
            var club = index < _gridClubCards.Length ? _gridClubCards[index] : null;
            _spawnedCards.TryGetValue(index, out GameObject? spawned);

            if (prize == null)
            {
                if (club != null) club.gameObject.SetActive(false);
                if (spawned != null) spawned.SetActive(false);
                return;
            }

            PrizeRecord record = prize.Value;

            if (record.Kind == PrizeRecord.KindClub)
            {
                if (spawned != null) spawned.SetActive(false);
                if (club == null) return;
                club.gameObject.SetActive(true);
                GachaPrizeCardBinder.Bind(club.gameObject, record);
                return;
            }

            if (club != null) club.gameObject.SetActive(false);

            // The spawned card is rebuilt per bind rather than pooled: a slot can hold a ball on
            // one pull and an item on the next, and the shop card's per-category bind leaves rows
            // hidden that the other category needs. One Instantiate per non-club prize, at most ten
            // per pull, on a screen that is already instantiating a reveal card per slot.
            if (spawned != null) Destroy(spawned);

            // The "slot" is the ROW — the authored cards are direct children of PrizeRow1/2/3, so
            // the spawned card goes in at the SAME sibling index the hidden club card occupies, and
            // is pinned to that card's footprint. Without both, a 978px-wide shop card lands as an
            // extra child at the end of a HorizontalLayoutGroup and pushes the whole row (and the
            // COST/PULL block under it) off the panel — which is exactly what it did, measured.
            var slot   = SlotSizeFor(index);
            int sibling = ClubSiblingIndexFor(index);

            var go = GachaPrizeCardBinder.Instantiate(record, _gridSlots[index], ClubPrefabFor(index),
                                                      slot, sibling);
            if (go != null) _spawnedCards[index] = go;
            else            _spawnedCards.Remove(index);
        }

        /// <summary>The BagClubCard prefab a slot's authored card came from — the binder needs it
        /// for the club case, and reading it off the authored instance is what keeps this screen
        /// from carrying a second serialized reference to a prefab it already holds.</summary>
        private GameObject? ClubPrefabFor(int index)
            => index < _gridClubCards.Length && _gridClubCards[index] != null
                ? _gridClubCards[index]!.gameObject
                : null;

        /// <summary>The authored club card's footprint — the size a non-club prize must fit into.</summary>
        private Vector2? SlotSizeFor(int index)
        {
            if (index >= _gridClubCards.Length || _gridClubCards[index] == null) return null;
            var rt = _gridClubCards[index]!.transform as RectTransform;
            return rt != null && rt.rect.width > 0f && rt.rect.height > 0f ? (Vector2?)rt.rect.size : null;
        }

        /// <summary>Where the authored club card sits in the row, so its replacement takes the same
        /// position rather than being appended after every other card.</summary>
        private int ClubSiblingIndexFor(int index)
            => index < _gridClubCards.Length && _gridClubCards[index] != null
                ? _gridClubCards[index]!.transform.GetSiblingIndex()
                : -1;

        private void BindX1Card()
        {
            if (s_result.Count == 0) return;

            PrizeRecord record = s_result[0];

            if (record.Kind == PrizeRecord.KindClub)
            {
                if (_x1SpawnedCard != null) _x1SpawnedCard.SetActive(false);
                if (_x1Card == null) return;
                _x1Card.gameObject.SetActive(true);
                GachaPrizeCardBinder.Bind(_x1Card.gameObject, record);
                return;
            }

            if (_x1Card != null) _x1Card.gameObject.SetActive(false);
            if (_x1SpawnedCard != null) Destroy(_x1SpawnedCard);

            Transform parent = _x1Card != null ? _x1Card.transform.parent : transform;
            var x1Rt   = _x1Card != null ? _x1Card.transform as RectTransform : null;
            Vector2? x1Slot = x1Rt != null && x1Rt.rect.width > 0f ? (Vector2?)x1Rt.rect.size : null;

            _x1SpawnedCard = GachaPrizeCardBinder.Instantiate(
                record, parent, _x1Card != null ? _x1Card.gameObject : null,
                x1Slot, x1Rt != null ? x1Rt.GetSiblingIndex() : -1);
        }

        /// <summary>The non-club card spawned into the x1 slot, if any.</summary>
        private GameObject? _x1SpawnedCard;

        // The prize-card binder moved to GachaPrizeCardBinder (gacha_client_real_pull §4.3): a
        // prize is no longer always a club, so choosing the prefab is part of binding and this
        // screen is no longer the natural owner of it. ResolveRarity went with it and then went
        // away entirely — the rarity is on the record now, straight from the server.

        // ── Entrance animation (gacha_reveal_animation §3) ─────────────────────

        private const float EntranceStaggerSec = 0.045f;

        private IEnumerator PlayEntrance()
        {
            bool isX10 = _pullCount != 1;

            // Whatever is actually SHOWING in each slot — the authored club card or the card
            // spawned over it — so a mixed pull staggers all ten, not just the clubs.
            var cards = new List<GameObject>();
            if (isX10)
            {
                for (int i = 0; i < _gridSlots.Length; i++)
                {
                    var go = ActiveCardInSlot(i);
                    if (go != null) cards.Add(go);
                }
            }
            else
            {
                var go = _x1SpawnedCard != null && _x1SpawnedCard.activeSelf
                    ? _x1SpawnedCard
                    : (_x1Card != null ? _x1Card.gameObject : null);
                if (go != null) cards.Add(go);
            }

            // Prime every card BEFORE the first pop, otherwise card 10 would be visible at full
            // size for 400 ms while the stagger walks down to it.
            var groups = new CanvasGroup[cards.Count];
            var homes  = new float[cards.Count];
            for (int i = 0; i < cards.Count; i++)
            {
                groups[i] = EnsureGroup(cards[i]);
                groups[i].alpha = 0f;
                homes[i]  = GachaPrizeCardBinder.HomeScaleOf(cards[i]);
                cards[i].transform.localScale = Vector3.zero;
            }

            float duration = isX10 ? 0.25f : 0.3f;

            for (int i = 0; i < cards.Count; i++)
            {
                StartCoroutine(PopIn(cards[i].transform, groups[i], duration, homes[i]));
                if (i < cards.Count - 1)
                    yield return new WaitForSecondsRealtime(EntranceStaggerSec);
            }

            yield return new WaitForSecondsRealtime(duration);
            _entranceRoutine = null;
        }

        /// <param name="home">The scale the card RESTS at — 1 for an authored card, and
        /// the wrapper's 1 for a scaled-to-fit prize card, whose fit lives on its child. The
        /// pop animates 0 → home, and lands on home; landing on 1 unconditionally is what wiped
        /// the fit and let the card overflow its slot.</param>
        private static IEnumerator PopIn(Transform t, CanvasGroup group, float duration, float home = 1f)
        {
            float e = 0f;
            while (e < duration)
            {
                e += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(e / duration);
                float s = Mathf.LerpUnclamped(0f, home, EaseOutBack(k));
                if (t != null) t.localScale = new Vector3(s, s, 1f);
                if (group != null) group.alpha = k;
                yield return null;
            }
            if (t != null) t.localScale = new Vector3(home, home, 1f);
            if (group != null) group.alpha = 1f;
        }

        private void ResetCardVisuals()
        {
            foreach (var c in _gridClubCards) ResetCardVisual(c);
            ResetCardVisual(_x1Card);

            foreach (var go in _spawnedCards.Values) ResetSpawnedVisual(go);
            ResetSpawnedVisual(_x1SpawnedCard);
        }

        private static void ResetSpawnedVisual(GameObject? go)
        {
            if (go == null) return;
            // The card's OWN home scale, not 1 — a scale-to-fit prize card (SPEC §4.3) rests
            // below 1 and priming it to 1 is what made it overflow its slot again.
            float home = GachaPrizeCardBinder.HomeScaleOf(go);
            go.transform.localScale = new Vector3(home, home, 1f);
            EnsureGroup(go).alpha = 1f;
        }

        private static void ResetCardVisual(BagClubCard? card)
        {
            if (card == null) return;
            card.transform.localScale = Vector3.one;
            EnsureGroup(card.gameObject).alpha = 1f;
        }

        /// <summary>The card currently visible in grid slot <paramref name="index"/>, or null when
        /// the slot has no prize.</summary>
        private GameObject? ActiveCardInSlot(int index)
        {
            if (_spawnedCards.TryGetValue(index, out GameObject? spawned) &&
                spawned != null && spawned.activeSelf)
                return spawned;

            var club = index < _gridClubCards.Length ? _gridClubCards[index] : null;
            return club != null && club.gameObject.activeSelf ? club.gameObject : null;
        }

        // The prize cards ship without a CanvasGroup; adding it at runtime keeps the prefab
        // untouched (SPEC §3).
        private static CanvasGroup EnsureGroup(GameObject card)
        {
            var g = card.GetComponent<CanvasGroup>();
            if (g == null) g = card.AddComponent<CanvasGroup>();
            return g;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        // ── Button handlers ────────────────────────────────────────────────────

        // nav_back_memory §3 — history first, the Rewards Center as the fallback.
        private void OnBack()
        {
            if (ScreenManager.Instance != null)
                ScreenManager.Instance.GoBack(GolfinRedux.UI.ScreenId.GeneralShop);
            else
                Debug.LogWarning("[GachaPrizesScreenController] ScreenManager not found — cannot go back.");
        }

        // PULL on the RESULT screen means "pull again": same banner, same count, full reveal, then
        // this screen re-binds to the new result — and it now costs real tickets.
        private void OnPull()
        {
            Debug.Log($"[GachaPrizesScreenController] Pull again tapped (x{_pullCount}).");
            // The banner and the count both come from the last pull — the screen deliberately does
            // not know what a banner is, and re-deriving one here is how the "again" would end up
            // rolling a different one than the player just pulled.
            GachaPullFlow.PullAgain();
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        /// <summary>
        /// Collects the ten authored grid cards from the direct children of each prize row —
        /// Row1 (4), Row2 (4), Row3 (2), in order — and remembers each one's PARENT as the slot a
        /// non-club prize is instantiated into.
        /// </summary>
        private void CollectGridCards()
        {
            var cards = new List<BagClubCard>();
            AppendChildCards(_prizeRow1, cards);
            AppendChildCards(_prizeRow2, cards);
            AppendChildCards(_prizeRow3, cards);

            if (cards.Count != 10)
                Debug.LogWarning($"[GachaPrizesScreenController] Expected 10 grid cards, found {cards.Count}.");

            _gridClubCards = cards.ToArray();

            var slots = new Transform[_gridClubCards.Length];
            for (int i = 0; i < _gridClubCards.Length; i++)
                slots[i] = _gridClubCards[i].transform.parent != null
                    ? _gridClubCards[i].transform.parent
                    : _gridClubCards[i].transform;
            _gridSlots = slots;
        }

        private static void AppendChildCards(GameObject? row, List<BagClubCard> list)
        {
            if (row == null) return;
            foreach (Transform child in row.transform)
            {
                var card = child.GetComponent<BagClubCard>();
                if (card != null) list.Add(card);
            }
        }

        private static void SetActive(GameObject? go, bool active)
        {
            if (go != null) go.SetActive(active);
        }
    }
}
