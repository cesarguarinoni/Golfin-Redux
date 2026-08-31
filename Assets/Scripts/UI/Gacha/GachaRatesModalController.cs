// Assets/Scripts/UI/Gacha/GachaRatesModalController.cs
// gacha_ops_polish §2 — the in-app RATES & RULES modal.
//
// Before this, RULES & RATES was `Application.OpenURL(rulesUrl)` on a `golfin.example.com`
// placeholder — a button that either did nothing or hid itself. The odds a player is entitled to
// see now live IN the app, generated from the SAME published rows the server rolls from, so a rate
// change published in the admin is disclosed correctly at the next open with no build and no page
// to keep in sync.
//
// THE SHELL IS TournamentSignupModal's RULES BLOCK, CLONED — same panel sprite, same separator,
// same TMP styles, same CLOSE button, same ModalController scrim and fade. Nothing here invents a
// visual language; the only structural addition is a ScrollRect around the body, because unlike a
// tournament's fixed rules paragraph this body is as long as the pool is.
//
// ⚠️ THE FILTER IS HERE, NOT IN GachaRatesText. GachaPoolCatalog hands back deactivated rows and
// rows this build has not reached the min_build of; both are dropped before the text seam sees
// them, so a weight that cannot be rolled never sits in a denominator. That is the same filter
// GachaBannerCatalog.IsRollable applies and the same set the admin's effectiveOdds is handed —
// which is what makes "the modal agrees with the admin panel" a property rather than a hope.
#nullable enable
using System.Collections.Generic;
using Golfin.Banners;
using Golfin.Content;
using Golfin.Inventory;
using Golfin.Roster;
using Golfin.Telemetry;
using Golfin.UI.Modals;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GolfinRedux.UI.Gacha
{
    /// <summary>
    /// Controller for GachaRatesModal.prefab. <c>Show(entry)</c> regenerates the whole body from
    /// the live catalogs; nothing is cached between opens.
    /// </summary>
    public class GachaRatesModalController : ModalController
    {
        // ── Wiring ────────────────────────────────────────────────────────────

        [Header("Body (generated at show time)")]
        [SerializeField] private TextMeshProUGUI? _titleText;
        [SerializeField] private TextMeshProUGUI? _bodyText;

        [Tooltip("Reset to the top on every open — a modal that reopens mid-scroll reads as broken.")]
        [SerializeField] private ScrollRect?      _bodyScroll;

        [Header("Full rules link (hidden unless rulesUrl is set AND allowlisted)")]
        [SerializeField] private GameObject?      _fullRulesRow;
        [SerializeField] private Button?          _fullRulesButton;
        [SerializeField] private TextMeshProUGUI? _fullRulesLabel;

        [Header("CLOSE")]
        [SerializeField] private TextMeshProUGUI? _closeLabel;

        // ── Instance ──────────────────────────────────────────────────────────

        /// <summary>
        /// The scene instance, so <c>GachaBannerCard.OnRules</c> can open it without a serialized
        /// reference on a card that is spawned per banner. Same arrangement, same reason, as
        /// <see cref="GachaRevealModalController.Instance"/>.
        /// </summary>
        public static GachaRatesModalController? Instance { get; private set; }

        /// <summary>The banner the open body was generated from. Re-read on a language change.</summary>
        private GachaBannerEntry? _entry;

        /// <summary>The link behind the Full rules row, re-gated at click time — the row may have
        /// been made visible by a banner that a content refresh has since replaced.</summary>
        private string? _rulesUrl;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void Awake()
        {
            base.Awake();
            Instance = this;

            if (_fullRulesButton != null)
            {
                _fullRulesButton.onClick.RemoveListener(OnFullRules);
                _fullRulesButton.onClick.AddListener(OnFullRules);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(Instance, this)) Instance = null;
        }

        // The body is built imperatively, so — exactly as on GachaBannerCard — nothing repaints it
        // when the language changes. The Settings toggle lives in an OVERLAY that leaves this modal
        // enabled, so without this the open modal would keep the old language until it was closed
        // and reopened.
        private void OnEnable()  => LocalizationManager.OnLanguageChanged += ReBuild;
        private void OnDisable() => LocalizationManager.OnLanguageChanged -= ReBuild;

        private void ReBuild()
        {
            if (_entry != null) Populate(_entry);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Populate from <paramref name="entry"/>'s pool and show. A null entry is
        /// refused rather than opening an empty modal.</summary>
        public void Show(GachaBannerEntry? entry)
        {
            if (entry == null)
            {
                Debug.LogWarning("[GachaRatesModal] Show called with no banner entry — refusing.");
                return;
            }

            _entry = entry;
            Populate(entry);
            Show();

            // §3 — the only event in the funnel that is not on the pull path. It says whether the
            // disclosure surface is read at all, which is the question that decides whether it is
            // worth a Figma pass.
            TelemetryService.Instance.RecordSafe(TelemetryEventNames.GachaRulesOpen,
                () => new Dictionary<string, object>
                {
                    ["banner_id"] = entry.BannerId,
                });
        }

        protected override void OnShow()
        {
            // Verticalnormalized position is 1 at the TOP. Set after Show() has activated the
            // panel, or the ScrollRect writes it back when its content is first laid out.
            if (_bodyScroll != null) _bodyScroll.verticalNormalizedPosition = 1f;
        }

        // ── The body ──────────────────────────────────────────────────────────

        private void Populate(GachaBannerEntry entry)
        {
            if (_titleText != null) _titleText.text = LocalizationManager.Get("GACHA_RATES_TITLE");
            if (_closeLabel != null) _closeLabel.text = LocalizationManager.Get("SETTINGS_CLOSE");

            var rates = GachaRatesCatalog.ForPool(entry.PoolId);
            var pool  = RollableEntries(entry.PoolId);

            var lines = GachaRatesText.Build(entry, rates, pool, LiveName);
            if (_bodyText != null) _bodyText.text = string.Join("\n", lines);

            BindFullRules(entry);
        }

        /// <summary>
        /// The pool entries this build could actually be paid — active, weighted, and within this
        /// build's <c>min_build</c>. See the file header for why the filter is here.
        /// </summary>
        private static List<GachaPoolEntry> RollableEntries(string poolId)
        {
            int build = ContentBuildNumber.Current;
            var kept  = new List<GachaPoolEntry>();

            foreach (var p in GachaPoolCatalog.ForPool(poolId))
            {
                if (!p.IsActive) continue;
                if (p.MinBuild > build) continue;
                kept.Add(p);
            }

            return kept;
        }

        /// <summary>
        /// The Full rules row appears only when the banner names a URL AND that URL survives
        /// <see cref="BannerPolicy.IsLinkAllowed"/>.
        ///
        /// <para>
        /// <c>rulesUrl</c> is a free-text column an operator fills in the admin, and this modal
        /// would open the browser on it unattended. Same column shape, same exposure and therefore
        /// the same allowlist as the cross-promotion banner's <c>link_url</c> — a URL the admin
        /// accepted but the shipped host list does not is a row that renders no link, which is the
        /// designed fail-closed answer.
        /// </para>
        /// </summary>
        private void BindFullRules(GachaBannerEntry entry)
        {
            _rulesUrl = entry.RulesUrl;

            bool allowed = BannerPolicy.IsLinkAllowed(_rulesUrl);
            if (_fullRulesRow != null) _fullRulesRow.SetActive(allowed);
            if (!allowed) return;

            if (_fullRulesLabel != null)
                _fullRulesLabel.text = LocalizationManager.Get("GACHA_RATES_FULL_RULES");
        }

        private void OnFullRules()
        {
            // Re-gated at the point of OpenURL, not only when the row was made visible: the two are
            // separated by a content refresh that can swap the banner underneath.
            if (!BannerPolicy.IsLinkAllowed(_rulesUrl))
            {
                Debug.LogWarning("[GachaRatesModal] Full rules tapped but the URL is not allowlisted " +
                                 $"— refusing to open '{_rulesUrl}'.");
                return;
            }

            Application.OpenURL(_rulesUrl);
        }

        // ── The shipping name resolver ────────────────────────────────────────

        /// <summary>
        /// The name resolver <see cref="GachaRatesText.Build"/> is handed — the same five lookups
        /// <see cref="GachaPrizeCardBinder"/> uses to draw a prize card, so the modal and the
        /// reveal cannot call the same prize two different things.
        ///
        /// <para>
        /// Unlike <c>GachaBannerCatalog.LiveResolver</c>, a NULL database here answers null. That
        /// resolver's job is to admit a banner, and admitting on ignorance is the safe answer; this
        /// one's job is to print a name, and there is no safe name to invent.
        /// </para>
        /// </summary>
        private static string? LiveName(string kind, string refId)
        {
            if (string.IsNullOrEmpty(refId)) return null;

            switch (kind)
            {
                case "club":
                {
                    var club = ClubDatabaseCSV.Instance?.GetClub(refId);
                    return club == null ? null : Join(club.name, club.brand);
                }
                case "ball":
                {
                    var ball = BallDatabaseCSV.Instance?.GetBall(refId);
                    return ball == null ? null : Join(ball.name, ball.brand);
                }
                case "character":
                {
                    var ch = CharacterDatabaseCSV.Instance?.GetCharacter(refId);
                    return ch == null ? null : Join(ch.characterName, ch.characterLastName);
                }
                case "item":
                {
                    var item = ItemDatabaseCSV.Instance?.GetItem(refId);
                    return item == null ? null : (item.name ?? string.Empty).Trim();
                }
                case "ticket":
                {
                    if (!int.TryParse(refId, out int id)) return null;
                    var type = TicketTypeCatalog.Get(id);
                    return type == null ? null : (type.DisplayName ?? string.Empty).Trim();
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// "Name Brand" on ONE line — the modal's list is one prize per row, so the card binder's
        /// two-line form would double the height of every entry.
        ///
        /// <para>
        /// The brand is dropped when the name ALREADY CONTAINS it, not merely when the two are
        /// equal. Clubs.csv spells the name with the brand in it — <c>club_pwedge_royal</c> is
        /// "P.Wedge Royal Swing" with brand "Royal Swing" — so an equality test alone rendered
        /// "P.Wedge Royal Swing Royal Swing" on every club row of the modal. The equality case
        /// (the ball named "Golfin" by brand "GOLFIN") is the substring case with nothing left
        /// over, so one rule covers both.
        /// </para>
        /// </summary>
        private static string Join(string? first, string? second)
        {
            string a = (first ?? string.Empty).Trim();
            string b = (second ?? string.Empty).Trim();
            if (b.Length == 0) return a;
            if (a.IndexOf(b, System.StringComparison.OrdinalIgnoreCase) >= 0) return a;
            return a + " " + b;
        }
    }
}
