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
using UnityEngine.UI;

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
            ScreenId.StoreHistory,
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


        // ═════════════════════════════════════════════════════════════════════
        // game_polish_b §D1.1 — every game modal pops
        // ═════════════════════════════════════════════════════════════════════
        //
        // THE COUNT IS 15, NOT 13. The SPEC's prose says thirteen twice; its own table lists
        // fifteen, and a sweep of every ModalController in ShellScene and under Assets/Prefabs
        // (2026-09-08) finds fifteen non-GPS modals with animateShow false. InGameSettingsModal is
        // the one that is easy to miss from the scene alone — it has no ShellScene instance and is
        // spawned into the gameplay scene at runtime. The list below is the measured set; the
        // discrepancy is called out in the report rather than quietly reconciled either way.
        //
        // WHERE THE FLAG IS WRITTEN, and it is not uniform because the modals are not uniform:
        //
        //   * a modal whose scene object is a PREFAB INSTANCE gets the flag on the PREFAB ASSET.
        //     The instance then inherits it, every other instance of that prefab gets it too, and
        //     ShellScene.unity gains not one line. Eight of the fifteen are like this.
        //   * a modal authored directly into the scene gets it on the scene object, through
        //     SerializedObject with RecordPrefabInstancePropertyModifications (trap C1 — a plain
        //     field assignment on a scene object is gone the moment the scene reloads). Seven.
        //
        // THE CODE DEFAULT STAYS FALSE. GpsScreenTransitionTests pins it and §D1.1 says not to
        // touch it: this is an authoring pass, not a behaviour change to ModalController.

        /// <summary>Modals whose flag lives on a prefab asset (the scene instance inherits).</summary>
        private static readonly string[] ModalPrefabs =
        {
            "Assets/Prefabs/UI/Modals/GachaRatesModal.prefab",
            "Assets/Prefabs/UI/Modals/GachaRevealModal.prefab",
            "Assets/Prefabs/UI/Modals/HoleCompleteModal.prefab",
            "Assets/Prefabs/UI/Modals/InGameSettingsModal.prefab",
            "Assets/Prefabs/UI/Modals/SchemeConfirmModal.prefab",
            "Assets/Prefabs/UI/Modals/StartingCharacterConfirmModal.prefab",
            "Assets/Prefabs/UI/Modals/TournamentResultModal.prefab",
            "Assets/Prefabs/UI/Modals/TournamentSignupModal.prefab",
        };

        /// <summary>Modals authored straight into ShellScene, by transform path from the scene root.</summary>
        private static readonly string[] ModalSceneObjects =
        {
            "Canvas/ScreensRoot/RosterScreen/LevelUpModal",
            "Canvas/ScreensRoot/InventoryScreen/ContentArea/BagsClubModal",
            "Canvas/ScreensRoot/InventoryScreen/ContentArea/ItemsContent/ItemUseModal",
            "Canvas/ScreensRoot/InventoryScreen/ClubLevelUpModal",
            "Canvas/ScreensRoot/InventoryScreen/BagSelectionModal",
            "Canvas/ScreensRoot/MatchMakingModal",
            "Canvas/VersusResultModal",
        };

        [MenuItem("GOLFIN/Game Polish/Apply — modals pop (game_polish_b D1)", priority = 251)]
        public static void ApplyModalsMenu() => Debug.Log(ApplyModals());

        /// <summary>Set <c>animateShow</c> on every game modal. Re-runnable; a second run reports
        /// "already" for every row and writes nothing.</summary>
        public static string ApplyModals()
        {
            var log = new StringBuilder();
            log.AppendLine("[GamePolishBuilder.ApplyModals] " + System.DateTime.Now.ToString("u"));
            int changed = 0, already = 0, missing = 0;

            foreach (string path in ModalPrefabs)
            {
                GameObject? contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    if (contents == null) { log.AppendLine($"  MISSING prefab {path}"); missing++; continue; }

                    var modal = contents.GetComponentInChildren<Golfin.UI.Modals.ModalController>(true);
                    if (modal == null) { log.AppendLine($"  NO ModalController in {path}"); missing++; continue; }

                    if (SetAnimateShow(modal))
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        log.AppendLine($"  set   {modal.GetType().Name,-38} {path}");
                        changed++;
                    }
                    else
                    {
                        log.AppendLine($"  already {modal.GetType().Name,-36} {path}");
                        already++;
                    }
                }
                finally
                {
                    if (contents != null) PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            bool sceneDirty = false;
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            foreach (string path in ModalSceneObjects)
            {
                GameObject? go = FindInScene(scene, path);
                if (go == null) { log.AppendLine($"  MISSING scene object {path}"); missing++; continue; }

                var modal = go.GetComponent<Golfin.UI.Modals.ModalController>();
                if (modal == null) { log.AppendLine($"  NO ModalController on {path}"); missing++; continue; }

                if (SetAnimateShow(modal))
                {
                    if (PrefabUtility.IsPartOfPrefabInstance(modal))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(modal);
                    log.AppendLine($"  set   {modal.GetType().Name,-38} {path}");
                    changed++; sceneDirty = true;
                }
                else { log.AppendLine($"  already {modal.GetType().Name,-36} {path}"); already++; }
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                log.AppendLine("  scene marked dirty — SAVE IT.");
            }
            log.AppendLine($"  == set {changed}, already {already}, missing {missing}, " +
                           $"total {ModalPrefabs.Length + ModalSceneObjects.Length} ==");
            return log.ToString();
        }

        /// <summary>Write the private <c>animateShow</c> field. Returns whether it changed.</summary>
        private static bool SetAnimateShow(Golfin.UI.Modals.ModalController modal)
        {
            var so = new SerializedObject(modal);
            SerializedProperty p = so.FindProperty("animateShow");
            if (p == null)
            {
                Debug.LogError($"[GamePolishBuilder] no serialized 'animateShow' on {modal.GetType().Name} " +
                               "— the field was renamed and this builder is now a no-op. FIX THIS.");
                return false;
            }
            if (p.boolValue) return false;
            p.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(modal);
            return true;
        }

        /// <summary>Resolve "Canvas/ScreensRoot/Foo" against a scene's roots, inactive included.</summary>
        private static GameObject? FindInScene(UnityEngine.SceneManagement.Scene scene, string path)
        {
            int slash = path.IndexOf('/');
            string rootName = slash < 0 ? path : path.Substring(0, slash);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name != rootName) continue;
                if (slash < 0) return root;
                Transform? t = root.transform.Find(path.Substring(slash + 1));
                if (t != null) return t.gameObject;
            }
            return null;
        }


        // ═════════════════════════════════════════════════════════════════════
        // game_polish_b §D4 — shimmer hosts
        // ═════════════════════════════════════════════════════════════════════
        //
        // One INACTIVE host per cold-fetch site, holding N ShimmerBlock instances laid out where
        // the real rows will land. Inactive at rest is what keeps A3 at 0 px: a screen that has
        // never fetched draws exactly what it drew at HEAD, and the placeholder is a SetActive
        // away rather than an Instantiate away.
        //
        // AUTHORED, NOT BUILT AT RUNTIME, and the reason is not style. ShimmerBlock.prefab does
        // not live under Resources/, so a runtime load returns null silently and the panel stays
        // blank — the very defect the shimmer closes. (That is ShimmerHost's own header, and it is
        // why GPS authors its blocks too.)
        //
        // GeneralShop IS ABSENT ON PURPOSE. §D4 lists it, but GeneralShopCatalog reads a BUNDLED
        // Resources/Data/shop_catalog.csv plus a content overlay, synchronously, on first access —
        // there is no state in which the player waits on a network for it. A shimmer there would
        // be a loading animation over data that never left, which is exactly what GPS §D8's
        // cold-only rule exists to prevent. Reported as a deviation rather than built to spec.

        private readonly struct ShimmerSite
        {
            public readonly ScreenId Screen;
            public readonly string   Site;
            public readonly string   ParentPath;
            public readonly int      Blocks;
            public readonly float    Width, Height, Gap;
            public readonly bool     Horizontal;
            /// <summary>Where the host sits among its siblings; -1 appends. Only matters under a
            /// layout group, where the placeholder has to occupy the slot the real thing will.</summary>
            public readonly int      SiblingIndex;

            public ShimmerSite(ScreenId screen, string site, string parentPath,
                               int blocks, float w, float h, float gap, bool horizontal = false,
                               int siblingIndex = -1)
            {
                Screen = screen; Site = site; ParentPath = parentPath;
                Blocks = blocks; Width = w; Height = h; Gap = gap; Horizontal = horizontal;
                SiblingIndex = siblingIndex;
            }
        }

        /// <summary>Sizes are the REAL rows' measured sizes, taken off the live scene, so the
        /// placeholder occupies the space the data will occupy rather than a guess at it.</summary>
        private static readonly ShimmerSite[] ShimmerSites =
        {
            new ShimmerSite(ScreenId.Leaderboard, GameShimmerSites.RankingsTop3,
                            "ContentArea/BarsArea/RankingsArea/Modal/Top3", 3, 282f, 433f, 24f, horizontal: true),
            new ShimmerSite(ScreenId.Leaderboard, GameShimmerSites.RankingsList,
                            "ContentArea/BarsArea/RankingsArea/Modal/Bottom97", 3, 978f, 100f, 20f),
            new ShimmerSite(ScreenId.TournamentSelection, GameShimmerSites.TournamentCards,
                            "ContentArea/BarsArea/RankingsArea/Modal", 2, 978f, 220f, 24f),
            new ShimmerSite(ScreenId.TournamentLeaderboard, GameShimmerSites.TournamentLeaderboard,
                            "ContentArea/BarsArea/RankingsArea/Modal", 3, 978f, 100f, 20f),
            new ShimmerSite(ScreenId.GachaHistory, GameShimmerSites.GachaHistory,
                            "GameScreenContent/ContentContainer/MainPanel/CardsContainer", 3, 978f, 100f, 20f),
            // store_history — the same path, because StoreHistoryScreen.prefab is a duplicate of
            // GachaHistoryScreen.prefab and its hierarchy is untouched.
            new ShimmerSite(ScreenId.StoreHistory, GameShimmerSites.StoreHistory,
                            "GameScreenContent/ContentContainer/MainPanel/CardsContainer", 3, 978f, 100f, 20f),
            // The MissionSelection daily site was REMOVED (polish_regressions_0909 R2). Its host —
            // Shimmer_missions_daily, sibling index 1 under Content — was deleted from the scene in
            // the same commit, so re-running this builder does not put it back. Why, in
            // GameShimmerSites where the constant used to be.
        };

        private const string ShimmerBlockPrefab = "Assets/Prefabs/UI/Common/ShimmerBlock.prefab";

        [MenuItem("GOLFIN/Game Polish/Apply — shimmer hosts (game_polish_b D4)", priority = 252)]
        public static void ApplyShimmerMenu() => Debug.Log(ApplyShimmer());

        /// <summary>Place one inactive shimmer host per site. Re-runnable; a second run reports
        /// "already" for every row and writes nothing.</summary>
        public static string ApplyShimmer()
        {
            var log = new StringBuilder();
            log.AppendLine("[GamePolishBuilder.ApplyShimmer] " + System.DateTime.Now.ToString("u"));

            var block = AssetDatabase.LoadAssetAtPath<GameObject>(ShimmerBlockPrefab);
            if (block == null)
                return log.AppendLine("FATAL: " + ShimmerBlockPrefab + " not found (D0 moved it — was it reverted?)").ToString();

            var sm = Object.FindFirstObjectByType<ScreenManager>(FindObjectsInactive.Include);
            if (sm == null) return log.AppendLine("FATAL: no ScreenManager — open ShellScene.").ToString();
            var so = new SerializedObject(sm);

            int made = 0, already = 0, missing = 0;
            bool dirty = false;

            foreach (ShimmerSite site in ShimmerSites)
            {
                GameObject? screen = ScreenObject(so, site.Screen);
                if (screen == null) { log.AppendLine($"  {site.Site}: screen {site.Screen} not wired — skipped"); missing++; continue; }

                Transform? parent = screen.transform.Find(site.ParentPath);
                if (parent == null)
                {
                    log.AppendLine($"  {site.Site}: MISSING parent '{site.ParentPath}' under {site.Screen}");
                    missing++; continue;
                }

                string hostName = "Shimmer_" + site.Site.Replace('.', '_');
                Transform? existing = parent.Find(hostName);
                if (existing != null && existing.GetComponent<Golfin.Gps.UI.ShimmerHost>() != null)
                {
                    // RE-RUN REPAIRS, it does not merely skip. A host placed by an earlier run of
                    // this builder predates the corner fix below, and "already" would leave it
                    // wrong forever.
                    int fixedCorners = 0;
                    foreach (Transform child in existing)
                    {
                        float before = 0f;
                        foreach (Image img in child.GetComponentsInChildren<Image>(true))
                            before += img != null ? img.pixelsPerUnitMultiplier : 0f;
                        FixCornerScale(child.gameObject, site);
                        float after = 0f;
                        foreach (Image img in child.GetComponentsInChildren<Image>(true))
                            after += img != null ? img.pixelsPerUnitMultiplier : 0f;
                        if (!Mathf.Approximately(before, after)) fixedCorners++;
                    }
                    if (fixedCorners > 0) { dirty = true; log.AppendLine($"  {site.Site}: already — repaired {fixedCorners} corner scale(s)"); }
                    else                   log.AppendLine($"  {site.Site}: already ({site.ParentPath}/{hostName})");
                    already++; continue;
                }

                var hostGo = new GameObject(hostName, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(hostGo, "shimmer host");
                var hostRt = (RectTransform)hostGo.transform;
                hostRt.SetParent(parent, worldPositionStays: false);
                Stretch(hostRt);
                if (site.SiblingIndex >= 0) hostRt.SetSiblingIndex(site.SiblingIndex);

                var host = Undo.AddComponent<Golfin.Gps.UI.ShimmerHost>(hostGo);
                var hso = new SerializedObject(host);
                hso.FindProperty("_site").stringValue = site.Site;
                hso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(host);

                if (site.SiblingIndex >= 0)
                {
                    // Under a layout group a stretched rect is meaningless — the group sizes its
                    // children. Give it the size of the thing it replaces so the slot is right.
                    hostRt.anchorMin = hostRt.anchorMax = new Vector2(0.5f, 0.5f);
                    hostRt.pivot = new Vector2(0.5f, 0.5f);
                    hostRt.sizeDelta = new Vector2(site.Width, site.Blocks * site.Height);
                }

                float span = site.Horizontal ? site.Width : site.Height;
                float total = site.Blocks * span + (site.Blocks - 1) * site.Gap;
                for (int i = 0; i < site.Blocks; i++)
                {
                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(block, hostGo.transform);
                    inst.name = "Block" + i;
                    var rt = (RectTransform)inst.transform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(site.Width, site.Height);
                    float offset = -total * 0.5f + span * 0.5f + i * (span + site.Gap);
                    rt.anchoredPosition = site.Horizontal
                        ? new Vector2(offset, 0f)
                        : new Vector2(0f, -offset);   // first block at the TOP, like a list

                    FixCornerScale(inst, site);
                }

                // LAST: inactive at rest. Done after the children exist so nothing is authored
                // into an inactive hierarchy and silently skipped.
                hostGo.SetActive(false);

                log.AppendLine($"  {site.Site}: + {hostName} ({site.Blocks} blocks {site.Width:0}x{site.Height:0}) " +
                               $"under {site.Screen}/{site.ParentPath}");
                made++; dirty = true;
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(sm.gameObject.scene);
                log.AppendLine("  scene marked dirty — SAVE IT.");
            }
            log.AppendLine($"  == made {made}, already {already}, missing {missing}, of {ShimmerSites.Length} ==");
            log.AppendLine("  (GeneralShop has no site: its catalog is bundled and never cold — see the header.)");
            return log.ToString();
        }

        /// <summary>
        /// Keep the block's rounded corner proportionate to the size it was stretched to.
        ///
        /// <para>FOUND BY THE A11 LINT, and the first fix was WRONG in an instructive way.
        /// ShimmerBlock is authored at 900x120 with `S_PillStadium` 9-sliced at
        /// pixelsPerUnitMultiplier 3.67 — an effective ~24 px corner, right for a short wide pill.
        /// The sites here stretch it a long way from that (the Rankings podium blocks are 282x433),
        /// and at 24 px the corner arc kinks against a ~70 px cap radius (`9slice-cap-kink`,
        /// trap C10). Pinning PPUM to 1 fixed those three sites and BROKE the other three: the full
        /// ~88 px border on a 100 px-tall block exceeds the rect and the 9-slice collapses, which
        /// the linter reported as 18 FAILs where there had been 6 warnings. One constant cannot
        /// serve both shapes.</para>
        ///
        /// <para>So it is computed per image from the sprite's OWN border and the box that image
        /// actually has. The linter leaves a band: it collapses when the two borders no longer fit
        /// the rect (effective &gt; half the shorter side) and it kinks when the corner is under an
        /// eighth of it (`UIFidelityLinter` P8b: estCapRadius = min(w,h)/4, warn below half of
        /// that). A third of the shorter side sits in the middle of that band at every size, which
        /// is why one rule covers a 282x433 podium block and a 978x100 row alike. Set on the
        /// INSTANCE, so the shared prefab is untouched and the GPS blocks built from it are
        /// unaffected.</para>
        /// </summary>
        private static void FixCornerScale(GameObject blockInstance, ShimmerSite site)
            => FixCornerScale((RectTransform)blockInstance.transform,
                              new Vector2(site.Width, site.Height));

        /// <summary>
        /// <paramref name="box"/> is the size this rect actually has. Walks down carrying it, so
        /// every 9-sliced Image is scaled against ITS OWN box rather than the block's — the Band
        /// is a fixed 180 px wide inside a 282 px block, and scaling it by the block's short side
        /// would collapse it across the other axis, which is the mirror image of the mistake
        /// above.
        /// </summary>
        private static void FixCornerScale(RectTransform rt, Vector2 box)
        {
            var img = rt.GetComponent<Image>();
            if (img != null && img.sprite != null && img.type == Image.Type.Sliced)
            {
                Vector4 b = img.sprite.border;                       // l, b, r, t in sprite px
                float authored = Mathf.Max(Mathf.Max(b.x, b.y), Mathf.Max(b.z, b.w));
                float shortSide = Mathf.Min(box.x, box.y);
                if (authored > 0f && shortSide > 1f)                 // 0 = not really 9-sliced
                {
                    float wanted = shortSide / 3f;                   // the effective corner we want
                    float ppum   = Mathf.Clamp(authored / wanted, 0.05f, 20f);
                    if (!Mathf.Approximately(img.pixelsPerUnitMultiplier, ppum))
                    {
                        img.pixelsPerUnitMultiplier = ppum;
                        EditorUtility.SetDirty(img);
                    }
                }
            }

            for (int i = 0; i < rt.childCount; i++)
                if (rt.GetChild(i) is RectTransform child)
                    FixCornerScale(child, ChildBox(child, box));
        }

        /// <summary>
        /// A RectTransform's own size given its parent's: the anchor span across the parent, plus
        /// sizeDelta. That is the layout arithmetic itself, so it holds with no rebuild pass —
        /// which matters here because these hosts are authored INACTIVE and never get one.
        /// </summary>
        private static Vector2 ChildBox(RectTransform rt, Vector2 parentBox)
            => new Vector2((rt.anchorMax.x - rt.anchorMin.x) * parentBox.x + rt.sizeDelta.x,
                           (rt.anchorMax.y - rt.anchorMin.y) * parentBox.y + rt.sizeDelta.y);

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
        }

        /// <summary>Collect one content rect by name, if the screen has it.</summary>
        private static void Take(GameObject screen, string child, List<RectTransform> into)
        {
            Transform? t = screen.transform.Find(child);
            if (t is RectTransform rt) into.Add(rt);
        }



        // ═════════════════════════════════════════════════════════════════════
        // game_polish_c §C1 — ButtonPressFeedback on every player-facing Button
        // ═════════════════════════════════════════════════════════════════════
        //
        // RULE 11 IS FIVE MONTHS OLD AND HAS NEVER BEEN SWEPT. It says "every new player-facing
        // Button gets Golfin.UI.Polish.ButtonPressFeedback" and it has been honoured going forward
        // — the nav bar, the mode cards, the auth screens are all covered — but nothing ever went
        // back over what predates it. The LIVE audit (GamePolishProbeC) found 410 player-facing
        // buttons in a running shell and 166 with the component.
        //
        // THE ORDER MATTERS AND IT IS NOT COSMETIC. Prefab ASSETS are fixed first, and the open
        // scene's instances inherit the component the moment the asset is saved; the scene pass
        // that follows therefore only touches objects that are genuinely scene-authored. Done the
        // other way round, every one of those instances would gain an "added component" override
        // and ShellScene.unity would carry hundreds of diff lines for a change that belongs in a
        // prefab.
        //
        // RUNTIME CLONES ARE FIXED AT THEIR SOURCE, never in code — §C1.3. A GeneralShopCard or a
        // rankings row gets the component because its PREFAB has it, so the hundredth row spawned
        // next year has it too and no spawner has to remember.
        //
        // RE-RUNNABLE. Every step is add-if-missing; a second run reports "already" for every row.

        [MenuItem("GOLFIN/Game Polish/Apply — press feedback (game_polish_c C1)", priority = 253)]
        public static void ApplyPressFeedbackMenu() => Debug.Log(ApplyPressFeedback());

        public static string ApplyPressFeedback()
        {
            var log = new StringBuilder();
            log.AppendLine("[GamePolishBuilder.ApplyPressFeedback] " + System.DateTime.Now.ToString("u"));

            var rows = new List<string>();
            int addedPrefab = 0, alreadyPrefab = 0, excludedPrefab = 0, prefabsTouched = 0;

            // ── prefab assets ────────────────────────────────────────────────
            foreach (string root in PressFeedbackScope.Roots)
            {
                if (!System.IO.Directory.Exists(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!PressFeedbackScope.PrefabInScope(path, out string skipWhy))
                    { log.AppendLine($"  tree-skip {path} — {skipWhy}"); continue; }

                    GameObject? contents = null;
                    try
                    {
                        contents = PrefabUtility.LoadPrefabContents(path);
                        if (contents == null) continue;
                        int added = 0;
                        foreach (Button b in contents.GetComponentsInChildren<Button>(true))
                        {
                            if (b == null) continue;
                            string p = PressFeedbackScope.PathIn(contents.transform, b.transform);
                            string excl = PressFeedbackScope.ExclusionFor(b, p, new Vector2(1170f, 2532f));
                            if (excl != "")
                            { excludedPrefab++; rows.Add($"excluded\t{path}\t{p}\t{excl}"); continue; }
                            if (b.GetComponent<ButtonPressFeedback>() != null)
                            { alreadyPrefab++; rows.Add($"already\t{path}\t{p}\t"); continue; }
                            b.gameObject.AddComponent<ButtonPressFeedback>();
                            added++; addedPrefab++;
                            rows.Add($"added\t{path}\t{p}\t");
                        }
                        if (added > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(contents, path);
                            prefabsTouched++;
                            log.AppendLine($"  +{added,-3} {path}");
                        }
                    }
                    finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
                }
            }

            // ── the open scene ───────────────────────────────────────────────
            //
            // AFTER the assets, so an instance that just inherited the component reads as "already"
            // here and gains no override. What is left is scene-authored: the Settings rows, the
            // Roster detail panel, the Inventory tab bar, the two Splash buttons.
            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            Vector2 canvasSize = new Vector2(1170f, 2532f);
            foreach (GameObject r in scene.GetRootGameObjects())
            {
                var c = r.GetComponentInChildren<Canvas>(true);
                if (c != null && c.transform is RectTransform crt && crt.rect.width > 1f)
                { canvasSize = crt.rect.size; break; }
            }

            int addedScene = 0, alreadyScene = 0, excludedScene = 0;
            bool sceneDirty = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Button b in root.GetComponentsInChildren<Button>(true))
                {
                    if (b == null) continue;
                    string p = FullPath(b.transform);
                    string excl = PressFeedbackScope.ExclusionFor(b, p, canvasSize);
                    if (excl != "") { excludedScene++; rows.Add($"scene-excluded\t(scene)\t{p}\t{excl}"); continue; }
                    if (b.GetComponent<ButtonPressFeedback>() != null)
                    { alreadyScene++; rows.Add($"scene-already\t(scene)\t{p}\t"); continue; }

                    // TRAP C1 — Undo.AddComponent so the write is recorded, then the prefab-instance
                    // modification so it survives a reload when the object is inside an instance.
                    var fb = Undo.AddComponent<ButtonPressFeedback>(b.gameObject);
                    if (PrefabUtility.IsPartOfPrefabInstance(b))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(fb);
                    EditorUtility.SetDirty(b.gameObject);
                    addedScene++; sceneDirty = true;
                    rows.Add($"scene-added\t(scene)\t{p}\t");
                }
            }

            if (sceneDirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                log.AppendLine("  scene marked dirty — SAVE IT.");
            }

            log.AppendLine($"  == prefabs: added {addedPrefab} (in {prefabsTouched} prefab(s)), " +
                           $"already {alreadyPrefab}, excluded {excludedPrefab}");
            log.AppendLine($"  == scene:   added {addedScene}, already {alreadyScene}, excluded {excludedScene}");
            WriteRows("game_polish_c_pressfeedback_authoring.tsv",
                      "verdict\tsource\tpath\treason", rows, log);
            return log.ToString();
        }


        // ═════════════════════════════════════════════════════════════════════
        // game_polish_c §C2 — one scroll feel
        // ═════════════════════════════════════════════════════════════════════
        //
        // THE REFERENCE IS gps_polish §D9's, applied to every GPS list and now to the game's:
        // Elastic / 0.1 / inertia on / 0.135. `scrollSensitivity` is unified to 20 as well, and it
        // is worth saying that this one is NOT what the player feels — it scales WHEEL and
        // TRACKPAD deltas only, which no phone has. It is unified so two Editor sessions scroll
        // the same list at the same speed, and it is called out as non-player-facing in the report
        // rather than sold as part of the feel.
        //
        // WHERE THE VALUE IS WRITTEN, and the answer is "wherever it will stick": the ASSET when
        // the object is a prefab instance, and then the instance's override on those five
        // properties is REVERTED so the instance follows the asset again. Half of these rects
        // already carried an override — RankingsScreen's list is Elastic in the scene and Clamped
        // in its prefab, GachaRatesModal's is the other way round — so writing only the instance
        // would leave the prefab wrong for every other user of it, and writing only the asset
        // would be invisible under the override. Both, in that order, ends with one value in one
        // place.
        //
        // THE CAROUSELS ARE IN SCOPE, and the SPEC expected them not to be. §C2 offers them as
        // likely exclusions "if the controller drives position itself". All five —
        // CarouselController, Bag/Club/Ball/ItemCarouselController — write
        // horizontalNormalizedPosition only from an ARROW TAP or a programmatic selection, and
        // none of them implements IBeginDragHandler / IEndDragHandler. A finger drag on any of them
        // is handled entirely by the ScrollRect, so the movement type IS what the player feels and
        // the SPEC's own test puts them in scope. ModeCarouselController, the one carousel that
        // really does own its drag, has no ScrollRect at all — it lerps a layout — so it never
        // appears in this table.

        public const string ScrollMovementType = "Elastic";
        public const float  ScrollElasticity   = 0.1f;
        public const float  ScrollDeceleration = 0.135f;
        public const float  ScrollSensitivity  = 20f;

        [MenuItem("GOLFIN/Game Polish/Apply — one scroll feel (game_polish_c C2)", priority = 254)]
        public static void ApplyScrollFeelMenu() => Debug.Log(ApplyScrollFeel());

        public static string ApplyScrollFeel()
        {
            var log = new StringBuilder();
            log.AppendLine("[GamePolishBuilder.ApplyScrollFeel] " + System.DateTime.Now.ToString("u"));
            var rows = new List<string>();

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            // Pass 1 — the prefab ASSETS behind the scene's instances, plus any in-scope prefab
            // that has a ScrollRect and no instance in this scene.
            var assets = new SortedSet<string>(System.StringComparer.Ordinal);
            foreach (string root in PressFeedbackScope.Roots)
            {
                if (!System.IO.Directory.Exists(root)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!PressFeedbackScope.PrefabInScope(path, out _)) continue;
                    if (ScrollExclusion(path) != "") continue;
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (go != null && go.GetComponentInChildren<ScrollRect>(true) != null) assets.Add(path);
                }
            }

            int setAsset = 0, alreadyAsset = 0;
            foreach (string path in assets)
            {
                GameObject? contents = null;
                try
                {
                    contents = PrefabUtility.LoadPrefabContents(path);
                    if (contents == null) continue;
                    int changed = 0;
                    foreach (ScrollRect sr in contents.GetComponentsInChildren<ScrollRect>(true))
                    {
                        string p = PressFeedbackScope.PathIn(contents.transform, sr.transform);
                        if (ScrollExclusion(path + "/" + p) != "")
                        { rows.Add($"asset-excluded\t{path}\t{p}\t{ScrollExclusion(path + "/" + p)}"); continue; }
                        string before = Describe(sr);
                        if (SetScrollFeel(sr)) { changed++; setAsset++; rows.Add($"asset-set\t{path}\t{p}\t{before} -> {Describe(sr)}"); }
                        else { alreadyAsset++; rows.Add($"asset-already\t{path}\t{p}\t{before}"); }
                    }
                    if (changed > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                        log.AppendLine($"  asset {changed} rect(s) -> {path}");
                    }
                }
                finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
            }

            // Pass 2 — the scene. An instance gets its five overrides REVERTED (the asset is now
            // right, so reverting lands on the reference and removes a scene diff line); a
            // scene-authored rect is written directly.
            int setScene = 0, revertedScene = 0, alreadyScene = 0, excludedScene = 0;
            bool dirty = false;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (ScrollRect sr in root.GetComponentsInChildren<ScrollRect>(true))
                {
                    if (sr == null) continue;
                    string p = FullPath(sr.transform);
                    string excl = ScrollExclusion(p);
                    if (excl != "") { excludedScene++; rows.Add($"scene-excluded\t(scene)\t{p}\t{excl}"); continue; }

                    string before = Describe(sr);
                    if (PrefabUtility.IsPartOfPrefabInstance(sr) && RevertFeelOverrides(sr) && Conforms(sr))
                    {
                        revertedScene++; dirty = true;
                        rows.Add($"scene-reverted\t(instance)\t{p}\t{before} -> {Describe(sr)} (follows its prefab again)");
                        continue;
                    }
                    if (SetScrollFeel(sr))
                    {
                        if (PrefabUtility.IsPartOfPrefabInstance(sr))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(sr);
                        EditorUtility.SetDirty(sr);
                        setScene++; dirty = true;
                        rows.Add($"scene-set\t(scene)\t{p}\t{before} -> {Describe(sr)}");
                    }
                    else { alreadyScene++; rows.Add($"scene-already\t(scene)\t{p}\t{before}"); }
                }
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                log.AppendLine("  scene marked dirty — SAVE IT.");
            }
            log.AppendLine($"  == assets: set {setAsset}, already {alreadyAsset}");
            log.AppendLine($"  == scene:  set {setScene}, reverted-to-prefab {revertedScene}, " +
                           $"already {alreadyScene}, excluded {excludedScene}");
            WriteRows("game_polish_c_scrollfeel_authoring.tsv", "verdict\tsource\tpath\tdetail", rows, log);
            return log.ToString();
        }

        /// <summary>The two exclusions §C2 names, decided from the path.</summary>
        public static string ScrollExclusion(string path)
        {
            foreach (string g in PressFeedbackScope.GpsRoots)
                if (path.Contains("/" + g) || path.Contains(g + "/")) return "Gps/ — SPEC § Untouched";
            if (path.Contains("/Gps/")) return "Gps/ — SPEC § Untouched";
            foreach (string a in AuthScreens)
                if (path.Contains(a)) return "auth screen — Tier 2, not this track (SPEC §C2)";
            return "";
        }

        static readonly string[] AuthScreens =
        { "LoginScreen", "SignUpScreen", "CreateUsernameScreen", "ResetPasswordScreen", "EmailConfirmationScreen" };

        public static bool Conforms(ScrollRect sr) =>
            sr.movementType == ScrollRect.MovementType.Elastic &&
            Mathf.Approximately(sr.elasticity, ScrollElasticity) && sr.inertia &&
            Mathf.Approximately(sr.decelerationRate, ScrollDeceleration) &&
            Mathf.Approximately(sr.scrollSensitivity, ScrollSensitivity);

        static string Describe(ScrollRect sr) =>
            $"{sr.movementType}/{sr.elasticity:0.###}/{(sr.inertia ? "inertia" : "NO-inertia")}/" +
            $"{sr.decelerationRate:0.###}/sens {sr.scrollSensitivity:0.##}";

        static readonly string[] FeelProps =
        { "m_MovementType", "m_Elasticity", "m_Inertia", "m_DecelerationRate", "m_ScrollSensitivity" };

        /// <summary>Write the five fields through SerializedObject (trap C1). Returns whether
        /// anything changed.</summary>
        static bool SetScrollFeel(ScrollRect sr)
        {
            if (Conforms(sr)) return false;
            var so = new SerializedObject(sr);
            so.FindProperty("m_MovementType").enumValueIndex   = (int)ScrollRect.MovementType.Elastic;
            so.FindProperty("m_Elasticity").floatValue         = ScrollElasticity;
            so.FindProperty("m_Inertia").boolValue             = true;
            so.FindProperty("m_DecelerationRate").floatValue   = ScrollDeceleration;
            so.FindProperty("m_ScrollSensitivity").floatValue  = ScrollSensitivity;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(sr);
            return true;
        }

        /// <summary>Drop the instance's overrides on the five feel properties so it follows its
        /// prefab. Returns whether any override was actually there to drop.</summary>
        static bool RevertFeelOverrides(ScrollRect sr)
        {
            var so = new SerializedObject(sr);
            bool any = false;
            foreach (string name in FeelProps)
            {
                SerializedProperty p = so.FindProperty(name);
                if (p == null || !p.prefabOverride) continue;
                PrefabUtility.RevertPropertyOverride(p, InteractionMode.AutomatedAction);
                any = true;
            }
            return any;
        }


        // ═════════════════════════════════════════════════════════════════════
        // game_polish_c §C3 — safe area
        // ═════════════════════════════════════════════════════════════════════
        //
        // TWO SURFACES OUT OF THIRTY-TWO, and finding them meant fixing the instrument first.
        // GamePolishProbeC walks every surface on a real iPhone 15 Pro Max in the Device Simulator
        // — Screen.safeArea (0, 102, 1290, 2517): 177 px of Dynamic Island at the top, 102 px of
        // home indicator at the bottom. Two elements cross the top band:
        //
        //   StaminaShopSelectionScreen/TitleLabel    glyph top at y 2639 against a band starting
        //                                            2619 — 20 px of "BOOST STAMINA" behind the
        //                                            island.
        //   ModeSelectionScreen/TournamentTempEntry  rect top 2620 against 2619 — 0.6 px. It is
        //                                            authored to sit immediately below an iPhone
        //                                            14 notch and rounds into a taller one.
        //
        // (An earlier run of the same probe reported 32/32 clear. It resolved the inset ONCE at
        // start-up and the play-mode view then changed size under it, so every band was computed
        // against a stale screen height. That run is discarded rather than quoted; the probe now
        // re-resolves per surface and records the view each verdict was measured at.)
        //
        // BASELINE 141, NOT 0, AND THAT IS THE WHOLE DESIGN. SafeAreaFitter's baseline says how
        // much inset the layout ALREADY clears; the shell's top bar uses 141 (safe_area_top_bar)
        // because its chrome is authored to clear an iPhone 14's 47 pt notch. These two screens are
        // authored against the same top edge — the title 154 px down, the button 176 px — so they
        // clear 141 too, and only the EXCESS matters. At baseline 141 the fix moves them
        // 177 - 141 = 36 px on a 15 Pro Max, nothing on an iPhone 14, and nothing at all at the
        // 1170x2532 reference where the safe area IS the whole screen — which is what keeps A5 at
        // 0 px. At baseline 0 the same fix would have shoved a 20 px problem 177 px down the
        // screen: breaking the layout to satisfy the measurement.
        //
        // A STRETCHED WRAPPER, because the fitter re-anchors WHATEVER IT IS ON — put it on the
        // label and the label becomes screen-sized. So the screen's non-background children move
        // inside a full-screen `SafeArea` child, exactly as PersistentUI does it. The background
        // stays where it is (the screen root's own Image for Stamina, a sibling for ModeSelection),
        // so §C3's "backgrounds stay full-bleed" holds by construction.
        //
        // AND ModeSelection WRAPS ONLY THE ONE BUTTON. Its other child is `CardsContainer`, which
        // LayeredPush.LayerMap resolves BY NAME under the screen root; re-parenting it would move
        // the push's content layer out from under a table this task may not touch.
        //
        // THE BOTTOM NAV IS DELIBERATELY LEFT ALONE — it is the other measured intrusion (icons
        // 19-36 px above the screen bottom, inside the 102 px home-indicator band). That is the
        // shipped Game bar, and gps_polish already tried insetting its clone:
        // EnsureNavBarSafeArea's header is the post-mortem — the bar floated 102 px up the screen
        // with background showing underneath, and the fix was to take the fitter back off.
        // Repeating it here would re-break something already fixed. It is a named, measured row in
        // the report for Cesar, not a silent pass and not a silent change.
        //
        // RE-RUNNABLE, and it repairs: a second run finds the wrapper, finds the children already
        // inside it, and writes nothing.

        /// <summary>
        /// The measured hits, and what moves inside the wrapper: (screen object name, the children
        /// that go under `SafeArea`). A child not listed — a background — stays where it is.
        /// </summary>
        private static readonly (string Screen, string[] Children)[] SafeAreaSites =
        {
            ("StaminaShopSelectionScreen", new[] { "TitleLabel", "StaminaShopRegionPill",
                                                   "StaminaShopPrefecturePill", "CardsPanel" }),
            ("ModeSelectionScreen",        new[] { "TournamentTempEntry" }),
        };

        /// <summary>The inset these layouts already clear — an iPhone 14 notch at 47 pt. The same
        /// number `safe_area_top_bar` uses on PersistentUI/SafeArea; see the header.</summary>
        public const float SafeAreaBaselinePixels = 141f;

        public const string SafeAreaWrapperName = "SafeArea";

        [MenuItem("GOLFIN/Game Polish/Apply — safe area (game_polish_c C3)", priority = 255)]
        public static void ApplySafeAreaMenu() => Debug.Log(ApplySafeArea());

        public static string ApplySafeArea()
        {
            var log = new StringBuilder();
            log.AppendLine("[GamePolishBuilder.ApplySafeArea] " + System.DateTime.Now.ToString("u"));

            UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            int found = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
                foreach (GolfinRedux.UI.Core.SafeAreaFitter f in
                         root.GetComponentsInChildren<GolfinRedux.UI.Core.SafeAreaFitter>(true))
                {
                    var so = new SerializedObject(f);
                    SerializedProperty bp = so.FindProperty("_baselineInsetPixels");
                    log.AppendLine($"  present: {FullPath(f.transform)}  baselineInsetPixels=" +
                                   (bp != null ? bp.floatValue.ToString("0.#") : "?"));
                    found++;
                }
            log.AppendLine($"  {found} SafeAreaFitter(s) in the open scene.");

            bool dirty = false;
            int made = 0, already = 0, moved = 0, missing = 0;
            foreach ((string screenName, string[] children) in SafeAreaSites)
            {
                GameObject? screen = FindScreenRoot(scene, screenName);
                if (screen == null) { log.AppendLine($"  MISSING screen {screenName}"); missing++; continue; }

                // A SCREEN THAT IS A PREFAB INSTANCE IS FIXED ON ITS ASSET, exactly as §C1's
                // press-feedback pass does. Doing it on the instance instead means Unity has to
                // express "four existing children moved under a newly added GameObject" as a set
                // of instance overrides — which it does, in a form that did not survive the
                // round-trip through the scene file, leaving a wrapper with nothing in it. On the
                // asset it is an ordinary re-parent, every instance inherits it, and ShellScene
                // gains no override at all.
                if (PrefabUtility.IsPartOfPrefabInstance(screen))
                {
                    string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(screen);
                    log.AppendLine($"  {screenName} is a prefab instance -> editing {assetPath}");
                    GameObject? contents = null;
                    try
                    {
                        contents = PrefabUtility.LoadPrefabContents(assetPath);
                        if (contents == null) { log.AppendLine("  could not load " + assetPath); missing++; continue; }
                        int m2 = 0, mv2 = 0;
                        WrapChildren(contents, children, log, ref m2, ref mv2, ref missing);
                        if (m2 > 0 || mv2 > 0)
                        {
                            PrefabUtility.SaveAsPrefabAsset(contents, assetPath);
                            made += m2; moved += mv2;
                        }
                        else already++;
                    }
                    finally { if (contents != null) PrefabUtility.UnloadPrefabContents(contents); }
                    continue;
                }

                int m1 = 0, mv1 = 0;
                WrapChildren(screen, children, log, ref m1, ref mv1, ref missing);
                if (m1 > 0 || mv1 > 0) { made += m1; moved += mv1; dirty = true; }
                else already++;
                EditorUtility.SetDirty(screen);
            }

            if (dirty)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                log.AppendLine("  scene marked dirty — SAVE IT.");
            }
            log.AppendLine($"  == wrappers made {made}, already {already}, children moved {moved}, " +
                           $"missing {missing}, sites {SafeAreaSites.Length}, baseline {SafeAreaBaselinePixels:0}px ==");
            return log.ToString();
        }

        /// <summary>
        /// Ensure `root/SafeArea` exists as a full-screen stretch carrying a
        /// <see cref="GolfinRedux.UI.Core.SafeAreaFitter"/> at the shell's baseline, and move the
        /// named children into it. Used for a scene object and for prefab contents alike; the only
        /// difference is who saves afterwards.
        /// </summary>
        private static void WrapChildren(GameObject root, string[] children, StringBuilder log,
                                         ref int made, ref int moved, ref int missing)
        {
            Transform? wrapper = root.transform.Find(SafeAreaWrapperName);
            if (wrapper == null)
            {
                var go = new GameObject(SafeAreaWrapperName, typeof(RectTransform));
                wrapper = go.transform;
                wrapper.SetParent(root.transform, worldPositionStays: false);
                made++;
                log.AppendLine($"  + {root.name}/{SafeAreaWrapperName}");
            }

            var fitter = wrapper.GetComponent<GolfinRedux.UI.Core.SafeAreaFitter>();
            if (fitter == null) fitter = wrapper.gameObject.AddComponent<GolfinRedux.UI.Core.SafeAreaFitter>();
            var fso = new SerializedObject(fitter);
            SerializedProperty bp = fso.FindProperty("_baselineInsetPixels");
            if (bp != null && !Mathf.Approximately(bp.floatValue, SafeAreaBaselinePixels))
            {
                bp.floatValue = SafeAreaBaselinePixels;
                fso.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(fitter);
            }

            // STRETCH LAST, AND THAT ORDER IS THE FIX FOR A REAL DEFECT. SafeAreaFitter is
            // [ExecuteAlways]: AddComponent runs its Awake, which runs Apply(), which writes
            // anchors computed against whatever "Screen" the Editor reports at that moment. Doing
            // the stretch first and adding the component second baked
            // anchorMax (1.76, 1.64) into StaminaShopSelectionScreen.prefab — a wrapper 76 %
            // wider than its parent at rest. It self-corrected at runtime (Awake re-applies), so
            // nothing looked wrong; it was found by grepping the diff for any property that can
            // move a pixel. Stretching after the component exists leaves the neutral 0-1 stretch
            // as the SERIALISED state, which is what it should be on every device.
            Stretch((RectTransform)wrapper);
            EditorUtility.SetDirty(wrapper);

            // The wrapper takes the sibling slot of the FIRST child it swallows, and the children
            // keep their order inside it, so draw order is unchanged — a title that drew over a
            // panel still does.
            int firstIndex = int.MaxValue;
            var toMove = new List<Transform>();
            foreach (string childName in children)
            {
                Transform? c = root.transform.Find(childName) ?? wrapper.Find(childName);
                if (c == null) { log.AppendLine($"  MISSING {root.name}/{childName}"); missing++; continue; }
                if (c.parent == wrapper) continue;
                firstIndex = Mathf.Min(firstIndex, c.GetSiblingIndex());
                toMove.Add(c);
            }
            if (firstIndex != int.MaxValue) wrapper.SetSiblingIndex(firstIndex);
            foreach (Transform c in toMove)
            {
                c.SetParent(wrapper, worldPositionStays: false);
                moved++;
                log.AppendLine($"    -> {root.name}/{SafeAreaWrapperName}/{c.name}");
            }
            EditorUtility.SetDirty(root);
        }

        /// <summary>A screen root by object name, anywhere under the scene's roots.</summary>
        private static GameObject? FindScreenRoot(UnityEngine.SceneManagement.Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name == name) return t.gameObject;
            }
            return null;
        }


        // ── shared plumbing for the three C methods ──────────────────────────

        public static string FullPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }

        /// <summary>The authoring-side table, TSV so the report generator can join it to the live
        /// audit. Written next to the probe's JSON.</summary>
        static void WriteRows(string file, string header, List<string> rows, StringBuilder log)
        {
            const string dir = "Docs/Diagnostics/_capture";
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, file);
            var sb = new StringBuilder();
            sb.AppendLine(header);
            rows.Sort(System.StringComparer.Ordinal);
            foreach (string r in rows) sb.AppendLine(r);
            System.IO.File.WriteAllText(path, sb.ToString());
            log.AppendLine($"  {rows.Count} row(s) -> {path}");
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
                ScreenId.StoreHistory            => "_storeHistoryScreen",
                _                                => null,
            };
            if (field == null) return null;
            SerializedProperty p = so.FindProperty(field);
            return p != null ? p.objectReferenceValue as GameObject : null;
        }
    }
}
