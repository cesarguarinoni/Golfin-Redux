// ─────────────────────────────────────────────────────────────────────────────
// score_upload_flow §2 — the six-step score upload (Figma 14022:32576 … 14024:101792).
//
// ONE ScreenId, ONE prefab, SIX roots. Not six screens: the steps share a draft,
// a background and a nav bar, and BACK has to walk them in order rather than
// through ScreenManager's history — which is per-PILLAR and would take the
// player out of the flow entirely. So the state machine is here, ScreenManager
// sees a single screen, and only CLOSE / BACK TO HOME actually leave.
//
// The first real PLAYLIFE feature in the game: a photo becomes a read becomes a
// GPS proof becomes points and Trust. Every step degrades rather than blocking —
// the AI can fail, the fix can fail, the venue can fail, and the post still goes.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using Golfin.Economy;
using Golfin.Net;
using Golfin.Social;
using Golfin.Telemetry;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    [DisallowMultipleComponent]
    public sealed class ScoreUploadFlowController : MonoBehaviour
    {
        private const string Tag = "[ScoreUpload]";

        /// <summary>What a cell shows when the API cannot know the number — distinct from the en
        /// dash an UNEDITED hole shows. OUT / IN / PUTTS live here permanently in v1: the
        /// recognition returns a total and nothing else.</summary>
        private const string Unknown = "—";

        public enum Step { Capture = 0, Reading = 1, Edit = 2, Gps = 3, Confirm = 4, Posted = 5 }

        // ═════════════════════════════════════════════════════════════════════
        // Serialized wiring
        // ═════════════════════════════════════════════════════════════════════

        [Header("Step roots (index order = Step)")]
        [SerializeField] private GameObject[] _stepRoots = new GameObject[6];

        [Header("Step strip")]
        [SerializeField] private GameObject? _stepStrip;
        [SerializeField] private Button? _stripLeftButton;
        [SerializeField] private TextMeshProUGUI? _stripLeftLabel;
        [SerializeField] private TextMeshProUGUI? _stripTitle;
        [SerializeField] private TextMeshProUGUI? _stripCounter;
        [Tooltip("Five 8px segments, left to right. Gold up to the current step, white @0.25 after.")]
        [SerializeField] private Image[] _stripSegments = new Image[5];
        [SerializeField] private Color _segmentDone = new Color(0.933f, 0.863f, 0.616f, 1f);   // #eedc9a
        [Tooltip("Linear-space alpha: the project renders in Linear, so a node's sRGB 0.25 needs " +
                 "1-(1-0.25)^2.2 to look the same, matching the card sprites.")]
        [SerializeField] private Color _segmentTodo = new Color(1f, 1f, 1f, 0.474f);

        // ── 1 Capture ────────────────────────────────────────────────────────
        [Header("1 · Capture")]
        [SerializeField] private RawImage? _preview;
        [SerializeField] private GameObject? _previewPlaceholder;
        [SerializeField] private TextMeshProUGUI? _captureHint;
        [SerializeField] private Button? _shutterButton;
        [SerializeField] private Button? _sourceCameraButton;
        [SerializeField] private Button? _sourceLibraryButton;
        [SerializeField] private Button? _sourceManualButton;

        // ── 2 AI Reading ─────────────────────────────────────────────────────
        [Header("2 · AI Reading")]
        [SerializeField] private RectTransform? _spinner;
        [SerializeField] private Image? _spinnerRing;
        [SerializeField] private TextMeshProUGUI? _readingTitle;
        [SerializeField] private TextMeshProUGUI? _readingSub;
        [SerializeField] private TextMeshProUGUI? _rowTotalValue;
        [SerializeField] private TextMeshProUGUI? _rowOutValue;
        [SerializeField] private TextMeshProUGUI? _rowInValue;
        [SerializeField] private TextMeshProUGUI? _rowPuttsValue;
        [SerializeField] private TextMeshProUGUI? _rowCourseValue;
        [SerializeField] private TextMeshProUGUI? _confidencePillLabel;
        [SerializeField] private Image? _confidencePill;
        [SerializeField] private Button? _confirmScoreButton;
        [SerializeField] private TextMeshProUGUI? _confirmScoreLabel;
        [SerializeField] private Button? _retakeButton;
        [SerializeField] private TextMeshProUGUI? _retakeLabel;

        // ── 3 Edit Score ─────────────────────────────────────────────────────
        [Header("3 · Edit Score")]
        [SerializeField] private TextMeshProUGUI? _sumTotal;
        [SerializeField] private TextMeshProUGUI? _sumOut;
        [SerializeField] private TextMeshProUGUI? _sumIn;
        [SerializeField] private TextMeshProUGUI? _sumPutts;
        [SerializeField] private Button? _holes18Button;
        [SerializeField] private Button? _holes9Button;
        [SerializeField] private Image? _holes18Bg;
        [SerializeField] private Image? _holes9Bg;
        [SerializeField] private TextMeshProUGUI? _holes18Label;
        [SerializeField] private TextMeshProUGUI? _holes9Label;
        [SerializeField] private TextMeshProUGUI? _sectionOutTotal;
        [SerializeField] private TextMeshProUGUI? _sectionInTotal;
        [SerializeField] private GameObject? _sectionInGroup;
        [SerializeField] private TextMeshProUGUI? _totalFromHolesNote;
        [Tooltip("Exactly 18 rows, hole 1 first.")]
        [SerializeField] private HoleRowView[] _holeRows = new HoleRowView[18];
        [SerializeField] private Button? _verifyGpsButton;

        // ── 4 GPS Proof ──────────────────────────────────────────────────────
        [Header("4 · GPS Proof")]
        [SerializeField] private TextMeshProUGUI? _gpsPillLabel;
        [SerializeField] private Image? _gpsPillBg;
        [SerializeField] private TextMeshProUGUI? _locatingLabel;
        [SerializeField] private GameObject? _foundStrip;
        [SerializeField] private TextMeshProUGUI? _foundLabel;
        [SerializeField] private GameObject? _venueCard;
        [SerializeField] private TextMeshProUGUI? _venueName;
        [SerializeField] private TextMeshProUGUI? _venueAddress;
        [SerializeField] private TextMeshProUGUI? _venueWithin;
        [SerializeField] private TextMeshProUGUI? _factPar;
        [SerializeField] private TextMeshProUGUI? _factYards;
        [SerializeField] private TextMeshProUGUI? _factHoles;
        [SerializeField] private Button? _confirmCourseButton;
        [SerializeField] private Button? _chooseManuallyButton;
        [SerializeField] private Button? _retryGpsButton;
        [SerializeField] private VenuePickerModalController? _venuePicker;

        // ── 5 Confirm ────────────────────────────────────────────────────────
        [Header("5 · Confirm")]
        [SerializeField] private TextMeshProUGUI? _heroScore;
        [SerializeField] private TextMeshProUGUI? _heroVsPar;
        [SerializeField] private TextMeshProUGUI? _heroOut;
        [SerializeField] private TextMeshProUGUI? _heroIn;
        [SerializeField] private TextMeshProUGUI? _heroPutts;
        [SerializeField] private TextMeshProUGUI? _confirmCourseName;
        [SerializeField] private TextMeshProUGUI? _confirmDate;
        [SerializeField] private TextMeshProUGUI? _trustPercent;
        [SerializeField] private Image? _trustFill;

        /// <summary>The fill's track. The fill is sized against this rather than clipped with
        /// <see cref="Image.fillAmount"/>, which would throw away the capsule's 9-slice.</summary>
        [SerializeField] private GameObject? _trustFillTrack;
        [SerializeField] private TextMeshProUGUI? _chkScreenshot;
        [SerializeField] private TextMeshProUGUI? _chkGps;
        [SerializeField] private TextMeshProUGUI? _chkFriend;
        [SerializeField] private TextMeshProUGUI? _pointsValue;
        [SerializeField] private Button? _postScoreButton;
        [SerializeField] private GameObject? _postErrorStrip;
        [SerializeField] private TextMeshProUGUI? _postErrorLabel;

        // ── 6 Posted ─────────────────────────────────────────────────────────
        [Header("6 · Posted")]
        [SerializeField] private TextMeshProUGUI? _postedPoints;
        [SerializeField] private TextMeshProUGUI? _shareTrust;
        [SerializeField] private TextMeshProUGUI? _shareCourse;
        [SerializeField] private TextMeshProUGUI? _shareScore;
        [SerializeField] private TextMeshProUGUI? _shareVsPar;
        [SerializeField] private TextMeshProUGUI? _shareDate;
        [SerializeField] private TextMeshProUGUI? _shareRound;

        /// <summary>The pill BEHIND <see cref="_shareRound"/>. The label is a child of it, so
        /// toggling the label alone left the hidden pill hidden and the row never appeared.</summary>
        [SerializeField] private GameObject? _shareRoundPill;
        [SerializeField] private GameObject? _votePanel;
        [SerializeField] private Button? _createVoteButton;
        [SerializeField] private Button? _voteSkipButton;
        [Tooltip("Instagram / X / TikTok / Copy link — inert in v1, they log. The share sheet is its own task.")]
        [SerializeField] private Button[] _shareButtons = new Button[0];
        [SerializeField] private string[] _shareNames = new string[0];
        [SerializeField] private Button? _backHomeButton;

        // ── colours ──────────────────────────────────────────────────────────
        [Header("Palette")]
        [SerializeField] private Color _green = new Color(0.494f, 0.831f, 0.533f, 1f);   // #7ed488
        [SerializeField] private Color _gold  = new Color(0.933f, 0.863f, 0.616f, 1f);   // #eedc9a
        [SerializeField] private Color _red   = new Color(0.941f, 0.502f, 0.502f, 1f);   // #f08080
        [SerializeField] private Color _muted = new Color(1f, 1f, 1f, 0.55f);

        // ═════════════════════════════════════════════════════════════════════
        // State
        // ═════════════════════════════════════════════════════════════════════

        private readonly ScoreUploadDraft _draft = new ScoreUploadDraft();
        private Step _step = Step.Capture;
        private bool _wiredOnce;

        private WebCamTexture? _webcam;
        private Coroutine? _spin;
        private bool _postInFlight;

        // ═════════════════════════════════════════════════════════════════════
        // Lifecycle
        // ═════════════════════════════════════════════════════════════════════

        private void OnEnable()
        {
            WireOnce();

            // A second upload must not inherit the first one's photo, venue or result. The screen
            // object is reused by ScreenManager, so "fresh" is this call, not a new instance.
            _draft.ResetForNewCapture();
            _draft.Source = ScoreSource.Camera;
            _postInFlight = false;

            GoTo(Step.Capture);

            TelemetryService.Instance.RecordSafe(TelemetryEventNames.ScoreUploadOpen,
                () => new Dictionary<string, object> { ["source"] = "gps_hub" });
        }

        private void OnDisable()
        {
            StopPreview();
            StopSpinner();
        }

        private void WireOnce()
        {
            if (_wiredOnce) return;
            _wiredOnce = true;

            if (_stripLeftButton != null) _stripLeftButton.onClick.AddListener(OnStripLeft);

            if (_shutterButton != null) _shutterButton.onClick.AddListener(() => Capture(ScoreSource.Camera));
            if (_sourceCameraButton != null) _sourceCameraButton.onClick.AddListener(() => Capture(ScoreSource.Camera));
            if (_sourceLibraryButton != null) _sourceLibraryButton.onClick.AddListener(() => Capture(ScoreSource.Library));
            if (_sourceManualButton != null) _sourceManualButton.onClick.AddListener(StartManual);

            if (_confirmScoreButton != null) _confirmScoreButton.onClick.AddListener(OnConfirmScoreClicked);
            if (_retakeButton != null) _retakeButton.onClick.AddListener(OnRetakeClicked);

            if (_holes18Button != null) _holes18Button.onClick.AddListener(() => SetHoleCount(18));
            if (_holes9Button != null) _holes9Button.onClick.AddListener(() => SetHoleCount(9));
            if (_verifyGpsButton != null) _verifyGpsButton.onClick.AddListener(() => GoTo(Step.Gps));

            if (_confirmCourseButton != null) _confirmCourseButton.onClick.AddListener(() => GoTo(Step.Confirm));
            if (_chooseManuallyButton != null) _chooseManuallyButton.onClick.AddListener(OpenVenuePicker);
            if (_retryGpsButton != null) _retryGpsButton.onClick.AddListener(RunLocationCapture);

            if (_postScoreButton != null) _postScoreButton.onClick.AddListener(OnPostScoreClicked);

            if (_createVoteButton != null)
                _createVoteButton.onClick.AddListener(() => Debug.Log($"{Tag} vote — not wired yet"));
            if (_voteSkipButton != null)
                _voteSkipButton.onClick.AddListener(() => { if (_votePanel != null) _votePanel.SetActive(false); });

            for (int i = 0; i < _shareButtons.Length; i++)
            {
                Button? b = _shareButtons[i];
                if (b == null) continue;
                string label = i < _shareNames.Length ? _shareNames[i] : b.name;
                b.onClick.AddListener(() => Debug.Log($"{Tag} share {label} — not wired yet"));
            }

            if (_backHomeButton != null) _backHomeButton.onClick.AddListener(LeaveToHub);

            for (int i = 0; i < _holeRows.Length; i++)
            {
                HoleRowView? row = _holeRows[i];
                if (row == null) continue;
                row.Bind(i + 1, null, null, OnHoleChanged);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // Step machine
        // ═════════════════════════════════════════════════════════════════════

        private void GoTo(Step step)
        {
            // Leaving Capture always stops the camera. A WebCamTexture left running is a live
            // hardware handle and an on-device battery drain that outlives the screen.
            if (_step == Step.Capture && step != Step.Capture) StopPreview();
            if (step != Step.Reading) StopSpinner();

            int previous = _shownStep;
            _step = step;

            SwapStepRoots((int)step);
            ApplyStrip();
            SlideStepIndicator(previous, (int)step);
            ApplyTopBarTitle();

            switch (step)
            {
                case Step.Capture: EnterCapture(); break;
                case Step.Reading: EnterReading(); break;
                case Step.Edit:    EnterEdit();    break;
                case Step.Gps:     EnterGps();     break;
                case Step.Confirm: EnterConfirm(); break;
                case Step.Posted:  EnterPosted();  break;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // gps_polish §D4 — the step swap
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Which root is currently shown. −1 before the first GoTo, which is what makes
        /// the FIRST step appear rather than cross-fade from nothing.</summary>
        private int _shownStep = -1;

        private Coroutine? _stepIn, _stepOut, _indicatorMotion;

        /// <summary>The scope that draws the wait on POST SCORE (gps_polish §D6).</summary>
        private PendingSpend? _postPending;

        /// <summary>
        /// Cross-fade one step root to the next instead of snapping both with SetActive.
        ///
        /// <para>The two roots overlap for <see cref="UiMotion.FadeDur"/>. Every OTHER root is
        /// deactivated immediately — only the pair actually changing is animated, so a GoTo that
        /// jumps two steps (Reading → Edit skips nothing, but BACK from Confirm can) still ends
        /// with exactly one root active.</para>
        ///
        /// <para>The outgoing root's alpha is restored to 1 as it is deactivated. A root left at
        /// alpha 0 would come back invisible on the next visit, and the incoming fade would be
        /// fighting a value it did not set.</para>
        /// </summary>
        private void SwapStepRoots(int index)
        {
            GameObject? outgoing = _shownStep >= 0 && _shownStep < _stepRoots.Length
                ? _stepRoots[_shownStep] : null;
            GameObject? incoming = index >= 0 && index < _stepRoots.Length
                ? _stepRoots[index] : null;

            for (int i = 0; i < _stepRoots.Length; i++)
            {
                GameObject? r = _stepRoots[i];
                if (r == null || r == outgoing || r == incoming) continue;
                r.SetActive(false);
            }

            if (incoming != null)
            {
                incoming.SetActive(true);
                CanvasGroup? cg = incoming.GetComponent<CanvasGroup>();
                if (cg != null) UiMotion.Run(this, ref _stepIn, UiMotion.Fade(cg, 0f, 1f));
            }

            if (outgoing != null && outgoing != incoming)
            {
                CanvasGroup? cg = outgoing.GetComponent<CanvasGroup>();
                GameObject leaving = outgoing;
                if (cg != null)
                {
                    UiMotion.Run(this, ref _stepOut, UiMotion.Then(
                        UiMotion.Fade(cg, cg.alpha, 0f),
                        () => { cg.alpha = 1f; leaving.SetActive(false); }));
                }
                else leaving.SetActive(false);
            }

            _shownStep = index;
        }

        /// <summary>
        /// §D4 — the step indicator slides instead of jumping.
        ///
        /// <para>Deviation D-4, in one sentence: the strip has no single "active indicator" to
        /// move — it is five fixed segments with a CUMULATIVE gold fill — so a travelling marker
        /// REPLACING that fill would delete the progress reading and change the screen at rest.
        /// The marker <c>GpsPolishBuilder</c> adds is a sixth, segment-shaped object that lives at
        /// <b>alpha 0</b> and is only visible while it travels from the old active segment to the
        /// new one. Rest pixels are untouched; the jump is gone.</para>
        /// </summary>
        private void SlideStepIndicator(int fromIndex, int toIndex)
        {
            if (_stepStrip == null || fromIndex < 0 || fromIndex == toIndex) return;

            Transform? segments = _stepStrip.transform.Find("Segments");
            if (segments == null) return;
            var marker = segments.Find("StepIndicator") as RectTransform;
            if (marker == null) return;

            var from = SegmentRect(segments, fromIndex);
            var to   = SegmentRect(segments, toIndex);
            if (from == null || to == null) return;

            CanvasGroup? cg = marker.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;

            UiMotion.Run(this, ref _indicatorMotion, UiMotion.Then(
                UiMotion.Slide(marker, from.anchoredPosition.x, to.anchoredPosition.x, UiMotion.PushDur),
                () => { if (cg != null) cg.alpha = 0f; }));
        }

        private static RectTransform? SegmentRect(Transform segments, int index)
            => segments.Find("Seg" + (index + 1)) as RectTransform;

        /// <summary>
        /// The strip's left control is CLOSE on step 1 (it leaves the flow) and BACK everywhere
        /// else (it walks one step back). The Posted step has no strip at all — the round is
        /// posted, there is nothing to go back to.
        /// </summary>
        private void ApplyStrip()
        {
            bool showStrip = _step != Step.Posted;
            if (_stepStrip != null) _stepStrip.SetActive(showStrip);
            if (!showStrip) return;

            if (_stripLeftLabel != null)
                _stripLeftLabel.text = LocalizationManager.Get(_step == Step.Capture ? "SU_CLOSE" : "SU_BACK");

            if (_stripTitle != null) _stripTitle.text = LocalizationManager.Get(StepTitleKey(_step));

            if (_stripCounter != null)
                _stripCounter.text = ((int)_step + 1).ToString(CultureInfo.InvariantCulture) + "/5";

            for (int i = 0; i < _stripSegments.Length; i++)
                if (_stripSegments[i] != null)
                    _stripSegments[i].color = i <= (int)_step ? _segmentDone : _segmentTodo;
        }

        private static string StepTitleKey(Step s)
        {
            switch (s)
            {
                case Step.Capture: return "SU_STEP_CAPTURE";
                case Step.Reading: return "SU_STEP_READING";
                case Step.Edit:    return "SU_STEP_EDIT";
                case Step.Gps:     return "SU_STEP_GPS";
                default:           return "SU_STEP_CONFIRM";
            }
        }

        /// <summary>The Posted step retitles the SHARED top bar. SetUsername is the sanctioned
        /// transient override (it deliberately does not write the cached username), and every other
        /// step restores the screen's own key so leaving and re-entering is stable.</summary>
        private void ApplyTopBarTitle()
        {
            var ui = Golfin.UI.PersistentUIManager.Instance;
            if (ui == null) return;

            if (_step == Step.Posted) ui.SetUsername(LocalizationManager.Get("SCORE_POSTED_TITLE"));
            else ui.HighlightScreen(ScreenId.ScoreUpload);
        }

        /// <summary>
        /// BACK inside the flow steps back one; CLOSE on step 1 leaves. Public because the Android
        /// back button routes here too — without that, back would pop ScreenManager's PILLAR history
        /// and drop the player out of the flow from step 4.
        /// </summary>
        public void OnStripLeft()
        {
            if (_step == Step.Capture) { LeaveToHub(); return; }

            switch (_step)
            {
                // A manual entry never went through the AI, so its BACK skips step 2 rather than
                // landing on a reading stage with no photo to read.
                case Step.Edit:
                    GoTo(_draft.Source == ScoreSource.Manual ? Step.Capture : Step.Reading);
                    break;
                case Step.Reading: GoTo(Step.Capture); break;
                case Step.Gps:     GoTo(Step.Edit);    break;
                case Step.Confirm: GoTo(Step.Gps);     break;
                default:           LeaveToHub();       break;
            }
        }

        private void LeaveToHub()
        {
            if (_step != Step.Posted)
            {
                TelemetryService.Instance.RecordSafe(TelemetryEventNames.ScoreUploadAbandon,
                    () => new Dictionary<string, object> { ["step"] = (int)_step + 1 });
            }

            // Restore the shared top bar's own title before leaving, so the hub does not inherit
            // "SCORE POSTED" on the way out.
            Golfin.UI.PersistentUIManager.Instance?.HighlightScreen(ScreenId.GpsHub);
            ScreenManager.Instance?.GoBack(ScreenId.GpsHub);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 1 · Capture
        // ═════════════════════════════════════════════════════════════════════

        private void EnterCapture()
        {
            SetHint(LocalizationManager.Get("SU_ALIGN_HINT"), _muted);
            StartPreview();
        }

        /// <summary>
        /// The live viewfinder is a NICETY and never a gate. There is no camera in the Editor, none
        /// in the simulator, and on device the permission may be refused — in every one of those
        /// cases the static guide frame is what the player sees and the shutter still works,
        /// because the shutter goes through the native picker, not through this texture.
        /// </summary>
        private void StartPreview()
        {
            if (_preview == null) return;

            try
            {
                if (_webcam == null)
                {
                    WebCamDevice[] devices = WebCamTexture.devices;
                    if (devices == null || devices.Length == 0) { ShowPlaceholder(); return; }

                    string device = devices[0].name;
                    foreach (WebCamDevice d in devices)
                        if (!d.isFrontFacing) { device = d.name; break; }   // rear camera reads a card

                    _webcam = new WebCamTexture(device, 1280, 720);
                }

                _webcam.Play();
                _preview.texture = _webcam;
                _preview.gameObject.SetActive(true);
                if (_previewPlaceholder != null) _previewPlaceholder.SetActive(false);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} camera preview unavailable ({e.Message}) — showing the static guide.");
                ShowPlaceholder();
            }
        }

        private void ShowPlaceholder()
        {
            if (_preview != null) _preview.gameObject.SetActive(false);
            if (_previewPlaceholder != null) _previewPlaceholder.SetActive(true);
        }

        private void StopPreview()
        {
            try
            {
                if (_webcam != null && _webcam.isPlaying) _webcam.Stop();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} stopping the camera preview threw: {e.Message}");
            }
            if (_preview != null) _preview.texture = null;
        }

        private void SetHint(string text, Color color)
        {
            if (_captureHint == null) return;
            _captureHint.text = text;
            _captureHint.color = color;
        }

        private void Capture(ScoreSource source)
        {
            _draft.Source = source;

            if (source == ScoreSource.Library) PickFromLibrary();
            else TakePicture();
        }

        private void TakePicture()
        {
            if (NativeCamera.IsCameraBusy()) return;

            NativeCamera.RequestPermissionAsync(permission =>
            {
                if (permission != NativeCamera.Permission.Granted)
                {
                    SetHint(LocalizationManager.Get("SU_ERR_CAMERA_PERM"), _red);
                    return;
                }

                NativeCamera.TakePicture(path => OnImagePicked(path),
                                         maxSize: RecognitionService.MaxUploadEdgePx);
            }, isPicturePermission: true);
        }

        private void PickFromLibrary()
        {
            if (NativeGallery.IsMediaPickerBusy()) return;

            NativeGallery.RequestPermissionAsync(permission =>
            {
                if (permission != NativeGallery.Permission.Granted)
                {
                    SetHint(LocalizationManager.Get("SU_ERR_LIBRARY_PERM"), _red);
                    return;
                }

                NativeGallery.GetImageFromGallery(path => OnImagePicked(path));
            }, NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
        }

        /// <summary>A null path is a CANCEL, not a failure — the player backed out of the picker and
        /// belongs on the capture step with no error.</summary>
        private void OnImagePicked(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;

            Texture2D? texture = null;
            try
            {
                // markTextureNonReadable:false — EncodeForUpload has to read the pixels back.
                texture = NativeGallery.LoadImageAtPath(path, RecognitionService.MaxUploadEdgePx,
                                                        markTextureNonReadable: false);
                if (texture == null)
                {
                    SetHint(LocalizationManager.Get("SU_READ_FAIL"), _red);
                    return;
                }

                _draft.Photo = RecognitionService.EncodeForUpload(texture);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{Tag} could not read the picked image: {e.Message}");
                SetHint(LocalizationManager.Get("SU_READ_FAIL"), _red);
                return;
            }
            finally
            {
                if (texture != null) Destroy(texture);
            }

            GoTo(Step.Reading);
        }

        private void StartManual()
        {
            _draft.Source = ScoreSource.Manual;
            _draft.Photo = null;
            _draft.Recognition = null;
            GoTo(Step.Edit);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 2 · AI Reading
        // ═════════════════════════════════════════════════════════════════════

        private void EnterReading()
        {
            ShowReadingPending();
            StartSpinner();

            if (_draft.Photo == null || _draft.Photo.Length == 0)
            {
                ShowReadFailure();
                return;
            }

            ApiClient.Instance.Run(RecognitionService.Instance.Analyze(_draft.Photo, OnAnalyzed));
        }

        private void ShowReadingPending()
        {
            if (_readingTitle != null)
            {
                _readingTitle.text = LocalizationManager.Get("SU_READING");
                _readingTitle.color = _green;
            }
            if (_readingSub != null)
            {
                _readingSub.gameObject.SetActive(true);
                _readingSub.text = LocalizationManager.Get("SU_READING_SUB");
            }
            if (_spinnerRing != null) _spinnerRing.color = _gold;

            SetText(_rowTotalValue, Unknown);
            SetText(_rowCourseValue, Unknown);
            // OUT / IN / PUTTS are permanently unknown after an AI read — the endpoint returns a
            // total only (recognition.py "## golf"). The rows stay so the layout matches Figma and
            // so the player can see what the AI did NOT claim.
            SetText(_rowOutValue, Unknown);
            SetText(_rowInValue, Unknown);
            SetText(_rowPuttsValue, Unknown);
            if (_confidencePillLabel != null) _confidencePillLabel.text = Unknown;

            SetLabel(_confirmScoreLabel, "SU_BTN_CONFIRM_SCORE");
            SetLabel(_retakeLabel, "SU_BTN_RETAKE");
            SetInteractable(_confirmScoreButton, false);
        }

        private void OnAnalyzed(ApiResult<RecognitionResult> result)
        {
            StopSpinner();

            if (result == null || !result.Success || result.Data == null)
            {
                Debug.LogWarning($"{Tag} /recognition/analyze failed: {result}");
                ShowReadFailure();
                return;
            }

            _draft.Recognition = result.Data;
            GolfExtraction g = result.Data.Golf();
            _draft.HoleCount = g.Holes == 9 ? 9 : 18;

            // Per-hole par, IF the model returned a breakdown. It does not today, so the Edit
            // step's cells stay white — but the moment it does, they colour themselves.
            if (g.Pars != null)
                for (int i = 0; i < ScoreUploadDraft.MaxHoles && i < g.Pars.Length; i++)
                    _draft.Pars[i] = g.Pars[i];

            if (_readingTitle != null)
            {
                _readingTitle.text = LocalizationManager.Get("SU_READING");
                _readingTitle.color = _green;
            }
            if (_readingSub != null) _readingSub.gameObject.SetActive(false);
            if (_spinnerRing != null) _spinnerRing.color = _green;

            // A failed read swaps the buttons to RETRY / ENTER MANUALLY. When the retry SUCCEEDS the
            // labels have to come back, or the player is offered "RETRY" for a read that just worked.
            SetLabel(_confirmScoreLabel, "SU_BTN_CONFIRM_SCORE");
            SetLabel(_retakeLabel, "SU_BTN_RETAKE");

            SetText(_rowTotalValue, g.Score.HasValue ? g.Score.Value.ToString() : Unknown);
            SetText(_rowCourseValue, string.IsNullOrWhiteSpace(g.Course) ? Unknown : g.Course);
            ApplyConfidence(result.Data.Confidence);

            // A LOW-confidence read still proceeds: the whole point of step 3 is that the player
            // corrects it, and blocking here would leave them with no path but re-shooting.
            SetInteractable(_confirmScoreButton, g.Score.HasValue);
        }

        private void ApplyConfidence(double confidence)
        {
            string key = confidence >= 0.8 ? "SU_CONF_HIGH" : confidence >= 0.6 ? "SU_CONF_MED" : "SU_CONF_LOW";
            Color color = confidence >= 0.8 ? _green : confidence >= 0.6 ? _gold : _red;

            if (_confidencePillLabel != null)
            {
                _confidencePillLabel.text = LocalizationManager.Get(key);
                _confidencePillLabel.color = color;
            }
            // ONE tint drives both layers: the pill sprite's alpha is opaque in the 1px rim and
            // 18% inside, exactly as the node draws it.
            if (_confidencePill != null) _confidencePill.color = color;
        }

        private void ShowReadFailure()
        {
            if (_readingTitle != null)
            {
                _readingTitle.text = LocalizationManager.Get("SU_READ_FAIL");
                _readingTitle.color = _red;
            }
            if (_readingSub != null) _readingSub.gameObject.SetActive(false);
            if (_spinnerRing != null) _spinnerRing.color = _red;

            // The buttons become RETRY / ENTER MANUALLY: the two things that can actually help.
            SetLabel(_confirmScoreLabel, "SU_BTN_RETRY");
            SetLabel(_retakeLabel, "SU_BTN_ENTER_MANUALLY");
            SetInteractable(_confirmScoreButton, _draft.Photo != null && _draft.Photo.Length > 0);
        }

        private bool ReadFailed => _draft.Recognition == null;

        private void OnConfirmScoreClicked()
        {
            if (ReadFailed) { EnterReading(); return; }   // RETRY — re-POST the same photo
            GoTo(Step.Edit);
        }

        private void OnRetakeClicked()
        {
            if (ReadFailed) { StartManual(); return; }    // ENTER MANUALLY

            _draft.ResetForNewCapture();
            GoTo(Step.Capture);
        }

        private void StartSpinner()
        {
            StopSpinner();
            if (_spinner != null && isActiveAndEnabled) _spin = StartCoroutine(Spin());
        }

        private void StopSpinner()
        {
            if (_spin != null) { StopCoroutine(_spin); _spin = null; }
        }

        /// <summary>One revolution per second while the read is in flight (SPEC § Reading stage).</summary>
        private IEnumerator Spin()
        {
            while (_spinner != null)
            {
                _spinner.Rotate(0f, 0f, -360f * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        // 3 · Edit Score
        // ═════════════════════════════════════════════════════════════════════

        private void EnterEdit()
        {
            for (int i = 0; i < _holeRows.Length; i++)
            {
                HoleRowView? row = _holeRows[i];
                if (row == null) continue;
                row.Bind(i + 1, _draft.Holes[i], _draft.Pars[i], OnHoleChanged);
            }
            ApplyHoleCount();
            RefreshEditTotals();
        }

        private void OnHoleChanged(int hole, int? value)
        {
            if (hole >= 1 && hole <= ScoreUploadDraft.MaxHoles) _draft.Holes[hole - 1] = value;
            RefreshEditTotals();
        }

        private void SetHoleCount(int holes)
        {
            _draft.HoleCount = holes == 9 ? 9 : 18;
            ApplyHoleCount();
            RefreshEditTotals();
        }

        private void ApplyHoleCount()
        {
            bool nine = _draft.HoleCount == 9;

            for (int i = 0; i < _holeRows.Length; i++)
                if (_holeRows[i] != null) _holeRows[i].SetActiveHole(!nine || i < 9);

            // The IN header stays VISIBLE and dimmed in 9-hole mode (frame 3b) rather than being
            // removed: the panel keeps its shape, and the player can see what the toggle turned off.
            if (_sectionInGroup != null)
            {
                var group = _sectionInGroup.GetComponent<CanvasGroup>();
                if (group != null) { group.alpha = nine ? 0.35f : 1f; group.interactable = !nine; }
            }

            // The active half is the gold-gradient sprite at full strength; the inactive half
            // draws no fill at all and switches its label from ink to white (14035:101732-101736).
            ApplySegment(_holes18Bg, _holes18Label, !nine);
            ApplySegment(_holes9Bg, _holes9Label, nine);
        }

        private static void ApplySegment(Image? bg, TextMeshProUGUI? label, bool active)
        {
            if (bg != null) bg.color = active ? Color.white : new Color(1f, 1f, 1f, 0f);
            if (label != null) label.color = active ? SegmentInk : Color.white;
        }

        /// <summary>`#2a1a00` — the node's ink on the gold segment.</summary>
        private static readonly Color SegmentInk = new Color(0.165f, 0.102f, 0f, 1f);

        private void RefreshEditTotals()
        {
            int? total = _draft.Total;
            SetText(_sumTotal, total.HasValue ? total.Value.ToString() : Unknown);

            bool nine = _draft.HoleCount == 9;
            SetText(_sumOut, Num(_draft.Out));
            SetText(_sumIn, nine ? Unknown : Num(_draft.In));
            SetText(_sumPutts, Unknown);   // no putts anywhere in the pipeline — see the class note

            SetText(_sectionOutTotal, Num(_draft.Out));
            SetText(_sectionInTotal, nine ? Unknown : Num(_draft.In));

            if (_totalFromHolesNote != null)
            {
                bool fromHoles = _draft.AnyHoleEdited;
                _totalFromHolesNote.gameObject.SetActive(fromHoles);
                if (fromHoles) _totalFromHolesNote.text = LocalizationManager.Get("SU_TOTAL_FROM_HOLES");
            }

            SetInteractable(_verifyGpsButton, _draft.TotalInBounds);
        }

        private static string Num(int? v) => v.HasValue ? v.Value.ToString() : Unknown;

        // ═════════════════════════════════════════════════════════════════════
        // 4 · GPS Proof
        // ═════════════════════════════════════════════════════════════════════

        private void EnterGps()
        {
            ApplyGpsPending();
            RunLocationCapture();
        }

        private void ApplyGpsPending()
        {
            if (_locatingLabel != null) _locatingLabel.text = LocalizationManager.Get("SU_LOCATING");
            if (_foundStrip != null) _foundStrip.SetActive(false);
            if (_venueCard != null) _venueCard.SetActive(false);
            if (_retryGpsButton != null) _retryGpsButton.gameObject.SetActive(false);
            SetInteractable(_confirmCourseButton, false);
            SetGpsPill(false);

            // PAR and YARDS are permanently unknown: /venue/auto-register returns a name, a distance
            // and coordinates — no course facts. HOLES is the only real number here and it comes
            // from the player's own toggle on step 3.
            SetText(_factPar, Unknown);
            SetText(_factYards, Unknown);
            SetText(_factHoles, _draft.HoleCount.ToString());
        }

        /// <summary>
        /// THE FLOW'S RecordFix SITE (gps_trust_core open question 2). <c>Capture</c> fetches a fix,
        /// records it into the session trace, reads the trace back, and auto-registers the venue —
        /// and RETRY runs the whole thing again, which is exactly how a player accumulates the three
        /// fixes that earn the K4 Trust bonus.
        /// </summary>
        private void RunLocationCapture()
        {
            if (_locatingLabel != null) _locatingLabel.text = LocalizationManager.Get("SU_LOCATING");
            if (_retryGpsButton != null) _retryGpsButton.gameObject.SetActive(false);

            ApiClient.Instance.Run(GpsScoreAttachment.Capture(OnAttachmentReady));
        }

        private void OnAttachmentReady(GpsScoreAttachment attachment)
        {
            _draft.Attachment = attachment;

            bool located = attachment.Position != null;
            SetGpsPill(located);

            if (_locatingLabel != null)
            {
                _locatingLabel.text = located
                    ? string.Format(LocalizationManager.Get("SU_ACCURACY_FMT"),
                                    Mathf.RoundToInt(attachment.Position!.AccuracyM))
                    : LocalizationManager.Get(LocationFailReasonKeys.For(attachment.PositionFailReason));
                _locatingLabel.color = located ? _muted : _red;
            }

            // The retry link is offered exactly when a retry could help.
            if (_retryGpsButton != null) _retryGpsButton.gameObject.SetActive(!located);

            ApplyVenue();
        }

        private void ApplyVenue()
        {
            bool hasVenue = _draft.HasVenue;

            if (_foundStrip != null) _foundStrip.SetActive(true);
            if (_foundLabel != null)
            {
                // The status dot is part of THIS label, not a sibling — see the builder's note.
                _foundLabel.text = "● " + LocalizationManager.Get(hasVenue ? "SU_COURSE_FOUND" : "SU_COURSE_NONE");
                _foundLabel.color = hasVenue ? _green : _muted;
            }

            if (_venueCard != null) _venueCard.SetActive(hasVenue);
            if (hasVenue)
            {
                SetText(_venueName, _draft.CourseName);
                // /venue/auto-register returns no address column — documented deviation.
                SetText(_venueAddress, LocalizationManager.Get("SU_ADDRESS_UNKNOWN"));

                double? distance = _draft.Attachment?.VenueDistanceM;
                if (_venueWithin != null)
                {
                    _venueWithin.gameObject.SetActive(distance.HasValue);
                    if (distance.HasValue)
                        _venueWithin.text = string.Format(LocalizationManager.Get("SU_WITHIN_FMT"),
                                                          Mathf.RoundToInt((float)distance.Value));
                }
            }

            SetText(_factHoles, _draft.HoleCount.ToString());
            SetInteractable(_confirmCourseButton, hasVenue);
        }

        private void SetGpsPill(bool on)
        {
            if (_gpsPillLabel != null)
            {
                _gpsPillLabel.text = LocalizationManager.Get(on ? "SU_GPS_ON" : "SU_GPS_OFF");
                _gpsPillLabel.color = on ? _green : _red;
            }
            if (_gpsPillBg != null) _gpsPillBg.color = on ? _green : _red;
        }

        private void OpenVenuePicker()
        {
            if (_venuePicker == null)
            {
                Debug.LogWarning($"{Tag} CHOOSE MANUALLY has no picker wired.");
                return;
            }

            _venuePicker.Open(venue =>
            {
                if (venue == null)
                {
                    // "Post without a course": no venue at all. The server will decide gps_verified
                    // is false, which is correct — an unnamed round is not a verified one. Clearing
                    // the ATTACHMENT too is what makes that true on the wire and not just on screen.
                    _draft.VenueOverrideId = null;
                    _draft.VenueOverrideName = null;
                    if (_draft.Attachment != null)
                    {
                        _draft.Attachment.VenueId = null;
                        _draft.Attachment.VenueName = null;
                        _draft.Attachment.VenueDistanceM = null;
                    }
                    GoTo(Step.Confirm);
                    return;
                }

                _draft.VenueOverrideId = venue.Id;
                _draft.VenueOverrideName = venue.Name;

                // STAMP THE ATTACHMENT TOO, do not just remember it on the draft. The attachment is
                // the SINGLE owner of venue_id on the wire (ScoreService merges it over the request),
                // so a pick that only lived on the draft would show the player a course name and
                // then post no venue at all — which is what the first Editor run did.
                //
                // gps_verified stays the attachment's own rule (a fix AND a venue), so picking a
                // course without a fix still posts gps_verified false; and picking one WITH a fix is
                // a request to verify that the server re-derives against the venue's radius.
                if (_draft.Attachment == null) _draft.Attachment = new GpsScoreAttachment();
                _draft.Attachment.VenueId = venue.Id;
                _draft.Attachment.VenueName = venue.Name;
                _draft.Attachment.VenueDistanceM = null;   // not measured — this was a manual choice

                ApplyVenue();
            });
        }

        // ═════════════════════════════════════════════════════════════════════
        // 5 · Confirm
        // ═════════════════════════════════════════════════════════════════════

        private void EnterConfirm()
        {
            int? total = _draft.Total;
            SetText(_heroScore, total.HasValue ? total.Value.ToString() : Unknown);

            int? vsPar = _draft.VsPar;
            if (_heroVsPar != null)
            {
                // Hidden rather than "(+0)" when the card carried no par: a made-up par is a made-up
                // headline number.
                _heroVsPar.gameObject.SetActive(vsPar.HasValue);
                if (vsPar.HasValue)
                    _heroVsPar.text = "(" + (vsPar.Value >= 0 ? "+" : "") + vsPar.Value + ")";
            }

            SetText(_heroOut, Num(_draft.Out));
            SetText(_heroIn, _draft.HoleCount == 9 ? Unknown : Num(_draft.In));
            SetText(_heroPutts, Unknown);

            SetText(_confirmCourseName, string.IsNullOrEmpty(_draft.CourseName) ? Unknown : _draft.CourseName);
            SetText(_confirmDate, _draft.DisplayDate);

            int trust = _draft.TrustEstimate;
            SetText(_trustPercent, trust + "%");
            if (_trustFill != null && _trustFillTrack != null)
            {
                float full = ((RectTransform)_trustFillTrack.transform).rect.width;
                var rt = (RectTransform)_trustFill.transform;
                rt.sizeDelta = new Vector2(full * Mathf.Clamp01(trust / 100f), rt.sizeDelta.y);
            }

            bool byScreenshot = _draft.Source != ScoreSource.Manual;
            bool byGps = _draft.HasVenue && _draft.Attachment?.Position != null;
            ApplyCheck(_chkScreenshot, "SU_CHK_SCREENSHOT", byScreenshot);
            ApplyCheck(_chkGps, "SU_CHK_GPS", byGps);
            ApplyCheck(_chkFriend, "SU_CHK_FRIEND", false);   // v2

            SetText(_pointsValue, string.Format(LocalizationManager.Get("SU_POINTS_FMT"), _draft.PointsEstimate));

            if (_postErrorStrip != null) _postErrorStrip.SetActive(false);
            SetInteractable(_postScoreButton, _draft.TotalInBounds && !_postInFlight);
        }

        private void ApplyCheck(TextMeshProUGUI? label, string key, bool done)
        {
            if (label == null) return;
            label.text = (done ? "✓ " : "○ ") + LocalizationManager.Get(key);
            label.color = done ? _green : _muted;
        }

        private void OnPostScoreClicked()
        {
            if (_postInFlight) return;

            _postInFlight = true;
            SetInteractable(_postScoreButton, false);
            // gps_polish §D6 — /score/submit uploads a photo, so this is the LONGEST wait on the
            // GPS surface and the one most likely to read as "nothing happened".
            _postPending?.Dispose();
            _postPending = PendingSpend.BeginOn(_postScoreButton);
            if (_postErrorStrip != null) _postErrorStrip.SetActive(false);

            ApiClient.Instance.Run(
                ScoreService.Instance.Submit(_draft.BuildRequest(), _draft.Attachment, OnPosted));
        }

        private void OnPosted(ApiResult<ScoreSubmitResult> result)
        {
            // A duplicate suppressed by the service's own latch is NOT an error — the real post is
            // still on its way and its answer is the one that matters.
            if (result != null && result.ErrorKind == ApiErrorKind.Disabled) return;

            _postInFlight = false;
            _postPending?.Dispose();
            _postPending = null;

            if (result == null || !result.Success || result.Data == null)
            {
                SetInteractable(_postScoreButton, _draft.TotalInBounds);
                if (_postErrorStrip != null) _postErrorStrip.SetActive(true);
                if (_postErrorLabel != null)
                    _postErrorLabel.text = ScoreService.ErrorMessageFor(result, LocalizationManager.Get);

                Debug.LogWarning($"{Tag} /score/submit failed: {result}");
                return;
            }

            _draft.Result = result.Data;

            // The RP pill in the shared top bar is the same number the server just moved.
            PointsService.Instance.RefreshBalanceAsync();

            GoTo(Step.Posted);
        }

        // ═════════════════════════════════════════════════════════════════════
        // 6 · Posted
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>gps_polish §D7 — the Posted total's pop.</summary>
        private Coroutine? _postedScorePop;

        private void EnterPosted()
        {
            ScoreSubmitResult? r = _draft.Result;

            // The SERVER's numbers, never the Confirm step's estimate: only the server knows about
            // the rate-limit penalty, the mock penalty and badges.
            SetText(_postedPoints, string.Format(LocalizationManager.Get("SU_POSTED_PTS_FMT"),
                                                 r != null ? r.PointsEarned : 0));
            SetText(_shareTrust, string.Format(LocalizationManager.Get("SU_SHARE_TRUST_FMT"),
                                               r != null ? r.Trust : 0));
            SetText(_shareCourse, string.IsNullOrEmpty(_draft.CourseName) ? Unknown : _draft.CourseName);

            int? total = _draft.Total;
            SetText(_shareScore, total.HasValue ? total.Value.ToString() : Unknown);

            // gps_polish §D7 — the number the whole flow was about POPS in (scale 0.9 -> 1 with
            // its own alpha), so the Posted step lands on the score rather than merely displaying
            // it. The CanvasGroup is added at runtime and settles at alpha 1: nothing is authored
            // and the rest frame is unchanged.
            if (_shareScore != null && total.HasValue)
            {
                var scoreRect = _shareScore.rectTransform;
                var scoreGroup = scoreRect.GetComponent<CanvasGroup>();
                if (scoreGroup == null) scoreGroup = scoreRect.gameObject.AddComponent<CanvasGroup>();
                UiMotion.Run(this, ref _postedScorePop, UiMotion.Pop(scoreRect, scoreGroup));
            }

            int? vsPar = _draft.VsPar;
            if (_shareVsPar != null)
            {
                _shareVsPar.gameObject.SetActive(vsPar.HasValue);
                if (vsPar.HasValue)
                    _shareVsPar.text = "(" + (vsPar.Value >= 0 ? "+" : "") + vsPar.Value + ")";
            }

            SetText(_shareDate, _draft.DisplayDate);

            // "23rd round" is the player's lifetime round count. /score/submit does not return it,
            // but /user/detail does and UserService has already cached it for the hub — so the
            // count is the CACHED value plus this post, and the pill hides outright when the
            // profile has never answered rather than showing a made-up "1st round".
            if (_shareRound != null)
            {
                int? rounds = UserService.Instance.LastDetail?.ActivitiesCount;
                if (_shareRoundPill != null) _shareRoundPill.SetActive(rounds.HasValue);
                _shareRound.gameObject.SetActive(rounds.HasValue);
                if (rounds.HasValue)
                {
                    int n = rounds.Value + 1;
                    _shareRound.text = string.Format(LocalizationManager.Get("SU_ROUND_N_FMT"),
                                                     n, OrdinalSuffix(n));
                }
            }

            if (_votePanel != null) _votePanel.SetActive(true);

            TelemetryService.Instance.RecordSafe(TelemetryEventNames.ScoreUploadPosted,
                () => new Dictionary<string, object>
                {
                    ["input_method"] = _draft.InputMethod,
                    ["gps_verified"] = r != null && r.GpsVerified,
                    ["trust"] = r != null ? r.Trust : 0,
                    ["points_earned"] = r != null ? r.PointsEarned : 0,
                    ["holes"] = _draft.HoleCount
                });
        }

        // ═════════════════════════════════════════════════════════════════════
        // Small helpers
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// The English ordinal SUFFIX only — "st" / "nd" / "rd" / "th", with the 11-13 exception a
        /// bare modulo gets wrong. It is passed as <c>{1}</c> so the STRING decides whether a
        /// language uses it at all: EN is "★ {0}{1} round", JA is "★ {0}ラウンド目" and simply
        /// ignores the extra argument. No language branch in code.
        /// </summary>
        private static string OrdinalSuffix(int n)
        {
            int lastTwo = n % 100;
            if (lastTwo >= 11 && lastTwo <= 13) return "th";
            switch (n % 10)
            {
                case 1:  return "st";
                case 2:  return "nd";
                case 3:  return "rd";
                default: return "th";
            }
        }

        private static void SetText(TextMeshProUGUI? label, string value)
        {
            if (label != null) label.text = value;
        }

        private static void SetLabel(TextMeshProUGUI? label, string key)
        {
            if (label != null) label.text = LocalizationManager.Get(key);
        }

        private static void SetInteractable(Button? button, bool on)
        {
            if (button != null) button.interactable = on;
        }
    }
}
