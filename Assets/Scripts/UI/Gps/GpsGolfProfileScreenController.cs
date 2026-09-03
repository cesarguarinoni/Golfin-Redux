// auth_golf_profile §3 — the post-signup Golf Profile capture (Figma 14029:33628).
#nullable enable
using System.Globalization;
using Golfin.Net;
using Golfin.Social;
using Golfin.UI.Account;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Offered once per device on the first Home entry after sign-in
    /// (<see cref="GpsAuthExtrasFlow"/>). Captures an avatar colour, a nickname, a golf-experience
    /// band and an optional handicap, writes them to the PLAYLIFE <c>profiles</c> row through
    /// <see cref="UserService.Update"/>, and hands off to the Welcome tutorial.
    ///
    /// <para>
    /// BOTH EXITS ARE TERMINAL. SAVE (on success) and "Skip for now" each call
    /// <see cref="GpsAuthExtrasFlow.MarkPrompted"/> and then show <see cref="ScreenId.GpsWelcome"/>.
    /// A FAILED save does not — a taken nickname or a dead network must leave the player on this
    /// screen with the offer still outstanding, not consume their one chance at it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpsGolfProfileScreenController : MonoBehaviour
    {
        private const string Tag = "[GpsGolfProfile]";

        /// <summary>
        /// The wire values, in slot order, for the four swatches and the three chips. These are
        /// the strings the server CHECK-constrains
        /// (backend/migrations/2026_09_02_golf_profile.sql), so the order here is the contract
        /// between the prefab's slot layout and the database — do not reorder one without the
        /// other.
        /// </summary>
        public static readonly string[] ColorIds = { "pink", "green", "blue", "gold" };
        public static readonly string[] ExperienceIds = { "beginner", "intermediate", "advanced" };

        // ── Avatar colour row (node 14029:33890) ──────────────────────────────
        [Header("Avatar colour")]
        [SerializeField] private Button[]          _swatchButtons  = new Button[0];
        [SerializeField] private Image[]           _swatchImages   = new Image[0];
        [SerializeField] private TextMeshProUGUI[] _swatchInitials = new TextMeshProUGUI[0];
        [Tooltip("Unselected disc: 100px, 4px #F3ECC2 rim. Slot order matches ColorIds.")]
        [SerializeField] private Sprite[] _swatchOffSprites = new Sprite[0];
        [Tooltip("Selected disc: 120px, 8px #EEDC9A rim. Slot order matches ColorIds.")]
        [SerializeField] private Sprite[] _swatchOnSprites = new Sprite[0];

        // ── Fields (nodes 14029:33906 / :33919) ───────────────────────────────
        [Header("Fields")]
        [SerializeField] private TMP_InputField? _nicknameInput;
        [SerializeField] private TMP_InputField? _handicapInput;
        [SerializeField] private TextMeshProUGUI? _errorLabel;

        // ── Experience chips (node 14029:33910) ───────────────────────────────
        [Header("Experience chips")]
        [SerializeField] private Button[]          _chipButtons = new Button[0];
        [SerializeField] private Image[]           _chipImages  = new Image[0];
        [SerializeField] private TextMeshProUGUI[] _chipLabels  = new TextMeshProUGUI[0];
        [SerializeField] private Sprite? _chipOffSprite;
        [SerializeField] private Sprite? _chipOnSprite;

        // ── Actions ───────────────────────────────────────────────────────────
        [Header("Actions")]
        [SerializeField] private Button? _saveButton;
        [SerializeField] private Button? _skipButton;

        // ── Colours read off the node, not guessed ────────────────────────────
        // Chip label: white on the unselected chip (14029:33912), #2a1a00 on the gold one
        // (14029:33914). The error label reuses CreateUsernameScreenController's #E5484D so the
        // two name-taken treatments are visibly the same thing.
        private static readonly Color ChipInkOff = Color.white;
        private static readonly Color ChipInkOn  = GpsUiColor.Hex("#2A1A00");
        private static readonly Color ErrColor   = new Color(0.898f, 0.282f, 0.302f); // #E5484D

        // Slot geometry from node 14029:33890 — a 492-wide centred row, gap 24, where the
        // SELECTED disc is 120px at y=0 and the other three are 100px at y=10. The row's total
        // width is 100*3 + 120 + 24*3 = 492 whichever slot is selected, so the row never shifts:
        // only the discs inside it resize and re-space.
        private const float SwatchOff = 100f;
        private const float SwatchOn  = 120f;
        private const float SwatchGap = 24f;
        // The node's 42/50, scaled by the SAME 59/66 the builder applies to every SemiBold run
        // (GpsAuthExtrasBuilder.SemiBoldSize): the project's Rubik SemiBold renders ~11 % larger
        // than the face the node draws with. These values OVERWRITE the builder's at runtime, so
        // leaving them unscaled would silently undo the correction on the first selection change.
        private const float SemiBoldSize   = 59f / 66f;
        private const float InitialSizeOff = 42f * SemiBoldSize;   // node 14029:33893 (42)
        private const float InitialSizeOn  = 50f * SemiBoldSize;   // node 14029:33896 (50)

        /// <summary>Node 14029:33628 renders slot 1 (green) and INTERMEDIATE pre-selected; those
        /// are the defaults a player who touches neither control saves.</summary>
        private int _colorIndex = 1;
        private int _experienceIndex = 1;

        private bool _busy;
        private bool _wiredOnce;

        // ═══════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═══════════════════════════════════════════════════════════════════

        private void Awake()
        {
            WireOnce();
        }

        private void WireOnce()
        {
            if (_wiredOnce) return;
            _wiredOnce = true;

            for (int i = 0; i < _swatchButtons.Length; i++)
            {
                int slot = i;
                if (_swatchButtons[i] != null)
                    _swatchButtons[i].onClick.AddListener(() => SelectColor(slot));
            }

            for (int i = 0; i < _chipButtons.Length; i++)
            {
                int slot = i;
                if (_chipButtons[i] != null)
                    _chipButtons[i].onClick.AddListener(() => SelectExperience(slot));
            }

            if (_saveButton != null) _saveButton.onClick.AddListener(OnSaveClicked);
            if (_skipButton != null) _skipButton.onClick.AddListener(OnSkipClicked);

            if (_nicknameInput != null)
            {
                // Type into the field itself rather than into the OS keyboard's input bar — same
                // reason CreateUsernameScreenController does it (the bar swallows the text and
                // only forwards it on commit, so the swatch initial would never update).
                _nicknameInput.shouldHideMobileInput = true;
                _nicknameInput.characterLimit = UsernameRules.MaxLength;
                _nicknameInput.onValueChanged.AddListener(_ => ApplyInitial());
            }

            if (_handicapInput != null)
            {
                _handicapInput.shouldHideMobileInput = true;
                _handicapInput.contentType = TMP_InputField.ContentType.DecimalNumber;
            }

            // §D9 — both fields sit under the iOS keyboard, and `shouldHideMobileInput = true`
            // means there is no OS input bar echoing the text either: without this the player
            // types blind. The container that moves is the screen's own ContentContainer, so the
            // labels above each field travel with it. No-op in the Editor.
            RectTransform? content = GpsScreenTransition.FindLayer(gameObject, "ContentContainer")
                                     as RectTransform;
            KeyboardInsetBinder.Attach(_nicknameInput, content);
            KeyboardInsetBinder.Attach(_handicapInput, content);
        }

        private void OnEnable()
        {
            ClearError();
            SetBusy(false);

            // Paint from cache first, then refresh — the hub's pattern
            // (GpsHubScreenController / GpsProfileScreenController OnEnable), so a second entry
            // shows the real nickname on frame one instead of an empty field.
            Prefill(UserService.Instance.LastDetail);
            ApplySelection();

            ApiClient.Instance.Run(UserService.Instance.Detail(r =>
            {
                if (r != null && r.Success && r.Data != null) Prefill(r.Data);
            }));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Prefill + selection state
        // ═══════════════════════════════════════════════════════════════════

        private void Prefill(UserDetailDto? d)
        {
            if (d == null) { ApplyInitial(); return; }

            // Only fill an EMPTY field: a late /user/detail must never overwrite what the player
            // has already typed into it.
            if (_nicknameInput != null && string.IsNullOrEmpty(_nicknameInput.text)
                && !string.IsNullOrEmpty(d.DisplayName))
                _nicknameInput.text = d.DisplayName;

            if (_handicapInput != null && string.IsNullOrEmpty(_handicapInput.text)
                && d.Handicap.HasValue)
                _handicapInput.text = d.Handicap.Value.ToString("0.#", CultureInfo.InvariantCulture);

            int c = IndexOf(ColorIds, d.AvatarColor);
            if (c >= 0) _colorIndex = c;
            int e = IndexOf(ExperienceIds, d.GolfExperience);
            if (e >= 0) _experienceIndex = e;

            ApplySelection();
        }

        private static int IndexOf(string[] ids, string? value)
        {
            if (string.IsNullOrEmpty(value)) return -1;
            for (int i = 0; i < ids.Length; i++)
                if (ids[i] == value) return i;
            return -1;
        }

        private void SelectColor(int slot)
        {
            int next = Mathf.Clamp(slot, 0, ColorIds.Length - 1);
            bool changed = next != _colorIndex;
            _colorIndex = next;
            ApplySelection(animate: changed);
            // §D6 — only the disc that just became selected bumps.
            if (changed && _colorIndex < _swatchButtons.Length && _swatchButtons[_colorIndex] != null)
                UiSelection.Bump(this, _swatchButtons[_colorIndex].transform);
        }

        private void SelectExperience(int slot)
        {
            int next = Mathf.Clamp(slot, 0, ExperienceIds.Length - 1);
            bool changed = next != _experienceIndex;
            _experienceIndex = next;
            ApplySelection(animate: changed);
            if (changed && _experienceIndex < _chipButtons.Length && _chipButtons[_experienceIndex] != null)
                UiSelection.Bump(this, _chipButtons[_experienceIndex].transform);
        }

        private void ApplySelection() => ApplySelection(animate: false);

        private void ApplySelection(bool animate)
        {
            LayoutSwatches(animate);
            ApplyInitial();

            for (int i = 0; i < _chipImages.Length; i++)
            {
                bool on = i == _experienceIndex;
                // §D6 — two Images, alpha-swapped: the incoming chip sprite rides a transient
                // overlay and dissolves in, then the base Image takes it. NEVER a colour tint —
                // these sprites carry their own baked rims (Build rule 2, gps_profile_pack).
                UiSelection.SetSprite(this, _chipImages[i], on ? _chipOnSprite : _chipOffSprite, animate);
                if (_chipLabels[i] != null)
                    _chipLabels[i].color = on ? ChipInkOn : ChipInkOff;
            }
        }

        /// <summary>
        /// Re-space the four discs so the SELECTED one is the 120px disc and the row stays
        /// centred. Positions are recomputed rather than authored per state because the selected
        /// slot moves: authoring one fixed layout would leave the 120px disc stranded on slot 1.
        /// </summary>
        private void LayoutSwatches(bool animate = false)
        {
            float x = 0f;
            for (int i = 0; i < _swatchImages.Length; i++)
            {
                bool on = i == _colorIndex;
                float size = on ? SwatchOn : SwatchOff;

                UiSelection.SetSprite(this, _swatchImages.Length > i ? _swatchImages[i] : null,
                                      on ? Get(_swatchOnSprites, i) : Get(_swatchOffSprites, i),
                                      animate);

                if (_swatchButtons.Length > i && _swatchButtons[i] != null)
                {
                    var rt = (RectTransform)_swatchButtons[i].transform;
                    rt.sizeDelta = new Vector2(size, size);
                    // Anchored top-left, pivot top-left (the builder's Rect() convention), so y
                    // is negative-down and the 100px discs drop by 10 to stay centre-aligned.
                    rt.anchoredPosition = new Vector2(x, on ? 0f : -(SwatchOn - SwatchOff) / 2f);
                }

                if (_swatchInitials.Length > i && _swatchInitials[i] != null)
                {
                    var irt = _swatchInitials[i].rectTransform;
                    irt.sizeDelta = new Vector2(size, size * 0.5f);
                    irt.anchoredPosition = new Vector2(0f, on ? -30.5f : -25f);
                    _swatchInitials[i].fontSize = on ? InitialSizeOn : InitialSizeOff;
                }

                x += size + SwatchGap;
            }
        }

        private static Sprite? Get(Sprite[] arr, int i) => (arr != null && i < arr.Length) ? arr[i] : null;

        /// <summary>
        /// The swatch initial is the FIRST LETTER OF THE LIVE NICKNAME FIELD, uppercased — not of
        /// the saved profile — so it updates as the player types. Empty while the field is empty,
        /// which is what the node draws (its Initial nodes are empty TEXT nodes).
        /// </summary>
        private void ApplyInitial()
        {
            string name = _nicknameInput != null ? _nicknameInput.text : string.Empty;
            string initial = string.IsNullOrEmpty(name)
                ? string.Empty
                : name.Substring(0, 1).ToUpperInvariant();
            for (int i = 0; i < _swatchInitials.Length; i++)
                if (_swatchInitials[i] != null) _swatchInitials[i].text = initial;
        }

        // ═══════════════════════════════════════════════════════════════════
        // Actions
        // ═══════════════════════════════════════════════════════════════════

        private void OnSaveClicked()
        {
            if (_busy) return;
            ClearError();

            string nickname = _nicknameInput != null ? _nicknameInput.text.Trim() : string.Empty;
            if (!UsernameRules.IsValid(nickname))
            {
                // The SAME rule and the SAME message the Create Username screen shows — the two
                // screens set the same column, so a name one accepts the other must too.
                SetError(UsernameRules.Requirement);
                return;
            }

            if (!TryReadHandicap(out double? handicap))
            {
                // NOTE (SPEC §3): the project has no input-shake / red-field treatment to reuse,
                // so an unreadable handicap refuses SAVE and says why in the same error label the
                // name rules use. Nothing is written.
                SetError(LocalizationManager.Get("GPS_GOLFPROF_HANDICAP_BAD"));
                return;
            }

            SetBusy(true);
            string color = ColorIds[_colorIndex];
            string experience = ExperienceIds[_experienceIndex];

            // gps_profile_prompt_server_flag §4 — the answer travels with the profile, in the SAME
            // PUT. A separate write would have a window where the profile is saved and the account
            // is not marked as answered, and that window ends with the player being asked again on
            // their other install.
            ApiClient.Instance.Run(UserService.Instance.Update(
                nickname, handicap, experience, color, golfProfilePrompted: true, onResult: result =>
                {
                    SetBusy(false);

                    if (result != null && result.Success)
                    {
                        Debug.Log($"{Tag} saved: name='{nickname}' hc={(handicap.HasValue ? handicap.Value.ToString("0.0", CultureInfo.InvariantCulture) : "null")} exp={experience} colour={color}");
                        Advance();
                        return;
                    }

                    // 409 = the display-name unique index. Same message the Create Username
                    // screen shows for the same condition (UsernameClaim.TakenMessage).
                    if (result != null && result.StatusCode == 409)
                    {
                        SetError(UsernameClaim.TakenMessage);
                        return;
                    }

                    SetError(result != null && !string.IsNullOrEmpty(result.ErrorMessage)
                        ? result.ErrorMessage
                        : LocalizationManager.Get("AUTH_ERR_OFFLINE"));
                }));
        }

        /// <summary>
        /// Blank = <c>null</c> (a legitimate answer: the field is marked OPTIONAL). Anything else
        /// must parse as a decimal in a plausible golf range — the column is
        /// <c>NUMERIC(5,1)</c>, so an out-of-range value would come back as a 500 rather than as
        /// something the player can act on.
        /// </summary>
        private bool TryReadHandicap(out double? handicap)
        {
            handicap = null;
            string raw = _handicapInput != null ? _handicapInput.text.Trim() : string.Empty;
            if (string.IsNullOrEmpty(raw)) return true;

            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                return false;
            if (v < -10.0 || v > 60.0) return false;

            handicap = v;
            return true;
        }

        /// <summary>
        /// gps_profile_prompt_server_flag §4 — Skip now WRITES.
        ///
        /// <para>
        /// It used to issue no request at all, which is precisely why "and vice versa" did not
        /// hold: a player who skipped in the game left no trace on the account, so the standalone
        /// app asked them again. The one field it sends besides the required <c>display_name</c>
        /// is the prompted flag — no profile value is written, because skipping means the player
        /// declined to give any.
        /// </para>
        /// <para>
        /// THE NAME COMES FROM THE ACCOUNT, not from the nickname field. The field is editable and
        /// may hold something half-typed or already taken; SAVE validates it and Skip does not, so
        /// sending the field's text here could 409 (or silently rename the player) on a path where
        /// they explicitly declined to change anything.
        /// </para>
        /// <para>
        /// ADVANCES REGARDLESS, and marks the local flag regardless. A failed write must not trap
        /// the player on a screen they just dismissed, and must not re-ask them on this device
        /// either — "never nag" outranks the cross-install guarantee in the one case where they
        /// conflict. The failure is logged loudly, and the account simply gets one more offer on
        /// its next fresh install.
        /// </para>
        /// </summary>
        private void OnSkipClicked()
        {
            if (_busy) return;

            string accountName = CurrentAccountDisplayName();
            if (string.IsNullOrEmpty(accountName))
            {
                // No name to send means the required field cannot be filled — the account has not
                // finished sign-up. Nothing to write; the local flag still covers this device.
                Debug.LogWarning($"{Tag} skipped — no account display name available, so the " +
                                 $"once-per-account flag could NOT be written. This device will " +
                                 $"not re-ask; another install may.");
                Advance();
                return;
            }

            Debug.Log($"{Tag} skipped — PUT /user/update {{display_name, golf_profile_prompted:true}}.");
            ApiClient.Instance.Run(UserService.Instance.Update(
                accountName, null, null, null, golfProfilePrompted: true, onResult: result =>
                {
                    if (result != null && result.Success)
                        Debug.Log($"{Tag} skip recorded on the account — no other install will offer this again.");
                    else
                        Debug.LogWarning($"{Tag} skip flag NOT written ({(result == null ? "no result" : result.ErrorMessage)}) " +
                                         $"— this device will not re-ask; another install may.");
                }));

            // Not gated on the write: the player asked to leave, so they leave now.
            Advance();
        }

        /// <summary>
        /// The account's CURRENT display name — the cached profile row first, the session second.
        /// Never the nickname input; see <see cref="OnSkipClicked"/>.
        /// </summary>
        private static string CurrentAccountDisplayName()
        {
            string fromRow = UserService.Instance?.LastDetail?.DisplayName;
            if (!string.IsNullOrEmpty(fromRow)) return fromRow;

            var session = Golfin.Auth.AuthService.Instance?.Session;
            return session != null ? session.DisplayName : null;
        }

        /// <summary>Answer recorded, on to the tutorial. The ONLY place the local flag is set.</summary>
        private void Advance()
        {
            GpsAuthExtrasFlow.MarkPrompted();
            ScreenManager.Instance?.ShowScreen(ScreenId.GpsWelcome);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Feedback
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>The scope that draws the wait on SAVE PROFILE (gps_polish §D6).</summary>
        private PendingSpend? _pending;

        /// <summary>
        /// gps_polish §D6 — the wait is now DRAWN, not merely latched.
        ///
        /// <para>The latch stays exactly as it was; what changes is that SAVE reads as busy
        /// instead of looking untouched for the whole round-trip. /user/update against a real
        /// server is not instant, and a button that does not react is a button a player taps
        /// again.</para>
        /// </summary>
        private void SetBusy(bool busy)
        {
            _busy = busy;

            if (busy)
            {
                _pending?.Dispose();
                _pending = _skipButton != null
                    ? PendingSpend.BeginOn(_saveButton, _skipButton)
                    : PendingSpend.BeginOn(_saveButton);
                return;
            }

            if (_pending != null) { _pending.Dispose(); _pending = null; return; }

            // No scope to restore (SetBusy(false) on a path that never began one).
            if (_saveButton != null) _saveButton.interactable = true;
            if (_skipButton != null) _skipButton.interactable = true;
        }

        private void SetError(string message)
        {
            if (_errorLabel != null)
            {
                bool has = !string.IsNullOrEmpty(message);
                _errorLabel.gameObject.SetActive(has);
                _errorLabel.text = message ?? string.Empty;
                _errorLabel.color = ErrColor;
            }
            if (!string.IsNullOrEmpty(message)) Debug.Log($"{Tag} {message}");
        }

        private void ClearError()
        {
            if (_errorLabel != null) _errorLabel.gameObject.SetActive(false);
        }
    }
}
