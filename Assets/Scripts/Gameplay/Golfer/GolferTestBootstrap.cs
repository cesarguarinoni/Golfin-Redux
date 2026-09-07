// golfer_3d_test §5.5 — the ONLY thing the scene knows about the experiment.
//
// The scene holds this component and NOTHING else: no prefab reference, no material, no clip.
// Without GOLFIN_GOLFER_TEST the body below is compiled out and this is an empty MonoBehaviour,
// so nothing under Assets/Art/3D/Characters/_Test/ is reachable from the scene graph and the
// build pulls none of it (GolferTestBuildGate stashes the folder besides, because a Resources/
// subfolder ships whether or not anything references it).

using UnityEngine;

namespace Golfin.Gameplay.Golfer
{
#if GOLFIN_GOLFER_TEST
    using Golfin.Gameplay.Session;

    /// <summary>
    /// Spawns <c>PfGolfer_Test</c> on <see cref="GameSession.OnRoundStarted"/> and replaces it
    /// on every subsequent hole start.
    ///
    /// <para>TWO ENTRY POINTS, ONE INSTALLER. SPEC §5.5 asks for a component in
    /// <c>GameplayScene</c>, and that component is present — but GameplayScene is NOT in
    /// EditorBuildSettings (only ShellScene and Physics/LabScaffold are) and holds nothing but
    /// a camera, a light and an empty root, so it never loads and a component there would never
    /// run. The <see cref="RuntimeInitializeOnLoadMethod"/> below is what actually installs the
    /// hook, in whichever scene the loop really runs. Both call the same idempotent
    /// <see cref="EnsureInstalled"/>, so having both changes nothing but coverage.</para>
    ///
    /// <para>NOT PARENTED TO TeePoint. SPEC §5.5 says to place the golfer through the path that
    /// positions <c>TeePoint.prefab</c> per hole; that path does not exist — the prefab's GUID
    /// (969311bd…) appears in no scene, prefab or script in the repo, and no C# file mentions
    /// TeePoint at all. The golfer is therefore spawned unparented into the active scene and
    /// positioned by <c>GolferPresenter.PlaceAtBall</c> from the live ball, which is the same
    /// answer for every hole and needs no per-hole authoring.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GolferTestBootstrap : MonoBehaviour
    {
        public const string ResourcePath = "GolferTest/PfGolfer_Test";

        /// <summary>
        /// SPEC §9.8 — which golfer this round spawns. Empty = <see cref="ResourcePath"/>.
        ///
        /// <para>The §9.8 comparison is only worth anything if both prefabs reach the hole down
        /// the SAME path: same bootstrap, same GameSession.OnRoundStarted, same PlaceAtBall, same
        /// harness. Swapping the asset at the Resources path instead, or spawning the Mixamo one
        /// by hand from the harness, would mean the two frames differ by more than the thing under
        /// test. One string, read at spawn, is the smallest change that keeps the comparison
        /// honest.</para>
        ///
        /// <para>Already inside <c>#if GOLFIN_GOLFER_TEST</c>, so it does not exist in a shipping
        /// build any more than the rest of this file does.</para>
        /// </summary>
        public static string ResourcePathOverride = "";

        static string ActiveResourcePath =>
            string.IsNullOrEmpty(ResourcePathOverride) ? ResourcePath : ResourcePathOverride;

        static bool       _installed;
        static GameObject _golfer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            // Re-arm cleanly, the way QualityTierService.Boot does: with domain reload disabled
            // these statics survive a play session, so _installed would still be true while the
            // subscription it refers to belongs to a dead session.
            _installed = false;
            _golfer    = null;
            EnsureInstalled();
        }

        void OnEnable() => EnsureInstalled();

        static void EnsureInstalled()
        {
            if (_installed) return;
            _installed = true;
            GameSession.OnRoundStarted -= SpawnGolfer;   // never double-subscribe
            GameSession.OnRoundStarted += SpawnGolfer;
            Debug.Log("[GolferTest] bootstrap installed — waiting for GameSession.OnRoundStarted.");
        }

        static void SpawnGolfer()
        {
            // GameSession is a static class, so this subscription outlives the play session that
            // made it and EDIT-MODE callers reach it too — GameSessionTests.SeedSession raises
            // OnRoundStarted, and Destroy/Instantiate from edit mode is a hard Unity error that
            // failed three tests before this guard existed. A golfer only ever belongs to a
            // running game.
            if (!Application.isPlaying) return;

            if (_golfer != null) Destroy(_golfer);

            var prefab = Resources.Load<GameObject>(ActiveResourcePath);
            if (prefab == null)
            {
                // The expected state in any build whose gate stashed _Test — say so once and
                // stay out of the way rather than throwing on a hole start.
                Debug.LogWarning($"[GolferTest] Resources/{ActiveResourcePath} not found — no golfer this round.");
                return;
            }

            _golfer = Instantiate(prefab);
            _golfer.name = "GolferTest";
            Debug.Log("[GolferTest] spawned " + _golfer.name + " for hole " + GameSession.CurrentHoleNumber + ".");
        }
    }
#else
    /// <summary>
    /// GOLFIN_GOLFER_TEST is absent: an empty component. The GameplayScene GameObject that
    /// carries it holds no reference to any <c>_Test</c> asset, which is the whole point of
    /// SPEC §5.5.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GolferTestBootstrap : MonoBehaviour { }
#endif
}
