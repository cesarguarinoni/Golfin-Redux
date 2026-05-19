using UnityEngine;
using Golfin.Gameplay.Loop;
using Golfin.Gameplay.UI.HUD;
using Golfin.Gameplay.Session;
using Golfin.Gameplay.UI.ShotUI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// §2d: subscribes to BallStateMachine.OnShotComplete; on terminal=InCup,
    /// reads strokes/par/course/hole context, computes score, and shows the
    /// HoleCompleteWidget Result Screen. The widget's button handlers call back
    /// via PhysicsLabController.RearmAfterHoleComplete() when dismissed.
    ///
    /// Also exposes ShowForDebug() for the DebugShotPanel "Hole Out" button.
    ///
    /// Iter-6: loads real hole-map sprites from
    ///   Assets/Art/In-Game UI/HoleMaps/Lomond - Hole N.png
    /// and populates NextHolePar + NextHoleTipText from HoleDatabase.csv via
    /// a lightweight CSV parse (editor-only; plays fine in LabScaffold).
    /// </summary>
    public class HoleCompleteDriver : MonoBehaviour, Golfin.Gameplay.UI.ShotUI.IHoleOutTrigger
    {
        [SerializeField] PhysicsLabController controller;
        [SerializeField] HoleCompleteWidget widget;

        BallStateMachine _sm;

        void Awake()
        {
            if (controller == null) controller = GetComponentInParent<PhysicsLabController>();
            _sm = controller != null ? controller.BallSM : null;
            if (_sm != null) _sm.OnShotComplete += HandleShotComplete;
            if (widget != null) widget.Hide();
        }

        void OnDestroy()
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
        }

        void HandleShotComplete(ShotResult result)
        {
            if (result.TerminalState != BallState.InCup) return;

            // Stage B: fire cross-scene signal so the ShellScene Result modal
            // (lands in Stage C) can react regardless of whether the lab widget
            // is hosting the modal locally.
            var completionData = new HoleCompletionData(
                terminalState:  result.TerminalState,
                strokes:        GameSession.TurnCount,
                penaltyStrokes: ComputePenaltyStrokesFromHistory(),
                holeNumber:     GameSession.CurrentHoleNumber > 0
                                    ? GameSession.CurrentHoleNumber
                                    : HoleContext.HoleNumber
            );
            GameSession.MarkHoleComplete(completionData);

            // Existing lab path (kept for now; Stage C migrates this off):
            ShowResultScreen(GameSession.TurnCount);
        }

        static int ComputePenaltyStrokesFromHistory()
        {
            int sum = 0;
            foreach (var rec in GameSession.ShotHistory) sum += rec.PenaltyStrokes;
            return sum;
        }

        // Public entrypoint — used by both the real InCup path and DebugShotPanel.
        public void ShowForDebug()
        {
            ShowResultScreen(GameSession.TurnCount > 0 ? GameSession.TurnCount : 1);
        }

        void ShowResultScreen(int strokes)
        {
            if (widget == null)
            {
                Debug.LogWarning("[HoleCompleteDriver] InCup fired but widget reference is null.");
                return;
            }

            int par = HoleContext.Par;
            int score = strokes - par;
            string scoreLabel = ScoreLabelFor(score);
            bool isFailed = score > 0;
            bool hasPersonalBest = false; // Q8 lock — no save layer in §2d.

            int holeNumber     = HoleContext.HoleNumber;
            int nextHoleNumber = holeNumber + 1;

            // ── Load real hole-map sprites (iter-6) ──────────────────────────
            Sprite holeMap     = LoadHoleMap(holeNumber);
            Sprite nextHoleMap = LoadHoleMap(nextHoleNumber);  // null if hole 19+ doesn't exist

            // ── Load next-hole info from HoleDatabase CSV (iter-6) ───────────
            int    nextHolePar  = 0;
            string nextHoleDesc = "—";
            LookupNextHoleInfo(nextHoleNumber, out nextHolePar, out nextHoleDesc);

            var data = new HoleCompleteData(
                strokes:          strokes,
                par:              par,
                score:            score,
                scoreLabel:       scoreLabel,
                isFailed:         isFailed,
                hasPersonalBest:  hasPersonalBest,
                courseName:       HoleContext.CourseName,
                holeNumber:       holeNumber,
                teeName:          HoleContext.TeeName,
                // Placeholders (Q8, no PB / no time tracking / no rewards economy):
                bestStrokes:      "—",
                bestStrokesLabel: "",
                timeStr:          "00:00:00",
                bestTimeStr:      "—",
                rewardCoinX:      10,
                rewardRepairX:    10,
                rewardBallX:      10,
                nextHoleNumber:   nextHoleNumber,
                nextHolePar:      nextHolePar,
                nextHoleTipText:  nextHoleDesc,
                holeMap:          holeMap,
                nextHoleMap:      nextHoleMap
            );

            widget.Show(data, OnModalClose);
        }

        void OnModalClose()
        {
            if (widget != null) widget.Hide();
            controller?.RearmAfterHoleComplete();
        }

        // ── Hole map loading (iter-6) ────────────────────────────────────────

        /// <summary>
        /// Loads the hole map sprite for the given hole number from
        /// Assets/Art/In-Game UI/HoleMaps/Lomond - Hole N.png.
        /// Editor-only (AssetDatabase). Returns null gracefully if the asset
        /// doesn't exist (e.g. hole 19+).
        /// </summary>
        static Sprite LoadHoleMap(int holeNumber)
        {
#if UNITY_EDITOR
            string path = $"Assets/Art/In-Game UI/HoleMaps/Lomond - Hole {holeNumber}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite == null)
                Debug.Log($"[HoleCompleteDriver] HoleMap not found at {path} (may not exist for hole {holeNumber}).");
            return sprite;
#else
            // Runtime fallback: HoleMaps are not in Resources, so return null.
            // The widget will display the placeholder sprite set by the builder.
            return null;
#endif
        }

        // ── Next-hole info from HoleDatabase.csv (iter-6) ───────────────────

        /// <summary>
        /// Parses Assets/Data/HoleDatabase.csv for the row matching nextHoleNumber,
        /// extracts par and description localization key, and resolves the description
        /// via LocalizationManager.
        ///
        /// CSV format: courseNameKey,holeNumber,par,descriptionKey,...
        /// Safe: returns 0/empty if parsing fails (no exception thrown).
        /// </summary>
        static void LookupNextHoleInfo(int holeNumber, out int par, out string description)
        {
            par = 0;
            description = "—";

#if UNITY_EDITOR
            const string csvPath = "Assets/Data/HoleDatabase.csv";
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            if (textAsset == null)
            {
                Debug.LogWarning($"[HoleCompleteDriver] HoleDatabase.csv not found at {csvPath}");
                return;
            }

            string[] lines = textAsset.text.Split('\n');
            // CSV header: courseNameKey,holeNumber,par,descriptionKey,...
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                string[] cols = line.Split(',');
                if (cols.Length < 4) continue;

                if (!int.TryParse(cols[1], out int rowHoleNum)) continue;
                if (rowHoleNum != holeNumber) continue;

                // Found the row
                if (int.TryParse(cols[2], out int rowPar))
                    par = rowPar;

                string descKey = cols[3].Trim();
                if (!string.IsNullOrEmpty(descKey))
                {
                    // Description is stored as a localization key (e.g. "HOLE_LOMOND_2_DESC").
                    // LocalizationManager is in Assembly-CSharp (root) which Golfin.Physics.Viewer
                    // does not reference. We read the EN description directly from the localization CSV.
                    description = LoadLocalizationEN(descKey);
                }
                return;
            }
            // Row not found (e.g. hole 19 doesn't exist)
            Debug.Log($"[HoleCompleteDriver] LookupNextHoleInfo: no row for hole {holeNumber} in HoleDatabase.csv.");
#else
            // Runtime: HoleDatabase CSV parsing not yet wired for runtime (§2e+ job).
            par = 0;
            description = "—";
#endif
        }

        // ── Localization CSV reader (editor-only) ────────────────────────────

        /// <summary>
        /// Reads the English text for a localization key directly from
        /// Assets/Localization/LocalizationText.csv.
        /// Avoids taking a dependency on LocalizationManager (Assembly-CSharp)
        /// which is not in the Golfin.Physics.Viewer asmdef references.
        ///
        /// CSV format: key,"EN text",JP text
        /// Returns the key itself if not found (safe fallback).
        /// </summary>
        static string LoadLocalizationEN(string key)
        {
#if UNITY_EDITOR
            const string csvPath = "Assets/Localization/LocalizationText.csv";
            var textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(csvPath);
            if (textAsset == null)
            {
                Debug.LogWarning($"[HoleCompleteDriver] LocalizationText.csv not found at {csvPath}");
                return key;
            }
            string[] lines = textAsset.text.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim();
                if (!line.StartsWith(key + ",")) continue;
                // Key matches. Parse: key,"EN value",JP value
                // EN value may be quoted with commas inside.
                int firstComma = line.IndexOf(',');
                if (firstComma < 0) return key;
                string rest = line.Substring(firstComma + 1);
                if (rest.StartsWith("\""))
                {
                    // Quoted field — find closing quote
                    int closeQuote = rest.IndexOf('"', 1);
                    if (closeQuote > 0)
                        return rest.Substring(1, closeQuote - 1);
                }
                else
                {
                    // Unquoted — take up to next comma
                    int nextComma = rest.IndexOf(',');
                    return nextComma >= 0 ? rest.Substring(0, nextComma) : rest;
                }
                return key;
            }
            return key; // key not found
#else
            return key;
#endif
        }

        public static string ScoreLabelFor(int score)
        {
            switch (score)
            {
                case -3: return "Albatross";
                case -2: return "Eagle";
                case -1: return "Birdie";
                case  0: return "Par";
                case  1: return "Bogey";
                case  2: return "Double Bogey";
                case  3: return "Triple Bogey";
                default: return score < 0 ? $"{score}" : $"+{score}";
            }
        }

        // EditMode-test injection helper.
        internal void InjectForTests(BallStateMachine sm, HoleCompleteWidget w)
        {
            if (_sm != null) _sm.OnShotComplete -= HandleShotComplete;
            _sm = sm;
            widget = w;
            if (_sm != null) _sm.OnShotComplete += HandleShotComplete;
        }

        // EditMode-test direct handler invocation — bypasses SM event firing.
        // Allows tests to verify logic without a full BallSimulation run.
        internal void HandleShotCompleteForTest(ShotResult result) => HandleShotComplete(result);
    }
}

