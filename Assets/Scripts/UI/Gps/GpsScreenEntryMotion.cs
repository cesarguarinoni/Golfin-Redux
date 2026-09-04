// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D3 — the entry, for screens reached through the boundary fade.
//
// ONE COMPONENT, NOT EIGHT OnEnable EDITS. The spec describes this per screen
// ("on every GPS screen's OnEnable"), but the behaviour is identical on all of
// them and the eight controllers have nothing else in common. Eight copies would
// be eight places to forget the SkipEntry check — and forgetting it is not a
// subtle bug, it is a screen that plays two entrances in 0.25 s.
//
// WHY IT MUST NOT RUN AFTER A PUSH. A pushed screen has already animated in: its
// content slid from +W and its chrome dissolved. Rising it 16 px again, from
// alpha 0, on the frame it arrives, would read as a stutter at the end of an
// otherwise continuous move. GpsScreenTransition arms EnteringViaPush around the
// one SetActive that starts a push, and this is the only thing that reads it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using Golfin.UI.Polish;
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>
    /// Rises a GPS screen's <c>ContentContainer</c> into place when the screen is shown through
    /// the fade. Added by <c>GpsPolishBuilder</c>; holds no serialized references.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GpsScreenEntryMotion : MonoBehaviour
    {
        private RectTransform? _content;
        private CanvasGroup?   _group;
        private Coroutine?     _motion;

        private void OnEnable()
        {
            // A screen that arrived on a push is already where it should be, at full alpha.
            if (GpsScreenTransition.EnteringViaPush) return;

            Resolve();
            if (_content == null) return;

            UiMotion.Run(this, ref _motion, UiMotion.Rise(_content, _group));
        }

        private void OnDisable()
        {
            // Belt and braces on top of UiMotionRunner: a screen must never be left mid-rise,
            // because the next thing that happens to it is ApplyScreen deactivating it.
            UiMotion.Stop(this, ref _motion);
        }

        private void Resolve()
        {
            if (_content != null) return;
            Transform? t = GpsScreenTransition.FindLayer(gameObject, "ContentContainer");
            if (t == null) return;
            _content = t as RectTransform;
            _group   = t.GetComponent<CanvasGroup>();
        }
    }
}
