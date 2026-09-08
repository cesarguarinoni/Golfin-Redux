// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D3 — the numbers INSIDE a modal, as opposed to the top bar's.
//
// The character and club level-up modals are the same panel twice: a level readout, four
// stat rows with a confirmed bar, a pending bar and a value, all redrawn by one
// RefreshDisplay that runs on open AND on every [+] tap. Both wanted the same three things,
// so this is the one copy of them.
//
// TWO RULES, and both matter more than the animation does.
//
//   THE FIRST PAINT OF AN OPEN NEVER ANIMATES. RefreshDisplay runs when the modal opens, and
//   tweening a bar from zero there would show every character's stats "filling up" on arrival
//   — a loading animation over data that was already correct, which is the same mistake the
//   shimmer's cold-only rule exists to prevent. `Painted` gates it: false until the open's
//   first pass has drawn, true for every change the player then makes.
//
//   EVERY TWEEN SETTLES ON THE EXACT VALUE, INCLUDING WHEN INTERRUPTED. A stat bar stranded at
//   0.63 is a WRONG STAT, not a cosmetic blemish. And interruption is the NORMAL case here,
//   not the edge: the panel redraws on every [+] tap, so a player allocating four points in a
//   second interrupts three tweens. Each target keeps its own handle, so a second change
//   settles the first rather than racing it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish
{
    /// <summary>Per-modal animation state for its own readouts. One instance per controller.</summary>
    public sealed class ModalNumbers
    {
        private readonly MonoBehaviour _host;
        private readonly Dictionary<Image, Coroutine?> _bars = new Dictionary<Image, Coroutine?>();
        private readonly Dictionary<TMP_Text, Coroutine?> _numbers = new Dictionary<TMP_Text, Coroutine?>();
        private Coroutine? _pop;

        public ModalNumbers(MonoBehaviour host) { _host = host; }

        /// <summary>False until this open's first paint has drawn. See the header.</summary>
        public bool Painted { get; set; }

        /// <summary>Call when the modal opens, before the first RefreshDisplay.</summary>
        public void BeginOpen()
        {
            Painted = false;
            StopAll();
        }

        /// <summary>Settle everything and forget it — call on close.</summary>
        public void StopAll()
        {
            foreach (Image k in new List<Image>(_bars.Keys))
            {
                Coroutine? h = _bars[k];
                UiMotion.Stop(_host, ref h);
            }
            _bars.Clear();

            foreach (TMP_Text k in new List<TMP_Text>(_numbers.Keys))
            {
                Coroutine? h = _numbers[k];
                UiMotion.Stop(_host, ref h);
            }
            _numbers.Clear();

            UiMotion.Stop(_host, ref _pop);
        }

        /// <summary>Drive an Image's fillAmount. Snaps on the first paint and when unchanged.</summary>
        public void Bar(Image? bar, float to)
        {
            if (bar == null) return;
            to = Mathf.Clamp01(to);

            if (!Painted || Mathf.Approximately(bar.fillAmount, to))
            {
                Settle(bar);
                bar.fillAmount = to;
                return;
            }

            float from = bar.fillAmount;
            _bars.TryGetValue(bar, out Coroutine? handle);
            UiMotion.Run(_host, ref handle,
                         UiMotion.Tween(from, to, UiMotion.CountDur,
                                        v => { if (bar != null) bar.fillAmount = v; }));
            _bars[bar] = handle;
        }

        /// <summary>Count a bare integer readout. Snaps on the first paint and when unchanged.</summary>
        public void Number(TMP_Text? label, int to)
        {
            if (label == null) return;

            if (!Painted || !int.TryParse(label.text, out int from) || from == to)
            {
                Settle(label);
                label.text = to.ToString();
                return;
            }

            _numbers.TryGetValue(label, out Coroutine? handle);
            UiMotion.Run(_host, ref handle, UiMotion.CountUp(label, from, to, UiMotion.CountDur, "0"));
            _numbers[label] = handle;
        }

        /// <summary>
        /// Pop a readout that just CHANGED. The caller decides "changed" — on these panels the
        /// level is redrawn on every [+] tap, and a level that pops when only a stat point moved
        /// would be lying about what happened.
        /// </summary>
        public void Pop(Component? target)
        {
            if (!Painted || target == null) return;
            if (target.transform is not RectTransform rect) return;
            UiMotion.Run(_host, ref _pop, UiMotion.Pop(rect, null));
        }

        private void Settle(Image bar)
        {
            if (!_bars.TryGetValue(bar, out Coroutine? h)) return;
            UiMotion.Stop(_host, ref h);
            _bars.Remove(bar);
        }

        private void Settle(TMP_Text label)
        {
            if (!_numbers.TryGetValue(label, out Coroutine? h)) return;
            UiMotion.Stop(_host, ref h);
            _numbers.Remove(label);
        }
    }
}
