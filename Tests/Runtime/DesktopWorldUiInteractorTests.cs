using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Verifies the desktop world-canvas click/drag path without a gamepad: a
    /// DesktopWorldUiInteractor aiming the camera ray at a world-space canvas
    /// (TrackedDeviceGraphicRaycaster — the raycaster the module's screen pointer
    /// can never see) must produce real Button clicks, real Slider drags, honor
    /// the drag-vs-click threshold, yield to covering screen-space UI, and cancel
    /// cleanly mid-drag. Input and aim are driven through the component's test
    /// seams (PressProbe / ScreenPointProbe), the same pattern as
    /// BroadcastControlsStatus.HmdMountedProbe.
    /// </summary>
    public class DesktopWorldUiInteractorTests
    {
        private GameObject _eventSystemGo;
        private GameObject _cameraGo;
        private GameObject _canvasGo;
        private GameObject _interactorGo;
        private DesktopWorldUiInteractor _interactor;
        private Vector2 _aimPoint;
        private bool _pressed;

        private static Vector2 ScreenCenter => new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        private class DragRecorder : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
        {
            public int beginDragCount;
            public int dragCount;
            public int endDragCount;
            public int clickCount;
            public void OnBeginDrag(PointerEventData _) => beginDragCount++;
            public void OnDrag(PointerEventData _) => dragCount++;
            public void OnEndDrag(PointerEventData _) => endDragCount++;
            public void OnPointerClick(PointerEventData _) => clickCount++;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _aimPoint = ScreenCenter;
            _pressed = false;

            // fully qualified: inside the jeanf.* namespace, "EventSystem" resolves to
            // the jeanf.EventSystem namespace instead of the UGUI component
            _eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(XRUIInputModule));

            // The interactor aims Camera.main — the tag is what resolves it.
            _cameraGo = new GameObject("TestCamera", typeof(Camera)) { tag = "MainCamera" };

            // world-space canvas with the same raycaster the package's UI prefabs use
            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cameraGo.GetComponent<Camera>();
            var canvasRect = _canvasGo.GetComponent<RectTransform>();
            canvasRect.position = new Vector3(0f, 0f, 2f);
            canvasRect.sizeDelta = new Vector2(400f, 400f);
            canvasRect.localScale = Vector3.one * 0.005f; // 2m x 2m — impossible to miss

            _interactorGo = new GameObject("DesktopWorldUiInteractor");
            _interactorGo.SetActive(false);
            _interactor = _interactorGo.AddComponent<DesktopWorldUiInteractor>();
            _interactor.ForceActiveForTests = true;
            _interactor.FadedProbe = () => false; // no FadeMask in the test scene: the static defaults to faded
            _interactor.ScreenPointProbe = () => _aimPoint;
            _interactor.PressProbe = () => _pressed;
            _interactorGo.SetActive(true);

            // a couple frames so the canvas lays out and the raycaster registers
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_interactorGo);
            Object.Destroy(_canvasGo);
            Object.Destroy(_eventSystemGo);
            Object.Destroy(_cameraGo);
            yield return null;
        }

        private GameObject AddFullCanvasChild(string name, params System.Type[] components)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            foreach (var component in components) go.AddComponent(component);
            go.transform.SetParent(_canvasGo.transform, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return go;
        }

        [UnityTest]
        public IEnumerator PressOverWorldButton_FiresOnClick()
        {
            var clickCount = 0;
            var button = AddFullCanvasChild("Button", typeof(Button)).GetComponent<Button>();
            button.onClick.AddListener(() => clickCount++);
            yield return null;

            _pressed = true;
            yield return null;
            yield return null;
            _pressed = false;
            yield return null;

            Assert.That(_interactor.CurrentHoverTarget, Is.Not.Null,
                "The interactor never hovered the world canvas — its TrackedDeviceEventData raycast is not " +
                "reaching the TrackedDeviceGraphicRaycaster (check rayPoints/eventCamera).");
            Assert.That(clickCount, Is.EqualTo(1),
                "A press+release over the world-space button produced no Button.onClick — the synthesized " +
                "pointerDown/Up/Click pipeline is broken.");
        }

        [UnityTest]
        public IEnumerator DragOverWorldSlider_ChangesValue()
        {
            var sliderGo = AddFullCanvasChild("Slider", typeof(Slider));
            var slider = sliderGo.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            // Slider.UpdateDrag needs a handle container to compute the value from.
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var areaRect = handleArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
            slider.handleRect = handle.GetComponent<RectTransform>();
            yield return null;

            _pressed = true;
            yield return null;
            var valueAtPress = slider.value;

            // sweep the aim to the right, well past the drag threshold
            for (var i = 1; i <= 10; i++)
            {
                _aimPoint = ScreenCenter + new Vector2(15f * i, 0f);
                yield return null;
            }
            _pressed = false;
            yield return null;

            Assert.That(slider.value, Is.GreaterThan(valueAtPress),
                $"Dragging right across the slider did not raise its value (at press: {valueAtPress:F3}, " +
                $"after drag: {slider.value:F3}) — the beginDrag/drag loop or pressEventCamera math is broken.");
        }

        [UnityTest]
        public IEnumerator DragPastThreshold_SuppressesClick()
        {
            var clickCount = 0;
            // the recorder is the drag surface; the button is the click target on top
            var surface = AddFullCanvasChild("DragSurface", typeof(DragRecorder));
            var recorder = surface.GetComponent<DragRecorder>();
            var buttonGo = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(surface.transform, false);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            buttonGo.GetComponent<Button>().onClick.AddListener(() => clickCount++);
            yield return null;

            _pressed = true;
            yield return null;
            for (var i = 1; i <= 10; i++)
            {
                _aimPoint = ScreenCenter + new Vector2(15f * i, 0f);
                yield return null;
            }
            _pressed = false;
            yield return null;

            Assert.That(recorder.beginDragCount, Is.EqualTo(1), "The 150px move never began a drag on the parent drag surface.");
            Assert.That(recorder.endDragCount, Is.EqualTo(1), "The release never ended the drag.");
            Assert.That(clickCount, Is.Zero,
                "The button clicked even though the press moved 150px — a drag must suppress the click.");
        }

        [UnityTest]
        public IEnumerator ScreenSpaceCanvasCovering_Yields()
        {
            var clickCount = 0;
            var button = AddFullCanvasChild("Button", typeof(Button)).GetComponent<Button>();
            button.onClick.AddListener(() => clickCount++);

            // screen-space UI covering the aim point — the module pipeline owns this press
            var overlayGo = new GameObject("Overlay", typeof(Canvas), typeof(GraphicRaycaster));
            overlayGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var blocker = new GameObject("Blocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            blocker.transform.SetParent(overlayGo.transform, false);
            var blockerRect = blocker.GetComponent<RectTransform>();
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            yield return null;
            yield return null;

            _pressed = true;
            yield return null;
            yield return null;
            _pressed = false;
            yield return null;

            Assert.That(_interactor.HasUiHover, Is.False,
                "The interactor still reports world-UI hover under covering screen-space UI — the yield rule is broken.");
            Assert.That(clickCount, Is.Zero,
                "The world button clicked through covering screen-space UI — that press belongs to the module pipeline.");

            Object.Destroy(overlayGo);
        }

        [UnityTest]
        public IEnumerator LockedMode_DualRaycasterCanvas_Clicks()
        {
            // The common project setup: a world canvas carrying BOTH raycasters
            // (plain GraphicRaycaster added historically, worldCamera left null).
            // With the cursor locked, this component owns it — click must land.
            _canvasGo.AddComponent<GraphicRaycaster>();
            var clickCount = 0;
            var button = AddFullCanvasChild("Button", typeof(Button)).GetComponent<Button>();
            button.onClick.AddListener(() => clickCount++);
            _interactor.LockedProbe = () => true;
            yield return null;

            _pressed = true;
            yield return null;
            yield return null;
            _pressed = false;
            yield return null;

            Assert.That(clickCount, Is.EqualTo(1),
                "With the cursor locked, a press on a dual-raycaster world canvas produced no click — " +
                "the locked-mode ownership rule is broken (the module pointer cannot drag there; we must own it).");
        }

        [UnityTest]
        public IEnumerator FreeMode_DualRaycasterCanvas_Yields()
        {
            // Free cursor: the module's moving pointer owns every canvas a plain
            // GraphicRaycaster can reach — we must stay idle on this one.
            _canvasGo.AddComponent<GraphicRaycaster>();
            var clickCount = 0;
            var button = AddFullCanvasChild("Button", typeof(Button)).GetComponent<Button>();
            button.onClick.AddListener(() => clickCount++);
            _interactor.LockedProbe = () => false;
            yield return null;
            yield return null;

            _pressed = true;
            yield return null;
            yield return null;
            _pressed = false;
            yield return null;

            Assert.That(_interactor.HasUiHover, Is.False,
                "Free-cursor mode still hovers a dual-raycaster canvas — that canvas belongs to the module pointer.");
            Assert.That(clickCount, Is.Zero,
                "Free-cursor mode clicked a dual-raycaster canvas — the module pointer owns it there (double-fire risk).");
        }

        [UnityTest]
        public IEnumerator StickNudge_DragsSlider()
        {
            var sliderGo = AddFullCanvasChild("Slider", typeof(Slider));
            var slider = sliderGo.GetComponent<Slider>();
            slider.transition = Selectable.Transition.None;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            var handleArea = new GameObject("HandleArea", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            var areaRect = handleArea.GetComponent<RectTransform>();
            areaRect.anchorMin = Vector2.zero;
            areaRect.anchorMax = Vector2.one;
            areaRect.offsetMin = Vector2.zero;
            areaRect.offsetMax = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            handle.transform.SetParent(handleArea.transform, false);
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(20f, 20f);
            slider.handleRect = handle.GetComponent<RectTransform>();

            // Locked cursor, aim NEVER moves — only the left-stick nudge drags.
            _interactor.LockedProbe = () => true;
            var stick = Vector2.zero;
            _interactor.DragStickProbe = () => stick;
            yield return null;

            _pressed = true;
            yield return null;
            var valueAtPress = slider.value;
            stick = new Vector2(5f, 0f); // exaggerated tilt so tiny batch-mode frame times still move pixels
            for (var i = 0; i < 30; i++) yield return null;
            stick = Vector2.zero;
            _pressed = false;
            yield return null;

            Assert.That(slider.value, Is.GreaterThan(valueAtPress),
                $"Holding the press and tilting the stick did not move the slider (at press: {valueAtPress:F3}, " +
                $"after: {slider.value:F3}) — the stick pointer nudge is broken.");
        }

        [UnityTest]
        public IEnumerator DisableMidDrag_CancelsCleanly()
        {
            var clickCount = 0;
            var surface = AddFullCanvasChild("DragSurface", typeof(DragRecorder));
            var recorder = surface.GetComponent<DragRecorder>();
            surface.AddComponent<Button>().onClick.AddListener(() => clickCount++);
            yield return null;

            _pressed = true;
            yield return null;
            for (var i = 1; i <= 10; i++)
            {
                _aimPoint = ScreenCenter + new Vector2(15f * i, 0f);
                yield return null;
            }
            Assert.That(recorder.beginDragCount, Is.EqualTo(1), "Precondition failed: the sweep never started a drag.");

            // scheme switch / component teardown mid-drag runs the same cancel path
            _interactor.enabled = false;
            yield return null;

            Assert.That(recorder.endDragCount, Is.EqualTo(1), "Cancel mid-drag never ended the drag — UI would be stuck dragging.");
            Assert.That(clickCount, Is.Zero, "Cancel mid-drag fired a click — a canceled press must not click.");
            Assert.That(_interactor.HasUiHover, Is.False, "Cancel left a stale hover target behind.");
        }
    }
}
