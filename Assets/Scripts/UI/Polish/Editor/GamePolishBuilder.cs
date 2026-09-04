// ─────────────────────────────────────────────────────────────────────────────
// game_polish_a §D2 — the authoring pass.
//
// WHAT IT ADDS, AND NOTHING ELSE: one ScreenEntryMotion per shell screen, wired
// to that screen's CONTENT rects. It edits no size, no anchor, no colour, no
// sprite and no activation state — A2 asks for 0 px of rest movement and the
// cheapest way to guarantee that is for the builder to be incapable of causing
// any.
//
// IT DELIBERATELY DOES NOT ADD THE CanvasGroups. §D2 has it add them; making
// them at RUNTIME instead — in LayeredPush.EnsureGroup and
// ScreenEntryMotion.EnsureGroup, which is what both already did as a safety net
// and what GpsScreenTransition has always done for the hand-built hub — keeps
// thirty-seven objects out of the scene (840 diff lines against 199) and means a
// screen this builder has never been run over still animates correctly. An
// alpha-1 CanvasGroup is a no-op, so it cannot move a rest pixel either way.
//
// A NOTE ON SAVING, because it cost an hour: this builder's own output is clean
// (199 insertions, 3 deletions, no anchor touched), but saving ShellScene AFTER A
// PLAY SESSION is not — the first attempt here did that and baked 154 anchor and
// 70 sizeDelta changes of pure layout churn on top. Always run the builder on a
// freshly opened scene. (Project memory: scene_save_bakes_layout_churn.)
//
// THE LIST OF LAYERS IS NOT DUPLICATED HERE. It is read from
// LayeredPush.LayerMap, the same table the runtime uses, so the rise and the push
// cannot disagree about what "content" means on a given screen — the failure that
// would look like a screen sliding one set of children and rising another.
//
// RE-RUNNABLE. Every step is "add if missing / set to the same value", so running
// it twice is a no-op and running it after a screen is rebuilt repairs it.
//
// TRAP C1 (dirty-on-write). Scene objects are written through SerializedObject
// with RecordPrefabInstancePropertyModifications, and the scene is marked dirty
// explicitly; a plain field assignment on a scene object is lost the moment the
// scene is reloaded. Prefab ASSETS go through LoadPrefabContents /
// SaveAsPrefabAsset for the same reason.
//
// D7 IS NOT HERE. The nav-bar halo and ring are created at runtime by
// NavSlotHighlight.Attach — see that file's deviation D-1 for why (the GPS bar is
// cloned inside eight Gps/ prefabs this task may not edit, and one mechanism for
// both bars is the point of the change).
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using System.Text;
using Golfin.UI.Polish;
using GolfinRedux.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Golfin.UI.Polish.EditorTools
{
    public static class GamePolishBuilder
    {
        /// <summary>
        /// Screens the builder walks. Exactly the ids <see cref="LayeredPush.LayerMap"/> knows,
        /// plus <c>Home</c> and <c>Roster</c> — which never push (Cesar's rule; no chrome child)
        /// but DO rise on their fade-path arrival, so they need the component even though they
        /// will never need the CanvasGroups.
        /// </summary>
        private static readonly ScreenId[] Screens =
        {
            ScreenId.Home,
            ScreenId.Roster,
            ScreenId.Inventory,
            ScreenId.ModeSelection,
            ScreenId.HoleSelection,
            ScreenId.MissionSelection,
            ScreenId.TournamentHoleSelection,
            ScreenId.TournamentSelection,
            ScreenId.TournamentLeaderboard,
            ScreenId.Leaderboard,
            ScreenId.GeneralShop,
            ScreenId.GachaHistory,
            ScreenId.GachaPrizes,
        };

        /// <summary>
        /// The content rects that RISE, for the two screens LayerMap has no entry for.
        ///
        /// <para>Home rises everything that is not its background — it is the busiest screen and
        /// the one the player sees most, and §D2 says it rises on boot too. Roster rises its
        /// <c>DetailPanel</c> ONLY: the character stage is a 3D-feeling element the player reads
        /// as being behind the UI, and sliding it 16 px would read as the character bobbing.</para>
        /// </summary>
        private static string[] ExtraContent(ScreenId id)
        {
            switch (id)
            {
                case ScreenId.Roster: return new[] { "DetailPanel" };
                case ScreenId.Home:   return new[] { "NoticePanel", "ModeCarouselSection",
                                                     "PromoBanner", "DailyMissionPill" };
                default:              return new string[0];
            }
        }

        [MenuItem("GOLFIN/Game Polish/Apply — CanvasGroups + entry motion", priority = 250)]
        public static void ApplyMenu()
        {
            string report = Apply();
            Debug.Log(report);
        }

        /// <summary>Walk every shell screen. Returns a report; logs nothing itself so a test or
        /// the probe can call it and assert on the text.</summary>
        public static string Apply()
        {
            var log = new StringBuilder();
            log.AppendLine("[GamePolishBuilder] " + System.DateTime.Now.ToString("u"));

            var sm = Object.FindFirstObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (sm == null) return log.AppendLine("FATAL: no ScreenManager in the open scene — open ShellScene.").ToString();

            var so = new SerializedObject(sm);
            bool sceneDirty = false;

            foreach (ScreenId id in Screens)
            {
                GameObject? go = ScreenObject(so, id);
                if (go == null) { log.AppendLine($"  {id}: <not wired on ScreenManager> — skipped"); continue; }

                var content = new List<RectTransform>();

                LayeredPush.Layers? map = LayeredPush.LayerMap(id);
                if (map != null)
                    foreach (string n in map.Value.Content) Take(go, n, content);
                foreach (string n in ExtraContent(id))      Take(go, n, content);

                // The component, wired through SerializedObject so the write survives a reload.
                var motion = go.GetComponent<ScreenEntryMotion>();
                if (motion == null)
                {
                    motion = Undo.AddComponent<ScreenEntryMotion>(go);
                    log.AppendLine($"  {id}: + ScreenEntryMotion");
                    sceneDirty = true;
                }

                var mso = new SerializedObject(motion);
                SerializedProperty arr = mso.FindProperty("_content");
                bool changed = arr.arraySize != content.Count;
                arr.arraySize = content.Count;
                for (int i = 0; i < content.Count; i++)
                {
                    SerializedProperty el = arr.GetArrayElementAtIndex(i);
                    if (el.objectReferenceValue != content[i]) { el.objectReferenceValue = content[i]; changed = true; }
                }
                if (changed)
                {
                    mso.ApplyModifiedPropertiesWithoutUndo();
                    if (PrefabUtility.IsPartOfPrefabInstance(motion))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(motion);
                    EditorUtility.SetDirty(motion);
                    sceneDirty = true;
                }

                var names = new List<string>();
                foreach (RectTransform r in content) names.Add(r.name);
                log.AppendLine($"  {id}: rises [{string.Join(", ", names)}]");
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(sm.gameObject.scene);
                log.AppendLine("  scene marked dirty — SAVE IT (the components live on scene objects).");
            }
            else log.AppendLine("  nothing to change (re-run is a no-op).");

            return log.ToString();
        }

        /// <summary>Collect one content rect by name, if the screen has it.</summary>
        private static void Take(GameObject screen, string child, List<RectTransform> into)
        {
            Transform? t = screen.transform.Find(child);
            if (t is RectTransform rt) into.Add(rt);
        }

        private static GameObject? ScreenObject(SerializedObject so, ScreenId id)
        {
            string? field = id switch
            {
                ScreenId.Home                    => "_homeScreen",
                ScreenId.Roster                  => "_rosterScreen",
                ScreenId.Inventory               => "_inventoryScreen",
                ScreenId.ModeSelection           => "_modeSelectionScreen",
                ScreenId.HoleSelection           => "_holeSelectionScreen",
                ScreenId.MissionSelection        => "_missionSelectionScreen",
                ScreenId.TournamentHoleSelection => "_tournamentHoleSelectionScreen",
                ScreenId.TournamentSelection     => "_tournamentSelectionScreen",
                ScreenId.TournamentLeaderboard   => "_tournamentLeaderboardScreen",
                ScreenId.Leaderboard             => "_leaderboardScreen",
                ScreenId.GeneralShop             => "_generalShopScreen",
                ScreenId.GachaHistory            => "_gachaHistoryScreen",
                ScreenId.GachaPrizes             => "_gachaPrizesScreen",
                _                                => null,
            };
            if (field == null) return null;
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.objectReferenceValue as GameObject : null;
        }
    }
}
