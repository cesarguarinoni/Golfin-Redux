// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D2 — the 16 px entry rise, for screens reached through the fade.
//
// ONE COMPONENT, NOT FOURTEEN OnEnable EDITS. The behaviour is identical on every
// shell screen and the fourteen controllers have nothing else in common; fourteen
// copies would be fourteen places to forget the SkipEntry check — and forgetting
// it is not a subtle bug, it is a screen that plays two entrances in 0.25 s.
// GpsScreenEntryMotion made the same call for the same reason on eight screens.
//
// WHY IT MUST NOT RUN AFTER A PUSH. A pushed screen has already animated in: its
// content slid from ±W. Rising it 16 px again, from alpha 0, on the frame it
// arrives would read as a stutter at the end of an otherwise continuous move.
// LayeredPush arms EnteringViaPush around the one SetActive that starts a push,
// and this is the only thing in the game shell that reads it.
//
// THE RECTS ARE SERIALIZED, NOT LOOKED UP, and that is the one real difference
// from the GPS component. Over there every screen's content layer is called
// `ContentContainer`, so a Find is exact. Here it is `Content` / `ContentArea` /
// `CardsContainer` / `GameScreenContent` depending on which task built the
// screen, and — more to the point — WHICH children should rise is a judgement the
// spec makes per screen (Roster rises its DetailPanel but NOT the character
// stage; Inventory rises its ContentArea but not the TabBar or the Rim, which are
// chrome). A name-based lookup could not express either. GamePolishBuilder fills
// these in from LayeredPush's own layer table, so the push and the rise cannot
// disagree about what "content" means on a given screen.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Golfin.UI.Polish
{
    /// <summary>
    /// Rises a shell screen's content layer(s) into place when the screen is shown through the
    /// boundary fade. Added and wired by <c>GamePolishBuilder</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenEntryMotion : MonoBehaviour
    {
        [Tooltip("The content rects that rise on a fade-path arrival. Chrome (backgrounds, the " +
                 "Inventory tab bar and rim) is deliberately NOT listed — it does not move.")]
        [SerializeField] private RectTransform[] _content = new RectTransform[0];

        private readonly List<Coroutine?> _motions = new List<Coroutine?>();

        private void OnEnable()
        {
            // A screen that arrived on a push is already where it should be, at full alpha.
            if (LayeredPush.EnteringViaPush) return;
            if (_content == null || _content.Length == 0) return;

            while (_motions.Count < _content.Length) _motions.Add(null);

            for (int i = 0; i < _content.Length; i++)
            {
                RectTransform r = _content[i];
                if (r == null) continue;
                Coroutine? h = _motions[i];
                UiMotion.Run(this, ref h, UiMotion.Rise(r, EnsureGroup(r.gameObject)));
                _motions[i] = h;
            }
        }

        private void OnDisable()
        {
            // Belt and braces on top of UiMotionRunner: a screen must never be left mid-rise,
            // because the next thing that happens to it is ApplyScreen deactivating it — and a
            // screen deactivated 16 px low at alpha 0.4 comes back exactly like that.
            for (int i = 0; i < _motions.Count; i++)
            {
                Coroutine? h = _motions[i];
                UiMotion.Stop(this, ref h);
                _motions[i] = h;
            }
        }

        /// <summary>
        /// The rise fades as well as moves, so it needs a CanvasGroup — and the group is created
        /// HERE, at runtime, rather than authored by the builder.
        ///
        /// <para>DEVIATION D-2 — the groups are made at runtime, not authored. §D2 has the builder
        /// add them. Making them here instead keeps thirty-seven objects out of the scene diff
        /// (199 lines rather than 840) and, more usefully, means a screen the builder has never
        /// been run over still rises correctly. A CanvasGroup at alpha 1 / blocksRaycasts true is
        /// a visual and behavioural no-op, so creating one at runtime cannot move a rest pixel —
        /// which is exactly the argument <c>GpsScreenTransition.EnsureGroup</c> and
        /// <c>LayeredPush.EnsureGroup</c> already make for the push's own groups. Doing it the
        /// same way in all three places means one rule rather than two.</para>
        /// </summary>
        private static CanvasGroup EnsureGroup(GameObject go)
        {
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }

        /// <summary>Builder seam — <c>GamePolishBuilder</c> writes the same list through
        /// <c>SerializedObject</c> so the change is recorded on the prefab instance. Public so a
        /// test can arrange one without reflection.</summary>
        public void SetContent(RectTransform[] content) => _content = content ?? new RectTransform[0];

        /// <summary>Read-back seam for the builder's verification pass and for the reviewer.</summary>
        public IReadOnlyList<RectTransform> Content => _content;
    }
}
