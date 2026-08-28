using UnityEngine;

namespace Golfin.Physics.Viewer
{
    /// <summary>
    /// Testable static helper for type-aware surface snapping.
    /// PhysicsLabController delegates SurfaceSnap here; tests call this directly.
    /// </summary>
    public static class PlacementSnapHelper
    {
        // preferredSurfaceTypeValue: Golfin.Course.SurfaceType int (1=Green, 4=Bunker, etc.) or null.
        // Among all downward hits from Y=500, prefers the HIGHEST Y on the preferred surface type.
        // Falls back to the nearest (highest) PLAYABLE-SURFACE hit when no preferred match.
        //
        // ob_ball_in_air fix (2026-08-05): two defects made an OB drop occasionally place the ball
        // metres up in the air, apparently "on a tree".
        //
        //   1. Trees are solid raycast geometry. The BPS fir prototypes carry a CapsuleCollider
        //      ~34 m tall (radius 0.88, centre y 17.2, IsTrigger 0), and Unity's Terrain generates
        //      colliders for tree prototypes that have them (the scene's
        //      m_PreserveTreePrototypeLayers only exists to decide their layer). The cast below
        //      uses layer mask ~0, and QueryTriggerInteraction.Ignore does not help because the
        //      capsules are not triggers — so a drop point under a canopy could snap onto the tree.
        //
        //      INCOMPLETE — reopened 2026-08-28 (ball came to rest on a canopy on Hole 1, turn 6).
        //      That fix rejected the tree by COLLIDER TYPE (capsule/sphere), which never fires:
        //      Unity reports a terrain-tree hit with collider == the *TerrainCollider*, not the
        //      tree's own capsule, and the very next clause accepted TerrainCollider as a playable
        //      surface. Verified in Play Mode: every above-ground hit at a Hole 1 tree centre comes
        //      back as `TerrainCollider 'TerrainRoot'`, and this function returned a Y more than 2 m
        //      above the baked ground at 1362 of 1362 tree centres (worst +28.2 m). Sorting the hits
        //      by distance (defect 2 below) only made the canopy win deterministically instead of
        //      sometimes. Note also that a prototype does NOT need a root collider for Unity to
        //      generate tree colliders — Hole 1's four prototypes carry colliders only on children.
        //
        //      Collider type therefore cannot separate a tree from the ground; only GEOMETRY can.
        //      The terrain surface at an XZ is the heightmap value there, so a TerrainCollider hit
        //      meaningfully above the heightmap is a tree — see IsTerrainTreeHit. The capsule/sphere
        //      rejection is KEPT: standalone (non-terrain) trees are real GameObjects and do arrive
        //      as their own capsules.
        //
        //      Terrain trees are NOT SurfaceMarker zone meshes, so the surface filter also rejects
        //      them on that count.
        //
        //   2. Physics.RaycastAll does NOT sort its results — Unity documents the order as
        //      undefined. The old code took hits[0] and called it "closest from above", so which
        //      collider won was arbitrary. That is why the ball floated only SOMETIMES; had the
        //      array really been distance-sorted, the canopy would have won every single time.
        //      Hits are now sorted by distance explicitly.
        //
        // The OB drop is the exposed caller because it passes preferredSurfaceTypeValue: null
        // (SetupAtTee passes 6/Tee and was always safe).
        public static float Snap(float x, float z, float defaultY,
                                 int? preferredSurfaceTypeValue = null,
                                 GameObject excludeBallGO = null)
        {
            var hits = UnityEngine.Physics.RaycastAll(
                new Vector3(x, 500f, z), Vector3.down, 1000f, ~0, QueryTriggerInteraction.Ignore);

            if (hits.Length == 0) return defaultY;

            // RaycastAll order is undefined — sort near-to-far so "first hit" means "highest
            // surface" deterministically.
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            // Optionally exclude the ball itself (belt+suspenders; colliders are disabled, but safe).
            GameObject ballGO = excludeBallGO != null ? excludeBallGO
                              : BallAnimator.Instance != null ? BallAnimator.Instance.gameObject
                              : null;

            // Reflect into Golfin.Course.SurfaceMarker (Assembly-CSharp, not directly referenceable).
            System.Type smType = System.Type.GetType("Golfin.Course.SurfaceMarker, Assembly-CSharp");
            System.Reflection.FieldInfo stField = smType?.GetField("surfaceType");

            float bestY          = float.NegativeInfinity;
            float surfaceY       = float.NegativeInfinity;
            float anyY           = float.NegativeInfinity;
            bool  foundPreferred = false;
            bool  foundSurface   = false;
            bool  foundAny       = false;

            foreach (var h in hits)
            {
                if (IsBall(h.collider, ballGO))
                    continue;

                // A terrain tree is never a surface — and must never win the anyY fallback either,
                // so it is skipped outright rather than merely failing IsPlayableSurface.
                if (IsTerrainTreeHit(h))
                    continue;

                bool isPreferred = false;
                if (preferredSurfaceTypeValue.HasValue && smType != null && stField != null)
                {
                    var marker = h.collider.GetComponentInParent(smType);
                    if (marker != null)
                        isPreferred = (int)stField.GetValue(marker) == preferredSurfaceTypeValue.Value;
                }

                if (isPreferred)
                {
                    // Among preferred-type hits, pick the highest Y (the visible top surface).
                    if (!foundPreferred || h.point.y > bestY)
                    {
                        bestY = h.point.y;
                        foundPreferred = true;
                    }
                }
                else if (!foundSurface && IsPlayableSurface(h.collider, smType))
                {
                    // Nearest playable surface (hits are distance-sorted) — the visible top.
                    surfaceY     = h.point.y;
                    foundSurface = true;
                }

                // Last-resort record of the nearest hit of ANY kind, so a scene with no
                // SurfaceMarkers at all (unit-test rigs, bare prototype scenes) behaves as before.
                if (!foundAny)
                {
                    anyY     = h.point.y;
                    foundAny = true;
                }
            }

            if (foundPreferred) return bestY;
            if (foundSurface)   return surfaceY;
            if (foundAny)       return anyY;
            return defaultY;
        }

        /// <summary>
        /// How far above the terrain heightmap a TerrainCollider hit may sit and still be treated
        /// as the ground surface. The tree colliders this separates out stand metres clear of the
        /// heightmap (measured: 8–28 m on Hole 1), while a genuine surface hit agrees with
        /// <see cref="Terrain.SampleHeight"/> to well under a centimetre — both are the same
        /// heightmap — so the exact value is not delicate.
        /// </summary>
        const float TerrainSurfaceToleranceMeters = 0.5f;

        /// <summary>
        /// True when a hit reported against the TerrainCollider is actually one of the terrain's
        /// TREE colliders rather than the ground itself.
        ///
        /// Unity does not surface terrain tree colliders as their own GameObjects: a ray that hits
        /// a tree on a Terrain comes back with <c>collider == the TerrainCollider</c>, on the
        /// terrain's own GameObject and layer. No type, name, tag or layer test can tell the two
        /// apart. What CAN: the terrain surface at an XZ is by definition the heightmap value
        /// there, so a hit sitting above the heightmap is on a tree, not on the ground.
        ///
        /// Also note tree colliders exist ONLY in Play Mode — an Edit-Mode raycast finds none even
        /// on a hole whose trees are solid at runtime, so this cannot be exercised from Edit Mode.
        /// </summary>
        static bool IsTerrainTreeHit(in RaycastHit hit)
        {
            var terrainCollider = hit.collider as TerrainCollider;
            if (terrainCollider == null) return false;

            var terrain = terrainCollider.GetComponent<Terrain>();
            if (terrain == null) return false;   // no heightmap to compare against — treat as ground

            float surfaceY = terrain.SampleHeight(hit.point) + terrain.transform.position.y;
            return hit.point.y > surfaceY + TerrainSurfaceToleranceMeters;
        }

        /// <summary>
        /// True when a collider belongs to the ball and must never be snapped onto.
        ///
        /// Checks the explicit <paramref name="ballGO"/> first, then falls back to walking the
        /// parent chain for a <see cref="BallAnimator"/>. The chain walk matters because
        /// <c>BallAnimator.Instance</c> is a first-wins singleton (<c>if (Instance == null)</c>),
        /// so a stale or unrelated instance can own it and leave the real ball unexcluded — which
        /// is precisely how the ball ended up winning the cast once hits became distance-sorted.
        /// </summary>
        static bool IsBall(Collider col, GameObject ballGO)
        {
            if (col == null) return false;
            if (ballGO != null && col.transform.IsChildOf(ballGO.transform)) return true;
            return col.GetComponentInParent<BallAnimator>() != null;
        }

        /// <summary>
        /// True when a collider can legitimately be stood on: a zone mesh carrying a
        /// <c>Golfin.Course.SurfaceMarker</c> (Fairway / Green / Tee / Bunker / CartPath / …) or the
        /// terrain heightmap itself.
        ///
        /// Capsule and sphere colliders are rejected outright — every golf surface in this project
        /// is a mesh or the TerrainCollider, while scattered props and STANDALONE (non-terrain)
        /// trees are capsules. Terrain-instanced trees do NOT arrive here as capsules, whatever an
        /// earlier version of this comment claimed — they are reported as the TerrainCollider and
        /// are filtered upstream by <see cref="IsTerrainTreeHit"/>, which is why accepting
        /// TerrainCollider below is safe.
        /// </summary>
        static bool IsPlayableSurface(Collider col, System.Type surfaceMarkerType)
        {
            if (col == null) return false;
            if (col is CapsuleCollider || col is SphereCollider) return false;   // trees / props
            if (col is TerrainCollider) return true;                            // heightmap
            return surfaceMarkerType != null
                && col.GetComponentInParent(surfaceMarkerType) != null;         // zone mesh
        }
    }
}
