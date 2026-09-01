using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    public class ControllerHandPoseTests
    {
        private GameObject _player;
        private ControllerHandPoseDriver _driver;
        private HandPoseManager _hand;
        private float _grip;
        private float _trigger;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            _grip = 0f;
            _trigger = 0f;

            _player = new GameObject("PoseDriverPlayer");
            var handGo = new GameObject("RightHand");
            handGo.transform.SetParent(_player.transform);
            _hand = handGo.AddComponent<HandPoseManager>();
            SetField(_hand, "handType", HandType.Right);

            _driver = _player.AddComponent<ControllerHandPoseDriver>();
            _driver.InputProbe = _ => (_grip, _trigger);
            AssignPoses();
            _driver.RefreshHands();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_player);
            yield return null;
        }

        private void AssignPoses()
        {
            foreach (var slot in new[] { "pointPose", "semiClosedFistPose", "closedFistPose", "fullClosedFistPose" })
                SetField(_driver, slot, ScriptableObject.CreateInstance<Pose>());
        }

        private static void SetField(object target, string field, object value)
        {
            var info = FindField(target.GetType(), field);
            Assert.That(info, Is.Not.Null, $"Field '{field}' not found on {target.GetType().Name} — update ControllerHandPoseTests alongside the refactor.");
            info.SetValue(target, value);
        }

        private static FieldInfo FindField(System.Type type, string field)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var info = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info != null) return info;
            }
            return null;
        }

        [UnityTest]
        public IEnumerator GripAndTrigger_MapToThePoseLadder()
        {
            _grip = 0f; _trigger = 0f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.Default),
                "Idle hand (trigger released) must point.");

            _grip = 0.15f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.SemiClosedFist),
                "A lightly touched grip must semi-close the fist.");

            _grip = 0f; _trigger = 0.55f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.Point),
                "A pressed trigger must close the fist.");

            _grip = 0.85f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.ClosedFist),
                "A hard grip must fully close the fist (it outranks the trigger).");
        }

        [UnityTest]
        public IEnumerator Hysteresis_KeepsTheState_OnASlightDip()
        {
            _grip = 0.16f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.SemiClosedFist));

            _grip = 0.10f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.SemiClosedFist),
                "A tiny dip below the threshold must not flicker the pose back open.");

            _grip = 0.02f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.Default),
                "Fully releasing the grip must reopen the hand.");
        }

        [UnityTest]
        public IEnumerator PoseHold_SuspendsTheDriver_AndItResumesOnRelease()
        {
            _grip = 0.85f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.ClosedFist));

            _hand.AcquirePoseHold();
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.Suspended),
                "While a pose hold is active the driver must not fight it.");

            _hand.ReleasePoseHold();
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.ClosedFist),
                "Releasing the hold must hand the pose back to the controller state.");
        }

        [UnityTest]
        public IEnumerator OutsideXr_TheDriverIsInert()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            _grip = 0.85f;
            yield return null;
            Assert.That(_driver.StateOf(HandType.Right), Is.EqualTo(ControllerHandPoseDriver.PoseState.Suspended),
                "Controller-driven posing is a VR-only affordance.");
        }
    }
}
