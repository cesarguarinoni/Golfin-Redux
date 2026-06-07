using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Golfin.UI
{
    /// <summary>
    /// Pool-able per-tap feedback instance.
    /// Attached to the TapFeedbackFX prefab root (UIParticle + ParticleSystem child).
    ///
    /// On Play():
    ///   1. Positions self at the given local canvas point.
    ///   2. Tweens the ring Image: scale from startPx→endPx, alpha 0→peak→0, ease-out.
    ///   3. Reconfigures and plays the child ParticleSystem (sparkles).
    ///   4. Self-deactivates after max(ringDuration, sparkleLifetime).
    ///
    /// All tuning comes from a <see cref="TapFeedbackConfig"/> passed by the controller, so the
    /// effect is editable in the Inspector via the config asset.
    ///
    /// raycastTarget on all graphics is false — effect is cosmetic, never intercepts input.
    /// </summary>
    [DisallowMultipleComponent]
    public class TapFeedbackFX : MonoBehaviour
    {
        [Header("References (auto-wired by TapFeedbackFXBuilder)")]
        [SerializeField] private RectTransform  _ring;         // Image child: ring/glow
        [SerializeField] private Image          _ringImage;    // Image component on ring
        [SerializeField] private ParticleSystem _sparkles;     // ParticleSystem child

        // UIParticle.scale is set to 10 on the prefab (autoScalingMode=Transform).
        // ParticleSystem values are in PS-units; on-screen px = PS-units × uiParticleScale.
        // To hit canvas-pixel targets we divide by the scale factor.
        private const float UiParticleScale = 10f;

        // ── Pool API ──────────────────────────────────────────────────────────────

        /// <summary>Called by TapFeedbackController to play the effect at a canvas local point.</summary>
        public void Play(Vector2 localPos, TapFeedbackConfig cfg)
        {
            // Position
            var rt = (RectTransform)transform;
            rt.anchoredPosition = localPos;

            // Stop any prior coroutines + reset particle system
            StopAllCoroutines();
            if (_sparkles != null) { _sparkles.Clear(); _sparkles.Stop(); }

            gameObject.SetActive(true);

            // Reconfigure sparkles at play-time
            if (_sparkles != null)
                ConfigureSparkles(cfg);

            // Tween ring + play sparkles
            StartCoroutine(DoPlay(cfg));
        }

        // ── Internal ──────────────────────────────────────────────────────────────

        private void ConfigureSparkles(TapFeedbackConfig cfg)
        {
            var main                 = _sparkles.main;
            main.startLifetime       = cfg.sparkleLifetime;
            // speed/size are in canvas-px (units). Divide by UIParticle.scale to get PS-units.
            main.startSpeed          = cfg.sparkleSpeed / UiParticleScale;
            // Explicitly set startSize so it is not left at the prefab's baked 0.06 (sub-pixel).
            main.startSize           = cfg.sparkleSizePx / UiParticleScale;
            main.startColor          = new ParticleSystem.MinMaxGradient(
                new Color(cfg.sparkleTint.r, cfg.sparkleTint.g, cfg.sparkleTint.b, cfg.sparklePeakAlpha));
            main.loop                = false;
            main.playOnAwake         = false;
            main.simulationSpace     = ParticleSystemSimulationSpace.Local;
            // Raise the cap so the density knob isn't silently clamped by the prefab's maxParticles.
            main.maxParticles        = Mathf.Max(8, cfg.sparkleCount);

            var emission = _sparkles.emission;
            emission.SetBurst(0, new ParticleSystem.Burst(0f, (short)cfg.sparkleCount));
            emission.rateOverTime = 0;

            // Spread radius: where sparkles START relative to the tap point (0 = exact point).
            var shape = _sparkles.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius    = Mathf.Max(0f, cfg.sparkleSpreadPx / UiParticleScale);

            _sparkles.Play();
        }

        private IEnumerator DoPlay(TapFeedbackConfig cfg)
        {
            float dur    = cfg.ringDuration;
            float maxDur = Mathf.Max(dur, cfg.sparkleLifetime + 0.05f);

            if (_ring != null && _ringImage != null)
            {
                float startPx = cfg.ringStartPx;
                float endPx   = cfg.ringEndPx;
                float peak    = cfg.ringPeakAlpha;
                Color rgb     = cfg.ringColor;

                // Ring tween: scale + alpha, ease-out
                float t = 0f;
                _ringImage.color = new Color(rgb.r, rgb.g, rgb.b, 0f);
                _ring.sizeDelta  = new Vector2(startPx, startPx);
                _ringImage.raycastTarget = false;

                while (t < dur)
                {
                    t += Time.unscaledDeltaTime;
                    float frac   = Mathf.Clamp01(t / dur);
                    float eased  = 1f - (1f - frac) * (1f - frac); // ease-out quad
                    float size   = Mathf.Lerp(startPx, endPx, eased);
                    float alpha  = peak * Mathf.Sin(frac * Mathf.PI); // ramp up then down
                    _ring.sizeDelta = new Vector2(size, size);
                    _ringImage.color = new Color(rgb.r, rgb.g, rgb.b, alpha);
                    yield return null;
                }
                _ringImage.color = new Color(rgb.r, rgb.g, rgb.b, 0f);
            }
            else
            {
                yield return new WaitForSecondsRealtime(maxDur);
            }

            // Wait for sparkles to finish
            float waitLeft = cfg.sparkleLifetime - dur;
            if (waitLeft > 0f) yield return new WaitForSecondsRealtime(waitLeft + 0.05f);

            gameObject.SetActive(false);
        }
    }
}
