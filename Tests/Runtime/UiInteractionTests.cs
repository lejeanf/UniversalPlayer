using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Verifies VR UI interaction without a headset: an XRRayInteractor pointing at a
    /// world-space canvas (TrackedDeviceGraphicRaycaster) must hit UI, and a manually
    /// queued press (XRI's ManualValue input mode — same trick the Hands Test Bench
    /// uses) must produce a real Button click through XRUIInputModule.
    /// </summary>
    public class UiInteractionTests
    {
        private GameObject _eventSystemGo;
        private GameObject _managerGo;
        private GameObject _interactorGo;
        private GameObject _canvasGo;
        private GameObject _cameraGo;
        private XRRayInteractor _rayInteractor;
        private Button _button;
        private int _clickCount;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _clickCount = 0;

            // UI event pipeline: EventSystem + XRI's input module (created before the
            // interactor so the interactor can register with it on enable)
            // fully qualified: inside the jeanf.* namespace, "EventSystem" resolves to the
            // jeanf.EventSystem namespace instead of the UGUI component
            _eventSystemGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(XRUIInputModule));
            _managerGo = new GameObject("XRInteractionManager", typeof(XRInteractionManager));
            _cameraGo = new GameObject("TestCamera", typeof(Camera));

            // world-space canvas with the same raycaster the package's UI prefabs use
            _canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(TrackedDeviceGraphicRaycaster));
            var canvas = _canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = _cameraGo.GetComponent<Camera>();
            var canvasRect = _canvasGo.GetComponent<RectTransform>();
            canvasRect.position = new Vector3(0f, 0f, 2f);
            canvasRect.sizeDelta = new Vector2(400f, 400f);
            canvasRect.localScale = Vector3.one * 0.005f; // 2m x 2m — impossible to miss

            var buttonGo = new GameObject("Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonGo.transform.SetParent(_canvasGo.transform, false);
            var buttonRect = buttonGo.GetComponent<RectTransform>();
            buttonRect.anchorMin = Vector2.zero;
            buttonRect.anchorMax = Vector2.one;
            buttonRect.offsetMin = Vector2.zero;
            buttonRect.offsetMax = Vector2.zero;
            _button = buttonGo.GetComponent<Button>();
            _button.onClick.AddListener(() => _clickCount++);

            // the ray: at origin, looking straight at the canvas
            _interactorGo = new GameObject("Ray Interactor");
            _interactorGo.SetActive(false);
            _rayInteractor = _interactorGo.AddComponent<XRRayInteractor>();
            _rayInteractor.enableUIInteraction = true;
            _interactorGo.SetActive(true);
            _rayInteractor.uiPressInput.inputSourceMode = XRInputButtonReader.InputSourceMode.ManualValue;

            // a few frames so the interactor registers with the UI input module
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_interactorGo);
            Object.Destroy(_canvasGo);
            Object.Destroy(_eventSystemGo);
            Object.Destroy(_managerGo);
            Object.Destroy(_cameraGo);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RayInteractor_PointedAtCanvas_HitsUi()
        {
            yield return null;

            var hit = _rayInteractor.TryGetCurrentUIRaycastResult(out var result);
            Assert.That(hit, Is.True,
                "The ray interactor gets no UI raycast result while aimed at a canvas. Checklist: " +
                "TrackedDeviceGraphicRaycaster on the canvas, enableUIInteraction on the interactor, " +
                "XRUIInputModule on the EventSystem (a plain InputSystemUIInputModule ignores XR rays).");
            Assert.That(result.gameObject.transform.IsChildOf(_canvasGo.transform), Is.True,
                $"The UI raycast hit '{result.gameObject.name}', not the test canvas.");
        }

        [UnityTest]
        public IEnumerator QueuedManualPress_ClicksTheButton()
        {
            yield return null;
            Assert.That(_rayInteractor.TryGetCurrentUIRaycastResult(out _), Is.True,
                "Precondition failed: ray is not hitting the UI (see RayInteractor_PointedAtCanvas_HitsUi).");

            _rayInteractor.uiPressInput.QueueManualState(true, 1f, true, false);
            yield return null;
            yield return null;
            _rayInteractor.uiPressInput.QueueManualState(false, 0f, false, true);
            yield return null;
            yield return null;

            Assert.That(_clickCount, Is.EqualTo(1),
                "A press+release over the button produced no Button.onClick. The XRUIInputModule " +
                "press pipeline is broken — check uiPressInput wiring on the ray interactor and that " +
                "the Button is raycast-targetable (Image.raycastTarget).");
        }
    }
}
