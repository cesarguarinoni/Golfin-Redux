// ─────────────────────────────────────────────────────────────────────────────
// starter_restore_gate — the one place that answers "picker, or not?".
//
// Spec: Docs/Specs/Active/starter_restore_gate/SPEC.md §2
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using Golfin.InventorySync;
using Golfin.Roster;
using UnityEngine;

namespace Golfin.UI.Account
{
    /// <summary>What the post-auth routers are allowed to do next.</summary>
    public enum StarterRoute
    {
        /// <summary><c>CharacterManager.NeedsStarter</c> is now trustworthy — route on it.</summary>
        Ready,

        /// <summary>Internal to the gate; a caller never sees it (the busy state covers the wait).</summary>
        WaitingForServer,

        /// <summary>The server could not be reached, so nothing is known about this account. Show the
        /// offline error and offer a retry — NEVER the picker (D1).</summary>
        ServerUnreachable,
    }

    /// <summary>
    /// The gate between "signed in" and "which screen".
    ///
    /// <para>
    /// THE BUG THIS EXISTS FOR. <c>CharacterManager.NeedsStarter</c> is
    /// <c>string.IsNullOrEmpty(save.starterCharacterId)</c> — a question about THIS DEVICE'S FILE,
    /// asked by three routers synchronously inside the sign-in callback. On a fresh install that
    /// file is empty, and the server's inventory fetch (which carries the starter) has not answered
    /// yet, so a player who has owned a starter for weeks was asked to pick one again after a
    /// delete + reinstall. The fix is not a new question — it is waiting for the existing one to be
    /// answered before reading the save.
    /// </para>
    ///
    /// <para>
    /// D1, AND THE REASON THERE IS NO TIMEOUT. A FAILED fetch must never show the picker: an empty
    /// save plus silence is indistinguishable from a brand-new account, and guessing wrong costs a
    /// player their roster. The picker appears only after a SUCCESSFUL fetch whose blob carried no
    /// starter. A safety timeout would be a second clock that could resolve <see cref="StarterRoute.Ready"/>
    /// on an empty save — precisely the failure this closes — so there is not one. The transport's
    /// own timeout is what ends the wait, as a failure.
    /// </para>
    /// </summary>
    public static class StarterGate
    {
        private const string Tag = "[StarterGate]";

        // ── Seams (defaults are the shipping behaviour; EditMode tests replace them) ──

        /// <summary>"Does the local save name a starter?" Behind a seam so the gate's rules are
        /// testable without a <c>SaveDataHost</c> or a <c>CharacterManager</c> in the scene.</summary>
        public static Func<bool> NeedsStarterProbe = DefaultNeedsStarter;

        /// <summary>True on the paths that never sign in and never fetch — a bot run, a demo build,
        /// or a service with sends disabled. They must resolve instantly and identically to how they
        /// resolved before this gate existed.</summary>
        public static Func<bool> BypassProbe = DefaultBypass;

        /// <summary>Nudge the boot read (re-run a failed one, or start one the bind window missed).
        /// A no-op in EditMode, where there is no behaviour.</summary>
        public static Action RequestBoot = InventorySyncBehaviour.RetryBoot;

        /// <summary>Restore the shipping seams (EditMode tests).</summary>
        public static void ResetForTest()
        {
            NeedsStarterProbe = DefaultNeedsStarter;
            BypassProbe       = DefaultBypass;
            RequestBoot       = InventorySyncBehaviour.RetryBoot;
        }

        private static bool DefaultNeedsStarter() =>
            CharacterManager.Instance != null && CharacterManager.Instance.NeedsStarter;

        private static bool DefaultBypass()
        {
#if UNITY_EDITOR || GOLFIN_BOT_HARNESS
            // The guard is repeated at every call site by contract — BotSessionOverride does not
            // exist in a player build (see its file header).
            if (Golfin.Dev.BotSessionOverride.Active) return true;
#endif
            if (GolfinRedux.Demo.DemoGate.IsDemo) return true;
            return !InventorySyncService.Instance.SendsEnabled;
        }

        // ── The gate ──────────────────────────────────────────────────────────

        /// <summary>
        /// Answer once, now or as soon as the server has spoken. Never answers twice, and never
        /// leaves the caller hanging: every path either calls back synchronously or subscribes to a
        /// boot that is in flight / about to be started here.
        /// </summary>
        public static void Resolve(Action<StarterRoute> done)
        {
            if (done == null) return;

            // 1. The local save already names a starter. Nothing the server says can make that
            //    untrue (the merge is fill-if-empty), so this costs zero network on the common path
            //    — a device that has already played routes exactly as fast as it did before.
            if (!NeedsStarterProbe()) { done(StarterRoute.Ready); return; }

            // 5. Harness / demo / sends-off: no fetch is coming, ever. Byte-identical routing.
            if (BypassProbe()) { done(StarterRoute.Ready); return; }

            var service = InventorySyncService.Instance;

            switch (service.LastBootOutcome)
            {
                // 2. The server already answered. NeedsStarter is now ITS answer, not this device's.
                case BootOutcome.Succeeded:
                    done(StarterRoute.Ready);
                    return;

                // 3. The server could not be reached — hold, do not guess (D1).
                case BootOutcome.Failed:
                    done(StarterRoute.ServerUnreachable);
                    return;
            }

            // 4. NotRun. If no boot can ever run — not signed in, or sends disabled — waiting would
            //    hang the caller's busy state forever. There is no server to consult on that path,
            //    so the local save is all there is, exactly as before this gate existed.
            if (!WillBoot(service))
            {
                Debug.Log($"{Tag} No boot read is possible (unauthenticated or sends off) — " +
                          "routing on the local save.");
                done(StarterRoute.Ready);
                return;
            }

            // Subscribe BEFORE nudging: with a synchronous transport (EditMode) the boot below
            // completes inside RequestBoot(), and an unsubscribed gate would miss its own answer.
            Action<BootOutcome>? handler = null;
            handler = outcome =>
            {
                service.OnBootFinished -= handler;
                done(outcome == BootOutcome.Succeeded
                    ? StarterRoute.Ready
                    : StarterRoute.ServerUnreachable);
            };
            service.OnBootFinished += handler;

            // The sign-in event can fire before InventorySyncBehaviour has bound its save host, in
            // which case its TryBoot no-opped and nothing is in flight. Idempotent — the behaviour
            // refuses to double-fetch.
            RequestBoot();
        }

        /// <summary>Is a boot read going to happen at all? Mirrors <c>InventorySyncService.Boot</c>'s
        /// own early-out, which is the only thing that decides it.</summary>
        private static bool WillBoot(InventorySyncService service)
        {
            if (!service.SendsEnabled) return false;
            try { return service.IsAuthenticated == null || service.IsAuthenticated(); }
            catch { return false; }
        }
    }
}
