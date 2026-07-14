using System;
using System.Collections;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Covers the PickableObject redesign: an item declares WHERE it docks while held
    /// (camera / right / left hand) and WHERE it goes when released — with no scene
    /// references anywhere, so additive loading cannot break it.
    ///
    /// PlayerItemAnchors is exercised directly: TakeObject's take/release is driven by an
    /// input action plus a physics raycast off the player rig, which a bare test scene
    /// cannot reconstruct.
    /// </summary>
    public class PickableObjectTests
    {
        private GameObject _cameraGo;
        private GameObject _anchorsGo;
        private PlayerItemAnchors _anchors;
        private BroadcastControlsStatus.ControlScheme _schemeBefore;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _schemeBefore = BroadcastControlsStatus.controlScheme;
            // Desktop: the camera/hand docks resolve (not the VR hands).
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _cameraGo = new GameObject("TestCamera", typeof(Camera)) { tag = "MainCamera" };
            _cameraGo.transform.SetPositionAndRotation(new Vector3(0f, 1.6f, 0f), Quaternion.identity);

            _anchorsGo = new GameObject("PlayerItemAnchors");
            _anchors = _anchorsGo.AddComponent<PlayerItemAnchors>();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BroadcastControlsStatus.controlScheme = _schemeBefore;
            UnityEngine.Object.Destroy(_anchorsGo);
            UnityEngine.Object.Destroy(_cameraGo);
            yield return null;
        }

        // The config is private [SerializeField]s (as it should be) — reflection stands in
        // for the inspector. UnityEditor is unavailable here: the tests asmdef targets all
        // platforms, so SerializedObject is not an option.
        private static void SetField(object target, string field, object value)
        {
            var info = target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(info, Is.Not.Null, $"No private field '{field}' on {target.GetType().Name} — the test is out of date.");
            info.SetValue(target, value);
        }

        /// <summary>
        /// Builds the pickable INACTIVE so `configure` lands before Awake runs — Awake
        /// captures the original spot and performs the legacy migration, so fields set
        /// afterwards would be too late.
        /// </summary>
        private static PickableObject NewPickable(string name, Action<GameObject, PickableObject> configure = null)
        {
            var go = new GameObject(name, typeof(Rigidbody));
            go.SetActive(false);
            var pickable = go.AddComponent<PickableObject>();
            configure?.Invoke(go, pickable);
            go.SetActive(true); // Awake -> Initialize
            return pickable;
        }

        private static void AssertClose(Vector3 actual, Vector3 expected, string message)
            => Assert.That(Vector3.Distance(actual, expected), Is.LessThan(0.001f),
                $"{message} (expected {expected}, got {actual})");

        [UnityTest]
        public IEnumerator CameraAnchor_ResolvesToTheCamera()
        {
            var pickable = NewPickable("Tablet", (_, p) => SetField(p, "heldAnchor", HeldAnchor.Camera));
            yield return null;

            var anchor = _anchors.Resolve(pickable, HandType.None, out var hand);

            Assert.That(anchor, Is.EqualTo(_cameraGo.transform),
                "A Camera-anchored item docks to the camera itself — its own local pose IS the dock.");
            Assert.That(hand, Is.EqualTo(HandType.None), "The camera dock is not a hand.");
            UnityEngine.Object.Destroy(pickable.gameObject);
        }

        [UnityTest]
        public IEnumerator RightHandAnchor_ResolvesToASteadyDockUnderTheCamera()
        {
            var pickable = NewPickable("Prop", (_, p) =>
            {
                SetField(p, "heldAnchor", HeldAnchor.RightHand);
                SetField(p, "handAttachMode", HandAttachMode.SteadyDock);
            });
            yield return null;

            var anchor = _anchors.Resolve(pickable, HandType.None, out var hand);

            Assert.That(anchor, Is.Not.Null, "The right-hand dock was not created (the rig ships without one).");
            Assert.That(hand, Is.EqualTo(HandType.Right));
            Assert.That(anchor.IsChildOf(_cameraGo.transform), Is.True,
                "A steady hand dock must ride the camera, or a held item's UI would not be steady on screen.");
            Assert.That(anchor.localPosition.x, Is.GreaterThan(0f), "The RIGHT dock should sit right of the view.");
            UnityEngine.Object.Destroy(pickable.gameObject);
        }

        [UnityTest]
        public IEnumerator LeftAndRightDocks_AreOnOppositeSides()
        {
            var right = NewPickable("Right", (_, p) => SetField(p, "heldAnchor", HeldAnchor.RightHand));
            var left = NewPickable("Left", (_, p) => SetField(p, "heldAnchor", HeldAnchor.LeftHand));
            yield return null;

            var rightAnchor = _anchors.Resolve(right, HandType.None, out _);
            var leftAnchor = _anchors.Resolve(left, HandType.None, out var leftHand);

            Assert.That(leftHand, Is.EqualTo(HandType.Left));
            Assert.That(rightAnchor, Is.Not.EqualTo(leftAnchor), "The two hands must resolve to different docks.");
            Assert.That(leftAnchor.localPosition.x, Is.LessThan(rightAnchor.localPosition.x));
            UnityEngine.Object.Destroy(right.gameObject);
            UnityEngine.Object.Destroy(left.gameObject);
        }

        [UnityTest]
        public IEnumerator AnimatedBone_WithNoBody_FallsBackToSteadyDockAndWarns()
        {
            // The body ships DISABLED, so this is the COMMON case: it must degrade to a
            // steady dock rather than return null and strand the item.
            var pickable = NewPickable("BoneProp", (_, p) =>
            {
                SetField(p, "heldAnchor", HeldAnchor.RightHand);
                SetField(p, "handAttachMode", HandAttachMode.AnimatedBone);
            });
            yield return null;

            LogAssert.Expect(LogType.Warning, new Regex("Animated Bone"));
            var anchor = _anchors.Resolve(pickable, HandType.None, out var hand);

            Assert.That(anchor, Is.Not.Null, "Animated Bone with no body must fall back to a dock, not return null.");
            Assert.That(hand, Is.EqualTo(HandType.Right));
            Assert.That(anchor.IsChildOf(_cameraGo.transform), Is.True, "The fallback is the steady camera-relative dock.");
            UnityEngine.Object.Destroy(pickable.gameObject);
        }

        [UnityTest]
        public IEnumerator InXr_TheGrabbingHandWinsOverTheAuthoredAnchor()
        {
            var vrLeft = new GameObject("VR Left Hand").transform;
            var vrRight = new GameObject("VR Right Hand").transform;
            SetField(_anchors, "vrLeftHand", vrLeft);
            SetField(_anchors, "vrRightHand", vrRight);

            // Authored for the camera — but in VR you grab with a hand, and that wins.
            var pickable = NewPickable("Tablet", (_, p) => SetField(p, "heldAnchor", HeldAnchor.Camera));
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            yield return null;

            var anchor = _anchors.Resolve(pickable, HandType.Left, out var hand);

            Assert.That(hand, Is.EqualTo(HandType.Left));
            Assert.That(anchor, Is.EqualTo(vrLeft),
                "In VR the hand that actually grabbed must win — docking a held item to the face is meaningless there.");

            UnityEngine.Object.Destroy(pickable.gameObject);
            UnityEngine.Object.Destroy(vrLeft.gameObject);
            UnityEngine.Object.Destroy(vrRight.gameObject);
        }

        [UnityTest]
        public IEnumerator ReleaseTarget_MigratesFromTheLegacyBool()
        {
            // Existing scenes have "return to initial position" ticked. Introducing the
            // enum must not silently flip them to DropInPlace (its default value).
            var pickable = NewPickable("Legacy", (_, p) =>
            {
                SetField(p, "returnToInitialPositionOnRelease", true);
                SetField(p, "_migrationVersion", 0);                      // as an un-migrated asset loads
                SetField(p, "releaseTarget", ReleaseTarget.DropInPlace);  // the enum's default
            });
            yield return null;

            Assert.That(pickable.ReleaseMode, Is.EqualTo(ReleaseTarget.OriginalSpot),
                "A legacy pickable with 'return to initial position' ticked must migrate to OriginalSpot, " +
                "not silently become DropInPlace.");
            UnityEngine.Object.Destroy(pickable.gameObject);
        }

        [UnityTest]
        public IEnumerator OriginalSpot_CapturesPositionAndRotation_NotJustTheParent()
        {
            // The bug being fixed: the old code kept only transform.parent (in a field
            // Awake overwrote), so a released object stayed at whatever pose it drifted to.
            var home = new GameObject("Home").transform;
            var startPosition = new Vector3(3f, 2f, 1f);
            var startRotation = Quaternion.Euler(10f, 20f, 30f);

            var pickable = NewPickable("Prop", (go, _) =>
            {
                go.transform.SetParent(home);
                go.transform.SetPositionAndRotation(startPosition, startRotation);
            });
            yield return null;

            Assert.That(pickable.OriginalParent, Is.EqualTo(home));
            AssertClose(pickable.OriginalPosition, startPosition, "The original POSITION was not captured");
            Assert.That(Quaternion.Angle(pickable.OriginalRotation, startRotation), Is.LessThan(0.5f),
                "The original ROTATION must be captured too — restoring only the parent leaves the object askew.");

            UnityEngine.Object.Destroy(pickable.gameObject);
            UnityEngine.Object.Destroy(home.gameObject);
        }

        [UnityTest]
        public IEnumerator HandPose_SuppliesTheAttachOffset()
        {
            var pose = ScriptableObject.CreateInstance<global::Pose>();
            pose.rightHandInfo.attachPosition = new Vector3(0.1f, 0.2f, 0.3f);
            pose.rightHandInfo.attachRotation = Quaternion.Euler(0f, 90f, 0f);

            var pickable = NewPickable("Posed", (_, p) =>
            {
                SetField(p, "heldAnchor", HeldAnchor.RightHand);
                SetField(p, "heldLocalPosition", new Vector3(9f, 9f, 9f)); // must LOSE to the pose
                SetField(p, "handPose", pose);
            });
            yield return null;

            pickable.GetHeldOffset(HandType.Right, out var localPosition, out var localRotation);
            AssertClose(localPosition, new Vector3(0.1f, 0.2f, 0.3f),
                "A hand pose carries its own attach offset and must win over the generic held offset");
            Assert.That(Quaternion.Angle(localRotation, Quaternion.Euler(0f, 90f, 0f)), Is.LessThan(0.5f));

            // With no hand (camera dock) the generic offset is used instead.
            pickable.GetHeldOffset(HandType.None, out var cameraLocal, out _);
            AssertClose(cameraLocal, new Vector3(9f, 9f, 9f), "The camera dock must use the generic held offset");

            UnityEngine.Object.Destroy(pickable.gameObject);
            UnityEngine.Object.Destroy(pose);
        }

        [UnityTest]
        public IEnumerator OriginalScale_IsCaptured_InBothFrames()
        {
            // Regression guard: reparenting rewrites scale as a side effect, so a
            // take/release round trip through a SCALED rig used to compound that factor
            // and the object grew a little every cycle. Restoring these captured values
            // is what makes the round trip reversible — so they must be right.
            var scaledParent = new GameObject("ScaledRoot").transform;
            scaledParent.localScale = new Vector3(2f, 2f, 2f);

            var pickable = NewPickable("Cube", (go, _) =>
            {
                go.transform.SetParent(scaledParent);
                go.transform.localScale = new Vector3(3f, 3f, 3f);
            });
            yield return null;

            AssertClose(pickable.OriginalLocalScale, new Vector3(3f, 3f, 3f),
                "The authored localScale must be captured (it is what restores the object under its original parent)");
            AssertClose(pickable.OriginalWorldScale, new Vector3(6f, 6f, 6f),
                "The authored WORLD scale must be captured too (it is what restores size when dropped at the scene root)");

            UnityEngine.Object.Destroy(pickable.gameObject);
            UnityEngine.Object.Destroy(scaledParent.gameObject);
        }

        [UnityTest]
        public IEnumerator WorldLocation_IsPlainCoordinates_NoSceneReference()
        {
            var pickable = NewPickable("Returner", (_, p) =>
            {
                SetField(p, "releaseTarget", ReleaseTarget.WorldLocation);
                SetField(p, "releaseWorldPosition", new Vector3(5f, 0f, -7f));
                SetField(p, "releaseWorldEuler", new Vector3(0f, 45f, 0f));
                SetField(p, "_migrationVersion", 1); // already migrated: keep the authored value
            });
            yield return null;

            Assert.That(pickable.ReleaseMode, Is.EqualTo(ReleaseTarget.WorldLocation));
            pickable.GetReleaseWorldPose(out var position, out var rotation);
            AssertClose(position, new Vector3(5f, 0f, -7f), "The release world position must survive verbatim");
            Assert.That(Quaternion.Angle(rotation, Quaternion.Euler(0f, 45f, 0f)), Is.LessThan(0.5f));
            UnityEngine.Object.Destroy(pickable.gameObject);
        }
    }
}
