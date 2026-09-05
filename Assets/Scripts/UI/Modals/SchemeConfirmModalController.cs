using Golfin.Gameplay.UI.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// The control-scheme confirm pop-up (<c>scheme_confirm_popup</c>): tapping a scheme that is
    /// not the current one — in Settings › Controls or the in-game gear modal — explains it and
    /// asks. The scheme only moves on CONFIRM.
    ///
    /// <para><b>The whole point is that selecting is not committing.</b> Both callers
    /// (<c>ControlsSubmenu.OnSchemeSelected</c>, <c>InGameSettingsModalController.OnSchemeSegmentTapped</c>)
    /// used to call <see cref="ControlSchemeService.Set"/> straight from the tap; they now route
    /// here and the highlight stays on the CURRENT scheme while the pop-up is open, so a cancelled
    /// selection leaves no trace. Tapping the scheme already in use is a no-op — no pop-up.</para>
    ///
    /// <para><b>Prefab (Rule 19 clone provenance).</b>
    /// <c>Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab</c> is an
    /// <c>AssetDatabase.CopyAsset</c> of <c>StartingCharacterConfirmModal.prefab</c>, so the
    /// backdrop, the navy <c>Background - HoleCard</c> panel plate, the <c>Divider</c> separator
    /// and the silver/gold <c>Main Buttons</c> pair are the shipping objects, not re-authored
    /// lookalikes. CONFIRM keeps the clone's gold <c>Button - Retry</c> sprite (never Copper).</para>
    ///
    /// <para><b>Two instances, one prefab.</b> One in <c>ShellScene</c> under the Settings canvas
    /// and one in <c>LabScaffold</c> under <c>ShotUI_Canvas</c> above <c>InGameSettingsModal</c>.
    /// <see cref="Instance"/> resolves whichever is loaded; the two scenes are never open together
    /// at runtime and the resolver logs if that assumption ever breaks.</para>
    /// </summary>
    public class SchemeConfirmModalController : ModalController
    {
        // ── Content slots (bound from SchemeConfirmContent, never authored text) ──
        [Header("Title")]
        [Tooltip("Gold 66px title. Bound to the scheme's SETTINGS_CONTROLS_* key via LocalizedText.")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("Steps (three tiles, captions are '<n>  <LABEL>')")]
        [SerializeField] private Image[] tileImages = new Image[3];
        [SerializeField] private TextMeshProUGUI[] captionTexts = new TextMeshProUGUI[3];

        [Header("How it works (three numbered lines)")]
        [SerializeField] private TextMeshProUGUI[] lineTexts = new TextMeshProUGUI[3];

        [Header("Buttons")]
        [SerializeField] private Button cancelButton;
        [SerializeField] private Button confirmButton;

        // ── The pending selection ─────────────────────────────────────────────
        //
        // The rules (no-op on the current scheme, CONFIRM commits exactly once, anything else
        // disarms) live in SchemeConfirmDecision, which is plain C# in an assembly the EditMode
        // tests can reach. This class is the Unity shell over it.
        private readonly SchemeConfirmDecision _decision = new SchemeConfirmDecision();

        /// <summary>The scheme CONFIRM would commit.</summary>
        public ControlScheme PendingScheme => _decision.Pending;

        /// <summary>The telemetry <c>where</c> CONFIRM would commit with.</summary>
        public string PendingSource => _decision.Source;

        // ── Instance resolution ───────────────────────────────────────────────
        private static SchemeConfirmModalController _instance;

        /// <summary>
        /// The pop-up in the loaded scene, or null when neither scene provides one (an EditMode
        /// test, or a scene that has not been re-authored). Callers MUST null-check: a missing
        /// pop-up must never swallow the player's tap silently, so they fall back to committing
        /// the change directly.
        ///
        /// <para>Resolved with <c>FindObjectsInactive.Include</c> because a modal sits in the
        /// scene with its panel deactivated; only the ROOT is active, and that root is what we
        /// need.</para>
        /// </summary>
        public static SchemeConfirmModalController Instance
        {
            get
            {
                if (_instance != null) return _instance;

                var found = FindObjectsByType<SchemeConfirmModalController>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);

                if (found.Length == 0) return null;
                if (found.Length > 1)
                {
                    // NOTE (§3.1): ShellScene and LabScaffold each carry one, and gameplay
                    // additively loads LabScaffold over ShellScene — so this DOES happen mid-round
                    // and the in-game one must win, which is the one in the non-shell scene.
                    for (int i = 0; i < found.Length; i++)
                        if (found[i].gameObject.scene.name != "ShellScene") { _instance = found[i]; break; }
                }

                if (_instance == null) _instance = found[0];
                return _instance;
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        /// <summary>
        /// Sorting order for both scene instances. Above <see cref="ModalScrim.SortingOrder"/>
        /// (500), which is exactly where the in-game gear modal lifts itself when IT opens, so the
        /// pop-up stacks over it (§1.5); below the hole-complete / tournament overlays (900) and
        /// the toast layer (950).
        /// </summary>
        public const int SortingOrder = 600;

        protected override void Awake()
        {
            // Base wires closeButton -> Hide and deactivates modalPanel + backdrop.
            base.Awake();

            ApplySortingOrder();

            if (cancelButton  != null) cancelButton.onClick.AddListener(Hide);
            if (confirmButton != null) confirmButton.onClick.AddListener(OnConfirm);

            _instance = this;
        }

        /// <summary>
        /// Own the sorting scope IN CODE rather than as a scene override.
        ///
        /// <para><c>overrideSorting</c> cannot be authored on the prefab — with no parent canvas
        /// the prefab's Canvas is a ROOT canvas and Unity forces the flag off — so it was set as a
        /// per-instance override in each scene. That override is silently lost the next time the
        /// prefab is rebuilt, and the pop-up quietly falls back to its parent canvas's order (0
        /// under <c>ShotUI_Canvas</c>). Setting it here makes the stacking a property of the
        /// component, which no prefab rebuild or scene re-save can drop.</para>
        /// </summary>
        private void ApplySortingOrder()
        {
            var canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();

            if (!canvas.isRootCanvas) canvas.overrideSorting = true;
            canvas.sortingOrder = SortingOrder;

            // A sorting canvas without a raycaster makes the whole modal untappable: graphics
            // register against their NEAREST enabled canvas, and a GraphicRaycaster only raycasts
            // its own canvas's graphics. Same guarantee ModalScrim makes for every other modal.
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
        }

        protected override void OnDisable()
        {
            if (_instance == this) _instance = null;
            base.OnDisable();
        }

        // ── Show / commit / dismiss ───────────────────────────────────────────

        /// <summary>
        /// Explain <paramref name="scheme"/> and ask. Nothing is written until CONFIRM.
        /// </summary>
        /// <param name="source">The telemetry <c>where</c> of the eventual
        /// <c>controls_scheme_changed</c> row — <c>"settings_popup"</c> or <c>"ingame_popup"</c>,
        /// so the dashboard can tell a menu switch from a mid-round one AND tell both apart from
        /// the pre-pop-up direct writes still in the history.</param>
        public void Show(ControlScheme scheme, string source)
        {
            // Belt and braces: the callers already skip the current scheme, and so does this.
            if (!_decision.Open(ControlSchemeService.Current, scheme, source))
            {
                Debug.Log($"[SchemeConfirm] {scheme} is already the live scheme — no pop-up.");
                return;
            }

            Bind(scheme);
            base.Show();
        }

        /// <summary>Hiding for ANY reason (CANCEL, the close button, a backdrop tap, the modal
        /// being force-disabled) disarms: only <see cref="OnConfirm"/> commits.</summary>
        public override void Hide()
        {
            _decision.Cancel();
            base.Hide();
        }

        private void OnConfirm()
        {
            if (!_decision.Confirm(out var scheme, out var source)) return;

            ControlSchemeService.Set(scheme, source);
            Hide();
        }

        // ── Binding ───────────────────────────────────────────────────────────

        /// <summary>
        /// Point every slot at the scheme's row in <see cref="SchemeConfirmContent"/>. Text is set
        /// through <see cref="LocalizedText.SetKey"/> rather than by writing <c>.text</c>, so the
        /// pop-up re-resolves itself if the language changes while it is open and there is no
        /// hardcoded string to go stale.
        /// </summary>
        private void Bind(ControlScheme scheme)
        {
            var entry = SchemeConfirmContent.For(scheme);

            SetKey(titleText, entry.TitleKey);

            for (int i = 0; i < 3; i++)
            {
                if (tileImages != null && i < tileImages.Length && tileImages[i] != null)
                {
                    Sprite tile = SchemeConfirmContent.LoadTile(scheme, i + 1);
                    tileImages[i].sprite = tile;
                    // A missing capture hides the Image instead of drawing a white box
                    // (Hard rule 7 / the UI-fidelity linter's flat-fill fabrication check).
                    tileImages[i].enabled = tile != null;
                    if (tile == null)
                        Debug.LogWarning($"[SchemeConfirm] no tile at Resources/{SchemeConfirmContent.TilePath(scheme, i + 1)} " +
                                         "— run GOLFIN > Capture > Scheme Confirm Tiles.");
                }

                if (captionTexts != null && i < captionTexts.Length)
                    SetKey(captionTexts[i], entry.CaptionKeys[i]);

                if (lineTexts != null && i < lineTexts.Length)
                    SetKey(lineTexts[i], entry.LineKeys[i]);
            }
        }

        /// <summary>Re-key a label through its <see cref="LocalizedText"/>. A label without one is
        /// a wiring bug, not a reason to fall back to writing <c>.text</c> — that is exactly the
        /// hardcoded-text regression the linter and the string tests exist to catch.</summary>
        private static void SetKey(TextMeshProUGUI label, string key)
        {
            if (label == null) return;

            var loc = label.GetComponent<LocalizedText>();
            if (loc == null)
            {
                Debug.LogError($"[SchemeConfirm] {label.name} has no LocalizedText — cannot bind {key}.");
                return;
            }

            loc.SetKey(key);
        }
    }
}
