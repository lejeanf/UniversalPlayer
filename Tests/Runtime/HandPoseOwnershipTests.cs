using System.Reflection;
using jeanf.EventSystem;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer.tests
{
    /// <summary>
    /// A hand's pose has an owner. Sources rank Grab > PrimaryItem > TriggerZone >
    /// Pointing > ControllerDriver: a lower source is refused while a higher one owns the
    /// hand, and releasing the top claim re-applies the next one (grab pose after a zone,
    /// default when nothing is left). Regression: drawing the tablet with the left X
    /// opened the RIGHT hand over the object it was holding.
    /// </summary>
    public class HandPoseOwnershipTests
    {
        private GameObject _root;
        private HandPoseManager _left;
        private HandPoseManager _right;
        private Pose _leftDefault;
        private Pose _rightDefault;
        private Pose _grabPose;
        private Pose _zonePose;
        private Pose _pointPose;
        private Pose _tabletPose;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("HandPoseOwnershipRoot");
            _leftDefault = NewPose("LeftDefault");
            _rightDefault = NewPose("RightDefault");
            _grabPose = NewPose("Grab");
            _zonePose = NewPose("Zone");
            _pointPose = NewPose("Point");
            _tabletPose = NewPose("Tablet");
            _left = NewHand("LeftHand", HandType.Left, _leftDefault);
            _right = NewHand("RightHand", HandType.Right, _rightDefault);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
        }

        private static Pose NewPose(string name)
        {
            var pose = ScriptableObject.CreateInstance<Pose>();
            pose.name = name;
            foreach (var info in new[] { pose.leftHandInfo, pose.rightHandInfo })
            {
                info.jointNames.Add("IndexProximal");
                info.fingerRotations.Add(Quaternion.identity);
            }
            return pose;
        }

        private static InputActionReference NewActionReference(string name)
        {
            var asset = ScriptableObject.CreateInstance<InputActionAsset>();
            var map = asset.AddActionMap("Hands");
            var action = map.AddAction(name);
            return InputActionReference.Create(action);
        }

        private HandPoseManager NewHand(string name, HandType type, Pose defaultPose)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            var hand = go.AddComponent<HandPoseManager>();
            hand.isLerpingOverTime = false;
            SetField(hand, "handType", type);
            SetField(hand, "defaultPose", defaultPose);
            go.SetActive(true);
            return hand;
        }

        private static void SetField(object target, string field, object value)
        {
            for (var t = target.GetType(); t != null; t = t.BaseType)
            {
                var info = t.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
                if (info == null) continue;
                info.SetValue(target, value);
                return;
            }
            Assert.Fail($"Field '{field}' not found on {target.GetType().Name} — update HandPoseOwnershipTests alongside the refactor.");
        }

        private GetPrimaryInHandItemWithVRController NewPrimaryItemController()
        {
            var go = new GameObject("PrimaryItemController");
            go.transform.SetParent(_root.transform);
            go.SetActive(false);
            var controller = go.AddComponent<GetPrimaryInHandItemWithVRController>();
            SetField(controller, "drawPrimaryItem_LeftHand", NewActionReference("DrawLeft"));
            SetField(controller, "drawPrimaryItem_RightHand", NewActionReference("DrawRight"));
            SetField(controller, "_leftHand", _left.transform);
            SetField(controller, "_rightHand", _right.transform);
            SetField(controller, "_leftHandPoseManager", _left);
            SetField(controller, "_rightHandPoseManager", _right);
            SetField(controller, "primaryItemPose", _tabletPose);
            SetField(controller, "_PrimaryItemStateChannel", ScriptableObject.CreateInstance<BoolEventChannelSO>());
            SetField(controller, "_primaryItemStateWithUsedHandChannel", ScriptableObject.CreateInstance<StringEventChannelSO>());
            go.SetActive(true);
            return controller;
        }

        [Test]
        public void DrawingTheItemInOneHand_LeavesTheOtherHandsGrabPoseAlone()
        {
            var heldObject = new object();
            _right.TryClaimPose(heldObject, HandPoseSource.Grab, _grabPose);
            var controller = NewPrimaryItemController();

            controller.SetIpadStateForLeftHand(_tabletPose.leftHandInfo);

            Assert.That(_right.LastAppliedPose, Is.SameAs(_grabPose),
                "Left X must not open the right hand while it holds an object (the reported regression).");
            Assert.That(_right.ActivePoseSource, Is.EqualTo(HandPoseSource.Grab));
            Assert.That(_left.LastAppliedPose, Is.SameAs(_tabletPose));
            Assert.That(_left.ActivePoseSource, Is.EqualTo(HandPoseSource.PrimaryItem));
        }

        [Test]
        public void MovingTheItemToTheOtherHand_ReopensTheHandItLeft()
        {
            var controller = NewPrimaryItemController();
            controller.SetIpadStateForRightHand(_tabletPose.rightHandInfo);
            Assert.That(_right.LastAppliedPose, Is.SameAs(_tabletPose));

            controller.SetIpadStateForLeftHand(_tabletPose.leftHandInfo);

            Assert.That(_right.LastAppliedPose, Is.SameAs(_rightDefault), "The hand the item moved away from must open again.");
            Assert.That(_right.IsPoseHeld, Is.False);
            Assert.That(_left.LastAppliedPose, Is.SameAs(_tabletPose));
        }

        [Test]
        public void HidingTheItem_ReturnsTheHandToDefault()
        {
            var controller = NewPrimaryItemController();
            controller.SetIpadStateForLeftHand(_tabletPose.leftHandInfo);

            controller.SetIpadStateForLeftHand(_tabletPose.leftHandInfo);

            Assert.That(_left.LastAppliedPose, Is.SameAs(_leftDefault));
            Assert.That(_left.IsPoseHeld, Is.False);
        }

        [Test]
        public void LowerRankedSource_IsRefused_WhileAHigherOneOwnsTheHand()
        {
            _right.TryClaimPose(new object(), HandPoseSource.Grab, _grabPose);

            var pointingAccepted = _right.TryClaimPose(new object(), HandPoseSource.Pointing, _pointPose);
            var zoneAccepted = _right.TryClaimPose(new object(), HandPoseSource.TriggerZone, _zonePose);

            Assert.That(pointingAccepted, Is.False);
            Assert.That(zoneAccepted, Is.False);
            Assert.That(_right.LastAppliedPose, Is.SameAs(_grabPose));
            Assert.That(_right.CanApplyPose(HandPoseSource.ControllerDriver), Is.False, "The controller driver must stay suspended while an object is held.");
        }

        [Test]
        public void ReleasingTheTopClaim_ReappliesTheNextOne_ThenDefault()
        {
            var zone = new object();
            var heldObject = new object();
            _right.TryClaimPose(zone, HandPoseSource.TriggerZone, _zonePose);
            _right.TryClaimPose(heldObject, HandPoseSource.Grab, _grabPose);
            Assert.That(_right.LastAppliedPose, Is.SameAs(_grabPose));

            _right.ReleasePoseClaim(heldObject);
            Assert.That(_right.LastAppliedPose, Is.SameAs(_zonePose), "Dropping the object inside a pose zone must fall back to the zone pose, not to default.");

            _right.ReleasePoseClaim(zone);
            Assert.That(_right.LastAppliedPose, Is.SameAs(_rightDefault));
            Assert.That(_right.IsPoseHeld, Is.False);
        }

        [Test]
        public void ReleasingABuriedClaim_DoesNotTouchTheAppliedPose()
        {
            var zone = new object();
            _right.TryClaimPose(zone, HandPoseSource.TriggerZone, _zonePose);
            _right.TryClaimPose(new object(), HandPoseSource.Grab, _grabPose);

            _right.ReleasePoseClaim(zone);

            Assert.That(_right.LastAppliedPose, Is.SameAs(_grabPose), "Leaving the zone while still holding the object must keep the grab pose.");
            Assert.That(_right.ActivePoseSource, Is.EqualTo(HandPoseSource.Grab));
        }

        [Test]
        public void AnOwner_ReplacesItsOwnClaim_InsteadOfStacking()
        {
            var owner = new object();
            _right.TryClaimPose(owner, HandPoseSource.Grab, _grabPose);
            _right.TryClaimPose(owner, HandPoseSource.Grab, _zonePose);

            _right.ReleasePoseClaim(owner);

            Assert.That(_right.IsPoseHeld, Is.False, "One owner, one claim: a single release must free the hand.");
            Assert.That(_right.LastAppliedPose, Is.SameAs(_rightDefault));
        }

        [Test]
        public void ReleasingAnUnknownOwner_IsANoOp()
        {
            _right.TryClaimPose(new object(), HandPoseSource.Grab, _grabPose);

            _right.ReleasePoseClaim(new object());

            Assert.That(_right.LastAppliedPose, Is.SameAs(_grabPose));
            Assert.That(_right.IsPoseHeld, Is.True);
        }
    }
}
