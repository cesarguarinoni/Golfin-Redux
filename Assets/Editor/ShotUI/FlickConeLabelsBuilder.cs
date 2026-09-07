using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Golfin.Gameplay.UI.ShotUI;
using Object = UnityEngine.Object;

namespace Golfin.EditorTools.ShotUI
{
    /// <summary>
    /// Flick's 100% / 120% labels, and the one reference that makes the finger and the drawn club
    /// read the same number (flick_pull_mapping §3.3).
    ///
    /// <para>LABELS, NO LINES. The spec asked for tick lines with labels and this builder first
    /// made both; Cesar, on seeing them: <i>"remove the 100% and 120% lines, leave only the
    /// labels"</i>. So it now DELETES any <c>Tick100</c> / <c>Tick120</c> it previously created —
    /// re-running is what cleans a scene that has them, rather than leaving orphans behind a
    /// null reference.</para>
    ///
    /// <para>WHY A BUILDER AND NOT A HAND-AUTHORED SCENE EDIT: the other three schemes each have
    /// one (<c>PendulumSchemeBuilder</c>, <c>NeedleSchemeBuilder</c>,
    /// <c>FreeSwingSchemeBuilder</c>) and Flick never did, because Flick predates them and was
    /// authored by hand — which is precisely why its cone height once lived on four objects at
    /// once. The label atom is the PENDULUM LANE'S, verbatim: 28px Rubik at the shell canvas's
    /// ÷1.2, white for 100% and the lane's red #FF5A5A for 120%.</para>
    ///
    /// <para>IT PLACES NOTHING. Each label's y is <c>ShotConeView.ApplyConfiguredGeometry</c>'s
    /// job, off <c>FlickPull100Px</c> / <c>FlickPull120Px</c>, and its x is
    /// <c>RefreshLabelOffsets</c>' — so a retune of either threshold moves the label and the power
    /// it names together. This builder only creates the two GameObjects, once, and wires them —
    /// plus <c>ClubHandleDragger._coneView</c>, which is the dragger's only route to the club's
    /// rest and therefore to a pull measured from it. Wired by <c>SerializedObject</c>, never a
    /// hand drag, and never <c>Find()</c>-ed at runtime.</para>
    ///
    /// <para>Idempotent: re-running reuses whatever already exists by name and re-asserts colour,
    /// font and wiring. Safe on a scene that has already been built.</para>
    /// </summary>
    public static class FlickConeLabelsBuilder
    {
        private const string ScenePath = "Assets/Scenes/Physics/LabScaffold.unity";

        // The Pendulum lane's label atom (PendulumSchemeBuilder), reused rather than re-picked.
        private const float LabelFontNodePx = 28f;
        private const float FontDivisor     = 1.2f;   // shell canvas: geometry 1:1, TMP ÷1.2
        /// <summary>The Pendulum's side offset is 76 = half a 120-wide lane + a 16px GAP. A cone is
        /// a different width at every height, so it is the GAP that ports across;
        /// <c>ShotConeView.RefreshLabelOffsets</c> parks each label that far outside the cone's own
        /// edge at that height. Seeded here, and wired onto the view so it lives in one place.</summary>
        private const float LabelGapPx      = 16f;

        private static readonly Color Label120C = Hex(0xFF5A5A);

        /// <summary>The two line GameObjects an earlier build of this task created. Named so the
        /// cleanup is explicit rather than a silent side effect of "rebuild".</summary>
        private static readonly string[] RetiredTicks = { "Tick100", "Tick120" };

        [MenuItem("GOLFIN/Build/Flick Cone % Labels + Dragger Wiring (LabScaffold)")]
        public static void BuildInOpenScene()
        {
            if (!EditorSceneManager.GetActiveScene().path.EndsWith("LabScaffold.unity"))
            {
                Debug.LogError($"[FlickConeLabelsBuilder] open {ScenePath} first.");
                return;
            }
            Debug.Log(Build());
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        }

        /// <summary>Returns a one-line report. Public so the acceptance run can build headlessly
        /// rather than asking a human to click a menu item.</summary>
        public static string Build()
        {
            // GameObject.Find skips INACTIVE objects and the scheme roots ship inactive
            // (ShotSchemeHost turns on only the live scheme), so this walks the scene.
            Transform coneMesh = FindInScene("ConeMesh");
            if (coneMesh == null) return "[FlickConeLabelsBuilder] ConeMesh not found — nothing built.";

            var view    = coneMesh.GetComponentInParent<ShotConeView>(true);
            var dragger = coneMesh.GetComponentInChildren<ClubHandleDragger>(true);
            if (view == null)    return "[FlickConeLabelsBuilder] ShotConeView not found above ConeMesh.";
            if (dragger == null) return "[FlickConeLabelsBuilder] ClubHandleDragger not found under ConeMesh.";

            var meshRt = (RectTransform)coneMesh;

            int removed = RemoveRetiredTicks(meshRt);

            var label100 = Label(meshRt, "Label100", "100%", Color.white);
            var label120 = Label(meshRt, "Label120", "120%", Label120C);

            // The club is drawn over the cone and the labels over the club — the Pendulum's
            // ordering, and for its reason: at full pull the head is ~230px wide and would swallow
            // text sitting beside it.
            dragger.transform.SetAsLastSibling();
            label100.SetAsLastSibling();
            label120.SetAsLastSibling();

            Wire(view,    ("_label100", label100.GetComponent<TextMeshProUGUI>()),
                          ("_label120", label120.GetComponent<TextMeshProUGUI>()));
            Wire(dragger, ("_coneView", view));
            WireFloat(view, "_labelGapPx", LabelGapPx);

            // Place them NOW so the edit-mode scene shows the shipped y rather than a zero.
            view.ApplyConfiguredGeometry();

            return $"[FlickConeLabelsBuilder] Label100 at {label100.anchoredPosition}, " +
                   $"Label120 at {label120.anchoredPosition}; removed {removed} retired tick line(s); " +
                   $"ClubHandleDragger._coneView -> {view.name}.";
        }

        /// <summary>Delete the two drawn lines an earlier build left under the cone. Not a
        /// no-op-if-absent convenience: a scene built before Cesar's call HAS them, and leaving
        /// them would keep drawing exactly what he asked to remove.</summary>
        private static int RemoveRetiredTicks(RectTransform parent)
        {
            int removed = 0;
            foreach (string name in RetiredTicks)
                for (int i = parent.childCount - 1; i >= 0; i--)
                {
                    Transform c = parent.GetChild(i);
                    if (c.name != name) continue;
                    Undo.DestroyObjectImmediate(c.gameObject);
                    removed++;
                }
            return removed;
        }

        private static RectTransform Label(RectTransform parent, string name, string text, Color color)
        {
            // CanvasRenderer up front: [RequireComponent] on the Graphic BASE is not reliably
            // honoured by AddComponent on a subclass, and without one the text never draws.
            var rt  = Ensure(parent, name, typeof(CanvasRenderer));
            // == null, never ?? — the project's Unity-null rule (tasks/lessons.md).
            var tmp = rt.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.font             = FindRubik();
            tmp.fontSize         = LabelFontNodePx / FontDivisor;
            tmp.color            = color;
            tmp.fontStyle        = FontStyles.Normal;
            tmp.text             = text;
            tmp.raycastTarget    = false;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.alignment        = TextAlignmentOptions.Left;

            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);   // ConeMesh's pivot = the cone BASE
            rt.pivot     = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 40f);
            // x/y are seeds only — ShotConeView re-places both from the config.
            rt.anchoredPosition = new Vector2(LabelGapPx, rt.anchoredPosition.y);
            return rt;
        }

        /// <summary>The Rubik SDF the shell canvas already uses — never a new import.
        /// <c>PendulumSchemeBuilder.FindRubik</c>'s Regular arm, verbatim.</summary>
        private static TMP_FontAsset FindRubik()
        {
            const string Want = "Rubik-VariableFont_wght SDF";
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (f != null && f.name == Want) return f;
            }
            Debug.LogWarning($"[FlickConeLabelsBuilder] font '{Want}' not found — TMP default used.");
            return null;
        }

        /// <summary>Reuse the child if it is already there — re-running the builder must not
        /// leave a second Label100 behind.</summary>
        private static RectTransform Ensure(RectTransform parent, string name, params System.Type[] comps)
        {
            for (int i = 0; i < parent.childCount; i++)
                if (parent.GetChild(i).name == name && parent.GetChild(i) is RectTransform existing)
                    return existing;

            var all = new System.Type[comps.Length + 1];
            all[0] = typeof(RectTransform);
            comps.CopyTo(all, 1);
            var go = new GameObject(name, all);
            Undo.RegisterCreatedObjectUndo(go, "Build Flick cone % labels");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            return rt;
        }

        // ── wiring (SerializedObject — never a hand drag) ───────────────────────

        private static void WireFloat(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            var p  = so.FindProperty(field);
            if (p == null) { Debug.LogError($"[FlickConeLabelsBuilder] no field '{field}' on {target.GetType().Name}"); return; }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void Wire(Object target, params (string field, Object value)[] pairs)
        {
            var so = new SerializedObject(target);
            foreach (var (field, value) in pairs)
            {
                var p = so.FindProperty(field);
                if (p == null)
                { Debug.LogError($"[FlickConeLabelsBuilder] no field '{field}' on {target.GetType().Name}"); continue; }
                p.objectReferenceValue = value;
                // A reference of the WRONG TYPE is dropped silently by SerializedProperty, which is
                // exactly how a [SerializeField] ends up null after a "successful" build.
                if (value != null && p.objectReferenceValue == null)
                    Debug.LogError($"[FlickConeLabelsBuilder] '{field}' rejected {value.GetType().Name} " +
                                   $"'{value.name}' — wrong type for that field.");
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Transform FindInScene(string name) => Object
            .FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(t => t.name == name);

        private static Color Hex(int rgb) => new Color(
            ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
