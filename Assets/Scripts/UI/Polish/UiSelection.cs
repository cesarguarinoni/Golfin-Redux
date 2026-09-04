// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D6 — what a selection pill does when it becomes the selected one.
//
// TWO IMAGES, ALPHA-SWAPPED. Never `Image.color` tinting: the GPS pills are
// 9-sliced sprites with baked rims (a 4px #F3ECC2 ring on the unselected avatar
// disc, an 8px #EEDC9A ring on the selected one), and tinting one into the other
// multiplies the rim as well as the fill — the ring goes muddy and the two
// states stop being the two states the design drew. This is Build rule 2 from
// `gps_profile_pack`, and it is why nothing here touches a colour.
//
// TWO SHAPES OF CALL SITE, because the prefabs are authored two ways:
//   CrossFade(show, hide) — the design already drew both states as two objects
//                           (the vote chip's Off/On pair, the gift amount
//                           button's Selected overlay). They were SetActive
//                           swapped; now they dissolve.
//   SetSprite(image, s)   — the design drew ONE Image whose sprite is swapped
//                           (the Golf Profile swatches and experience chips).
//                           A transient overlay child carries the incoming
//                           sprite and fades in on top, then the base Image
//                           takes it and the overlay drops back to alpha 0.
//
// EVERYTHING RESTS INVISIBLE. The overlay is created at runtime, is never a
// raycast target, and settles at alpha 0 — so no prefab gains a child and no
// rest pixel moves (A2).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Polish
{
    /// <summary>Selection bump + two-Image cross-fade for the GPS selection pills.</summary>
    public static class UiSelection
    {
        /// <summary>The runtime overlay's name, so a second call finds the one it made.</summary>
        public const string OverlayName = "SelectionFade";

        // ═════════════════════════════════════════════════════════════════════
        // Bump
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>Scale 1 → 1.06 → 1 on the element that just became selected (§D6).</summary>
        public static void Bump(MonoBehaviour host, Transform? target)
        {
            if (host == null || target == null) return;
            if (target is RectTransform rt) UiMotion.Run(host, UiMotion.Bump(rt));
        }

        // ═════════════════════════════════════════════════════════════════════
        // game_polish_a §D3 — a list that repaints
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Fade a panel out, run <paramref name="repaint"/>, fade it back in.
        ///
        /// <para>The game's tab bars do not swap two authored panels the way the GPS pills swap two
        /// authored states — the Rankings tabs and the gacha history rebuild the SAME list in
        /// place, destroying and respawning rows. There is nothing to cross-fade WITH, so the
        /// honest motion is to hide the swap rather than to dissolve between two things that never
        /// coexist.</para>
        ///
        /// <para>The repaint runs at the MIDPOINT, while the panel is invisible — a repaint before
        /// the fade would show the new list fading out, and one after would pop it in at full
        /// alpha. And it runs UNCONDITIONALLY, on every exit path: a repaint skipped because motion
        /// was off, or because the host was disabled mid-fade, is a list that silently stops
        /// updating. That is why it is passed as an Action rather than being left to the caller
        /// after the call.</para>
        /// </summary>
        public static void FadeSwap(MonoBehaviour? host, CanvasGroup? group, System.Action repaint)
        {
            if (repaint == null) return;

            if (group == null || host == null || !host.isActiveAndEnabled || !UiMotion.Enabled
                || !Application.isPlaying)
            {
                repaint();
                if (group != null) group.alpha = 1f;
                return;
            }

            UiMotion.Run(host, UiMotion.Then(UiMotion.Fade(group, group.alpha, 0f), () =>
            {
                repaint();
                // The second half is started from the finalizer, so an interruption that settles
                // the fade-out still repaints AND still leaves the panel visible rather than
                // stranding it at alpha 0 — a stranded list looks like a screen that failed to load.
                if (group == null) return;
                if (host == null || !host.isActiveAndEnabled) { group.alpha = 1f; return; }
                UiMotion.Run(host, UiMotion.Fade(group, 0f, 1f));
            }));
        }

        /// <summary>
        /// The tab bars' active indicator, which the game authors as one Image per tab toggled with
        /// <c>enabled</c> or <c>SetActive</c>.
        ///
        /// <para>§D3 offers a choice: one sliding indicator, or cross-fade the four. Cross-fade,
        /// and the reason is A2 — a single sliding indicator means deleting three authored Images
        /// and re-anchoring the fourth, which changes the REST geometry of four tab bars. The spec
        /// says to choose the one that keeps 0 px rest parity, and this is it. The object stays
        /// active with a CanvasGroup at alpha 0, which renders identically to
        /// <c>enabled = false</c>.</para>
        /// </summary>
        public static void Indicator(MonoBehaviour? host, Component? indicator, bool on, bool animate)
        {
            if (indicator == null) return;
            GameObject go = indicator.gameObject;

            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();

            // Whatever the prefab did before, the object is active from here on and the ALPHA is
            // the state — otherwise a fade would be running on a deactivated object.
            if (!go.activeSelf) go.SetActive(true);
            var img = indicator as Image;
            if (img != null && !img.enabled) img.enabled = true;

            if (!animate || host == null || !host.isActiveAndEnabled || !UiMotion.Enabled
                || !Application.isPlaying)
            {
                cg.alpha = on ? 1f : 0f;
                return;
            }
            UiMotion.Run(host, UiMotion.Fade(cg, cg.alpha, on ? 1f : 0f));
        }

        // ═════════════════════════════════════════════════════════════════════
        // Two authored objects
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Dissolve between two authored state objects. <paramref name="animate"/> false is the
        /// instant path (a repaint, a screen opening, motion disabled) and leaves exactly the
        /// active/alpha state the SetActive swap used to leave.
        /// </summary>
        public static void CrossFade(MonoBehaviour host, GameObject? show, GameObject? hide,
                                     bool animate)
        {
            if (show == null && hide == null) return;

            if (!animate || host == null || !host.isActiveAndEnabled || !UiMotion.Enabled)
            {
                Settle(show, true);
                Settle(hide, false);
                return;
            }

            if (show != null)
            {
                CanvasGroup cg = Group(show);
                int gen = Stamp(show);
                show.SetActive(true);
                UiMotion.Run(host, UiMotion.Then(UiMotion.Fade(cg, 0f, 1f), () =>
                {
                    if (show == null || Gen(show) != gen) return;
                    cg.alpha = 1f;
                }));
            }

            if (hide != null && hide.activeSelf)
            {
                CanvasGroup cg = Group(hide);
                int gen = Stamp(hide);
                UiMotion.Run(host, UiMotion.Then(UiMotion.Fade(cg, cg.alpha, 0f), () =>
                {
                    // GENERATION CHECK, and it is not defensive padding. Tapping PUBLIC → MINE →
                    // PUBLIC inside 0.3 s queues a hide for the chip that is by then the SHOWN
                    // one, and this tail would deactivate it — a chip that vanishes because the
                    // player tapped it twice.
                    if (hide == null || Gen(hide) != gen) return;
                    hide.SetActive(false);
                    cg.alpha = 1f;              // rest state is "hidden and opaque", as authored
                }));
            }
        }

        private static void Settle(GameObject? go, bool on)
        {
            if (go == null) return;
            Stamp(go);
            CanvasGroup cg = Group(go);
            cg.alpha = 1f;
            if (go.activeSelf != on) go.SetActive(on);
        }

        // ═════════════════════════════════════════════════════════════════════
        // One Image, two sprites
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Move <paramref name="image"/> onto <paramref name="target"/> through a
        /// <see cref="UiMotion.FadeDur"/> dissolve rather than a hard swap.
        /// </summary>
        public static void SetSprite(MonoBehaviour host, Image? image, Sprite? target, bool animate)
        {
            if (image == null) return;
            if (image.sprite == target) return;

            if (!animate || host == null || !host.isActiveAndEnabled || !UiMotion.Enabled
                || image.sprite == null || target == null)
            {
                image.sprite = target;
                HideOverlay(image);
                return;
            }

            Image overlay = Overlay(image);
            overlay.sprite                 = target;
            overlay.type                   = image.type;
            overlay.pixelsPerUnitMultiplier = image.pixelsPerUnitMultiplier;
            overlay.color                  = image.color;
            overlay.preserveAspect         = image.preserveAspect;

            CanvasGroup cg = Group(overlay.gameObject);
            int gen = Stamp(overlay.gameObject);
            overlay.gameObject.SetActive(true);

            UiMotion.Run(host, UiMotion.Then(UiMotion.Fade(cg, 0f, 1f), () =>
            {
                if (overlay == null || image == null || Gen(overlay.gameObject) != gen) return;
                image.sprite = target;          // the base Image takes the new state…
                cg.alpha = 0f;
                overlay.gameObject.SetActive(false);   // …and the overlay goes back to nothing
            }));
        }

        /// <summary>The overlay child, created on first use. Stretched over its host Image, never
        /// a raycast target — it must not swallow the tap that selected the pill.</summary>
        private static Image Overlay(Image image)
        {
            Transform? t = image.transform.Find(OverlayName);
            if (t != null)
            {
                var existing = t.GetComponent<Image>();
                if (existing != null) return existing;
            }

            var go = new GameObject(OverlayName, typeof(RectTransform), typeof(CanvasRenderer),
                                    typeof(Image), typeof(CanvasGroup));
            var rt = (RectTransform)go.transform;
            rt.SetParent(image.transform, worldPositionStays: false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;

            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            go.GetComponent<CanvasGroup>().alpha = 0f;
            go.SetActive(false);
            return img;
        }

        private static void HideOverlay(Image image)
        {
            Transform? t = image.transform.Find(OverlayName);
            if (t == null) return;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
            t.gameObject.SetActive(false);
        }

        // ═════════════════════════════════════════════════════════════════════
        // Shared
        // ═════════════════════════════════════════════════════════════════════

        private static CanvasGroup Group(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        private static UiFadeGeneration Tag(GameObject go)
        {
            var t = go.GetComponent<UiFadeGeneration>();
            if (t == null) t = go.AddComponent<UiFadeGeneration>();
            return t;
        }

        private static int Stamp(GameObject go) => ++Tag(go).Generation;
        private static int Gen(GameObject go)   => Tag(go).Generation;
    }

    /// <summary>
    /// The counter that lets a cross-fade tail know it is still the newest request for its
    /// object. Hidden; added on demand by <see cref="UiSelection"/>, never authored.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("")]
    public sealed class UiFadeGeneration : MonoBehaviour
    {
        public int Generation;
    }
}
