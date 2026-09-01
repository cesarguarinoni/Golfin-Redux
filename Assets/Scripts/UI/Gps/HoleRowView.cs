// score_upload_flow §2 — one row of the Edit Score holes panel (Figma 14024:32751 › Holes Panel).
#nullable enable
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// A single hole: its number, an optional meta line, − / + steppers and the score cell.
    ///
    /// <para>
    /// THE META LINE IS EMPTY IN V1 and that is a documented deviation, not an oversight: the Figma
    /// row reads "Par 4 · 380y" and PLAYLIFE has no hole-level course data to fill it from — no
    /// yardage, no per-hole par, not even from the recognition (which returns a round total). The
    /// label is kept in the prefab and blanked so the day that data exists is a one-line change.
    /// </para>
    /// <para>
    /// The score COLOUR follows the node exactly — see <see cref="ColourFor"/>. It needs a par for
    /// THIS hole, which nothing in the pipeline returns today, so in production every cell renders
    /// white (the "par unknown" case, which is also the "level par" case). The rule is live: the
    /// day a per-hole par arrives the colours arrive with it.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HoleRowView : MonoBehaviour
    {
        /// <summary>Golf's practical floor and a generous ceiling. The server never sees a hole
        /// outside this — it only ever sees their sum, which it bounds itself.</summary>
        public const int MinStroke = 1, MaxStroke = 15;

        [SerializeField] private TextMeshProUGUI? _number;
        [SerializeField] private TextMeshProUGUI? _meta;
        [SerializeField] private TextMeshProUGUI? _score;
        [SerializeField] private Button? _minus;
        [SerializeField] private Button? _plus;
        [SerializeField] private CanvasGroup? _group;

        /// <summary>What an untouched hole shows — an en dash, distinct from a real 0 and from the
        /// em dash the summary uses for "the API never told us".</summary>
        private const string EmptyCell = "–";

        // The node's four states, measured off the reference render: birdie-or-better #6FA5E8,
        // level white, bogey #EEDC9A, double-or-worse #F08080.
        private static readonly Color Under = new Color32(0x6F, 0xA5, 0xE8, 0xFF);
        private static readonly Color Level = Color.white;
        private static readonly Color Bogey = new Color32(0xEE, 0xDC, 0x9A, 0xFF);
        private static readonly Color Worse = new Color32(0xF0, 0x80, 0x80, 0xFF);

        private int _hole = 1;
        private int? _value;
        private int? _par;
        private Action<int, int?>? _onChanged;
        private bool _wired;

        /// <summary>Hole number is 1-based. <paramref name="onChanged"/> receives (hole, newValue).
        /// <paramref name="par"/> is null when this hole's par is unknown, which is every hole in
        /// v1 — the score then renders white, the same as level par.</summary>
        public void Bind(int hole, int? value, int? par, Action<int, int?> onChanged)
        {
            _hole = hole;
            _value = value;
            _par = par;
            _onChanged = onChanged;

            if (!_wired)
            {
                _wired = true;
                if (_minus != null) _minus.onClick.AddListener(() => Step(-1));
                if (_plus != null) _plus.onClick.AddListener(() => Step(+1));
            }

            if (_number != null) _number.text = hole.ToString();

            // The node reads "Par 4 · 380y". The par half is real when we have it; the yardage
            // never is, so the line is the par alone rather than a faked distance.
            if (_meta != null)
                _meta.text = par.HasValue
                    ? string.Format(LocalizationManager.Get("SU_HOLE_PAR_FMT"), par.Value)
                    : string.Empty;

            Repaint();
        }

        /// <summary>9-hole mode dims IN and takes it out of the tab order (frame 3b).</summary>
        public void SetActiveHole(bool active)
        {
            if (_group != null)
            {
                _group.alpha = active ? 1f : 0.35f;
                _group.interactable = active;
                _group.blocksRaycasts = active;
            }
            if (_minus != null) _minus.interactable = active;
            if (_plus != null) _plus.interactable = active;
        }

        /// <summary>
        /// First tap on an empty hole seeds a 4 rather than 1 or 5: a par-4 is the commonest hole in
        /// golf, so it is the fewest taps to the truth from a blank card.
        /// </summary>
        private void Step(int delta)
        {
            int next = _value.HasValue ? _value.Value + delta : 4;
            if (next < MinStroke) next = MinStroke;
            if (next > MaxStroke) next = MaxStroke;

            _value = next;
            Repaint();
            _onChanged?.Invoke(_hole, _value);
        }

        private void Repaint()
        {
            if (_score == null) return;
            _score.text = _value.HasValue ? _value.Value.ToString() : EmptyCell;
            _score.color = ColourFor(_value, _par);
            _score.alpha = _value.HasValue ? 1f : 0.45f;
        }

        /// <summary>
        /// The node's score-vs-par colouring (14024:32751 › Holes Panel), verified against the
        /// reference render row by row: par 4 / 5 gold, par 3 / 3 white, par 4 / 7 red,
        /// par 3 / 2 blue. An unknown par or an unplayed hole is white — there is nothing to
        /// compare, and white is the neutral the node already uses for level.
        /// </summary>
        public static Color ColourFor(int? score, int? par)
        {
            if (!score.HasValue || !par.HasValue) return Level;
            int d = score.Value - par.Value;
            if (d < 0) return Under;
            if (d == 0) return Level;
            return d == 1 ? Bogey : Worse;
        }
    }
}
