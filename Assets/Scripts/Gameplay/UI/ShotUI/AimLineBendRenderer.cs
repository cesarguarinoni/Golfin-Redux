using UnityEngine;
using UnityEngine.UI;

namespace Golfin.Gameplay.UI.ShotUI
{
    /// <summary>
    /// Single-mesh renderer for the in-game aim direction line (Order 355).
    ///
    /// Implemented as ONE <see cref="MaskableGraphic"/> that builds a single textured
    /// triangle-strip ("ribbon") following the parametric aim curve via
    /// <see cref="OnPopulateMesh"/>. This replaces the earlier segmented-Image approach
    /// (N child Image GameObjects), which produced disconnected, rung-like dashes because
    /// the <c>imgLine1</c> sprite (14×500, with transparent vertical bleed) was stretched
    /// into each short rect and gapped between segments. A single mesh has no gaps, one
    /// draw call, and reads as a continuous curve.
    ///
    /// The ribbon is textured with the same <c>imgLine1</c> sprite (Figma 2714:3536,
    /// "Indicator - Direction"), UV-stretched once along the curve length and across the
    /// 3px width, so the look is identical to the original single-rect line (D6).
    ///
    /// Curve model (unchanged): lateralOffsetPx(t) = signedFinetune · k · t² · reach,
    /// t ∈ [0,1], k = AimLineCurveScale, clamped so the tip never exceeds MaxLateralClampPx.
    ///
    /// Sign convention (D5 / 356): fadeDrawInput −1 = DRAW (handle LEFT) → negative local X.
    /// fadeDrawInput +1 = FADE (handle RIGHT) → positive local X. (Screen direction after
    /// the AimAngleDeg rotation + camera projection is hole/camera dependent.)
    /// </summary>
    [AddComponentMenu("Golfin/UI/Aim Line Bend Renderer")]
    [RequireComponent(typeof(CanvasRenderer))]
    [RequireComponent(typeof(RectTransform))]
    public class AimLineBendRenderer : MaskableGraphic
    {
        // ── Inspector ──────────────────────────────────────────────────────────
        [Header("Curve resolution")]
        [Tooltip("Number of sampled points along the curve (16–32 is smooth).")]
        [SerializeField] private int _sampleCount = 24;

        [Header("Line look (D6 — preserve original)")]
        [Tooltip("Line width in canvas px. Matches the original TargetingLine sizeDelta.x = 3.")]
        [SerializeField] private float _lineWidthPx = 3f;

        [Tooltip("The imgLine1 sprite (Indicator - Direction). Pulled from the TargetingLine Image if null.")]
        [SerializeField] private Sprite _lineSprite;

        // ── Public API (unchanged surface — ShotConeView feeds these) ────────────

        /// <summary>True while FadeDraw mode is armed and the line should bend.</summary>
        public bool  FadeDrawArmed  { get; set; }

        /// <summary>ConeFinetuneX: −1 = full draw (handle LEFT), +1 = full fade (handle RIGHT). Sign per D5.</summary>
        public float FinetuneX      { get; set; }

        /// <summary>Line rotation in canvas degrees (Atan2 of aim direction, −90° offset).</summary>
        public float AimAngleDeg    { get; set; }

        /// <summary>Total reach of the line in canvas px. Scales with power; default at rest.</summary>
        public float ReachPx        { get; set; } = 500f;

        /// <summary>Curvature gain k (ControlsConfig.AimLineCurveScale).</summary>
        public float CurveScale     { get; set; } = 0.35f;

        /// <summary>Maximum absolute lateral deviation of the bent tip in canvas px (Phase D).</summary>
        public float MaxLateralClampPx { get; set; } = 350f;

        // ── Texture binding ──────────────────────────────────────────────────────

        public override Texture mainTexture
            => (_lineSprite != null && _lineSprite.texture != null) ? _lineSprite.texture : base.mainTexture;

        // ── Curve math (pure — used by mesh build AND EditMode tests) ─────────────

        private float SignedFinetune => FadeDrawArmed ? FinetuneX : 0f;

        /// <summary>
        /// Lateral offset (local X) at curve param t∈[0,1], including the Phase-D clamp.
        /// Pure function of the public properties — safe to call without a Canvas.
        /// </summary>
        public float LateralAtT(float t)
        {
            float reach      = Mathf.Max(1f, ReachPx);
            float tipRaw     = SignedFinetune * CurveScale * reach;          // at t=1, t²=1
            float tipClamped = Mathf.Clamp(tipRaw, -MaxLateralClampPx, MaxLateralClampPx);
            float effK       = (Mathf.Abs(tipRaw) > 1e-4f)
                ? CurveScale * (tipClamped / tipRaw)
                : CurveScale;
            return SignedFinetune * effK * (t * t) * reach;
        }

        /// <summary>Lateral offset of the bent tip (t=1), clamped. Used by tests.</summary>
        public float TipLateralX => LateralAtT(1f);

        private Vector2 SamplePoint(float t, float reach) => new Vector2(LateralAtT(t), t * reach);

        // ── Per-frame refresh ────────────────────────────────────────────────────

        /// <summary>
        /// Apply current parameters: rotate the line to the aim angle and rebuild the mesh.
        /// Cheap — no allocation beyond the VertexHelper buffers Unity pools.
        /// </summary>
        public void Refresh()
        {
            if (rectTransform != null)
                rectTransform.localRotation = Quaternion.Euler(0f, 0f, AimAngleDeg);
            SetVerticesDirty();
        }

        // ── Mesh build ───────────────────────────────────────────────────────────

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            int   N     = Mathf.Max(2, _sampleCount);
            float reach = Mathf.Max(1f, ReachPx);
            float halfW = Mathf.Max(0.5f, _lineWidthPx) * 0.5f;

            // Sprite UV rect (handles atlased and standalone sprites).
            Vector4 uv = (_lineSprite != null)
                ? UnityEngine.Sprites.DataUtility.GetOuterUV(_lineSprite)
                : new Vector4(0f, 0f, 1f, 1f);
            float uMin = uv.x, vMin = uv.y, uMax = uv.z, vMax = uv.w;

            Color32 col = color;

            // Emit a left/right vertex pair at each of N+1 samples.
            for (int i = 0; i <= N; i++)
            {
                float t = (float)i / N;
                Vector2 p = SamplePoint(t, reach);

                // Tangent via central difference (forward/backward at the ends).
                Vector2 tan;
                if      (i == 0) tan = SamplePoint(1f / N, reach)            - p;
                else if (i == N) tan = p - SamplePoint((float)(N - 1) / N, reach);
                else             tan = SamplePoint((float)(i + 1) / N, reach) - SamplePoint((float)(i - 1) / N, reach);
                if (tan.sqrMagnitude < 1e-6f) tan = Vector2.up;
                tan.Normalize();

                Vector2 nrm   = new Vector2(tan.y, -tan.x);   // perpendicular (line thickness axis)
                Vector2 left  = p - nrm * halfW;
                Vector2 right = p + nrm * halfW;
                float   vC    = Mathf.Lerp(vMin, vMax, t);    // sprite stretched once along length

                var vL = UIVertex.simpleVert; vL.color = col; vL.position = left;  vL.uv0 = new Vector2(uMin, vC);
                var vR = UIVertex.simpleVert; vR.color = col; vR.position = right; vR.uv0 = new Vector2(uMax, vC);
                vh.AddVert(vL);
                vh.AddVert(vR);
            }

            for (int i = 0; i < N; i++)
            {
                int b = i * 2;
                vh.AddTriangle(b,     b + 1, b + 2);
                vh.AddTriangle(b + 2, b + 1, b + 3);
            }
        }

        // ── Bootstrap / visibility ───────────────────────────────────────────────

        /// <summary>Copy sprite + colour from the existing single-rect TargetingLine Image (D6 look).</summary>
        public void SetSpriteFrom(Image sourceImage)
        {
            if (sourceImage == null) return;
            if (sourceImage.sprite != null) _lineSprite = sourceImage.sprite;
            color = sourceImage.color;
            SetMaterialDirty();
            SetVerticesDirty();
        }

        public void SetVisible(bool visible) => enabled = visible;
    }
}
