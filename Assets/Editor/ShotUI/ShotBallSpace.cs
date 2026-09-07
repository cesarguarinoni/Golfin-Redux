using UnityEngine;
using UnityEditor;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// The <c>BallSpace</c> rect every scheme root's ball-relative UI hangs off
    /// (shot_view_layout §3.2), and the one place that creates it.
    ///
    /// <para>WHY A CONTAINER AND NOT FOUR SETS OF OFFSETS. Before this, every scheme's lane,
    /// bar, handle, rings and grade pop were anchored to the CANVAS CENTRE with absolute
    /// offsets that silently assumed the ball was at (0,0). Moving the ball would have meant
    /// re-deriving every one of those numbers, in four builders, and getting it wrong once is a
    /// lane drawn 300px away from the club sliding down it. One rect between the root and its
    /// children turns "move the whole scheme" into a single write, and the offsets stay the
    /// ball-relative numbers the Figma nodes actually state.</para>
    ///
    /// <para>FULL-STRETCH, NOT ZERO-SIZED. The rect matches the canvas exactly, so a child keeps
    /// whatever anchors it already had — Flick's <c>ConeRoot</c> is itself full-stretch and
    /// <c>PutterTrack</c> is anchored to the canvas TOP, and both survive unchanged. A zero-sized
    /// container would collapse the first and re-base the second. <c>anchoredPosition</c> on a
    /// stretched rect is a pure offset, which is exactly what is wanted here.</para>
    /// </summary>
    public static class ShotBallSpace
    {
        public const string Name = "BallSpace";

        /// <summary>
        /// Return <paramref name="schemeRoot"/>'s <c>BallSpace</c>, creating it if absent, and
        /// empty it ready for a rebuild.
        ///
        /// <para>The existing rect is REUSED rather than replaced: <c>ShotLayoutController</c>
        /// holds a serialized reference to it, and a builder re-run that handed back a new object
        /// would leave that reference dangling and the scheme stuck at the canvas centre. Its
        /// <c>anchoredPosition</c> is preserved for the same reason — the controller owns that
        /// number at runtime, and the authored value is what the Editor shows before play.</para>
        /// </summary>
        public static RectTransform EnsureAndClear(RectTransform schemeRoot)
        {
            RectTransform space = Ensure(schemeRoot);

            for (int i = space.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(space.GetChild(i).gameObject);

            // Anything the previous layout left directly on the root (a pre-BallSpace build)
            // goes too — the builder owns every one of them and is about to remake them.
            for (int i = schemeRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = schemeRoot.GetChild(i);
                if (child != space) Object.DestroyImmediate(child.gameObject);
            }

            return space;
        }

        /// <summary>Create or find the rect, without touching any children. Used by the
        /// one-off scene migration for roots that no builder owns (Flick).</summary>
        public static RectTransform Ensure(RectTransform schemeRoot)
        {
            Transform found = schemeRoot.Find(Name);
            RectTransform space = found as RectTransform;

            bool created = space == null;
            if (created)
            {
                var go = new GameObject(Name, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(go, "Create BallSpace");
                space = (RectTransform)go.transform;
                space.SetParent(schemeRoot, worldPositionStays: false);
                space.SetSiblingIndex(0);
            }

            // Re-asserted AFTER Configure: changing anchors makes Unity rewrite the offsets to
            // hold the rect still, which moves anchoredPosition out from under us.
            Vector2 offset = created ? Vector2.zero : space.anchoredPosition;
            Configure(space);
            space.anchoredPosition = offset;
            return space;
        }

        /// <summary>Full-stretch, zero offsets, centre pivot — with <c>anchoredPosition</c>
        /// deliberately left alone.</summary>
        public static void Configure(RectTransform space)
        {
            space.anchorMin   = Vector2.zero;
            space.anchorMax   = Vector2.one;
            space.pivot       = new Vector2(0.5f, 0.5f);
            space.sizeDelta   = Vector2.zero;
            space.localScale  = Vector3.one;
            space.localRotation = Quaternion.identity;
        }
    }
}
