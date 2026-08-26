// ─────────────────────────────────────────────────────────────────────────────
// TestSupport — the ONE fake host boot, shared by every EditMode harness.
//
// Task: content_cleanup_quick item 3 (Docs/TellCode.md)
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Reflection;
using System.Threading.Tasks;
using Golfin.Save;
using UnityEngine;

// The helper method below is deliberately called `TestBoot.SaveDataHost()` — it reads as the thing
// it produces at every call site. That makes the bare name ambiguous with the TYPE inside this
// file, so the type is referred to through this alias throughout and never by its bare name.
using SaveHost = Golfin.Save.SaveDataHost;

namespace Golfin.TestSupport
{
    /// <summary>In-memory <see cref="ISavePersister"/> — never touches disk.</summary>
    public sealed class NullPersister : ISavePersister
    {
        public bool TryLoad(out string? json) { json = null; return false; }
        public Task SaveAsync(string json) => Task.CompletedTask;
    }

    /// <summary>
    /// Stand a <see cref="SaveDataHost"/> up the way a real boot would, from an EditMode test.
    ///
    /// <para>
    /// WHY THIS EXISTS. Three harnesses — <c>TournamentServiceWireupTests</c>,
    /// <c>RealItemRewardAdapterTests</c> and <c>ApplyServerBalanceTests</c> — each hand-rolled the
    /// same five-step dance: clear the static <c>Instance</c> through its compiler-generated backing
    /// field, add the component, inject a null persister, force-set <c>Instance</c> when
    /// <c>Awake</c> did not fire, and put the previous instance back in TearDown. Three copies of a
    /// reflection sequence is three places to update, and it had already gone wrong once:
    /// <c>content_kill_switch_and_order</c> §2 added a <c>SaveDataHost.IsLoaded</c> assert to
    /// <c>CharacterManager</c>, two of the three copies grew a <c>ReloadFromDisk()</c> call, and
    /// <c>ApplyServerBalanceTests</c> did not — so it was booting a host that had never read a save.
    /// The next boot-order invariant would have hit all three again.
    /// </para>
    ///
    /// <para>
    /// ⚠️ THE BOOT MUST BE COMPLETE, NOT MERELY PRESENT. EditMode never runs <c>Awake</c>, so a
    /// freshly-added component has <c>IsLoaded == false</c> — a state no real boot ever reaches
    /// past its first frame. <see cref="SaveDataHost.ReloadFromDisk"/> is the load <c>Awake</c>
    /// would have done and is what makes the fake host indistinguishable from a booted one.
    /// </para>
    /// </summary>
    public static class TestBoot
    {
        /// <summary>
        /// The compiler-generated backing field of an auto-property. <c>SaveDataHost.Instance</c>
        /// has a private setter, and a test has to be able to both clear it (so <c>Awake</c>'s
        /// self-registration cannot be pre-empted by a stale instance from another fixture) and
        /// restore it (so this fixture does not leave the Editor holding a destroyed host).
        /// </summary>
        private static FieldInfo? InstanceField =>
            typeof(SaveHost).GetField("<Instance>k__BackingField",
                                      BindingFlags.Static | BindingFlags.NonPublic);

        /// <summary>
        /// Boot a fake <see cref="SaveDataHost"/> and take over the singleton.
        ///
        /// <para>
        /// Dispose the returned lease in <c>[TearDown]</c> — it destroys the GameObject and puts
        /// the previous <c>Instance</c> back, so a fixture that fails mid-test still cannot leak a
        /// destroyed host into whatever runs next.
        /// </para>
        /// </summary>
        /// <param name="name">GameObject name, so a leak is identifiable in the hierarchy.</param>
        /// <param name="persister">Defaults to <see cref="NullPersister"/> — the real save file is
        /// never read or written by a test.</param>
        public static SaveDataHostLease SaveDataHost(
            string name = "TEST_SaveDataHost", ISavePersister? persister = null)
        {
            SaveHost? previous = SaveHost.Instance;

            // Clear first: Awake only self-registers into an EMPTY slot, so a stale instance left
            // by another fixture would silently keep ownership and this host would never be live.
            InstanceField?.SetValue(null, null);

            var go = new GameObject(name);
            var host = go.AddComponent<SaveHost>();
            host.SetPersister(persister ?? new NullPersister());

            // See the type docstring: present is not booted. This is the step ApplyServerBalanceTests
            // was missing.
            host.ReloadFromDisk();

            // AddComponent does not reliably fire Awake in every EditMode context, so registration
            // is asserted rather than assumed.
            if (SaveHost.Instance == null) InstanceField?.SetValue(null, host);

            return new SaveDataHostLease(host, go, previous);
        }

        /// <summary>What <see cref="TestBoot.SaveDataHost"/> hands back: the live host, and the
        /// teardown that undoes every part of standing it up.</summary>
        public sealed class SaveDataHostLease : IDisposable
        {
            private readonly GameObject? _go;
            private readonly SaveHost? _previous;
            private bool _disposed;

            internal SaveDataHostLease(SaveHost host, GameObject go, SaveHost? previous)
            {
                Host      = host;
                _go       = go;
                _previous = previous;
            }

            /// <summary>The booted host. <c>IsLoaded</c> is already true.</summary>
            public SaveHost Host { get; }

            /// <summary>The live save the fixture should read and assert against.</summary>
            public SaveData Data => Host.Data;

            /// <summary>Idempotent, so a fixture may dispose defensively as well as in TearDown.</summary>
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;

                if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
                InstanceField?.SetValue(null, _previous);
            }
        }
    }
}
