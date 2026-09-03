// ─────────────────────────────────────────────────────────────────────────────
// gps_polish — the prefab-side additions, in ONE place.
//
// Every existing GPS builder stays the source of truth for its own screen; this
// file is called at the END of each of them (and by its own menu item for the
// hub, which has no builder at all — it was hand-built over MCP in
// gps_hub_entry). Forking the builders was the alternative and it would have
// left five copies of "does this screen have its CanvasGroups yet".
//
// IDEMPOTENT BY CONSTRUCTION. Every step is "ensure", never "add": the menu item
// can be run over all nine prefabs any number of times, and re-running a screen
// builder afterwards produces the same asset. That matters because A6 re-runs
// every builder and then lints every prefab.
//
// IT MOVES NO REST PIXEL. Everything here is either invisible at rest (a
// CanvasGroup at alpha 1, a step indicator at alpha 0, a shimmer block that
// starts inactive) or a behaviour value with no visual (scroll feel). The one
// thing that DOES move on a real device is the nav-bar safe-area wrapper, and it
// moves by exactly the home-indicator inset, which is zero at the 1170x2532
// reference the screens are authored against.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.Gps.UI;
using GolfinRedux.UI.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gps.EditorTools
{
    /// <summary>Shared polish pass applied to every GPS screen prefab.</summary>
    public static class GpsPolishBuilder
    {
        private const string PrefabDir = "Assets/Prefabs/UI/Gps/";
        private const string SprPill   = "Assets/Art/Tournaments/S_PillStadium.png";

        /// <summary>The vote list's own card silhouette — see <c>ShimmerSite.Shape</c>.</summary>
        private const string SprVoteCard = "Assets/Art/UI/Gps/S_GV_CardSimple.png";

        /// <summary>The nine GPS screen prefabs, in nav order.</summary>
        public static readonly string[] ScreenPrefabs =
        {
            "GpsHubScreen", "ScoreUploadScreen", "GpsProfileScreen", "GpsAvatarScreen",
            "GpsBadgesScreen", "GpsGolfProfileScreen", "GpsWelcomeScreen",
            "GpsGiftScreen", "GpsVoteScreen",
        };

        /// <summary>The three modals that opt in to the pop-in (§D5). Nothing else in the game
        /// does — see ModalController.animateShow's tooltip.</summary>
        public static readonly string[] AnimatedModals =
        {
            "VenuePickerModal", "GiftSendModal", "VoteCreateModal",
        };

        // ── Scroll feel, quoted from the Inventory screen (§D9) ──────────────
        // Canvas/ScreensRoot/InventoryScreen/.../ItemUseModal/ModalPanel/ModalContainer/ScrollArea
        //   move=Elastic elast=0.1 inertia=True decel=0.135 sens=20   (vertical)
        // Canvas/ScreensRoot/InventoryScreen/.../ClubCarouselSection/ScrollView
        //   move=Elastic elast=0.1 inertia=True decel=0.135 sens=30   (horizontal)
        public const ScrollRect.MovementType ScrollMovement = ScrollRect.MovementType.Elastic;
        public const float ScrollElasticity     = 0.1f;
        public const float ScrollDeceleration   = 0.135f;
        public const float ScrollSensitivityV   = 20f;
        public const float ScrollSensitivityH   = 30f;

        // ═════════════════════════════════════════════════════════════════════
        // Menu
        // ═════════════════════════════════════════════════════════════════════

        [MenuItem("GOLFIN/Gps/Apply GPS Polish (all screens + modals)", priority = 230)]
        public static void ApplyAll()
        {
            int screens = 0, modals = 0;
            foreach (string name in ScreenPrefabs)
                if (ApplyToPrefab(PrefabDir + name + ".prefab")) screens++;

            foreach (string name in AnimatedModals)
                if (SetModalAnimated(PrefabDir + name + ".prefab")) modals++;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[GpsPolishBuilder] applied to {screens} screen prefab(s), " +
                      $"{modals} modal prefab(s).");
        }

        /// <summary>
        /// Apply the same pass to the LIVE SCENE copies under <c>Canvas/ScreensRoot</c>.
        ///
        /// <para>YOU ALMOST CERTAINLY DO NOT NEED THIS. It was written on a belief that turned
        /// out to be false — that the GPS screens had been unpacked into ShellScene, so a prefab
        /// edit would reach the asset and nothing the player runs. All nine are ordinary PREFAB
        /// INSTANCES, and the prefab pass alone reaches the live scene.</para>
        ///
        /// <para>The belief came from checking <c>IsPartOfPrefabInstance</c> IN PLAY MODE, where
        /// it returns false for every object in the scene. Re-checked in EDIT mode (gps_polish
        /// iteration 2, and again by the review gate) it is true for all nine, each resolving to
        /// its own <c>Assets/Prefabs/UI/Gps/*.prefab</c>. Running this menu item on top of a
        /// prefab pass that has already landed adds nothing and cost 1,296 lines of
        /// prefab-override churn in <c>ShellScene.unity</c> the one time it was used.</para>
        ///
        /// <para>It is kept because it is idempotent and harmless, and because a screen that is
        /// ever genuinely unpacked would need it. Check <c>PrefabUtility.IsPartOfPrefabInstance</c>
        /// in EDIT MODE before reaching for it.</para>
        ///
        /// <para>IN PLACE, never replaced. Re-instantiating the prefab over the scene copy would
        /// break every serialized reference pointing INTO it — ScreenManager's nine screen fields
        /// and each controller's dozens of wired children — and would have to re-wire them by
        /// path. <see cref="Apply"/> only ADDS components and moves the nav bar one level down, so
        /// every existing reference keeps pointing at the same object.</para>
        /// </summary>
        [MenuItem("GOLFIN/Gps/Apply GPS Polish to SCENE copies", priority = 232)]
        public static void ApplyToScene()
        {
            // Play-mode edits are discarded when play stops, and MarkSceneDirty throws outright.
            // Refusing loudly beats "it said it worked" followed by nothing being saved.
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("[GpsPolishBuilder] stop play mode first — scene edits made in play " +
                               "mode are discarded.");
                return;
            }

            GameObject? screensRoot = GameObject.Find("Canvas/ScreensRoot");
            if (screensRoot == null)
            {
                Debug.LogError("[GpsPolishBuilder] no Canvas/ScreensRoot — open ShellScene first.");
                return;
            }

            var touched = new List<string>();
            foreach (Transform child in screensRoot.transform)
            {
                bool isGps = false;
                foreach (string n in ScreenPrefabs) if (child.name == n) { isGps = true; break; }
                if (!isGps) continue;

                Apply(child.gameObject);
                touched.Add(child.name);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                screensRoot.scene);
            Debug.Log("[GpsPolishBuilder] scene copies polished: " + string.Join(", ", touched));
        }

        // ═════════════════════════════════════════════════════════════════════
        // Per-screen
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Apply the polish pass to a prefab ASSET, opening and saving it.
        /// Returns false when the path holds no prefab.
        /// </summary>
        public static bool ApplyToPrefab(string assetPath)
        {
            GameObject? root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
            {
                Debug.LogWarning($"[GpsPolishBuilder] no prefab at {assetPath}");
                return false;
            }
            try
            {
                Apply(root);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return true;
        }

        /// <summary>
        /// THE shared entry point. Called at the end of every GPS screen builder, with the
        /// in-memory root it just built, and by <see cref="ApplyToPrefab"/> for the hub.
        /// </summary>
        public static void Apply(GameObject root)
        {
            if (root == null) return;

            EnsureLayerGroups(root);
            EnsureNavBarSafeArea(root);
            EnsureNavBarBinder(root);
            EnsureEntryMotion(root);
            ApplyScrollFeel(root);
            EnsureStepPolish(root);
            EnsureShimmerHosts(root);
        }

        // ═════════════════════════════════════════════════════════════════════
        // §D8 — the five cold-fetch placeholders
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>One placeholder site: where it hangs, how many blocks, and their geometry.</summary>
        private readonly struct ShimmerSite
        {
            public readonly string Screen;      // prefab / scene-copy root name
            public readonly string ParentPath;  // under the root
            public readonly string Site;        // ShimmerHost.Site
            public readonly int    Count;
            public readonly Vector2 Origin;     // host anchoredPosition, top-left
            public readonly Vector2 Block;      // one block's size
            public readonly Vector2 Step;       // per-index offset (x right, y DOWN, positive)
            public readonly int    Columns;

            /// <summary>Optional: the SHAPE this site's rows actually are, as a sprite asset path
            /// rendered <see cref="Image.Type.Simple"/>. The default pill is a 9-slice and suits a
            /// row; a vote card is a 232-tall baked panel, and standing a 9-sliced capsule in for
            /// it both looks wrong and trips the fidelity linter's cap-radius heuristic (it
            /// estimates a 58 px cap where the sprite gives 24). Using the card's OWN sprite is
            /// the honest placeholder and the warning goes with it.</summary>
            public readonly string? Shape;

            public ShimmerSite(string screen, string parentPath, string site, int count,
                               Vector2 origin, Vector2 block, Vector2 step, int columns = 1,
                               string? shape = null)
            {
                Screen = screen; ParentPath = parentPath; Site = site; Count = count;
                Origin = origin; Block = block; Step = step; Columns = columns; Shape = shape;
            }
        }

        /// <summary>
        /// The five sites the SPEC names, with geometry read off the prefabs rather than guessed:
        /// each block stands where a real row will stand, so the list does not jump when the data
        /// replaces the placeholder.
        ///
        /// <para>The badge grid hangs off the SECTION, not off <c>CellContainer</c> — that
        /// container carries a <see cref="GridLayoutGroup"/> and would lay the host itself out as
        /// a 220x153 cell, collapsing all six blocks into one.</para>
        /// </summary>
        private static readonly ShimmerSite[] Sites =
        {
            // Hub: RoundRows is 958x392 with three 958x130 rows at y 0 / -130 / -260.
            new ShimmerSite("GpsHubScreen", "ContentContainer/RecentRoundsPanel/RoundRows",
                            ShimmerHost.HubRounds, 3,
                            new Vector2(32f, -12f), new Vector2(894f, 106f), new Vector2(0f, 130f)),

            // Badges: CellContainer is at (20,-62) in the section; grid cell 220.5x153, spacing 12.
            new ShimmerSite("GpsBadgesScreen", "ContentContainer/Section_GOLF",
                            ShimmerHost.Badges, 6,
                            new Vector2(20f, -62f), new Vector2(220.5f, 153f),
                            new Vector2(232.5f, 165f), columns: 4),

            // Gift: Supporter0..2 are 958x96 at y -80 / -176 / -272.
            new ShimmerSite("GpsGiftScreen", "ContentContainer/Supporters",
                            ShimmerHost.Supporters, 3,
                            new Vector2(32f, -92f), new Vector2(894f, 72f), new Vector2(0f, 96f)),

            // Gift: Golfer0..4 share that shape; the placeholder stands in for the first three.
            new ShimmerSite("GpsGiftScreen", "ContentContainer/Golfers",
                            ShimmerHost.Golfers, 3,
                            new Vector2(32f, -92f), new Vector2(894f, 72f), new Vector2(0f, 96f)),

            // Vote: the simple card is 958x232 and the list's own gap is 24.
            new ShimmerSite("GpsVoteScreen", "ContentContainer/VoteList/Content",
                            ShimmerHost.VoteList, 2,
                            new Vector2(0f, 0f), new Vector2(958f, 232f), new Vector2(0f, 256f),
                            shape: SprVoteCard),
        };

        /// <summary>
        /// Author (or re-author) the placeholder groups for whichever sites belong to this root.
        ///
        /// <para>THE HOST IS SAVED INACTIVE. That is the whole reason A2 can still read 0 px: a
        /// screen at rest draws exactly what it drew at HEAD, and a cold fetch is one
        /// <c>SetActive</c> away rather than an <c>Instantiate</c> away — which matters because
        /// <c>ShimmerBlock.prefab</c> does not live under <c>Resources/</c>, so a runtime load
        /// would return null and the panel would stay blank.</para>
        /// </summary>
        private static void EnsureShimmerHosts(GameObject root)
        {
            foreach (ShimmerSite site in Sites)
            {
                if (!root.name.StartsWith(site.Screen)) continue;

                Transform? parent = root.transform.Find(site.ParentPath);
                if (parent == null)
                {
                    Debug.LogWarning($"[GpsPolishBuilder] {root.name}: no {site.ParentPath} — " +
                                     $"shimmer site '{site.Site}' not placed.");
                    continue;
                }

                Transform? hostT = parent.Find(ShimmerHostName);
                if (hostT == null)
                {
                    var go = new GameObject(ShimmerHostName, typeof(RectTransform), typeof(ShimmerHost));
                    hostT = go.transform;
                    hostT.SetParent(parent, worldPositionStays: false);
                }

                var hrt = (RectTransform)hostT;
                hrt.anchorMin        = new Vector2(0f, 1f);
                hrt.anchorMax        = new Vector2(0f, 1f);
                hrt.pivot            = new Vector2(0f, 1f);
                hrt.anchoredPosition = site.Origin;
                hrt.localScale       = Vector3.one;
                int rows = Mathf.CeilToInt(site.Count / (float)site.Columns);
                hrt.sizeDelta = new Vector2(site.Block.x + site.Step.x * (site.Columns - 1),
                                            site.Block.y + site.Step.y * (rows - 1));

                var marker = hostT.GetComponent<ShimmerHost>();
                if (marker == null) marker = hostT.gameObject.AddComponent<ShimmerHost>();
                var so = new SerializedObject(marker);
                so.FindProperty("_site").stringValue = site.Site;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(marker);

                // Re-author the blocks only when the count is wrong, so a second run of this pass
                // preserves every fileID (A6's reason for not re-running the screen builders).
                if (hostT.childCount != site.Count)
                {
                    for (int i = hostT.childCount - 1; i >= 0; i--)
                        Object.DestroyImmediate(hostT.GetChild(i).gameObject);
                    for (int i = 0; i < site.Count; i++)
                        CreateShimmerBlock(hostT, "Block" + i, site.Shape);
                }

                for (int i = 0; i < site.Count; i++)
                {
                    var brt = hostT.GetChild(i) as RectTransform;
                    if (brt == null) continue;
                    brt.anchorMin        = new Vector2(0f, 1f);
                    brt.anchorMax        = new Vector2(0f, 1f);
                    brt.pivot            = new Vector2(0f, 1f);
                    brt.sizeDelta        = site.Block;
                    brt.anchoredPosition = new Vector2(site.Step.x * (i % site.Columns),
                                                       -site.Step.y * (i / site.Columns));
                    brt.localScale       = Vector3.one;
                }

                hostT.gameObject.SetActive(false);
            }
        }

        private const string ShimmerHostName = "ShimmerHost";

        /// <summary>
        /// A CanvasGroup on each layer the push cross-fades. Alpha 1, interactable, raycasts on —
        /// a no-op at rest, which is what makes A2's pixel parity possible at all.
        /// </summary>
        private static void EnsureLayerGroups(GameObject root)
        {
            foreach (string layer in new[] { "Background", "ContentContainer", "GpsNavBar", "BackPill" })
            {
                Transform? t = root.transform.Find(layer);
                if (t == null) continue;
                CanvasGroup cg = Ensure<CanvasGroup>(t.gameObject);
                cg.alpha          = 1f;
                cg.interactable   = true;
                cg.blocksRaycasts = true;
                cg.ignoreParentGroups = false;
            }

            // ScoreUpload's six step roots are cross-faded by ScoreUploadFlowController (§D4)
            // rather than by the push — the screen itself never pushes (CanPush is false for it).
            foreach (Transform child in root.transform)
            {
                if (!child.name.StartsWith("Step") || child.name.Length < 6) continue;
                CanvasGroup cg = Ensure<CanvasGroup>(child.gameObject);
                cg.alpha          = 1f;
                cg.blocksRaycasts = true;
            }
        }

        /// <summary>
        /// §D9 — the bottom safe area, and ONLY the bottom.
        ///
        /// <para>MEASURED, not assumed. At the 1170x2532 reference the content starts 361 px down
        /// and the hub's BackPill 250 px down, against a worst-case top inset of 177 px (iPhone 15
        /// Pro Max Dynamic Island, 59 pt) — 184 px and 73 px of clearance, so the TOP needs
        /// nothing and re-anchoring it would only risk moving a rest pixel for no gain. The BOTTOM
        /// is a different story: the nav bar is 196 px tall, its icons sit 20–176 px above its
        /// bottom edge, and the home indicator claims the bottom 102 px (34 pt) — the icons' lower
        /// half is inside it.</para>
        ///
        /// <para>The shell's own component is reused verbatim, on a stretched wrapper, exactly as
        /// <c>safe_area_top_bar</c> does it: <see cref="SafeAreaFitter"/> re-anchors whatever it
        /// is attached to, so attaching it directly to the nav bar would stretch the bar over the
        /// whole screen. Baseline 0 (not the top bar's 141) because the nav bar is authored flush
        /// to the bottom edge, so the FULL inset is the excess.</para>
        /// </summary>
        private static void EnsureNavBarSafeArea(GameObject root)
        {
            Transform? nav = FindNavBar(root);
            if (nav == null) return;

            Transform? wrapper = root.transform.Find("NavSafeArea");
            if (wrapper == null)
            {
                var go = new GameObject("NavSafeArea", typeof(RectTransform));
                wrapper = go.transform;
                wrapper.SetParent(root.transform, worldPositionStays: false);
            }

            var wrt = (RectTransform)wrapper;
            wrt.anchorMin        = Vector2.zero;
            wrt.anchorMax        = Vector2.one;
            wrt.offsetMin        = Vector2.zero;
            wrt.offsetMax        = Vector2.zero;
            wrt.localScale       = Vector3.one;

            // THE FITTER COMES OFF. It was the bug: a full-screen SafeAreaFitter at baseline 0
            // inset the wrapper's BOTTOM edge by the home indicator, and the bar — pinned to the
            // wrapper's bottom — floated 102 px up the screen with the background showing under
            // it. The wrapper stays (it keeps the `NavSafeArea/GpsNavBar` path every caller,
            // recorder and probe already resolves) but it is now an inert full-screen
            // pass-through, so the bar's bottom is the screen's bottom again.
            var stale = wrapper.GetComponent<SafeAreaFitter>();
            if (stale != null) Object.DestroyImmediate(stale, allowDestroyingAssets: true);

            if (nav.parent != wrapper)
            {
                int order = nav.GetSiblingIndex();
                nav.SetParent(wrapper, worldPositionStays: false);
                wrapper.SetSiblingIndex(order);
            }

            // The inset is handled ON THE BAR instead: it grows upward from a pinned bottom, so
            // the background still reaches the screen edge and only the content clears the
            // indicator. See GpsNavBarSafeArea's header.
            Ensure<GpsNavBarSafeArea>(nav.gameObject);
        }

        /// <summary>
        /// The nav bar, wherever it currently lives.
        ///
        /// <para>Every path lookup for the bar goes through here rather than
        /// <c>Find("GpsNavBar")</c>, because <see cref="EnsureNavBarSafeArea"/> moves it one level
        /// down. Callers that hard-coded the old path would silently find nothing and log a
        /// warning instead of failing — the worst kind of break.</para>
        /// </summary>
        public static Transform? FindNavBar(GameObject root)
            => GpsScreenTransition.FindLayer(root, "GpsNavBar");

        /// <summary>
        /// Deviation D-5 — make the cloned nav bar work on the screens that draw it.
        ///
        /// <para>See <see cref="GpsNavBarBinder"/>'s header for why a polish task is fixing a dead
        /// widget: two of this task's acceptance items (A4 (b), and D2's slot-order direction
        /// rule) are unreachable while the bar only works on the hub, and Profile / Badges /
        /// Avatar currently have no way out at all.</para>
        ///
        /// <para>The HUB is skipped — <c>GpsHubScreenController</c> already wires its own bar,
        /// and a second binder would add a second listener to every slot.</para>
        /// </summary>
        private static void EnsureNavBarBinder(GameObject root)
        {
            if (root.name.StartsWith("GpsHubScreen")) return;
            if (FindNavBar(root) == null) return;                 // Golf Profile / Welcome
            Ensure<GpsNavBarBinder>(root);
        }

        /// <summary>
        /// §D3 — the boundary-entry rise, as one component rather than eight OnEnable edits.
        /// See <see cref="GpsScreenEntryMotion"/> for why it must not fire after a push.
        /// </summary>
        private static void EnsureEntryMotion(GameObject root)
        {
            if (GpsScreenTransition.FindLayer(root, "ContentContainer") == null) return;
            Ensure<GpsScreenEntryMotion>(root);
        }

        /// <summary>§D9 — the Inventory screen's scroll feel, applied to every GPS scroll rect.
        /// Horizontal and vertical get Inventory's own two sensitivities.</summary>
        private static void ApplyScrollFeel(GameObject root)
        {
            foreach (ScrollRect sr in root.GetComponentsInChildren<ScrollRect>(true))
            {
                sr.movementType      = ScrollMovement;
                sr.elasticity        = ScrollElasticity;
                sr.inertia           = true;
                sr.decelerationRate  = ScrollDeceleration;
                sr.scrollSensitivity = sr.horizontal && !sr.vertical
                    ? ScrollSensitivityH
                    : ScrollSensitivityV;
            }
        }

        /// <summary>
        /// §D4 — the sliding step indicator on the Score Upload strip.
        ///
        /// <para>Deviation D-4. The spec asks for "one moving RectTransform … positions computed
        /// from the step pills' anchored X", replacing an indicator that jumps. The strip has no
        /// such indicator: it is five fixed segments with a CUMULATIVE fill (segments 0..step
        /// gold, the rest 15 % white), so turning it into one travelling marker would delete the
        /// progress reading and change the screen at rest — the exact regression the spec's
        /// Reference section forbids. Instead ONE marker is added, shaped and coloured exactly
        /// like a segment, that is INVISIBLE AT REST (alpha 0) and only appears while it travels
        /// from the old active segment to the new one, landing as that segment lights. Rest pixels
        /// are byte-identical to HEAD; the jump is gone.</para>
        /// </summary>
        private static void EnsureStepPolish(GameObject root)
        {
            Transform? segments = root.transform.Find("ContentContainer/StepStrip/Segments");
            if (segments == null) return;

            var first = segments.Find("Seg1") as RectTransform;
            if (first == null) return;

            Transform? marker = segments.Find("StepIndicator");
            if (marker == null)
            {
                var go = new GameObject("StepIndicator", typeof(RectTransform), typeof(CanvasRenderer),
                                        typeof(Image), typeof(CanvasGroup));
                marker = go.transform;
                marker.SetParent(segments, worldPositionStays: false);
            }

            var mrt = (RectTransform)marker;
            mrt.anchorMin        = first.anchorMin;
            mrt.anchorMax        = first.anchorMax;
            mrt.pivot            = first.pivot;
            mrt.sizeDelta        = first.sizeDelta;
            mrt.anchoredPosition = first.anchoredPosition;
            mrt.localScale       = Vector3.one;

            var srcImg = first.GetComponent<Image>();
            var img    = Ensure<Image>(marker.gameObject);
            img.sprite = srcImg != null ? srcImg.sprite : AssetDatabase.LoadAssetAtPath<Sprite>(SprPill);
            img.type   = srcImg != null ? srcImg.type   : Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = srcImg != null ? srcImg.pixelsPerUnitMultiplier : 1f;
            img.color  = GpsUiColor.Gold;
            img.raycastTarget = false;

            // Invisible at rest — the whole reason this addition cannot shift a pixel.
            Ensure<CanvasGroup>(marker.gameObject).alpha = 0f;
            marker.SetAsLastSibling();
        }

        // ═════════════════════════════════════════════════════════════════════
        // Modals
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Turn on <c>ModalController.animateShow</c> for one modal prefab. Written through
        /// <see cref="SerializedObject"/> because the field is private and serialized — the trap
        /// C1 checklist item (dirty-on-write) that has bitten this pipeline before.
        /// </summary>
        public static bool SetModalAnimated(string assetPath)
        {
            GameObject? root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
            {
                Debug.LogWarning($"[GpsPolishBuilder] no modal prefab at {assetPath}");
                return false;
            }
            try
            {
                var modal = root.GetComponentInChildren<Golfin.UI.Modals.ModalController>(true);
                if (modal == null)
                {
                    Debug.LogWarning($"[GpsPolishBuilder] {assetPath} has no ModalController");
                    return false;
                }
                var so   = new SerializedObject(modal);
                var prop = so.FindProperty("animateShow");
                if (prop == null)
                {
                    Debug.LogWarning($"[GpsPolishBuilder] {assetPath}: no animateShow property");
                    return false;
                }
                prop.boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(modal);
                PrefabUtility.SaveAsPrefabAsset(root, assetPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            return true;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Shimmer prefab
        // ═════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Build (or rebuild) <c>Assets/Prefabs/UI/Gps/ShimmerBlock.prefab</c> — a rounded dark
        /// block with one highlight band, clipped by a RectMask2D. §D8.
        /// </summary>
        [MenuItem("GOLFIN/Gps/Build Shimmer Block", priority = 231)]
        public static void BuildShimmerBlock()
        {
            const string path = PrefabDir + "ShimmerBlock.prefab";
            GameObject root = CreateShimmerBlock(null, "ShimmerBlock");
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[GpsPolishBuilder] ShimmerBlock.prefab written to " + path);
        }

        /// <summary>
        /// ONE construction, used by the prefab above and by every in-screen host.
        ///
        /// <para>The in-screen blocks are BUILT rather than instantiated from the prefab on
        /// purpose: nesting a prefab instance inside each screen prefab would put a
        /// <c>m_CorrespondingSourceObject</c> chain into nine assets whose scene copies are
        /// unpacked, and re-running this pass would then reshuffle the nested instance's fileIDs.
        /// Same object, no provenance chain to break.</para>
        ///
        /// <para>The band carries the pill SPRITE, not a flat fill. A null-sprite Image is what
        /// the fidelity linter fails as a fabricated flat box (Rule 21 render-health), and these
        /// blocks now live inside prefabs the linter reads.</para>
        /// </summary>
        private static GameObject CreateShimmerBlock(Transform? parent, string name,
                                                     string? shapePath = null)
        {
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(SprPill);
            var shape = shapePath != null ? AssetDatabase.LoadAssetAtPath<Sprite>(shapePath) : null;

            var root = new GameObject(name,
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(RectMask2D), typeof(ShimmerBlock));
            var rrt = root.GetComponent<RectTransform>();
            if (parent != null) rrt.SetParent(parent, worldPositionStays: false);
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot     = new Vector2(0f, 1f);
            rrt.sizeDelta = new Vector2(900f, 120f);
            rrt.localScale = Vector3.one;

            var bg = root.GetComponent<Image>();
            if (shape != null)
            {
                // The row this block stands in for is a baked panel, not a capsule: use its own
                // silhouette, Simple, so the placeholder is the shape of the thing that replaces it.
                bg.sprite = shape;
                bg.type   = Image.Type.Simple;
                bg.pixelsPerUnitMultiplier = 1f;
            }
            else
            {
                bg.sprite = pill;
                bg.type   = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 88f / 24f;  // S_PillStadium border 88 -> r24
            }
            bg.color  = GpsUiColor.ADark(Color.black, 0.35f);
            bg.raycastTarget = false;

            var bandGo = new GameObject("Band", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            bandGo.transform.SetParent(root.transform, worldPositionStays: false);
            var brt = bandGo.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(0f, 0f);
            brt.anchorMax = new Vector2(0f, 1f);
            brt.pivot     = new Vector2(0f, 0.5f);
            brt.offsetMin = new Vector2(0f, 0f);
            brt.offsetMax = new Vector2(0f, 0f);
            brt.sizeDelta = new Vector2(180f, 0f);
            var band = bandGo.GetComponent<Image>();
            band.sprite = pill;
            band.type   = Image.Type.Sliced;
            band.pixelsPerUnitMultiplier = 88f / 24f;
            band.color = GpsUiColor.A(Color.white, 0.08f);
            band.raycastTarget = false;

            var so = new SerializedObject(root.GetComponent<ShimmerBlock>());
            so.FindProperty("_band").objectReferenceValue = brt;
            so.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        // ═════════════════════════════════════════════════════════════════════
        // Utility
        // ═════════════════════════════════════════════════════════════════════

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T c = go.GetComponent<T>();
            if (c == null) c = go.AddComponent<T>();
            return c;
        }

        /// <summary>Every prefab this pass touches, for the report's file table.</summary>
        public static IEnumerable<string> TouchedPrefabPaths()
        {
            foreach (string n in ScreenPrefabs)  yield return PrefabDir + n + ".prefab";
            foreach (string n in AnimatedModals) yield return PrefabDir + n + ".prefab";
        }
    }
}
