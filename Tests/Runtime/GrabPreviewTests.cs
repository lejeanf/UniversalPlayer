using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Grab preview: hovering a grabbable tints its renderers (non-destructively, via
    /// MaterialPropertyBlock) and shows a translucent ghost hand at the attach point
    /// when a PoseContainer is present; everything clears on hover exit and outside
    /// the XR scheme.
    /// </summary>
    public class GrabPreviewTests
    {
        private GameObject _player;
        private GrabPreview _preview;
        private GameObject _cube;
        private XRGrabInteractable _interactable;
        private Renderer _renderer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;

            _player = new GameObject("GrabPreviewPlayer");
            _preview = _player.AddComponent<GrabPreview>();

            _cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cube.transform.position = new Vector3(0f, 1f, 2f);
            _renderer = _cube.GetComponent<Renderer>();
            _interactable = _cube.AddComponent<XRGrabInteractable>(); // auto-creates its Rigidbody + interaction manager
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_cube);
            Object.Destroy(_player);
            var manager = Object.FindFirstObjectByType<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();
            if (manager != null) Object.Destroy(manager.gameObject);
            yield return null;
        }

        private static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — update GrabPreviewTests alongside the refactor.");
            info.SetValue(target, value);
        }

        [UnityTest]
        public IEnumerator Hover_TintsRenderers_AndExitRestoresThem()
        {
            Assert.That(_renderer.HasPropertyBlock(), Is.False, "Test rig sanity: no property block before hover.");

            _preview.ShowPreview(_interactable, HandType.Right);
            Assert.That(_renderer.HasPropertyBlock(), Is.True,
                "Hovering a grabbable did not apply the highlight property block.");

            _preview.HidePreview(_interactable, HandType.Right);
            Assert.That(_renderer.HasPropertyBlock(), Is.False,
                "Leaving hover did not clear the highlight — the object stays tinted forever.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator TwoHandsHovering_HighlightSurvivesUntilBothLeave()
        {
            _preview.ShowPreview(_interactable, HandType.Left);
            _preview.ShowPreview(_interactable, HandType.Right);

            _preview.HidePreview(_interactable, HandType.Left);
            Assert.That(_renderer.HasPropertyBlock(), Is.True,
                "The highlight vanished while the second hand was still hovering (refcount broken).");

            _preview.HidePreview(_interactable, HandType.Right);
            Assert.That(_renderer.HasPropertyBlock(), Is.False, "Highlight not cleared after the last hand left.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoverWithPose_ShowsGhostHand_AtTheAttachPoint()
        {
            // Non-uniform scale like a stretched placeholder: the ghost must NOT inherit it.
            _cube.transform.localScale = new Vector3(4f, 0.2f, 2.5f);

            var container = _cube.AddComponent<PoseContainer>();
            container.pose = ScriptableObject.CreateInstance<Pose>();
            try
            {
                // An empty pose has 0 finger rotations: BaseHand warns loudly and skips
                // the fingers, but the ghost must still appear at the attach point.
                LogAssert.ignoreFailingMessages = true;
                _preview.ShowPreview(_interactable, HandType.Right);
                yield return null;
                LogAssert.ignoreFailingMessages = false;

                // The ghost lives under a unit-scale anchor on the player (never under
                // the grabbable — a scaled grabbable would stretch/shear it) and the
                // anchor follows the interactable's attach transform.
                var ghost = _player.GetComponentInChildren<PreviewHand>(false);
                Assert.That(ghost, Is.Not.Null, "No active ghost PreviewHand under the player's ghost anchor.");
                Assert.That(ghost.gameObject.activeInHierarchy, Is.True, "The ghost hand exists but is not shown.");
                Assert.That(ghost.transform.IsChildOf(_cube.transform), Is.False,
                    "The ghost parented under the grabbable — a scaled grabbable would distort it.");
                var attach = _interactable.attachTransform != null ? _interactable.attachTransform : _cube.transform;
                Assert.That(Vector3.Distance(ghost.transform.parent.position, attach.position), Is.LessThan(1e-3f),
                    "The ghost anchor is not at the grabbable's attach point.");

                _preview.HidePreview(_interactable, HandType.Right);
                Assert.That(ghost.gameObject.activeInHierarchy, Is.False,
                    "The ghost hand stayed visible after hover ended.");
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
                Object.Destroy(container.pose);
            }
        }

        [UnityTest]
        public IEnumerator OutsideXrScheme_NoPreviewAtAll()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            _preview.ShowPreview(_interactable, HandType.Right);

            Assert.That(_renderer.HasPropertyBlock(), Is.False,
                "The grab preview ran outside the XR scheme — it is a VR-only affordance.");
            yield return null;
        }
    }
}
