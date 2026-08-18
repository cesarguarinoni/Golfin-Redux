// ─────────────────────────────────────────────────────────────────────────────
// TournamentSignupModalController
// Signup / registration modal for Open/Ending tournaments (Figma 13480:2479).
// Extends ModalController; mirrors MatchmakingModalController for show/hide +
// prior-panel active-state capture so closing never resurrects a stale panel.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Golfin.Banners;
using Golfin.Economy;
using Golfin.EconomyRuntime;
using Golfin.Roster;
using Golfin.Tournaments;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using Golfin.UI.Toast;

namespace GolfinRedux.UI.Tournaments
{
    /// <summary>
    /// Controller for the TournamentSignupModal prefab (Figma 13480:2479).
    /// Call Open(tournamentId) to show; CONFIRM → Register + navigate to HoleSelection.
    /// </summary>
    public class TournamentSignupModalController : ModalController
    {
        // ── Header content ────────────────────────────────────────────────────
        [Header("Header Text")]
        [SerializeField] private TextMeshProUGUI _sponsorText   = null!;
        [SerializeField] private TextMeshProUGUI _titleText     = null!;
        [SerializeField] private TextMeshProUGUI _venueText     = null!;
        /// <summary>
        /// Single combined line: "MMM d – MMM d — Ends in Xd Xh" (Figma 13480:2579+2580+2582)
        /// </summary>
        [SerializeField] private TextMeshProUGUI _dateLineText  = null!;

        // ── Cross Promotion Banner (13892:3435) ───────────────────────────────
        [Header("Cross Promotion Banner (13892:3435)")]
        [SerializeField] private GameObject _bannerRoot   = null!;   // hidden when there is no banner
        [SerializeField] private Image      _bannerImage  = null!;
        [SerializeField] private Button     _bannerButton = null!;

        // ── Info row (13498:2107) ─────────────────────────────────────────────
        [Header("Info Row (13498:2107)")]
        [SerializeField] private GameObject      _infoRow         = null!;
        [SerializeField] private Image           _tournamentImage = null!;   // 260×360 (13892:3440)
        [SerializeField] private TextMeshProUGUI _descriptionText = null!;   // (13892:3250)

        // ── Rules (13892:3254) ────────────────────────────────────────────────
        [Header("Rules (13892:3254)")]
        [SerializeField] private TextMeshProUGUI _rulesLabelText = null!;   // (13892:3255)
        [SerializeField] private TextMeshProUGUI _rulesBodyText  = null!;   // (13892:3442)

        // ── Separators ────────────────────────────────────────────────────────
        /// <summary>
        /// Index 0 is the separator directly ABOVE the info row — it hides with the row so the
        /// 24px vertical rhythm never doubles up. The rest are static.
        /// </summary>
        [Header("Separators")]
        [SerializeField] private List<GameObject> _separators = new List<GameObject>();

        // ── Layout (the banner state changes the container's top padding) ─────
        [Header("Layout")]
        [SerializeField] private RectTransform       _contentContainer = null!;   // 13498:2070 / 13892:3457
        [SerializeField] private VerticalLayoutGroup _contentLayout    = null!;

        // ── Entry pill ────────────────────────────────────────────────────────
        [Header("Entry Pill (13480:2618)")]
        [SerializeField] private TextMeshProUGUI _entryLabelText  = null!;   // "ENTRY" (13480:2620)
        [SerializeField] private Image           _entryCoinIcon   = null!;   // 30×30 RP coin (13480:2621)
        [SerializeField] private TextMeshProUGUI _entryAmountText = null!;   // fee number (13480:2622)

        // ── Reward row ────────────────────────────────────────────────────────
        [Header("Reward (13480:2624+2625)")]
        [SerializeField] private Image           _rewardCoinIcon = null!;   // 40×40 RP coin (13480:2624)
        [SerializeField] private TextMeshProUGUI _rewardText     = null!;   // "{prize:N0} + Trophy" (13480:2625)

        // ── Action Buttons ────────────────────────────────────────────────────
        [Header("Buttons")]
        [SerializeField] private Button _cancelButton  = null!;
        [SerializeField] private Button _confirmButton = null!;

        // ── Navigation ────────────────────────────────────────────────────────
        [Header("Navigation")]
        [SerializeField] private ScreenId _holeSelectionTarget = ScreenId.TournamentHoleSelection;

        // ── Prior active-state capture (MatchmakingModal pattern) ─────────────
        [Header("Panels to hide while open (optional)")]
        [SerializeField] private List<GameObject> _panelsToHide = new List<GameObject>();

        // ── Layout constants (Figma 13498:2067 vs 13892:3454) ─────────────────
        //
        // The two states are NOT one layout minus the banner. Frame A is 1411 tall with the
        // content container padded `0 48 32`; frame B is 1167 with `32 48 32`. 1411 − 1167 = 244
        // = 252 (banner) + 24 (its gap) − 32 (the top padding B adds back). Toggling _bannerRoot
        // alone yields 1379 with a bare 32px hole at the top, which is why these move together in
        // ApplyBannerState and why the height is left to the layout group rather than written.
        private const int ContentPadTopWithBanner    = 0;
        private const int ContentPadTopWithoutBanner = 32;

        // ── Runtime state ─────────────────────────────────────────────────────
        private string _tournamentId = string.Empty;
        private readonly List<bool> _panelWasActive = new List<bool>();

        /// <summary>Link behind the cross-promotion strip, re-gated at click time.</summary>
        private string? _bannerLink;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            if (_cancelButton  != null) _cancelButton.onClick.AddListener(OnCancel);
            if (_confirmButton != null) _confirmButton.onClick.AddListener(OnConfirm);
            if (_bannerButton  != null) _bannerButton.onClick.AddListener(OnBannerTapped);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Populate the modal from the tournament definition and show it.
        /// </summary>
        public void Open(string tournamentId)
        {
            if (string.IsNullOrEmpty(tournamentId)) return;

            _tournamentId = tournamentId;

            if (TournamentService.Instance == null)
            {
                Debug.LogWarning("[TournamentSignupModal] TournamentService not ready; cannot open modal.");
                return;
            }

            // TryGetTournament, not Backend.GetTournament: the latter THROWS KeyNotFoundException for
            // an unknown id, which made this null-check dead code and an unknown id an uncaught
            // exception. See OnConfirm for why an unknown id is now an ordinary occurrence.
            var def = TournamentService.Instance.TryGetTournament(tournamentId);
            if (def == null)
            {
                Debug.LogWarning($"[TournamentSignupModal] Unknown tournament id={tournamentId}");
                return;
            }

            Populate(def);
            Show();
        }

        // ── ModalController overrides ─────────────────────────────────────────

        protected override void OnShow()
        {
            // Capture + hide panels (MatchmakingModal pattern)
            _panelWasActive.Clear();
            foreach (var panel in _panelsToHide)
            {
                _panelWasActive.Add(panel != null && panel.activeSelf);
                if (panel != null) panel.SetActive(false);
            }
        }

        protected override void OnHide()
        {
            // Restore panels
            for (int i = 0; i < _panelsToHide.Count && i < _panelWasActive.Count; i++)
            {
                if (_panelsToHide[i] != null)
                    _panelsToHide[i].SetActive(_panelWasActive[i]);
            }
        }

        protected override void OnDisable()
        {
            // Call base FIRST so the OpenModalCount leak guard fires (S2)
            base.OnDisable();
            // Safety restore if disabled before Hide() completes
            for (int i = 0; i < _panelsToHide.Count && i < _panelWasActive.Count; i++)
            {
                if (_panelsToHide[i] != null)
                    _panelsToHide[i].SetActive(_panelWasActive[i]);
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────

        private void OnCancel()
        {
            Hide();
            // No Register call; no RP change; no navigation.
        }

        private void OnConfirm()
        {
            if (TournamentService.Instance == null)
            {
                ShowToast("Tournament service unavailable.");
                return;
            }

            // The tournament can legitimately disappear BETWEEN Open() and CONFIRM: the schedule is
            // refetched on every entry to the selection screen, and an admin deactivating a
            // tournament simply removes it from the payload. The player is not entered yet, so
            // MergePreservingEntered does not carry it forward — correctly, it is gone.
            //
            // Backend.GetTournament would throw KeyNotFoundException here and dead-end the player in
            // an open modal with no feedback. Toast and close instead; the list behind is already
            // (or about to be) rebuilt without the row.
            var def = TournamentService.Instance.TryGetTournament(_tournamentId);
            if (def == null)
            {
                Debug.LogWarning(
                    $"[TournamentSignupModal] '{_tournamentId}' left the schedule while the modal was " +
                    "open (deactivated, or its window closed). Closing without registering.");
                ShowToast("Tournament no longer available.");
                Hide();
                return;
            }

            // Pre-check RP before calling Register (guard matches mode_select_system pattern)
            long fee = def.EntryFeeRP;
            if (fee > 0 && RewardPointsManager.Instance != null)
            {
                if (RewardPointsManager.Instance.GetPoints() < (int)fee)
                {
                    ShowToast("Not enough RP");
                    Debug.Log($"[TournamentSignupModal] Insufficient RP — need {fee}, have {RewardPointsManager.Instance.GetPoints()}");
                    return;
                }
            }

            string charId = CharacterManager.Instance != null
                ? CharacterManager.Instance.GetSelectedCharacterId()
                : string.Empty;

            if (string.IsNullOrEmpty(charId))
            {
                ShowToast("No character selected.");
                return;
            }

            // Slice 2: the entry fee is now paid BEFORE Register instead of inside it, so an
            // unreachable server cannot enter the player into a tournament the ledger never charged
            // for. Register is therefore called with a fee of 0 — the payment already happened.
            //
            // Register's own idempotence (already-registered → return the existing entry, no
            // re-charge) is preserved by this GetMyEntry short-circuit; without it, moving payment in
            // front of Register would charge a second time on a re-entry.
            if (TournamentService.Instance.Backend.GetMyEntry(_tournamentId) != null)
            {
                Debug.Log($"[TournamentSignupModal] Already registered for {_tournamentId} — no charge.");
                CompleteSignup(charId, alreadyPaid: 0L);
                return;
            }

            // ── Async-multiplayer path (tournament_async_board SPEC §3) ───────
            //
            // The SERVER debits the entry fee inside POST /enter, through spend_pts with a
            // deterministic uuid5(user:slug) key — which is what makes a retry after a dropped
            // connection safe. Running the client's own TrySpendAsync as well would charge the
            // player TWICE for one entry, so on this path the local spend is skipped entirely and
            // the register call IS the payment.
            var remote = TournamentService.Instance.Remote;
            if (remote != null)
            {
                remote.RegisterAsync(_tournamentId, charId, outcome =>
                {
                    switch (outcome.Status)
                    {
                        case TournamentRegisterStatus.Entered:
                        case TournamentRegisterStatus.AlreadyEntered:
                            // CompleteSignup's Register call is a no-op here — the entry is already
                            // mirrored locally — and is kept so both paths navigate identically.
                            CompleteSignup(charId, alreadyPaid: fee);
                            break;

                        case TournamentRegisterStatus.Insufficient:
                            // Same copy the spend gate uses, so a short balance reads the same
                            // whether the client or the server was the one to notice.
                            ShowToast(PointsSpendGate.InsufficientMessage);
                            Debug.Log($"[TournamentSignupModal] Server refused entry to {_tournamentId} — " +
                                      $"needs {outcome.Requested}RP, holds {outcome.TotalPoints}RP.");
                            break;

                        case TournamentRegisterStatus.Offline:
                            // Entry is online-only by decision of record: there is no queue to fall
                            // back on, because a queued entry is an unpaid one.
                            ShowToast(PointsSpendGate.OfflineMessage);
                            Debug.Log($"[TournamentSignupModal] Entry to {_tournamentId} could not reach the " +
                                      "server — nothing charged, nothing entered.");
                            break;

                        default:
                            ShowToast("Registration failed.");
                            break;
                    }
                });
                return;
            }

            TournamentService.Instance.RewardPoints.TrySpendAsync(
                fee,
                SpendReasons.TournamentEntry,
                paid =>
                {
                    // The gate has already toasted the reason (insufficient vs connection required).
                    if (!paid)
                    {
                        Debug.Log($"[TournamentSignupModal] Entry fee of {fee}RP not paid — signup aborted.");
                        return;
                    }

                    CompleteSignup(charId, alreadyPaid: fee);
                });
        }

        /// <summary>
        /// Registers and navigates, with the entry fee already settled. Always passes 0 to
        /// <c>Register</c> — the debit is the caller's responsibility now (see <see cref="OnConfirm"/>).
        /// </summary>
        private void CompleteSignup(string charId, long alreadyPaid)
        {
            EntryState entry;
            try
            {
                entry = TournamentService.Instance.Backend.Register(_tournamentId, 0L, charId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[TournamentSignupModal] Register failed: {ex.Message}");
                ShowToast("Registration failed.");
                return;
            }

            if (entry == null)
            {
                ShowToast("Registration failed.");
                return;
            }

            Debug.Log($"[TournamentSignupModal] Registered tournament={_tournamentId} char={charId} entryFee={alreadyPaid}RP");

            Hide();
            ScreenManager.Instance?.ShowScreen(_holeSelectionTarget);
        }

        // ── Data population ───────────────────────────────────────────────────

        private void Populate(TournamentDefinition def)
        {
            // ── Presentation blocks added by tournament_signup_modal ───────────
            // Run FIRST so a later early-return in the (unchanged) content bindings below can
            // never leave the previous tournament's blurb or banner on screen.
            ApplyBanner(def);
            ApplyInfoRow(def);
            ApplyRules();

            // Sponsor: "{SPONSOR} PRESENTS"
            string sponsor = string.IsNullOrEmpty(def.SponsorKey)
                ? "GOLFIN PRESENTS"
                : def.SponsorKey.ToUpperInvariant() + " PRESENTS";
            if (_sponsorText != null) _sponsorText.text = sponsor;

            // Title: localize(NameKey) → Title → Id. Same ladder as the selection card — a
            // dashboard-created tournament has no localization key in this build.
            if (_titleText != null)
                _titleText.text = TournamentDisplayName.Resolve(def);

            // Venue: the shared ladder. Was an inline copy that appended a hole count on top of a
            // localized string which already had one, guarded by sniffing the output for "Holes" —
            // blind to "ホール", so JP rendered the count twice. See TournamentVenueLine.
            if (_venueText != null)
                _venueText.text = TournamentVenueLine.Resolve(def);

            // Date line (combined): "MMM d – MMM d — Ends in Xd Xh" (Figma 13480:2579+2580+2582)
            if (_dateLineText != null)
            {
                string dateRange = def.StartUtc.ToString("MMM d") + " – " + def.EndUtc.ToString("MMM d");
                TimeSpan diff = def.EndUtc - DateTime.UtcNow;
                string countdown = diff.TotalDays >= 1
                    ? $"Ends in {(int)diff.TotalDays}d {diff.Hours:D2}h"
                    : $"Ends in {(int)diff.TotalHours:D2}h {diff.Minutes:D2}m";
                _dateLineText.text = $"{dateRange} — {countdown}";
            }

            // Entry pill
            if (_entryLabelText  != null) _entryLabelText.text  = "ENTRY";
            if (_entryAmountText != null) _entryAmountText.text = def.EntryFeeRP.ToString("N0");
            // Coin icon visibility
            if (_entryCoinIcon   != null) _entryCoinIcon.enabled = true;

            // Reward
            if (_rewardCoinIcon  != null) _rewardCoinIcon.enabled = true;
            if (_rewardText != null && TournamentService.Instance != null)
            {
                long topPrize = TournamentService.Instance.GetTopPrizeRP(def.Id);
                _rewardText.text = $"{topPrize:N0} + Trophy";
            }
        }

        // ── Presentation blocks (tournament_signup_modal §5.2) ────────────────

        /// <summary>
        /// Cross-promotion strip (13892:3435) plus the layout state it drags with it.
        /// <para>
        /// ⚠️ The banner SOURCE is <c>game_banners</c> §9 — <c>tournaments.modal_banner_id</c>
        /// pointing at a <c>placement = 'tournament_modal'</c> row. That half has not landed:
        /// <c>BannerPlacement</c> currently declares only <c>HomePromo</c> and <c>Rankings</c>
        /// (<c>BannerService.TryParsePlacement</c>). Until it does,
        /// <see cref="TryResolveModalBanner"/> returns false and every tournament renders state B,
        /// which is a complete and correct modal (SPEC §7). The wiring below is the whole of the
        /// consuming side, so §9 landing is a change to that one resolver, not to this method.
        /// </para>
        /// </summary>
        private void ApplyBanner(TournamentDefinition def)
        {
            bool hasBanner = TryResolveModalBanner(def, out string? imageUrl, out string? linkUrl);

            _bannerLink = hasBanner ? linkUrl : null;

            if (hasBanner && _bannerImage != null)
            {
                TournamentArtService.Banners.Request(imageUrl, sprite =>
                {
                    if (_bannerImage == null || sprite == null) return;
                    _bannerImage.sprite = sprite;
                });
            }

            if (_bannerButton != null)
                _bannerButton.interactable = hasBanner && BannerPolicy.IsLinkAllowed(_bannerLink);

            ApplyBannerState(hasBanner);
        }

        /// <summary>
        /// The one place the two frames' difference lives: the strip's active state AND the content
        /// container's top padding move together, or state B renders 1379 tall with a 32px hole
        /// where the banner was. Neither height is written anywhere — the vertical layout group
        /// derives 1411 / 1167 from its own children.
        /// </summary>
        private void ApplyBannerState(bool hasBanner)
        {
            if (_bannerRoot != null) _bannerRoot.SetActive(hasBanner);

            if (_contentLayout != null)
            {
                var pad = _contentLayout.padding;
                int wanted = hasBanner ? ContentPadTopWithBanner : ContentPadTopWithoutBanner;
                if (pad.top != wanted)
                {
                    pad.top = wanted;
                    _contentLayout.padding = pad;   // assign back: RectOffset is a reference, but
                                                    // the setter is what marks the layout dirty
                }
            }

            RebuildLayout();
        }

        /// <summary>
        /// Info row (13498:2107): the 260×360 card art beside the blurb.
        /// <para>
        /// The two halves collapse INDEPENDENTLY. SPEC §5.1 originally hid the whole row —
        /// thumbnail included — whenever the blurb was empty, on the grounds that a lone 260-wide
        /// image in an 882 row reads as a layout bug. Cesar overrode that (2026-08-17: <i>"I have no
        /// idea why you are not showing description and image in state B. They are there in
        /// Figma."</i>): a tournament always has course art, and the blurb is empty only until the
        /// <c>description_*</c> columns land, so hiding the row on that basis hid the design.
        /// The row now shows whenever there is EITHER art or a blurb, and collapses — with the
        /// hairline above it — only when there is neither.
        /// </para>
        /// </summary>
        private void ApplyInfoRow(TournamentDefinition def)
        {
            string blurb = TournamentDescription.Resolve(def);
            bool hasBlurb = !string.IsNullOrEmpty(blurb);
            bool hasArt   = ApplyThumbnail(def);

            if (_descriptionText != null)
            {
                _descriptionText.text = blurb;
                _descriptionText.gameObject.SetActive(hasBlurb);
            }

            bool showRow = hasBlurb || hasArt;
            if (_infoRow != null) _infoRow.SetActive(showRow);
            SetSeparatorActive(0, showRow);   // the hairline directly above the row

            RebuildLayout();
        }

        /// <summary>
        /// Same three-layer art ladder the selection card uses (bundled first so the box is never
        /// empty while a download is in flight, then the server's allowlisted URL through the same
        /// <c>TournamentArtService.Instance</c> cache the card already warms).
        /// </summary>
        /// <returns><c>true</c> when the thumbnail has art to show.</returns>
        private bool ApplyThumbnail(TournamentDefinition def)
        {
            if (_tournamentImage == null) return false;

            Sprite? bundled = LoadCourseSprite(def.ClubId);
            if (bundled != null) _tournamentImage.sprite = bundled;

            // No art at all → hide the image and let the blurb take the full 882.
            bool hasRemote = !string.IsNullOrEmpty(def.BannerUrl);
            bool hasArt = bundled != null || hasRemote;
            _tournamentImage.enabled = hasArt;
            // The 1px #3E7CA8 rim is a sibling, so it has to follow the image, not the row.
            if (_tournamentImage.transform.parent != null)
                _tournamentImage.transform.parent.parent.gameObject.SetActive(hasArt);

            if (!hasRemote) return hasArt;

            var art = TournamentArtService.Instance;
            if (art.TryGet(def.BannerUrl, out Sprite remote) && remote != null)
            {
                _tournamentImage.sprite  = remote;
                _tournamentImage.enabled = true;
                return true;
            }

            art.Request(def.BannerUrl, sprite =>
            {
                // The modal can be closed and re-populated before the download lands.
                if (_tournamentImage == null || sprite == null) return;
                _tournamentImage.sprite  = sprite;
                _tournamentImage.enabled = true;
            });
            return true;   // remote art is on its way; the row stays open for it
        }

        /// <summary>
        /// RULES (13892:3254). Static content — it never collapses — but localized, so a JP player
        /// gets the Japanese table rows rather than English literals baked into C#.
        /// The body is JOINED at runtime from five separate keys, not authored pre-joined, so one
        /// line can change length in one language without disturbing the others.
        /// </summary>
        private void ApplyRules()
        {
            if (_rulesLabelText != null)
                _rulesLabelText.text = LocalizationManager.Get("tourn.rules.label");

            if (_rulesBodyText != null)
                _rulesBodyText.text = string.Join("\n", new[]
                {
                    LocalizationManager.Get("tourn.rules.max_players"),
                    LocalizationManager.Get("tourn.rules.divisions"),
                    LocalizationManager.Get("tourn.rules.per_division"),
                    LocalizationManager.Get("tourn.rules.gear"),
                    LocalizationManager.Get("tourn.rules.characters"),
                });
        }

        private void OnBannerTapped()
        {
            // Re-gate at the call site: the URL was allowlisted when the row was parsed, but this
            // is the moment it becomes an outbound navigation.
            if (!BannerPolicy.IsLinkAllowed(_bannerLink)) return;
            Application.OpenURL(_bannerLink);
        }

        /// <summary>
        /// The cross-promotion strip's artwork for this tournament, if it has one
        /// (`tournament_banners` §4.2).
        /// <para>
        /// The locale ladder is <b>not reimplemented here</b> — it calls
        /// <see cref="BannerService.ResolveImageUrl"/>, the same function the Home and Rankings
        /// slots go through, so the three placements cannot drift apart. <c>expiresAtUtc</c> is
        /// passed as null on purpose: a <c>tournament_modal</c> row has no window of its own, the
        /// tournament's own start/end governs when the strip is on screen.
        /// </para>
        /// <para>
        /// The chosen URL is re-checked against <see cref="BannerPolicy.IsArtAllowed"/> before it
        /// is returned. The server has already vetted it, so reaching that branch means a call
        /// site skipped the mapper or the allowlist moved — either way, refuse rather than
        /// download it. Same defence in depth <c>BannerService</c> applies at ingest.
        /// </para>
        /// <para>
        /// <paramref name="linkUrl"/> is passed through raw: <see cref="ApplyBanner"/> gates it
        /// with <c>IsLinkAllowed</c> when it sets <c>interactable</c>, and
        /// <see cref="OnBannerTapped"/> gates it again at the moment of the tap.
        /// </para>
        /// </summary>
        /// <returns><c>false</c> for "no banner" — which is state B, a complete modal.</returns>
        private static bool TryResolveModalBanner(
            TournamentDefinition def, out string? imageUrl, out string? linkUrl)
        {
            imageUrl = null;
            linkUrl  = null;

            string? url = BannerService.ResolveImageUrl(
                def.ModalBannerImageUrlEn,
                def.ModalBannerImageUrlJa,
                LocalizationManager.CurrentLanguage == Language.Japanese,
                expiresAtUtc: null,
                nowUtc: DateTime.UtcNow);

            if (string.IsNullOrEmpty(url)) return false;

            if (!BannerPolicy.IsArtAllowed(url))
            {
                Debug.LogWarning(
                    "[TournamentSignupModal] Refusing a modal banner URL outside the allowlisted " +
                    $"Storage prefix for '{def.Id}'. Rendering the no-banner state.");
                return false;
            }

            imageUrl = url;
            linkUrl  = def.ModalBannerLinkUrl;
            return true;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Bundled course photo, memoised — the selection card's layer 3.</summary>
        private static readonly Dictionary<string, Sprite?> _bundledArtMemo =
            new Dictionary<string, Sprite?>(StringComparer.Ordinal);

        private static Sprite? LoadCourseSprite(string clubId)
        {
            if (string.IsNullOrEmpty(clubId)) return null;
            if (_bundledArtMemo.TryGetValue(clubId, out var cached)) return cached;

            var sprite = Resources.Load<Sprite>("TournamentImages/" + clubId);
            _bundledArtMemo[clubId] = sprite;   // memoise misses too, so a bad id costs one lookup
            return sprite;
        }

        /// <summary>
        /// Force the nested layout groups to settle in the same frame. Populate runs BEFORE
        /// <c>Show()</c>, so without this the first frame can paint at the previous state's height.
        /// </summary>
        private void RebuildLayout()
        {
            if (_contentContainer != null)
                LayoutRebuilder.MarkLayoutForRebuild(_contentContainer);
        }

        private void SetSeparatorActive(int index, bool active)
        {
            if (index < 0 || index >= _separators.Count) return;
            if (_separators[index] != null) _separators[index].SetActive(active);
        }

        private static void ShowToast(string message)
        {
            if (ToastController.Instance != null)
                ToastController.Instance.Show(message, 2f);
            else
                Debug.LogWarning($"[TournamentSignupModal] Toast: {message}");
        }
    }
}
