using NUnit.Framework;
using UnityEngine;
using Golfin.Gameplay.Config;
using Golfin.Gameplay.UI.ShotUI;

namespace Golfin.Gameplay.Tests
{
    /// <summary>
    /// flick_shot_view §3.5 — the cone's geometry comes from <c>controls.csv</c>, not from four
    /// serialized numbers that have to agree.
    ///
    /// <para>WHY THIS FIXTURE EXISTS. <c>ShotLayoutMathTests</c> pins where the BALL goes; nothing
    /// pinned that the cone actually drawn under it is the same size the clamp assumed. Those were
    /// two independent numbers before this task — <c>ShotConeView._coneHeightPx</c>,
    /// <c>ConeMeshGraphic._heightPx</c>, <c>TimingSlabGraphic._coneHeightPx</c> and the mesh's
    /// scene-authored <c>-1160</c> — which is exactly why Flick could not be re-framed with the
    /// other three schemes. This fixture is the seam that keeps them one number.</para>
    /// </summary>
    [TestFixture]
    public class ShotConeViewConfigTests
    {
        private GameObject      _root;
        private ShotConeView    _view;
        private ConeMeshGraphic _cone;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("ShotConeViewConfigTest_Root", typeof(RectTransform));

            // A stand-in for BallSpace: the cone hangs BALL-relative, so the mesh's parent has to
            // be a rect for anchoredPosition to mean what production means by it.
            var ballSpace = new GameObject("BallSpace", typeof(RectTransform));
            ballSpace.transform.SetParent(_root.transform, false);

            var coneGO = new GameObject("ConeMesh", typeof(RectTransform));
            coneGO.transform.SetParent(ballSpace.transform, false);
            _cone = coneGO.AddComponent<ConeMeshGraphic>();

            var viewGO = new GameObject("ShotConeView", typeof(RectTransform));
            viewGO.transform.SetParent(_root.transform, false);
            _view = viewGO.AddComponent<ShotConeView>();
            _view.InjectForTests(null, _cone);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        /// <summary>The shipping numbers, read back off the view the same way the acceptance run
        /// reads them.</summary>
        [Test]
        public void WithTheShippingConfig_TheCone_HandleRest_AndPutterTrack_AreTheCsvNumbers()
        {
            _view.InjectControlsConfig(ControlsConfig.Default);
            _view.ApplyConfiguredGeometry();

            Assert.AreEqual(792f, _view.ConeHeightPx, 1e-3f, "FlickConeHeightPx");
            Assert.AreEqual(648f, _view.HandleRestYPx, 1f,   "flick_pull_mapping D2: 0.8182 x 792 = FlickPull120Px, so the BASE is 120%");
            Assert.AreEqual(792f, _view.PutterTrackHeightPx, 1e-3f, "FlickPutterTrackHeightPx");
        }

        [Test]
        public void TheConeGraphicAndTheMeshPosition_FollowTheSameKey_NotTheirOwnSerializedCopies()
        {
            var cfg = ControlsConfig.Default;
            _view.InjectControlsConfig(cfg);
            _view.ApplyConfiguredGeometry();

            // Awake pushes the height onto the graphic; ApplyConfiguredGeometry places the mesh.
            // Test the placement here and the height explicitly, since Awake does not run in
            // EditMode for a component added to a live GameObject.
            _cone.HeightPx = _view.ConeHeightPx;

            Assert.AreEqual(792f, _cone.HeightPx, 1e-3f);
            Assert.AreEqual(-(cfg.FlickConeApexGapPx + cfg.FlickConeHeightPx),
                            _cone.rectTransform.anchoredPosition.y, 1e-3f,
                            "the mesh hangs apex-gap + height below the ball, so its base is the " +
                            "shared bottom baseline (D2)");
            Assert.AreEqual(-792f, _cone.rectTransform.anchoredPosition.y, 0.5f);
            Assert.AreEqual(0f, cfg.FlickConeApexGapPx, 1e-4f,
                            "and with a zero gap the apex lands ON the ball, not 151 below it");
        }

        /// <summary>The same arithmetic the D6 clamp runs, off the same two keys — if these ever
        /// diverge the guard is guarding a cone that is not the one being drawn.</summary>
        [Test]
        public void TheMeshPosition_IsExactlyTheDepthTheBallClampGuards()
        {
            var cfg = ControlsConfig.Default;
            _view.InjectControlsConfig(cfg);
            _view.ApplyConfiguredGeometry();

            float clampDepth = ShotLayoutMath.FlickLaneDepthBelowBall(cfg.FlickConeApexGapPx,
                                                                      cfg.FlickConeHeightPx);
            Assert.AreEqual(-clampDepth, _cone.rectTransform.anchoredPosition.y, 1e-3f);
        }

        /// <summary>A re-cut cone moves the handle rest with it, because the key is a fraction.
        /// An absolute rest would change what merely touching the club registers as, since
        /// <c>ClubHandleDragger</c> reads power off the whole cone height.</summary>
        [Test]
        public void ARecutCone_MovesTheHandleRestWithIt_BecauseTheKeyIsAFraction()
        {
            var cfg = ControlsConfig.Default;
            cfg.FlickConeHeightPx = 800f;
            _view.InjectControlsConfig(cfg);
            _view.ApplyConfiguredGeometry();

            Assert.AreEqual(800f, _view.ConeHeightPx, 1e-3f);
            Assert.AreEqual(0.8182f * 800f, _view.HandleRestYPx, 1e-3f);
            Assert.AreEqual(_view.HandleRestYPx / _view.ConeHeightPx,
                            648f / 792f, 1e-3f,
                            "the fraction is what survives a re-cut, not the pixel count — which " +
                            "is also why a re-cut cone LOSES both the 540px Pendulum parity AND " +
                            "the base-is-120% identity, and has to have this key re-derived as " +
                            "FlickPull120Px / newHeight");
        }

        /// <summary>Applying twice must not drift — Awake, a scheme re-apply and the putter track's
        /// re-assert all reach this method.</summary>
        [Test]
        public void ApplyingTwice_IsIdempotent()
        {
            _view.InjectControlsConfig(ControlsConfig.Default);
            _view.ApplyConfiguredGeometry();
            float h = _view.ConeHeightPx, rest = _view.HandleRestYPx, track = _view.PutterTrackHeightPx;
            float meshY = _cone.rectTransform.anchoredPosition.y;

            _view.ApplyConfiguredGeometry();

            Assert.AreEqual(h,     _view.ConeHeightPx,        1e-4f);
            Assert.AreEqual(rest,  _view.HandleRestYPx,       1e-4f);
            Assert.AreEqual(track, _view.PutterTrackHeightPx, 1e-4f);
            Assert.AreEqual(meshY, _cone.rectTransform.anchoredPosition.y, 1e-4f);
        }

        /// <summary>An Editor scene opened with no config keeps every serialized fallback — which
        /// is the only reason those fields still exist.</summary>
        [Test]
        public void WithNoConfig_TheSerializedInspectorFallbacksStand()
        {
            _view.InjectControlsConfig(default(ControlsConfig));   // every key zero
            _view.ApplyConfiguredGeometry();

            Assert.AreEqual(792f, _view.ConeHeightPx,        1e-3f, "the authored fallback");
            Assert.AreEqual(540f, _view.HandleRestYPx,       1e-3f);
            Assert.AreEqual(792f, _view.PutterTrackHeightPx, 1e-3f);

            // The fallbacks are kept in step with the csv on purpose, so the two paths agree; what
            // the zeroed config must NOT do is collapse the cone to nothing.
            Assert.Greater(_view.ConeHeightPx, 0f);
            Assert.AreEqual(-792f, _cone.rectTransform.anchoredPosition.y, 0.5f,
                            "the zero apex gap falls back too, so the mesh is still placed");
        }
    }
}
