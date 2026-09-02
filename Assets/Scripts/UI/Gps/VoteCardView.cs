// gps_gifts_votes — one vote card. Four authored shapes, one behaviour.
#nullable enable
using System;
using System.Globalization;
using Golfin.Social;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Binds one <see cref="VoteDto"/> to one card.
    ///
    /// <para>
    /// The card is a TEMPLATE the screen clones, not a prefab it instantiates, for the same
    /// reason <c>VenuePickerModalController</c> pools its rows: four shapes x N votes would be
    /// four prefab loads and a spike on the first open, and every shape is already authored in
    /// the screen the clone lands in.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VoteCardView : MonoBehaviour
    {
        [Header("Author strip (photo cards only)")]
        [SerializeField] private Image? _authorAvatar;
        [SerializeField] private TextMeshProUGUI? _authorInitial;
        [SerializeField] private TextMeshProUGUI? _authorName;
        [SerializeField] private TextMeshProUGUI? _authorWhen;

        [Header("Body")]
        [SerializeField] private TextMeshProUGUI? _question;
        [SerializeField] private GameObject? _rewardPill;
        [SerializeField] private TextMeshProUGUI? _rewardLabel;

        [Header("Yes/No bars (bar cards only)")]
        [SerializeField] private Image? _yesFill;
        [SerializeField] private TextMeshProUGUI? _yesPct;
        [SerializeField] private Image? _noFill;
        [SerializeField] private TextMeshProUGUI? _noPct;

        [Header("Option pills (multi cards only)")]
        [SerializeField] private GameObject[] _optionPills = new GameObject[0];
        [SerializeField] private TextMeshProUGUI[] _optionLabels = new TextMeshProUGUI[0];

        [Header("Footer")]
        [SerializeField] private TextMeshProUGUI? _meta;
        [SerializeField] private Button? _giftButton;
        [SerializeField] private Button? _voteButton;

        /// <summary>The vote this card is currently showing, or null before the first bind.</summary>
        public VoteDto? Vote { get; private set; }

        /// <summary>The four avatar discs, in <c>avatar_color</c> enum order. Assigned by the
        /// screen once at clone time so the view does not have to know asset paths.</summary>
        public Sprite[] AvatarSprites = new Sprite[0];

        /// <summary>
        /// Bind a vote. <paramref name="voted"/> makes the VOTE button non-interactive — the
        /// player has already cast on this one and the server would answer 400.
        /// </summary>
        public void Bind(VoteDto vote, bool voted, string rewardText, DateTime utcNow)
        {
            Vote = vote;
            if (vote == null) return;

            if (_question != null) _question.text = vote.Question ?? string.Empty;
            if (_rewardLabel != null) _rewardLabel.text = rewardText;
            if (_rewardPill != null) _rewardPill.SetActive(!string.IsNullOrEmpty(rewardText));

            BindAuthor(vote);
            BindResults(vote);
            BindMeta(vote, utcNow);

            if (_voteButton != null) _voteButton.interactable = !voted;
        }

        private void BindAuthor(VoteDto vote)
        {
            string name = string.IsNullOrWhiteSpace(vote.CreatorName) ? "—" : vote.CreatorName!;
            if (_authorName != null) _authorName.text = name;
            if (_authorInitial != null)
                _authorInitial.text = name.Length > 0 && name != "—"
                    ? name.Substring(0, 1).ToUpperInvariant()
                    : "?";
            if (_authorAvatar != null && AvatarSprites.Length > 0)
                _authorAvatar.sprite = AvatarSprites[AvatarIndex(vote.CreatorId) % AvatarSprites.Length];
            if (_authorWhen != null) _authorWhen.text = Ago(vote.CreatedAt, DateTime.UtcNow);
        }

        /// <summary>
        /// Paint the bars or the option pills from the SERVER's percentages — never from a count
        /// this client divided, because <c>_update_percentages</c> rounds to one decimal and two
        /// different roundings on one screen would not add to 100.
        /// </summary>
        private void BindResults(VoteDto vote)
        {
            if (_yesFill != null || _noFill != null)
            {
                // BY LABEL, not by index — the server's option order is not stable, so
                // Options[0] is not reliably the one the YES bar is labelled for. See
                // VoteDto.YesOption.
                SetBar(_yesFill, _yesPct, vote.YesOption);
                SetBar(_noFill, _noPct, vote.NoOption);
            }

            for (int i = 0; i < _optionPills.Length; i++)
            {
                VoteOptionDto? o = OptionAt(vote, i);
                if (_optionPills[i] != null) _optionPills[i].SetActive(o != null);
                if (o != null && i < _optionLabels.Length && _optionLabels[i] != null)
                    _optionLabels[i].text = string.Format(CultureInfo.InvariantCulture, "{0} {1:0}%",
                                                          o.Label, o.Percentage);
            }
        }

        private static VoteOptionDto? OptionAt(VoteDto vote, int i)
            => vote.Options != null && i < vote.Options.Count ? vote.Options[i] : null;

        private static void SetBar(Image? fill, TextMeshProUGUI? pct, VoteOptionDto? option)
        {
            float p = option != null ? option.Percentage : 0f;
            // WIDTH, not fillAmount: Image.Type.Filled throws the 9-slice away and renders the
            // cap as a thin wedge. GpsUiColor.SetBarFill is the one implementation.
            GpsUiColor.SetBarFill(fill, p / 100f);
            if (pct != null) pct.text = Mathf.RoundToInt(p).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private void BindMeta(VoteDto vote, DateTime utcNow)
        {
            if (_meta == null) return;
            int? days = vote.DaysLeft(utcNow);
            _meta.text = string.Format(LocalizationManager.Get("GPS_VOTE_META"),
                                       vote.TotalVotes.ToString("N0", CultureInfo.InvariantCulture),
                                       Mathf.Max(0, days ?? 0).ToString(CultureInfo.InvariantCulture));
        }

        /// <summary>Wire the two buttons. Called once per clone by the screen; the card itself
        /// owns no navigation.</summary>
        public void WireButtons(Action<VoteCardView> onVote, Action<VoteCardView> onGift)
        {
            if (_voteButton != null)
            {
                _voteButton.onClick.RemoveAllListeners();
                _voteButton.onClick.AddListener(() => onVote?.Invoke(this));
            }
            if (_giftButton != null)
            {
                _giftButton.onClick.RemoveAllListeners();
                _giftButton.onClick.AddListener(() => onGift?.Invoke(this));
            }
        }

        /// <summary>After a cast the server hands back the repainted vote; re-bind from THAT
        /// rather than incrementing a local count.</summary>
        public void Repaint(VoteDto vote, string rewardText)
        {
            Bind(vote, voted: true, rewardText, DateTime.UtcNow);
        }

        public void SetVoteInteractable(bool on)
        {
            if (_voteButton != null) _voteButton.interactable = on;
        }

        /// <summary>
        /// A stable avatar colour for an id. There is no colour on a vote row — <c>creator_id</c>
        /// is all there is — so the disc is chosen by a hash of the id rather than at random,
        /// which keeps the same author the same colour across sessions and across screens.
        /// </summary>
        public static int AvatarIndex(string? id)
        {
            if (string.IsNullOrEmpty(id)) return 0;
            int h = 0;
            foreach (char c in id!) h = unchecked(h * 31 + c);
            return Mathf.Abs(h % 4);
        }

        /// <summary>"2h ago" / "1d ago", from an ISO timestamp. Localized through
        /// <c>GPS_VOTE_AGO_*</c> so the Japanese build does not read "2h ago".</summary>
        public static string Ago(string? iso, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(iso)) return string.Empty;
            if (!DateTime.TryParse(iso, CultureInfo.InvariantCulture,
                                   DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
                                   out DateTime when))
                return string.Empty;

            TimeSpan d = utcNow - when;
            if (d.TotalHours < 1)
                return string.Format(LocalizationManager.Get("GPS_VOTE_AGO_MIN"),
                                     Mathf.Max(1, (int)d.TotalMinutes));
            if (d.TotalDays < 1)
                return string.Format(LocalizationManager.Get("GPS_VOTE_AGO_HOUR"), (int)d.TotalHours);
            return string.Format(LocalizationManager.Get("GPS_VOTE_AGO_DAY"), (int)d.TotalDays);
        }
    }
}
