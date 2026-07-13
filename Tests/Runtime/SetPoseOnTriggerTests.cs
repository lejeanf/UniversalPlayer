using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// SetPoseOnTrigger with per-finger-bone colliders (HandColliderBuilder): a hand
    /// now touches a zone with MANY colliders, so the pose must be applied when the
    /// FIRST collider enters and released only when the LAST one leaves — one finger
    /// slipping out must not strip the pose off a hand still inside.
    /// </summary>
    public class SetPoseOnTriggerTests
    {
        private GameObject _zone;
        private SetPoseOnTrigger _trigger;
        private GameObject _hand;
        private HandPoseManager _manager;
        private Collider _fingerA;
        private Collider _fingerB;

        private static readonly MethodInfo Enter = typeof(SetPoseOnTrigger)
            .GetMethod("OnTriggerEnter", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo Exit = typeof(SetPoseOnTrigger)
            .GetMethod("OnTriggerExit", BindingFlags.Instance | BindingFlags.NonPublic);

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _zone = new GameObject("PoseZone");
            _zone.AddComponent<BoxCollider>().isTrigger = true;
            _trigger = _zone.AddComponent<SetPoseOnTrigger>();
            // The grab-check gate is a separate feature — off for this test.
            typeof(SetPoseOnTrigger).GetField("isUsingGrabCheck", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_trigger, false);

            _hand = new GameObject("Hand");
            _manager = _hand.AddComponent<HandPoseManager>();
            _fingerA = new GameObject("L_IndexProximal").AddComponent<BoxCollider>();
            _fingerA.transform.SetParent(_hand.transform, false);
            _fingerB = new GameObject("L_MiddleProximal").AddComponent<BoxCollider>();
            _fingerB.transform.SetParent(_hand.transform, false);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Object.Destroy(_zone);
            Object.Destroy(_hand);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PoseHeldUntilTheLastFingerLeaves()
        {
            Enter.Invoke(_trigger, new object[] { _fingerA });
            Assert.That(_manager.IsPoseHeld, Is.True, "First finger in: the zone must acquire the pose hold.");

            Enter.Invoke(_trigger, new object[] { _fingerB });
            Exit.Invoke(_trigger, new object[] { _fingerA });
            Assert.That(_manager.IsPoseHeld, Is.True,
                "One finger left but another is still inside — releasing now flickers the pose.");

            Exit.Invoke(_trigger, new object[] { _fingerB });
            Assert.That(_manager.IsPoseHeld, Is.False, "Last finger out: the hold must be released.");
            yield return null;
        }

        [UnityTest]
        public IEnumerator NonHandColliderIsIgnored()
        {
            var stray = new GameObject("NotAHand").AddComponent<BoxCollider>();
            try
            {
                Enter.Invoke(_trigger, new object[] { stray });
                Assert.That(_manager.IsPoseHeld, Is.False, "A collider with no HandPoseManager above it is not a hand.");
            }
            finally
            {
                Object.Destroy(stray.gameObject);
            }
            yield return null;
        }
    }
}
