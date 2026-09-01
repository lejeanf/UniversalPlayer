using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace jeanf.universalplayer
{
    public class ControllerHandPoseDriver : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public enum PoseState
        {
            Suspended, // someone else owns the pose (grab, zone, item) — reapply when released
            Default,
            Point,
            SemiClosedFist,
            ClosedFist
        }

        [Tooltip("Master switch. Off = hands keep their default/select poses exactly as before.")]
        [SerializeField] private bool driveEnabled = true;

        [Header("Poses (author with Tools/Jeanf/UniversalPlayer/Pose Editor)")]
        [Tooltip("Idle pose while the trigger is released. Empty = the hand's default pose.")]
        [SerializeField] private Pose defaultPose;
        [Tooltip("Grip slightly touched. Empty = falls back to the closest assigned pose.")]
        [SerializeField] private Pose semiClosedFistPose;
        [Tooltip("Grip pressed hard. Empty = falls back to the closest assigned pose.")]
        [SerializeField] private Pose closedFistPose;
        [Tooltip("Trigger pressed. Empty = falls back to the closest assigned pose.")]
        [SerializeField] private Pose pointPose;

        [Header("Thresholds")]
        [Range(0.01f, 0.5f)][SerializeField] private float gripTouchThreshold = 0.15f;
        [Range(0.5f, 1f)][SerializeField] private float gripHardThreshold = 0.85f;
        [Range(0.1f, 1f)][SerializeField] private float triggerPressThreshold = 0.55f;
        [Tooltip("How far a value must fall back below its threshold before the pose releases (anti-flicker).")]
        [Range(0f, 0.2f)][SerializeField] private float hysteresis = 0.08f;

        /// <summary>Test seam: returns (grip, trigger) for a hand. Null = read the real XR device.</summary>
        public Func<HandType, (float grip, float trigger)?> InputProbe;

        private sealed class HandState
        {
            public HandPoseManager Manager;
            public PoseState Applied = PoseState.Suspended;
            public bool GripTouched;
            public bool GripHard;
            public bool TriggerPressed;
        }

        private readonly List<HandState> hands = new List<HandState>();
        private float nextHandScan;
        private bool missingPosesWarned;

        public PoseState StateOf(HandType handType)
        {
            foreach (var hand in hands)
            {
                if (hand.Manager != null && hand.Manager.HandType == handType) return hand.Applied;
            }
            return PoseState.Suspended;
        }

        private void OnDisable()
        {
            hands.Clear();
        }

        private void Update()
        {
            if (!driveEnabled) return;
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
            {
                foreach (var hand in hands) hand.Applied = PoseState.Suspended;
                return;
            }

            // Hands spawn (and can respawn) at runtime: rescan periodically until found.
            if (hands.Count == 0 && Time.unscaledTime >= nextHandScan) RefreshHands();

            foreach (var hand in hands)
            {
                if (hand.Manager == null) continue;
                DriveHand(hand);
            }
        }

        /// <summary>Re-discovers HandPoseManagers under the player (also a test seam).</summary>
        public void RefreshHands()
        {
            nextHandScan = Time.unscaledTime + 1f;
            hands.Clear();
            foreach (var manager in GetComponentsInChildren<HandPoseManager>(true))
                hands.Add(new HandState { Manager = manager });
        }

        private void DriveHand(HandState hand)
        {
            if (hand.Manager.IsPoseHeld || hand.Manager.IsSelecting)
            {
                // A zone, the primary item or a grabbed object owns the pose right now.
                hand.Applied = PoseState.Suspended;
                return;
            }

            var reading = ReadController(hand.Manager.HandType);
            if (reading == null) return; // no device — leave the hand as it is

            var (grip, trigger) = reading.Value;
            hand.GripHard = WithHysteresis(grip, gripHardThreshold, hand.GripHard);
            hand.GripTouched = WithHysteresis(grip, gripTouchThreshold, hand.GripTouched);
            hand.TriggerPressed = WithHysteresis(trigger, triggerPressThreshold, hand.TriggerPressed);

            var state = hand.GripHard ? PoseState.ClosedFist
                : hand.GripTouched ? PoseState.SemiClosedFist
                : hand.TriggerPressed ? PoseState.Point
                : PoseState.Default;
            if (state == hand.Applied) return;

            hand.Applied = state;
            var pose = PoseFor(state);
            if (pose != null) hand.Manager.ApplyPose(pose);
            else hand.Manager.ApplyDefaultPose();
        }

        private bool WithHysteresis(float value, float threshold, bool wasAbove)
        {
            return value >= (wasAbove ? threshold - hysteresis : threshold);
        }

        private Pose PoseFor(PoseState state)
        {
            Pose pose = null;
            switch (state)
            {
                case PoseState.ClosedFist:
                    pose = FirstAssigned(closedFistPose, semiClosedFistPose, pointPose, defaultPose);
                    break;
                case PoseState.SemiClosedFist:
                    pose = FirstAssigned(semiClosedFistPose, closedFistPose, defaultPose, pointPose);
                    break;
                case PoseState.Point:
                    pose = FirstAssigned(pointPose, closedFistPose, semiClosedFistPose, defaultPose);
                    break;
                case PoseState.Default:
                    pose = defaultPose; // empty point = the hand's own default pose
                    break;
            }

            if (pose == null && state != PoseState.Point && !missingPosesWarned)
            {
                missingPosesWarned = true;
                Debug.LogWarning($"{LogPrefix} ControllerHandPoseDriver on '{name}': no fist/point poses assigned — " +
                    "grip and trigger fall back to the hands' default pose. Author the poses with " +
                    "Tools/Jeanf/UniversalPlayer/Pose Editor and assign them on your Player variant (or disable the driver).", this);
            }
            return pose;
        }

        private static Pose FirstAssigned(params Pose[] poses)
        {
            foreach (var pose in poses)
            {
                if (pose != null) return pose;
            }
            return null;
        }

        private (float grip, float trigger)? ReadController(HandType handType)
        {
            if (InputProbe != null) return InputProbe(handType);

            var node = handType == HandType.Left ? XRNode.LeftHand : XRNode.RightHand;
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return null;
            device.TryGetFeatureValue(CommonUsages.grip, out var grip);
            device.TryGetFeatureValue(CommonUsages.trigger, out var trigger);
            return (grip, trigger);
        }
    }
}
