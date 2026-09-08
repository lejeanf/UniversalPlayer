using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// While the XR display keeps running, the engine treats every game camera as a
    /// stereo camera: its target is the eye texture and its viewport the eye size, so a
    /// flat HDRP render lands stretched in the Game view with a strip of the other eye.
    /// The presenter takes the camera off the eye texture (a window-sized render
    /// texture is never XR) and shows it through a screen-space overlay, which the
    /// engine draws on the window itself.
    /// </summary>
    public class FlatViewPresenterTests
    {
        private GameObject _root;
        private Camera _camera;
        private FlatViewPresenter _presenter;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("FlatViewPresenterTest");
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.transform.SetParent(_root.transform);
            _camera.enabled = false;
            _presenter = _root.AddComponent<FlatViewPresenter>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        [Test]
        public void Begin_RendersTheCameraIntoAWindowSizedTexture()
        {
            _presenter.Begin(_camera);

            Assert.That(_presenter.IsPresenting, Is.True);
            Assert.That(_camera.targetTexture, Is.SameAs(_presenter.Target),
                "The flat camera must render off the XR eye texture, into the presenter's own target.");
            Assert.That(new Vector2Int(_presenter.Target.width, _presenter.Target.height), Is.EqualTo(FlatViewPresenter.WindowSize),
                "The target must be the window size so the flat picture keeps the window's aspect.");
        }

        [Test]
        public void Begin_ShowsTheTextureThroughAnOverlayThatNeverBlocksUi()
        {
            _presenter.Begin(_camera);

            var canvas = _root.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas, Is.Not.Null);
            Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay),
                "Only a screen-space overlay is drawn on the window by the engine while an XR display runs.");
            Assert.That(canvas.sortingOrder, Is.EqualTo(FlatViewPresenter.CanvasSortingOrder),
                "The presenter must sit below every project canvas and the UI Toolkit HUD.");
            var image = canvas.GetComponentInChildren<RawImage>(true);
            Assert.That(image, Is.Not.Null);
            Assert.That(image.texture, Is.SameAs(_presenter.Target));
            Assert.That(image.raycastTarget, Is.False, "A full-window raycast target would swallow every desktop UI click.");
            var rect = image.rectTransform;
            Assert.That(rect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rect.anchorMax, Is.EqualTo(Vector2.one));
        }

        [Test]
        public void End_HandsTheCameraBackToTheScreen()
        {
            _presenter.Begin(_camera);
            _presenter.End();

            Assert.That(_presenter.IsPresenting, Is.False);
            Assert.That(_camera.targetTexture, Is.Null, "Entering VR must put the camera back on the XR eye texture.");
            var canvas = _root.GetComponentInChildren<Canvas>(true);
            Assert.That(canvas == null || !canvas.enabled, Is.True, "The overlay must not cover the mirror while in VR.");
        }

        [Test]
        public void Begin_Twice_KeepsOneTargetAndOneCanvas()
        {
            _presenter.Begin(_camera);
            var first = _presenter.Target;
            _presenter.Begin(_camera);

            Assert.That(_presenter.Target, Is.SameAs(first));
            Assert.That(_root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
        }
    }
}
