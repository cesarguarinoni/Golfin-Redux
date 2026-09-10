#nullable enable
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Golfin.Roster
{
    /// <summary>
    /// Keeps a character-name label from running underneath the status icons that overlay
    /// its top-right corner (IconSelectedBig / IconLevelUpBig).
    ///
    /// <para><b>The defect this fixes.</b> `CharacterNameText` is 489 px wide, `wrap = false`,
    /// `overflow = Overflow` and a fixed `fontSize = 45`, while the icons sit ON the label's
    /// rect rather than beside it — `StatusIconsRow` is anchored to the panel's top-right and
    /// its leftmost edge lands 418 px into the label. "CHRISTOFFERSON" renders 445 px at 45,
    /// so the level-up arrow drew over the final glyph. Nothing was wrong with the icon: the
    /// label simply had no idea the icon was there.</para>
    ///
    /// <para><b>Why margin and not the rect.</b> Shrinking the RectTransform would move the
    /// label's left edge or fight whatever laid it out. TMP's auto-sizer already fits text to
    /// `rect minus margin`, so reserving the icon strip in `margin.right` is the whole fix —
    /// the rect, the anchors and the alignment are untouched, and a name short enough to clear
    /// the icons still renders at the full design size.</para>
    ///
    /// <para><b>The reserve is measured, never hardcoded.</b> It is derived from the icons'
    /// live world corners each call, so moving or resizing `StatusIconsRow` in the scene needs
    /// no code change here — and only <i>active</i> icons reserve anything, so a character with
    /// no badges gets the full width back.</para>
    ///
    /// <para>⚠️ With <c>enableAutoSizing</c> on, <c>fontSize</c> becomes an <b>output</b> — TMP
    /// overwrites it on the next layout. That is why the design size is passed in as
    /// <paramref name="maxSize"/> and applied to <c>fontSizeMax</c>. Never read the current
    /// <c>fontSize</c> back to re-derive it; you would be reading whatever the last fit
    /// produced.</para>
    /// </summary>
    public static class NameIconFitter
    {
        /// <summary>Breathing room between the last glyph and the icon, in label-local px.</summary>
        public const float DefaultGap = 10f;

        /// <summary>
        /// Turn on auto-sizing for <paramref name="label"/> and reserve the width covered by any
        /// active icon among <paramref name="icons"/>. Safe to call every refresh; it is
        /// idempotent and does nothing if the label is null.
        /// </summary>
        public static void Fit(TextMeshProUGUI? label, float maxSize, float minSize,
                               float gap, params GameObject?[] icons)
        {
            if (label == null) return;

            // fontSizeMax must be set BEFORE enableAutoSizing, or the first fit runs against
            // whatever max the component happened to carry (0 on these labels, which TMP reads
            // as "no upper bound" and lets the text grow).
            label.fontSizeMax = maxSize;
            label.fontSizeMin = minSize;
            label.enableAutoSizing = true;

            var rt = label.rectTransform;
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            float labelRight = corners[2].x;

            // ⚠️ The icons live in a layout group, and callers toggle them with SetActive
            // immediately before calling here — so their rects are still one frame STALE.
            // Measured on the real panel: with both icons authored and only the level-up one
            // showing, the row had not yet collapsed, the surviving icon still reported its
            // two-icon x (1074 instead of 1036), and the reserve came out 36 instead of 74 —
            // the label "fitted" perfectly into a gap that no longer existed and the arrow
            // still sat on the last glyph. Rebuild each row ONCE before reading it.
            RebuildIconRows(icons);

            float iconLeft = float.MaxValue;
            if (icons != null)
            {
                foreach (var go in icons)
                {
                    // Only what is actually drawn can be collided with.
                    if (go == null || !go.activeInHierarchy) continue;
                    if (go.transform is not RectTransform irt) continue;
                    var ic = new Vector3[4];
                    irt.GetWorldCorners(ic);
                    if (ic[0].x < iconLeft) iconLeft = ic[0].x;
                }
            }

            if (iconLeft == float.MaxValue)
            {
                // No icon showing — give the full width back rather than leaving a stale reserve.
                SetRightMargin(label, 0f);
                return;
            }

            // World units → the label's own units, so a scaled canvas cannot skew the reserve.
            float scale = rt.lossyScale.x;
            if (Mathf.Approximately(scale, 0f)) scale = 1f;
            float reserve = Mathf.Max(0f, (labelRight - iconLeft) / scale + gap);
            SetRightMargin(label, reserve);
        }

        /// <inheritdoc cref="Fit(TextMeshProUGUI, float, float, float, GameObject[])"/>
        public static void Fit(TextMeshProUGUI? label, float maxSize, float minSize,
                               params GameObject?[] icons)
            => Fit(label, maxSize, minSize, DefaultGap, icons);

        /// <summary>
        /// Force the icons' own layout rows to settle so their rects are current. Scoped to
        /// those rows deliberately — <c>Canvas.ForceUpdateCanvases()</c> would rebuild every
        /// canvas in the scene, and an editor-side caller that later saves would bake the
        /// resulting anchor churn into the scene file.
        /// </summary>
        private static void RebuildIconRows(GameObject?[] icons)
        {
            if (icons == null) return;
            Transform? lastRow = null;
            foreach (var go in icons)
            {
                if (go == null) continue;
                Transform? row = go.transform.parent;
                if (row == null || row == lastRow) continue;   // rows repeat; rebuild each once
                lastRow = row;
                if (row is RectTransform rrt) LayoutRebuilder.ForceRebuildLayoutImmediate(rrt);
            }
        }

        private static void SetRightMargin(TextMeshProUGUI label, float right)
        {
            Vector4 m = label.margin;
            if (Mathf.Approximately(m.z, right)) return;   // no needless mesh rebuild
            label.margin = new Vector4(m.x, m.y, right, m.w);
        }
    }
}
