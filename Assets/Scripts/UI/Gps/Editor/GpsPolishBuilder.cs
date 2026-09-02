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
        /// <para>THE SCENE COPIES ARE NOT PREFAB INSTANCES. Every GPS screen was unpacked into
        /// ShellScene when it was deployed (verified: <c>IsPartOfPrefabInstance</c> is false for
        /// all nine), so a prefab edit reaches the asset and NOTHING the player runs. That is the
        /// single most important fact about changing a GPS screen in this project, and it is why
        /// this menu item exists rather than a re-deploy.</para>
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
        }

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
            Ensure<SafeAreaFitter>(wrapper.gameObject);

            if (nav.parent != wrapper)
            {
                // The nav bar keeps its own anchors and anchoredPosition (bottom-centre, 0) — the
                // wrapper is exactly the screen rect until a real inset shrinks it, so nothing
                // moves at the reference resolution.
                int order = nav.GetSiblingIndex();
                nav.SetParent(wrapper, worldPositionStays: false);
                wrapper.SetSiblingIndex(order);
            }
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
            var pill = AssetDatabase.LoadAssetAtPath<Sprite>(SprPill);

            var root = new GameObject("ShimmerBlock",
                typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(RectMask2D), typeof(ShimmerBlock));
            var rrt = root.GetComponent<RectTransform>();
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(0f, 1f);
            rrt.pivot     = new Vector2(0f, 1f);
            rrt.sizeDelta = new Vector2(900f, 120f);

            var bg = root.GetComponent<Image>();
            bg.sprite = pill;
            bg.type   = Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = 88f / 24f;      // S_PillStadium border 88 -> r24
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
            band.color = GpsUiColor.A(Color.white, 0.08f);
            band.raycastTarget = false;

            var so = new SerializedObject(root.GetComponent<ShimmerBlock>());
            so.FindProperty("_band").objectReferenceValue = brt;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Debug.Log("[GpsPolishBuilder] ShimmerBlock.prefab written to " + path);
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
