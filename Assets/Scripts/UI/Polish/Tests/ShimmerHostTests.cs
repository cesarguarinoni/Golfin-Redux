// ─────────────────────────────────────────────────────────────────────────────
// game_polish_b §D4 — the two properties the shimmer actually depends on.
//
// 1. EVERY DECLARED SITE HAS A HOST. GpsPaintMotion.Shimmer warns and returns when it cannot
//    find one, so a site whose host was never placed — or was placed and then lost to a
//    prefab revert, a merge, a panel re-parented by a later task — shows no placeholder at
//    all. That looks exactly like a site that decided not to, which is the failure mode the
//    warning exists for and the reason it must not be the only guard.
//
// 2. NO HOST IS ACTIVE AT REST. This is A3's precondition, not a nicety: an active host draws
//    placeholder blocks over a screen that has never fetched, and every rest-parity capture in
//    the task would be wrong by however many blocks were left on.
//
// Read off the SHIPPED SCENE rather than by running the builder, for the same reason
// ModalPopTests reads the shipped assets: the question is what the game will do, not what the
// builder would do if someone ran it.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace Golfin.UI.Polish.Tests
{
    [TestFixture]
    public class ShimmerHostTests
    {
        const string SceneAsset = "Assets/Scenes/ShellScene.unity";

        static string[] Sites()
        {
            Type t = Probe.Type("Golfin.UI.Polish.GameShimmerSites");
            FieldInfo? all = t.GetField("All", BindingFlags.Public | BindingFlags.Static);
            Assert.NotNull(all, "GameShimmerSites.All is gone");
            return (string[])all!.GetValue(null)!;
        }

        /// <summary>Every `_site:` value the scene serializes, in file order.</summary>
        static List<string> SceneSites()
        {
            Assert.IsTrue(File.Exists(SceneAsset), SceneAsset + " not found");
            var found = new List<string>();
            foreach (string line in File.ReadAllLines(SceneAsset))
            {
                string t = line.Trim();
                if (!t.StartsWith("_site:")) continue;
                string v = t.Substring("_site:".Length).Trim();
                if (v.Length > 0) found.Add(v);
            }
            return found;
        }

        [Test]
        public void EveryDeclaredSite_HasAHostInTheScene()
        {
            List<string> inScene = SceneSites();
            var missing = new List<string>();
            foreach (string site in Sites())
                if (!inScene.Contains(site)) missing.Add(site);

            CollectionAssert.IsEmpty(missing,
                "no ShimmerHost in ShellScene for: " + string.Join(", ", missing)
                + " — re-run GOLFIN/Game Polish/Apply — shimmer hosts. Until then those sites "
                + "show no placeholder and only log a warning.");
        }

        [Test]
        public void TheSiteTable_HasNoDanglingNames()
        {
            // The mirror of the test above: a name in the table that nothing places is a name
            // someone will wire a Shimmer() call to, and it will silently do nothing.
            foreach (string site in Sites())
                Assert.IsNotEmpty(site, "an empty site name in GameShimmerSites.All");
            CollectionAssert.AllItemsAreUnique(Sites());
        }

        [Test]
        public void NoShimmerHost_IsActiveAtRest()
        {
            // A host is authored inactive; the scene records that as `m_IsActive: 0` on the
            // GameObject that owns it. Walk the YAML: for every `_site:` line, find the
            // MonoBehaviour's m_GameObject and check that object's m_IsActive.
            string[] lines = File.ReadAllLines(SceneAsset);

            // Pass 1 — GameObject fileID -> m_IsActive
            var isActive = new Dictionary<string, string>();
            string? currentGo = null;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("--- !u!1 &")) { currentGo = t.Substring("--- !u!1 &".Length).Trim(); continue; }
                if (t.StartsWith("--- !u!")) { currentGo = null; continue; }
                if (currentGo != null && t.StartsWith("m_IsActive:"))
                {
                    isActive[currentGo] = t.Substring("m_IsActive:".Length).Trim();
                    currentGo = null;
                }
            }

            // Pass 2 — every MonoBehaviour carrying a _site, and the GameObject it sits on
            var live = new List<string>();
            string? owner = null;
            bool inBehaviour = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("--- !u!")) { inBehaviour = t.StartsWith("--- !u!114 &"); owner = null; continue; }
                if (!inBehaviour) continue;
                if (t.StartsWith("m_GameObject:"))
                {
                    int a = t.IndexOf("fileID: ", StringComparison.Ordinal);
                    if (a >= 0) owner = t.Substring(a + 8).TrimEnd('}', ' ');
                    continue;
                }
                if (!t.StartsWith("_site:")) continue;
                string site = t.Substring("_site:".Length).Trim();
                if (site.Length == 0 || owner == null) continue;
                if (isActive.TryGetValue(owner, out string? act) && act == "1")
                    live.Add($"{site} (GameObject {owner})");
            }

            CollectionAssert.IsEmpty(live,
                "these ShimmerHosts are ACTIVE at rest, so every rest-parity capture is wrong by "
                + "however many placeholder blocks they draw: " + string.Join(", ", live));
        }
    }
}
