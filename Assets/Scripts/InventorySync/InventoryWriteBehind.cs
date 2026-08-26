// ─────────────────────────────────────────────────────────────────────────────
// InventorySync — the write-behind coalescing rule.
//
// Spec: Docs/Specs/Active/content_player_inventory/SPEC.md §3
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable

namespace Golfin.InventorySync
{
    /// <summary>
    /// When a PUT is actually owed.
    ///
    /// <para>
    /// AT MOST ONE PUT PER 30 SECONDS, PLUS ONE ON PAUSE/QUIT. NEVER PER MUTATION (SPEC §3). This
    /// matters more than it looks: <c>SaveDataHost.OnSaved</c> fires after every debounced disk
    /// write, and a player allocating a stack of SP or buying a bag of items produces a burst of
    /// them. One request per mutation would be a request per button press, all but the last already
    /// obsolete on arrival — the same failure <c>GolfinCharacterSyncPolicy.ShouldSend</c> exists to
    /// prevent, one layer up.
    /// </para>
    ///
    /// <para>
    /// EXTRACTED AS A PLAIN CLASS WITH AN INJECTED CLOCK so the "10 rapid mutations produce ONE PUT"
    /// acceptance is a unit test that runs in microseconds, rather than a play-mode session someone
    /// has to sit through for 30 seconds to disprove.
    /// </para>
    /// </summary>
    public sealed class InventoryWriteBehind
    {
        /// <summary>SPEC §3. Not a tuning knob — the number is in the acceptance criteria.</summary>
        public const float DefaultMinIntervalSeconds = 30f;

        public float MinIntervalSeconds = DefaultMinIntervalSeconds;

        private bool _dirty;
        private bool _hasSent;
        private float _lastSentAt;

        /// <summary>True when a mutation has happened that no PUT has carried yet.</summary>
        public bool IsDirty => _dirty;

        /// <summary>Seconds until the next windowed send is allowed. 0 when one is due now.</summary>
        public float SecondsUntilDue(float now)
        {
            if (!_dirty) return float.PositiveInfinity;
            if (!_hasSent) return 0f;
            float elapsed = now - _lastSentAt;
            return elapsed >= MinIntervalSeconds ? 0f : MinIntervalSeconds - elapsed;
        }

        /// <summary>A mutation happened. Cheap and idempotent — called from every
        /// <c>SaveDataHost.OnSaved</c>.</summary>
        public void MarkDirty() => _dirty = true;

        /// <summary>
        /// Claim the right to send, or return false.
        ///
        /// <para>
        /// <paramref name="force"/> is pause/quit: it bypasses the 30 s window (that is the whole
        /// point of the pause flush) but NOT the dirty check — a pause with nothing pending must not
        /// spend a request re-asserting a blob the server already has.
        /// </para>
        /// <para>
        /// Claiming clears the dirty flag BEFORE the request completes, on purpose. A mutation that
        /// lands mid-flight re-dirties, which schedules the NEXT window — that mutation was not in
        /// the payload, so it genuinely still needs sending. Clearing on completion instead would
        /// drop it.
        /// </para>
        /// </summary>
        public bool TryClaim(float now, bool force = false)
        {
            if (!_dirty) return false;
            if (!force && _hasSent && now - _lastSentAt < MinIntervalSeconds) return false;

            _dirty = false;
            _hasSent = true;
            _lastSentAt = now;
            return true;
        }

        /// <summary>
        /// Hand the claim back after a FAILED send, so the next window retries.
        ///
        /// <para>
        /// The 30 s clock is deliberately NOT rewound: a failing server should be asked again on the
        /// normal cadence, not hammered. Offline, this is the whole retry policy — mark dirty, fail,
        /// re-dirty, try again in 30 s, forever, at a cost of one request per half minute.
        /// </para>
        /// </summary>
        public void ReleaseFailed() => _dirty = true;

        /// <summary>Drop all state (sign-out, tests).</summary>
        public void Reset()
        {
            _dirty = false;
            _hasSent = false;
            _lastSentAt = 0f;
        }
    }
}
