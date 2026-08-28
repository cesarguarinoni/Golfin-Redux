// Assets/Scripts/UI/Polish/PendingSpend.cs
// Order: transaction_feedback §3 — the shared "waiting on the server" affordance.
#nullable enable
using System;
using TMPro;
using UnityEngine.UI;

namespace Golfin.UI.Polish
{
    /// <summary>
    /// Marks a control as WAITING ON THE SERVER, from the tap until the callback answers.
    ///
    /// <para>
    /// Every RP spend became a server round-trip (reward_points_backend Slice 2, then
    /// shop_server_purchase and progress_server_side). The call sites all latch a
    /// <c>_purchaseInFlight</c>-style bool so a double-tap cannot fire a second debit — but a latch is
    /// INVISIBLE. The button looked untouched for the whole round-trip, so a purchase read as "nothing
    /// happened", and if the player navigated away in that window the item appeared unannounced on
    /// whatever screen they had reached. That is the defect this closes: the wait is now drawn at the
    /// control that started it.
    /// </para>
    /// <para>
    /// NO NEW ART. The visual is the Button's own <c>Disabled</c> transition (already authored on every
    /// one of these buttons) plus an ellipsis on the label. There is no spinner sprite in the project
    /// and fabricating one would be a flat-fill (Rule 21). If a rotating icon is wanted later it is an
    /// art request, and this class grows one optional <c>Graphic</c> parameter — the call sites do not
    /// change.
    /// </para>
    /// <para>
    /// DISPOSABLE, because a callback can leave by many doors. Level-up <c>OnServerAnswered</c> alone
    /// has five verdict arms, two of which close the modal, and the shop's price-changed arm destroys
    /// the very card that was tapped. A disposable scope restores from all of them (and from an
    /// exception), where a hand-restored field only restores from the paths someone remembered.
    /// </para>
    /// <para>
    /// ORDERING AT THE CALL SITE: dispose FIRST, before the result is acted on. Restoring means
    /// "put back what was there before the tap", and the result handler's job is to overwrite that with
    /// the new truth (OWNED + disabled, a re-priced card, a closed modal). Disposing afterwards would
    /// undo the answer.
    /// </para>
    /// <para>
    /// The latches STAY. This is the affordance, not the guard: it makes the wait visible, it does not
    /// make the double-debit impossible. Both are needed — the disabled button cannot be tapped, but
    /// nothing stops a second spend arriving from another surface.
    /// </para>
    /// </summary>
    public sealed class PendingSpend : IDisposable
    {
        private readonly Button?    _button;
        private readonly bool       _wasInteractable;
        private readonly TMP_Text?  _label;
        private readonly string     _labelText;
        private readonly Button?[]? _alsoDisabled;
        private readonly bool[]?    _alsoWasInteractable;

        private bool _disposed;

        /// <summary>The ellipsis shown in place of the label. U+2026, not three periods — it is one
        /// glyph, so it cannot wrap or re-flow the button, and it needs no localization row.</summary>
        public const string PendingLabel = "…";

        private PendingSpend(Button? button, TMP_Text? label, Button[]? alsoDisable)
        {
            // TWO PASSES, and the order is load-bearing. Every cached value is read BEFORE anything is
            // written, so a control that appears twice (a card whose PLAY and tap-to-expand buttons
            // are the same component on one prefab variant) caches its PRE-TAP state both times.
            // Reading and writing in one pass would cache the second occurrence as "already disabled"
            // and restore it to disabled — a dead button, from the very code meant to hand it back.
            _button = button;
            if (button != null) _wasInteractable = button.interactable;

            _label     = label;
            _labelText = label != null ? label.text : string.Empty;

            if (alsoDisable != null && alsoDisable.Length > 0)
            {
                _alsoDisabled        = new Button?[alsoDisable.Length];
                _alsoWasInteractable = new bool[alsoDisable.Length];
                for (int i = 0; i < alsoDisable.Length; i++)
                {
                    var extra = alsoDisable[i];
                    _alsoDisabled[i] = extra;
                    if (extra != null) _alsoWasInteractable[i] = extra.interactable;
                }
            }

            // ── Pass 2: write ──
            if (button != null) button.interactable = false;
            if (label  != null) label.text          = PendingLabel;

            if (_alsoDisabled == null) return;
            foreach (var extra in _alsoDisabled)
                if (extra != null) extra.interactable = false;
        }

        /// <summary>
        /// Put <paramref name="button"/> into the pending state and return the scope that restores it.
        /// </summary>
        /// <param name="button">The tapped control. Null is tolerated (an unwired reference must not
        /// turn a working purchase into a NullReferenceException) — the scope is then inert.</param>
        /// <param name="label">Optional label on the button; its text is cached and replaced with
        /// <see cref="PendingLabel"/>.</param>
        /// <param name="alsoDisable">Other controls that must not be usable while the spend is in
        /// flight — a modal's CANCEL, a card's tap-to-expand.</param>
        public static PendingSpend Begin(Button? button, TMP_Text? label = null, params Button[] alsoDisable)
            => new PendingSpend(button, label, alsoDisable);

        /// <summary>
        /// Restore everything <see cref="Begin"/> changed. Idempotent, and safe on a control that has
        /// since been destroyed — Unity's overloaded <c>==</c> reports a destroyed object as null, and
        /// the shop's price-changed arm really does destroy the tapped card.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_button != null) _button.interactable = _wasInteractable;
            if (_label  != null) _label.text          = _labelText;

            if (_alsoDisabled == null || _alsoWasInteractable == null) return;
            for (int i = 0; i < _alsoDisabled.Length; i++)
            {
                var extra = _alsoDisabled[i];
                if (extra != null) extra.interactable = _alsoWasInteractable[i];
            }
        }
    }
}
