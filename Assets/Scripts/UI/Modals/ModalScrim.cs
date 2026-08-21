using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI.Modals
{
    /// <summary>
    /// The two guarantees every modal overlay in the game owes the player:
    ///
    ///   1. the UI behind it is visibly DARKENED, and
    ///   2. nothing behind it can be TAPPED.
    ///
    /// Both were being satisfied per-modal by whatever the scene happened to be authored with,
    /// and two failure modes had crept in (measured in play mode 2026-08-21, iPhone-14 frame):
    ///
    ///   • <b>Too-weak scrims.</b> The project renders in LINEAR colour space, so a 50%-alpha
    ///     black scrim only pulls an sRGB white down to ~187/255 — it reads as "no dim at all".
    ///     Settings (<c>SettingsScreen/Background</c>) and Matchmaking were both authored at
    ///     50%. Measured: with Settings open the top bar went 26,72,113 → 16,50,81. The heavier
    ///     modals already sit at 70–92%, which is what actually reads as a scrim.
    ///
    ///   • <b>Scrims that never reached the persistent chrome.</b> The top bar and bottom nav
    ///     live on their own root canvas (<c>PersistentUI</c>, sortingOrder 0) while the screens
    ///     — and therefore most modals — live on <c>Canvas</c> at sortingOrder −1. A modal
    ///     parented under a screen paints BELOW the bars, so with the roster Level Up modal open
    ///     the nav buttons stayed at full brightness AND stayed clickable. Measured: the roster
    ///     card behind the modal dimmed 242 → 141, while the nav bar sat unchanged at 15,40,71.
    ///
    /// This helper enforces both at <see cref="ModalController.Show"/> time instead, so a modal
    /// is correct by construction and no scene needs re-authoring (which is also why nothing here
    /// touches serialized data — see the scene-save churn scar tissue in CLAUDE.md).
    ///
    /// Deliberately a FLOOR, not a fixed value: a modal authored darker than
    /// <see cref="MinAlpha"/> (the hole-complete and tournament-result screens at 92%) keeps its
    /// own look. This only lifts the ones that were too light to read as a scrim.
    /// </summary>
    public static class ModalScrim
    {
        /// <summary>
        /// Minimum scrim opacity, in the middle of the range the heavier modals already ship with
        /// (bag/item modals 0.85, hole-complete and tournament screens 0.92). Measured on the
        /// iPhone-14 frame, an sRGB white behind the scrim lands at ~123/255 here, versus ~149 at
        /// 0.70 and ~187 at the 0.50 that made Settings read as undimmed.
        /// </summary>
        public const float MinAlpha = 0.80f;

        /// <summary>
        /// Sorting order a modal is lifted to so its scrim covers the <c>PersistentUI</c> chrome
        /// (top bar + bottom nav, sortingOrder 0). Chosen to sit above every screen-level canvas
        /// and below the overlays that must stay on top of a modal: the hole-complete /
        /// tournament-result screens (900) and the toast layer (950).
        /// </summary>
        public const int SortingOrder = 500;

        /// <summary>Name given to a scrim this helper had to create because none was authored.</summary>
        public const string AutoScrimName = "AutoScrim";

        /// <summary>
        /// Bring <paramref name="modalRoot"/> up to both guarantees and return the scrim to show.
        ///
        /// Returns the authored <paramref name="backdrop"/> (normalized), a newly created scrim,
        /// or <c>null</c> when the modal needs none because <paramref name="panel"/> already
        /// covers the whole canvas by itself — in which case the panel is made raycast-blocking
        /// instead. Callers assign the result back over their backdrop reference.
        /// </summary>
        public static GameObject Apply(Transform modalRoot, GameObject backdrop, GameObject panel = null)
        {
            if (modalRoot == null) return backdrop;

            LiftAbovePersistentChrome(modalRoot);

            if (backdrop == null)
            {
                // A modal whose panel already fills the canvas (the versus/hole result screens)
                // hides everything behind it on its own; adding a scrim under it would only
                // double-darken artwork the player cannot see anyway.
                if (CoversCanvas(panel))
                {
                    BlockInput(panel);
                    return null;
                }

                backdrop = CreateScrim(modalRoot);
            }

            NormalizeScrim(backdrop);
            return backdrop;
        }

        // ── Sorting ───────────────────────────────────────────────────────────

        /// <summary>
        /// Give the modal its own sorting scope at <see cref="SortingOrder"/> so both its scrim
        /// and its panel paint over the PersistentUI chrome. Never lowers a modal that already
        /// declares a higher order (hole-complete 900, versus result 901).
        /// </summary>
        private static void LiftAbovePersistentChrome(Transform modalRoot)
        {
            var canvas = modalRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = modalRoot.gameObject.AddComponent<Canvas>();

            if (canvas.sortingOrder < SortingOrder)
            {
                // overrideSorting is what makes a NESTED canvas leave its parent's order behind.
                // It is ignored on a root canvas (Settings owns one), where sortingOrder alone rules.
                if (!canvas.isRootCanvas) canvas.overrideSorting = true;
                canvas.sortingOrder = SortingOrder;
            }
            else if (!canvas.isRootCanvas && !canvas.overrideSorting)
            {
                canvas.overrideSorting = true;
            }

            // Graphics register against their NEAREST enabled canvas, and a GraphicRaycaster only
            // raycasts the graphics of its own canvas — so a sorting canvas without a raycaster
            // would make the whole modal untappable.
            if (modalRoot.GetComponent<GraphicRaycaster>() == null)
                modalRoot.gameObject.AddComponent<GraphicRaycaster>();
        }

        // ── Scrim ─────────────────────────────────────────────────────────────

        private static GameObject CreateScrim(Transform modalRoot)
        {
            var go = new GameObject(AutoScrimName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = modalRoot.gameObject.layer;

            var rt = (RectTransform)go.transform;
            rt.SetParent(modalRoot, false);
            rt.SetAsFirstSibling(); // behind the panel

            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, MinAlpha);

            return go;
        }

        /// <summary>
        /// Make an existing scrim actually do its job: opaque enough to read as a dim, raycast
        /// blocking, and geometrically covering the whole canvas rather than just its own parent
        /// (the inventory modals hang off a shorter <c>ContentArea</c>).
        /// </summary>
        private static void NormalizeScrim(GameObject backdrop)
        {
            if (backdrop == null) return;

            var img = backdrop.GetComponent<Image>();
            if (img != null)
            {
                img.enabled = true;
                img.raycastTarget = true;

                var c = img.color;
                if (c.a < MinAlpha) img.color = new Color(c.r, c.g, c.b, MinAlpha);
            }

            StretchOverCanvas(backdrop.transform as RectTransform);
        }

        /// <summary>
        /// Size and centre <paramref name="scrim"/> on the ROOT canvas rect, expressed in its own
        /// parent's space. Anchor-stretching only covers the parent, which is not the screen for
        /// a modal nested inside a screen section.
        /// </summary>
        private static void StretchOverCanvas(RectTransform scrim)
        {
            if (scrim == null) return;

            var parent = scrim.parent as RectTransform;
            if (parent == null) return;

            var canvas = scrim.GetComponentInParent<Canvas>();
            var rootRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (rootRect == null) return;

            var corners = new Vector3[4];
            rootRect.GetWorldCorners(corners);

            Vector2 min = parent.InverseTransformPoint(corners[0]);
            Vector2 max = parent.InverseTransformPoint(corners[2]);

            scrim.anchorMin = scrim.anchorMax = new Vector2(0.5f, 0.5f);
            scrim.pivot = new Vector2(0.5f, 0.5f);
            scrim.sizeDelta = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            scrim.anchoredPosition = (min + max) * 0.5f - parent.rect.center;
        }

        // ── Full-screen panels ────────────────────────────────────────────────

        /// <summary>True when <paramref name="panel"/> is at least as large as the root canvas.</summary>
        private static bool CoversCanvas(GameObject panel)
        {
            if (panel == null) return false;

            var rt = panel.transform as RectTransform;
            var canvas = panel.GetComponentInParent<Canvas>();
            var rootRect = canvas != null ? canvas.rootCanvas.transform as RectTransform : null;
            if (rt == null || rootRect == null) return false;

            Vector2 canvasSize = rootRect.rect.size;
            Vector2 panelSize = rt.rect.size;

            // 1px slack: authored full-screen rects are routinely off by a rounding step.
            return panelSize.x >= canvasSize.x - 1f && panelSize.y >= canvasSize.y - 1f;
        }

        /// <summary>
        /// Make a full-screen panel swallow taps meant for the UI behind it. Uses the panel's own
        /// Image when it has one, otherwise the first canvas-covering Image among its children
        /// (the versus result screen keeps its fill on a <c>BG</c> child).
        /// </summary>
        private static void BlockInput(GameObject panel)
        {
            if (panel == null) return;

            var own = panel.GetComponent<Image>();
            if (own != null)
            {
                own.raycastTarget = true;
                return;
            }

            foreach (Transform child in panel.transform)
            {
                var img = child.GetComponent<Image>();
                if (img == null || !CoversCanvas(child.gameObject)) continue;

                img.raycastTarget = true;
                return;
            }
        }
    }
}
