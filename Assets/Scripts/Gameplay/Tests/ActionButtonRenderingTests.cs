#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// Guard against the action-button "white top" regression.
    ///
    /// The in-game action buttons (Spin / FadeDraw / Golfin-ball / Driver-club) and the
    /// ball/club selector option cards use the <c>Button - All</c> sprite whose design is a
    /// WHITE top half (icon tray) + navy bottom (label) + gold rounded border. `IconArea`
    /// must therefore have NO opaque background.
    ///
    /// At Order 354 (commit 72bbb8db4) ActionButtonsBuilder baked an opaque
    /// navy quad onto every button's IconArea ("…so it fades cleanly"), which masked the
    /// white top and overflowed the rounded border. Cesar rejected it on sight. This test
    /// fails if any such opaque navy mask reappears in LabScaffold — catching a silent
    /// re-introduction (e.g. another builder re-run) before it ships.
    /// </summary>
    [TestFixture]
    public class ActionButtonRenderingTests
    {
        const string LabScaffoldPath = "Assets/Scenes/Physics/LabScaffold.unity";

        // Button-All navy half = RGB (0, 30, 57) / 255.
        static readonly Color Navy = new Color(0f, 30f / 255f, 57f / 255f, 1f);

        [Test]
        public void ActionButtonIconAreas_HaveNoOpaqueNavyMask()
        {
            Scene scene = EditorSceneManager.OpenScene(LabScaffoldPath, OpenSceneMode.Additive);
            try
            {
                var offenders = new List<string>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var img in root.GetComponentsInChildren<Image>(includeInactive: true))
                    {
                        if (img.gameObject.name != "IconArea") continue;
                        if (!img.enabled) continue;          // disabled mask is harmless (the current fixed state)
                        if (img.sprite != null) continue;    // a real sprite is not the solid-quad mask
                        Color c = img.color;
                        bool navy = Mathf.Abs(c.r - Navy.r) < 0.05f
                                 && Mathf.Abs(c.g - Navy.g) < 0.05f
                                 && Mathf.Abs(c.b - Navy.b) < 0.05f;
                        if (navy && c.a > 0.5f) offenders.Add(PathOf(img.transform));
                    }
                }

                Assert.IsEmpty(offenders,
                    "An opaque navy IconArea mask is covering the WHITE top half of the Button-All sprite " +
                    "on these action/selector buttons — the Order-354 / commit-72bbb8db4 regression. " +
                    "Button-All's white top IS the design; do NOT add an opaque IconArea background " +
                    "(see ActionButtonsBuilder IconArea comment). Offenders: " + string.Join(", ", offenders));
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, removeScene: true);
            }
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
#endif
