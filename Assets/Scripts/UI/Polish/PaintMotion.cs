// ─────────────────────────────────────────────────────────────────────────────
// gps_polish §D3 / §D8 — WHICH PAINT IS THIS, and what the answer is allowed to
// animate.
//
// Every GPS screen paints twice: once from whatever the service already has
// cached, on the first frame of OnEnable ("paint what is already known BEFORE
// any request"), and once more when the fetch answers. Those two paints must
// NOT look the same:
//
//   paint(cache)  — instant. The numbers were already correct a moment ago;
//                   staggering them in would be a loading animation played over
//                   data that never left.
//   paint(fetch)  — staggered, but only the FIRST one, and only when the cache
//                   had nothing to show. A refresh that lands on top of a
//                   painted list re-flows rows the player is already reading.
//
// The same single question answers §D8: a cold fetch (no cache, nothing painted)
// is exactly when a shimmer is honest, and it is the only time it may be shown.
// One gate object per site, so the two features can never disagree about what
// "cold" means.
//
// EVERY DECISION IS LOGGED, one line per site per paint, because that log line
// is the acceptance evidence for both R1 and R5 — a stagger you cannot tell
// apart from a repaint is not reviewable.
// ─────────────────────────────────────────────────────────────────────────────
#nullable enable
using System.Collections.Generic;
using Golfin.UI.Polish;
using UnityEngine;

namespace Golfin.Gps.UI
{
    /// <summary>Which of a site's three paints this is.</summary>
    public enum PaintKind
    {
        /// <summary>Painted from the service's cache in <c>OnEnable</c>. Never staggered.</summary>
        Cache,
        /// <summary>Painted from a network answer. The first cold one staggers.</summary>
        Fetch,
        /// <summary>Painted again from data already on screen — a language change, a filter
        /// switch. Never staggered, never logged, never gates a shimmer.</summary>
        Repaint,
    }

    /// <summary>
    /// Per-site memory of whether the cache had anything and whether the first fetch paint has
    /// already been spent. One instance per list on a screen; re-armed in <c>OnEnable</c>.
    /// </summary>
    public sealed class PaintGate
    {
        private readonly string _tag;
        private readonly string _site;

        /// <summary>Whether this site's first cold fetch paint actually staggers anything. False
        /// for a region that only fades its panel in (the gift catalog strip) — logging
        /// "staggered" there would describe motion that does not happen.</summary>
        private readonly bool _staggers;

        private bool _cacheHit;
        private bool _spent;

        public PaintGate(string tag, string site, bool staggers = true)
        {
            _tag      = tag;
            _site     = site;
            _staggers = staggers;
        }

        /// <summary>
        /// True while nothing has been painted from cache and no fetch paint has landed — i.e.
        /// the player is looking at an empty panel waiting on the network. The ONLY condition
        /// under which a shimmer may be shown (§D8).
        /// </summary>
        public bool IsCold => !_cacheHit && !_spent;

        /// <summary>The one entry point the paint paths call: log the paint, and answer whether
        /// it may stagger.</summary>
        public bool Should(PaintKind kind, int count)
        {
            switch (kind)
            {
                case PaintKind.Cache: Cache(count); return false;
                case PaintKind.Fetch: return Fetch(count);
                default:              return false;
            }
        }

        /// <summary>Call first thing in <c>OnEnable</c>, before any paint.</summary>
        public void Rearm()
        {
            _cacheHit = false;
            _spent    = false;
        }

        /// <summary>Record a paint that came from the service's cache. Never staggered.</summary>
        public void Cache(int count)
        {
            if (count > 0) _cacheHit = true;
            Debug.Log($"{_tag} {_site} paint(cache) n={count} — instant" +
                      (count > 0 ? "" : " (cache empty)"));
        }

        /// <summary>
        /// Ask whether a paint driven by a fetch result should stagger, and log the verdict.
        /// Returns true at most once per <see cref="Rearm"/>.
        /// </summary>
        public bool Fetch(int count)
        {
            bool first = !_cacheHit && !_spent && count > 0;
            _spent = true;
            Debug.Log($"{_tag} {_site} paint(fetch) n={count} — " +
                      (first ? (_staggers ? "staggered" : "first paint")
                             : _cacheHit ? "instant (cache hit)" : "instant (repaint)"));
            return first && _staggers;
        }
    }

    /// <summary>
    /// §D4 — one data panel that fades in on a COLD open and is instant on a cache hit.
    ///
    /// <para>The fade rides the placeholder, not the data: on a cold open the panel arrives with
    /// its shimmer and the rows then stagger in on top of it (§D3). Fading the panel itself in
    /// only once the rows landed would hide the very placeholder that says they are coming.</para>
    ///
    /// <para>Resolved BY PATH once per screen entry and cached. Every paint path — including the
    /// failure arms — calls <see cref="Reveal"/>, so a panel can never be stranded at alpha 0 by
    /// a fetch that never answers.</para>
    /// </summary>
    public sealed class PanelReveal
    {
        private readonly string _path;
        private CanvasGroup? _group;
        private bool _revealed;

        public PanelReveal(string path) { _path = path; }

        /// <summary>Call in <c>OnEnable</c>, before any paint.</summary>
        public void Rearm(GameObject root)
        {
            _revealed = false;
            if (_group != null) return;
            Transform? t = root != null ? root.transform.Find(_path) : null;
            if (t == null) return;
            var cg = t.GetComponent<CanvasGroup>();
            if (cg == null) cg = t.gameObject.AddComponent<CanvasGroup>();
            _group = cg;
        }

        /// <summary>Show the panel. The FIRST call of a screen entry fades when
        /// <paramref name="animate"/> is true; every later call is a no-op that guarantees
        /// alpha 1.</summary>
        public void Reveal(MonoBehaviour host, bool animate)
        {
            if (_group == null) return;
            if (_revealed) { _group.alpha = 1f; return; }
            _revealed = true;

            if (!animate) { _group.alpha = 1f; return; }
            UiMotion.Run(host, UiMotion.Fade(_group, 0f, 1f));
        }
    }

    /// <summary>The staggered rise itself, and the shimmer show/hide that shares its gate.</summary>
    public static class GpsPaintMotion
    {
        /// <summary>
        /// Rise a group of freshly painted rows, <see cref="UiMotion.StaggerDelay"/> apart.
        ///
        /// <para>Every row is taken to alpha 0 IMMEDIATELY — before the first beat — and only
        /// then released one at a time. Without that, a row that has not had its beat yet is
        /// drawn at rest, and the group reads as "all six appear, then all six flicker down and
        /// rise", which is worse than no stagger at all.</para>
        ///
        /// <para>The CanvasGroups are added at RUNTIME and settle at alpha 1, so no prefab gains
        /// a component and no rest pixel moves (A2).</para>
        /// </summary>
        public static void StaggerRise(MonoBehaviour host, IList<Transform> rows)
        {
            if (host == null || rows == null || rows.Count == 0) return;

            int n = rows.Count;
            var rects  = new RectTransform[n];
            var groups = new CanvasGroup[n];
            for (int i = 0; i < n; i++)
            {
                Transform t = rows[i];
                if (t == null) continue;
                rects[i]  = t as RectTransform;
                groups[i] = Ensure(t.gameObject);
                if (groups[i] != null) groups[i].alpha = 0f;
            }

            UiMotion.Run(host, UiMotion.Stagger(n, i =>
            {
                if (i < 0 || i >= n) return;
                RectTransform rect = rects[i];
                if (rect == null) return;
                UiMotion.Run(host, UiMotion.Rise(rect, groups[i]));
            }));
        }

        /// <summary>Overload for the common case of a fixed authored row array.</summary>
        public static void StaggerRise(MonoBehaviour host, IList<GameObject?> rows, int count)
        {
            var live = new List<Transform>(count);
            for (int i = 0; i < count && i < rows.Count; i++)
                if (rows[i] != null && rows[i]!.activeSelf) live.Add(rows[i]!.transform);
            StaggerRise(host, live);
        }

        /// <summary>
        /// Show or hide a site's shimmer placeholder. <paramref name="cold"/> is the gate's own
        /// <see cref="PaintGate.IsCold"/> — never "is a request running", which is true on every
        /// re-entry and would flash a loading state over correct numbers.
        /// </summary>
        public static void Shimmer(GameObject screenRoot, string site, bool cold)
        {
            ShimmerHost? host = ShimmerHost.Find(screenRoot, site);
            if (host == null)
            {
                // LOUD, not silent. A site whose host cannot be found shows no placeholder at all,
                // which looks exactly like a site that decided not to — the failure mode this
                // whole file exists to avoid.
                Debug.LogWarning($"[Shimmer] {site} — NO HOST under {screenRoot?.name} " +
                                 "(GpsPolishBuilder has not been run on this prefab)");
                return;
            }
            bool was = host.gameObject.activeSelf;
            host.Set(cold);
            Debug.Log($"[Shimmer] {site} cold={cold} {(was ? "shown" : "hidden")}" +
                      $" -> {(cold ? "shown" : "hidden")}");
        }

        /// <summary>Fade a panel in with its data. A no-op when it is already at full alpha, so a
        /// repaint does not re-fade a panel the player is reading (§D4).</summary>
        public static void FadeInPanel(MonoBehaviour host, GameObject? panel, bool animate)
        {
            if (panel == null) return;
            CanvasGroup cg = Ensure(panel);
            if (!animate) { cg.alpha = 1f; return; }
            UiMotion.Run(host, UiMotion.Fade(cg, 0f, 1f));
        }

        private static CanvasGroup Ensure(GameObject go)
        {
            // `== null`, not `??`: GetComponent hands back a fake-null UnityEngine.Object when the
            // component is absent and `??` does not see that as null (CLAUDE.md Basic Rules #4).
            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            return cg;
        }
    }
}
