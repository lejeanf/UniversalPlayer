using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// Behavior tests for hand visibility: HandsDisplayer must show the hands when the
    /// control scheme is XR and hide them otherwise, reacting to the scheme-change
    /// delegate (BroadcastControlsStatus.SendControlScheme — channel wiring lives on
    /// the PlayerEventBridge now). This is the exact chain that broke when the hands
    /// "disappeared" in consuming projects, so it stays under test.
    /// </summary>
    public class HandsDisplayTests
    {
        private GameObject _root;
        private GameObject _leftHand;
        private GameObject _rightHand;
        private HandsDisplayer _displayer;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;

            _root = new GameObject("HandsDisplayerRig");
            _root.SetActive(false);
            _leftHand = new GameObject("LeftHand");
            _rightHand = new GameObject("RightHand");
            _leftHand.transform.SetParent(_root.transform);
            _rightHand.transform.SetParent(_root.transform);

            _displayer = _root.AddComponent<HandsDisplayer>();
            SetField(_displayer, "leftHand", _leftHand);
            SetField(_displayer, "rightHand", _rightHand);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.KeyboardMouse;
            Object.Destroy(_root);
            yield return null;
        }

        private static void AnnounceScheme(BroadcastControlsStatus.ControlScheme scheme)
        {
            BroadcastControlsStatus.controlScheme = scheme;
            BroadcastControlsStatus.SendControlScheme?.Invoke(scheme);
        }

        [UnityTest]
        public IEnumerator SchemeChangesToXr_HandsAppear()
        {
            _root.SetActive(true);
            yield return null;

            AnnounceScheme(BroadcastControlsStatus.ControlScheme.XR);

            Assert.That(_leftHand.activeSelf, Is.True,
                "Left hand did not appear when the control scheme switched to XR — " +
                "HandsDisplayer is not reacting to SendControlScheme.");
            Assert.That(_rightHand.activeSelf, Is.True, "Right hand did not appear on switch to XR.");
        }

        [UnityTest]
        public IEnumerator SchemeChangesBackToKeyboard_HandsDisappear()
        {
            _root.SetActive(true);
            yield return null;
            AnnounceScheme(BroadcastControlsStatus.ControlScheme.XR);
            Assert.That(_leftHand.activeSelf, Is.True, "Precondition failed: hands not shown in XR.");

            AnnounceScheme(BroadcastControlsStatus.ControlScheme.KeyboardMouse);

            Assert.That(_leftHand.activeSelf, Is.False,
                "Left hand stayed visible after switching back to Keyboard&Mouse.");
            Assert.That(_rightHand.activeSelf, Is.False,
                "Right hand stayed visible after switching back to Keyboard&Mouse.");
        }

        [UnityTest]
        public IEnumerator AwakeInXrScheme_HandsStartVisible()
        {
            BroadcastControlsStatus.controlScheme = BroadcastControlsStatus.ControlScheme.XR;
            _root.SetActive(true); // Awake runs DisplayHands
            yield return null;

            Assert.That(_leftHand.activeSelf && _rightHand.activeSelf, Is.True,
                "Hands must be visible immediately when the scene starts with the XR scheme active " +
                "(HandsDisplayer.Awake applies the current scheme).");
        }

        [UnityTest]
        public IEnumerator ForceDisplay_OverridesSchemeForTesting()
        {
            _root.SetActive(true);
            yield return null;
            Assert.That(_leftHand.activeSelf, Is.False, "Precondition failed: hands visible in Keyboard&Mouse.");

            _displayer.ForceDisplay(true);
            Assert.That(_leftHand.activeSelf && _rightHand.activeSelf, Is.True,
                "ForceDisplay(true) did not show the hands — the Hands Test Bench 'Show hands' button relies on it.");

            _displayer.ForceDisplay(false);
            Assert.That(_leftHand.activeSelf || _rightHand.activeSelf, Is.False,
                "ForceDisplay(false) did not hide the hands.");
        }

        [UnityTest]
        public IEnumerator DisabledDisplayer_StopsListening_NoLeakedReactions()
        {
            _root.SetActive(true);
            yield return null;
            _displayer.enabled = false; // OnDisable must unsubscribe (regression: it never did)

            AnnounceScheme(BroadcastControlsStatus.ControlScheme.XR);

            Assert.That(_leftHand.activeSelf, Is.False,
                "A disabled HandsDisplayer still reacted to the scheme change — " +
                "the OnDisable unsubscribe regressed (leaked delegates).");
            yield return null;
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                $"Field '{fieldName}' not found on {target.GetType().Name} — it was renamed; update HandsDisplayTests alongside the refactor.");
            field.SetValue(target, value);
        }
    }
}
