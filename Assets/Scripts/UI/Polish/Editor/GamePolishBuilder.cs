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
            // The DAILY card's slot, not the mission list: the list is local and never cold, the
            // daily is fetched and hidden until the server answers. Sibling index 1 puts the
            // placeholder exactly where the card it stands in for will appear, under Content's
            // VerticalLayoutGroup.
            new ShimmerSite(ScreenId.MissionSelection, GameShimmerSites.MissionsDaily,
                            "Content", 1, 978f, 374f, 0f, siblingIndex: 1),
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
