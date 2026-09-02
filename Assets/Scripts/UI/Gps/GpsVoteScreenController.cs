// ─────────────────────────────────────────────────────────────────────────────
// gps_gifts_votes §Client data bindings — the GPS Vote screen (Figma 14028:33534).
//
// The feed is live: /vote/list drives every card, a cast goes to the server and
// the card repaints from ITS answer, and the +10 lands through the same
// /points/earn the PLAYLIFE app uses. What is static is what has no backend to
// be live against — the stories strip is decorative, the photo areas are the
// node's placeholder gradients, and TRENDING / FRIENDS are drawn disabled
// rather than pretending to filter.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using Golfin.Auth;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Toast;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    [DisallowMultipleComponent]
    public sealed class GpsVoteScreenController : MonoBehaviour
    {
        private const string Tag = "[GpsVote]";

        /// <summary>The four chips, in the node's order. TRENDING and FRIENDS have no backend in
        /// v1 and are rendered disabled (SPEC § Goal).</summary>
        private enum Filter { Trending = 0, Friends = 1, Public = 2, Mine = 3 }

        /// <summary>Chips that are drawn but cannot be selected, at the node's own 45 % (the
        /// "rendered disabled" the SPEC asks for — NOT hidden, because the node draws them).</summary>
        private const float DisabledAlpha = 0.45f;

        /// <summary>How many votes one page pulls. The list is short today (five active rows);
        /// this is the cap, not an expectation.</summary>
        private const int PageSize = 20;

        [Header("Stories (decorative)")]
        [Tooltip("Six authored story cells. Bound from /user/discover's first six names — the " +
                 "strip is decoration, so a cell with no name is simply hidden.")]
        [SerializeField] private GameObject[] _storyCells = new GameObject[0];

        [Tooltip("The NEW cell — the only interactive one. Opens the same CREATE modal the " +
                 "+ CREATE button does.")]
        [SerializeField] private Button? _createStoryButton;

        [Header("Filters")]
        [SerializeField] private GameObject[] _chipRoots = new GameObject[0];
        [SerializeField] private Button[] _chipButtons = new Button[0];
        [SerializeField] private Button? _createButton;

        [Header("List")]
        [SerializeField] private RectTransform? _listContent;
        [SerializeField] private ScrollRect? _listScroll;
        [SerializeField] private TextMeshProUGUI? _emptyLabel;

        [Header("Card templates (authored inactive)")]
        [SerializeField] private VoteCardView? _cardPhotoTemplate;
        [SerializeField] private VoteCardView? _cardPhoto2Template;
        [SerializeField] private VoteCardView? _cardSimpleTemplate;
        [SerializeField] private VoteCardView? _cardMultiTemplate;

        [Tooltip("The four avatar discs at 48px, in avatar_color order (pink, green, blue, gold). " +
                 "Handed to each cloned card so the view never touches an asset path.")]
        [SerializeField] private Sprite[] _authorAvatars = new Sprite[0];

        [Header("Modal")]
        [SerializeField] private VoteCreateModalController? _createModal;

        private Filter _filter = Filter.Public;
        private bool _wired;
        private readonly List<VoteDto> _all = new List<VoteDto>();
        private readonly List<VoteCardView> _cards = new List<VoteCardView>();

        // ═════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            WireOnce();
            ApplyChips();

            // Paint from cache first — the same posture as every other GPS screen.
            if (VoteService.Instance.LastVotes != null) Rebuild(VoteService.Instance.LastVotes!);
            ApplyStories(UserService.Instance.LastDiscover);

            LocalizationManager.OnLanguageChanged += OnLanguageChanged;

            ApiClient client = ApiClient.Instance;
            client.Run(VoteService.Instance.List(0, PageSize, OnListResult));
            client.Run(UserService.Instance.Discover(r => ApplyStories(r != null && r.Success ? r.Data : null)));

            TelemetryService.Instance.RecordSafe("gps_vote_open",
                () => new Dictionary<string, object> { ["source"] = "gps_hub_tile" });
        }

        private void OnDisable()
        {
            LocalizationManager.OnLanguageChanged -= OnLanguageChanged;
        }

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;

            for (int i = 0; i < _chipButtons.Length; i++)
            {
                var which = (Filter)i;
                if (_chipButtons[i] == null) continue;
                // TRENDING / FRIENDS keep their listener so turning one on later is one line, but
                // interactable is false — the same posture the hub uses for its inert tiles.
                bool live = which == Filter.Public || which == Filter.Mine;
                _chipButtons[i].interactable = live;
                _chipButtons[i].onClick.AddListener(() => OnChip(which));
            }

            if (_createButton != null) _createButton.onClick.AddListener(OpenCreate);
            if (_createStoryButton != null) _createStoryButton.onClick.AddListener(OpenCreate);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Filters
        // ═════════════════════════════════════════════════════════════════════

        private void OnChip(Filter which)
        {
            if (which != Filter.Public && which != Filter.Mine) return;   // belt and braces
            if (_filter == which) return;
            _filter = which;
            ApplyChips();
            Rebuild(_all);
        }

        /// <summary>
        /// Paint the four chips. The selected one swaps its ring for the gold capsule and its
        /// label to the dark ink; the two dead ones drop to 45 % through a CanvasGroup, which
        /// dims the ring, the fill and the label together — setting <c>Image.color</c> would have
        /// dimmed only whichever one it was called on.
        /// </summary>
        private void ApplyChips()
        {
            for (int i = 0; i < _chipRoots.Length; i++)
            {
                GameObject? chip = _chipRoots[i];
                if (chip == null) continue;

                var which = (Filter)i;
                bool live = which == Filter.Public || which == Filter.Mine;
                bool on = live && which == _filter;

                Transform? off = chip.transform.Find("Off");
                Transform? onGo = chip.transform.Find("On");
                if (off != null) off.gameObject.SetActive(!on);
                if (onGo != null) onGo.gameObject.SetActive(on);

                Transform? label = chip.transform.Find("Label");
                var tmp = label != null ? label.GetComponent<TextMeshProUGUI>() : null;
                if (tmp != null) tmp.color = on ? GpsUiColor.Hex("#2A1A00") : Color.white;

                // `== null`, NOT `??`. GetComponent returns a FAKE-NULL UnityEngine.Object when the
                // component is absent, and `??` does not see that as null — so the first version of
                // this line handed back a destroyed reference and threw MissingComponentException
                // on the first frame of the screen, which took OnEnable down with it and left the
                // whole list unfetched. (CLAUDE.md Basic Rules #4.) The builder now authors the
                // CanvasGroup, so this branch is a guard rather than the normal path.
                var group = chip.GetComponent<CanvasGroup>();
                if (group == null) group = chip.AddComponent<CanvasGroup>();
                group.alpha = live ? 1f : DisabledAlpha;
                group.interactable = live;
                group.blocksRaycasts = live;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Stories (decorative)
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The strip is DECORATION: it carries no stories, opens nothing, and exists because the
        /// node draws it. Binding it to real names rather than to Misaki/Yui/Taro is the only
        /// thing that makes it honest — those are mockup data (SPEC § Reference).
        /// </summary>
        private void ApplyStories(List<DiscoverUserDto>? users)
        {
            int count = 0;
            if (users != null)
            {
                foreach (DiscoverUserDto u in users)
                {
                    if (count >= _storyCells.Length) break;
                    if (u == null || string.IsNullOrWhiteSpace(u.DisplayName)) continue;
                    GameObject? cell = _storyCells[count];
                    if (cell != null)
                    {
                        cell.SetActive(true);
                        SetText(cell, "Label", StoryLabel(u.DisplayName));
                        SetText(cell, "Avatar/Initial",
                                u.DisplayName!.Substring(0, 1).ToUpperInvariant());
                    }
                    count++;
                }
            }
            for (int i = count; i < _storyCells.Length; i++)
                if (_storyCells[i] != null) _storyCells[i].SetActive(false);
        }

        // ═════════════════════════════════════════════════════════════════════
        // The list
        // ═════════════════════════════════════════════════════════════════════

        private void OnListResult(ApiResult<List<VoteDto>> result)
        {
            if (result == null || !result.Success || result.Data == null)
            {
                if (result != null)
                    Debug.LogWarning($"{Tag} /vote/list failed ({result.ErrorKind}) — list left as it was.");
                if (_all.Count == 0) Rebuild(new List<VoteDto>());
                return;
            }

            Debug.Log($"{Tag} /vote/list -> {result.Data.Count} active votes.");
            Rebuild(result.Data);
        }

        /// <summary>
        /// Rebuild the whole list. Cheap enough to do wholesale: the endpoint returns one page,
        /// and a diffing rebuild would have to reason about a card whose vote moved position
        /// between two fetches, which is a bug surface for no gain at this size.
        /// </summary>
        private void Rebuild(List<VoteDto> votes)
        {
            if (!ReferenceEquals(votes, _all))
            {
                _all.Clear();
                _all.AddRange(votes);
            }

            foreach (VoteCardView card in _cards)
                if (card != null) Destroy(card.gameObject);
            _cards.Clear();

            List<VoteDto> shown = Filtered(_all);
            float y = 0f;
            DateTime now = DateTime.UtcNow;

            for (int i = 0; i < shown.Count; i++)
            {
                VoteDto v = shown[i];
                VoteCardView? template = TemplateFor(v, i);
                if (template == null) continue;

                var card = Instantiate(template, _listContent);
                card.name = "Card" + i;
                card.AvatarSprites = _authorAvatars;

                var rt = (RectTransform)card.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(0f, -y);
                y += rt.rect.height + 24f;   // the container's own 24px gap (14027:101847)

                // BIND BEFORE ACTIVATING. The template's bars are authored at FULL width (a
                // zero-width 9-slice collapses its caps into an oval, which is what the fidelity
                // linter fails), so a card activated before its first Bind would flash 100 %.
                card.Bind(v, VoteService.Instance.VotedLocally(v.Id),
                          LocalizationManager.Get("GPS_VOTE_REWARD_PILL"), now);
                card.WireButtons(OnVote, OnGift);
                card.gameObject.SetActive(true);
                _cards.Add(card);
            }

            if (_listContent != null)
                _listContent.sizeDelta = new Vector2(_listContent.sizeDelta.x, Mathf.Max(y, 1f));
            if (_listScroll != null) _listScroll.verticalNormalizedPosition = 1f;

            if (_emptyLabel != null)
            {
                _emptyLabel.gameObject.SetActive(shown.Count == 0);
                if (shown.Count == 0)
                    _emptyLabel.text = LocalizationManager.Get(
                        _filter == Filter.Mine ? "GPS_VOTE_EMPTY_MINE" : "GPS_VOTE_EMPTY");
            }
        }

        /// <summary>PUBLIC is the whole list; MINE is the rows this account created, matched on
        /// <c>creator_id</c> against the session's own id.</summary>
        private List<VoteDto> Filtered(List<VoteDto> all)
        {
            if (_filter != Filter.Mine) return new List<VoteDto>(all);

            string me = PlayerIdentity.UserId ?? string.Empty;
            var mine = new List<VoteDto>();
            foreach (VoteDto v in all)
                if (v != null && !string.IsNullOrEmpty(me) &&
                    string.Equals(v.CreatorId, me, StringComparison.Ordinal))
                    mine.Add(v);
            return mine;
        }

        /// <summary>
        /// Which of the four card shapes a vote gets.
        ///
        /// <para>
        /// SHAPE comes from the DATA: more than two options is the multi-pill card, two is a bar
        /// card. POSITION decides the photo header, and that is a v1 PRESENTATION rule, not a
        /// property of the vote: the SPEC keeps the photo areas "static per Figma", and the Figma
        /// frame puts a green photo on the first card and a brown one on the fourth. There is no
        /// photo on a votes row to key off — <c>related_activity_id</c> is null on every live row
        /// — so reproducing the node's rhythm is the honest reading. When votes carry a posted
        /// round's screenshot, this is the one function that changes.
        /// </para>
        /// </summary>
        private VoteCardView? TemplateFor(VoteDto v, int index)
        {
            if (v.Options != null && v.Options.Count > 2) return _cardMultiTemplate;
            if (index == 0) return _cardPhotoTemplate;
            if (index == 3) return _cardPhoto2Template;
            return _cardSimpleTemplate;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Casting
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// v1 casts YES — the first option. The card draws two bars and one VOTE button, which is
        /// the node's own shape (14028:33864: a single Gold-Small labelled VOTE, not a YES button
        /// and a NO button), so "vote" means "agree". A NO path needs a second button the design
        /// does not have and is a design question, not an implementation gap.
        /// </summary>
        private void OnVote(VoteCardView card)
        {
            VoteDto? v = card != null ? card.Vote : null;
            if (v == null || string.IsNullOrEmpty(v.Id)) return;
            if (v.Options == null || v.Options.Count == 0)
            {
                Debug.LogWarning($"{Tag} vote {v.Id} has no options — nothing to cast.");
                return;
            }

            card!.SetVoteInteractable(false);
            string optionId = v.Options[0].Id;
            Debug.Log($"{Tag} casting on {v.Id} -> option {optionId}.");

            ApiClient.Instance.Run(VoteService.Instance.Cast(v.Id, optionId, r => OnCast(card, v, r)));
        }

        private void OnCast(VoteCardView card, VoteDto original, ApiResult<VoteDto> result)
        {
            if (result != null && result.Success && result.Data != null)
            {
                // Repaint from the SERVER's copy — it has already recomputed every percentage.
                card.Repaint(result.Data, LocalizationManager.Get("GPS_VOTE_REWARD_PILL"));
                Replace(result.Data);

                // +10 RP, and ONLY here. /points/earn is not idempotent (it calls the unkeyed
                // earn_activity_pts), so it must never be reachable from the already-voted branch
                // below — the cast is what makes it unrepeatable, not the earn.
                PointsService.Instance.EarnActionAsync("vote_cast", OnEarned);
                return;
            }

            if (VoteService.AlreadyVoted(result))
            {
                // A STATE, not a failure: the player voted on this in an earlier session, which
                // this client had no way to know. The card simply flips to its voted form, and
                // NOTHING is earned.
                Debug.Log($"{Tag} {original.Id} was already voted — card flipped, no earn.");
                card.SetVoteInteractable(false);
                if (ToastController.Instance != null)
                    ToastController.Instance.Show(LocalizationManager.Get("GPS_VOTE_ALREADY"));
                return;
            }

            Debug.LogWarning($"{Tag} cast failed: {result}");
            card.SetVoteInteractable(true);
            if (ToastController.Instance != null)
                ToastController.Instance.Show(LocalizationManager.Get("GPS_VOTE_CAST_FAILED"));
        }

        private void OnEarned(ApiResult<PointsEarnResult> result)
        {
            int awarded = result != null && result.Success && result.Data != null
                ? result.Data.Awarded : 0;
            Debug.Log($"{Tag} vote_cast earn -> +{awarded} (total {result?.Data?.TotalPoints}).");

            if (ToastController.Instance != null && awarded > 0)
                ToastController.Instance.Show(
                    string.Format(LocalizationManager.Get("GPS_VOTE_CAST_TOAST"), awarded));

            // EarnActionRoutine already folded the new balance into the cache; this makes the
            // top bar agree with the server rather than with the response's partial payload.
            PointsService.Instance.RefreshBalanceAsync();
        }

        /// <summary>Swap the server's repainted row into the cached list, so a filter change or a
        /// re-entry does not resurrect the pre-cast counts.</summary>
        private void Replace(VoteDto updated)
        {
            for (int i = 0; i < _all.Count; i++)
                if (_all[i] != null && string.Equals(_all[i].Id, updated.Id, StringComparison.Ordinal))
                {
                    _all[i] = updated;
                    return;
                }
        }

        /// <summary>The GIFT button on a photo card routes to the Gift screen (SPEC § Client data
        /// bindings) — it does not open a modal here, because the recipient a player wants is
        /// chosen there.</summary>
        private void OnGift(VoteCardView card)
        {
            Debug.Log($"{Tag} GIFT -> GpsGift.");
            ScreenManager.Instance?.ShowScreen(ScreenId.GpsGift);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Create
        // ═════════════════════════════════════════════════════════════════════

        private void OpenCreate()
        {
            if (_createModal == null)
            {
                Debug.LogWarning($"{Tag} no create modal wired.");
                return;
            }
            _createModal.Open(OnCreated);
        }

        /// <summary>Prepend the new card rather than re-listing: the server orders by
        /// <c>created_at desc</c>, so a fresh vote IS the head of the next page anyway, and this
        /// shows it without a round trip.</summary>
        private void OnCreated(VoteDto created)
        {
            if (created == null) return;
            _all.Insert(0, created);
            Rebuild(_all);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Helpers
        // ═════════════════════════════════════════════════════════════════════

        private void OnLanguageChanged()
        {
            ApplyStories(UserService.Instance.LastDiscover);
            Rebuild(_all);
        }

        /// <summary>
        /// A display name cut to what an 88px story cell can hold.
        ///
        /// <para>
        /// The node's six labels are all short first names, so nothing there says what a real one
        /// does: "Apple Reviewer" is fourteen characters and runs straight across its neighbours,
        /// because the label is NoWrap + Overflow at the node's own 21px height. TMP's Ellipsis
        /// cannot help — it needs wrapping on, and a wrapped 18px line does not fit 21px, so it
        /// draws nothing at all. So the STRING is cut instead of the box: nine characters is what
        /// fits at 18px in 88px, and the tenth becomes the ellipsis.
        /// </para>
        /// </summary>
        public static string StoryLabel(string? name)
        {
            string n = (name ?? string.Empty).Trim();
            return n.Length <= StoryLabelChars ? n : n.Substring(0, StoryLabelChars - 1) + "…";
        }

        /// <summary>Characters an 88px cell holds at 18px, ellipsis included.</summary>
        private const int StoryLabelChars = 9;

        private static void SetText(GameObject root, string path, string? value)
        {
            Transform? t = root.transform.Find(path);
            if (t == null) return;
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null) tmp.text = value ?? string.Empty;
        }
    }
}
