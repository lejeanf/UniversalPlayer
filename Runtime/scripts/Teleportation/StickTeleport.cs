using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Visuals;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace jeanf.universalplayer
{
    /// <summary>
    /// XRI-style stick teleportation (ships on the Player root, zero wiring):
    /// pushing the right thumbstick forward summons the curved teleport ray (the
    /// packaged Teleport Interactor with its valid/blocked gradients and directional
    /// reticle), the curve reaches further the harder the stick is pushed, rotating
    /// the stick aims the landing orientation, and releasing the stick teleports —
    /// onto TeleportationArea/TeleportationAnchor surfaces only. The left stick keeps
    /// driving continuous movement.
    /// </summary>
    public class StickTeleport : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("Stick deflection (forward) that summons the teleport ray.")]
        [Range(0.3f, 0.95f)][SerializeField] private float activateThreshold = 0.65f;
        [Tooltip("Deflection below which the stick counts as released (teleports if aiming at a valid target).")]
        [Range(0.05f, 0.6f)][SerializeField] private float releaseThreshold = 0.3f;
        [Tooltip("Curve velocity at the lightest push — how close the shortest teleport lands.")]
        [SerializeField] private float minCurveVelocity = 6f;
        [Tooltip("Curve velocity at full deflection — how far the longest teleport reaches.")]
        [SerializeField] private float maxCurveVelocity = 13f;

        [Tooltip("Override the teleport ray's aim while summoned, so it fires forward out of the hand instead of along whatever the finger/stabilizer points at. Turn OFF to use the interactor's own ray origin unchanged.")]
        [SerializeField] private bool overrideAim = true;
        [Tooltip("Aim offset (Euler) applied on top of the controller's orientation while aiming. X tilts the launch DOWN. Tune live in Play mode until the arc points where you expect — this is the 'rotate the anchor' knob.")]
        [SerializeField] private Vector3 aimEulerOffset = new Vector3(35f, 0f, 0f);

        /// <summary>Test seam: stick value per hand. Null = read the real XR device.</summary>
        public Func<XRNode, Vector2?> StickProbe;

        private sealed class TeleportHand
        {
            public XRNode Node;
            public XRRayInteractor Ray;
            public XRInteractorLineVisual LineVisual;
            public Transform RayOrigin; // what the interactor casts from (stabilizer-driven)
            public bool Aiming;
            public float TargetYaw;
        }

        private readonly List<TeleportHand> hands = new List<TeleportHand>();
        private Camera playerCamera;
        private CharacterController characterController;
        private PlayerMovement playerMovement;
        private float nextScan;

        private void OnEnable()
        {
            playerCamera = Camera.main;
            characterController = GetComponentInChildren<CharacterController>(true);
            playerMovement = GetComponentInChildren<PlayerMovement>(true);
        }

        private void OnDisable()
        {
            foreach (var hand in hands) StopAiming(hand);
            hands.Clear();
        }

        private void Update()
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
            {
                foreach (var hand in hands) StopAiming(hand);
                return;
            }

            if (hands.Count == 0 && Time.unscaledTime >= nextScan) RefreshInteractors();

            foreach (var hand in hands) DriveHand(hand);
        }

        // The ray origin is driven by an XRTransformStabilizer (position + rotation) that
        // aims along the finger/controller in a way that reads "sideways" for a natural
        // hand pose. We keep its stabilized POSITION but re-aim its ROTATION forward out
        // of the controller here in LateUpdate — after the stabilizer has run this frame,
        // so our aim wins. Only while the ray is actually summoned.
        private void LateUpdate()
        {
            if (!overrideAim) return;
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR) return;
            foreach (var hand in hands)
            {
                if (!hand.Aiming || hand.Ray == null) continue;
                if (hand.RayOrigin == null)
                    hand.RayOrigin = hand.Ray.rayOriginTransform != null ? hand.Ray.rayOriginTransform : hand.Ray.transform;
                // The interactor transform tracks the controller (identity-local under it),
                // so its rotation is the controller's aim; offset it forward/down.
                hand.RayOrigin.rotation = hand.Ray.transform.rotation * Quaternion.Euler(aimEulerOffset);
            }
        }

        /// <summary>Finds the packaged teleport rays (projectile-curve interactors); also a test seam.</summary>
        public void RefreshInteractors()
        {
            nextScan = Time.unscaledTime + 1f;
            hands.Clear();
            foreach (var ray in GetComponentsInChildren<XRRayInteractor>(true))
            {
                if (ray.lineType != XRRayInteractor.LineType.ProjectileCurve) continue;
                var node = NodeFor(ray.transform);
                hands.Add(new TeleportHand
                {
                    Node = node,
                    Ray = ray,
                    LineVisual = ray.GetComponent<XRInteractorLineVisual>(),
                    RayOrigin = ray.rayOriginTransform != null ? ray.rayOriginTransform : ray.transform,
                });
                // The ray only shows while aiming.
                ray.gameObject.SetActive(false);
            }

            // Teleport lives on the RIGHT stick: the left stick drives continuous move.
            hands.RemoveAll(h => h.Node != XRNode.RightHand);
        }

        private static XRNode NodeFor(Transform t)
        {
            for (var current = t; current != null; current = current.parent)
            {
                var n = current.name;
                if (n.Contains("Left")) return XRNode.LeftHand;
                if (n.Contains("Right")) return XRNode.RightHand;
            }
            return XRNode.RightHand;
        }

        private void DriveHand(TeleportHand hand)
        {
            if (hand.Ray == null) return;
            var stick = ReadStick(hand.Node);
            if (stick == null)
            {
                StopAiming(hand);
                return;
            }

            var value = stick.Value;
            var deflection = Mathf.Clamp01(value.magnitude);

            if (!hand.Aiming)
            {
                // Forward push summons the ray (sideways stays free for snap turn).
                if (value.y >= activateThreshold && value.y >= Mathf.Abs(value.x))
                {
                    hand.Aiming = true;
                    hand.Ray.gameObject.SetActive(true);
                }
                return;
            }

            if (deflection > releaseThreshold)
            {
                // Curve length follows the push; stick rotation aims the landing yaw.
                hand.Ray.velocity = Mathf.Lerp(minCurveVelocity, maxCurveVelocity,
                    Mathf.InverseLerp(releaseThreshold, 1f, deflection));
                var headYaw = playerCamera != null ? playerCamera.transform.eulerAngles.y : 0f;
                hand.TargetYaw = headYaw + Mathf.Atan2(value.x, value.y) * Mathf.Rad2Deg;
                RotateReticle(hand);
                return;
            }

            // Released: teleport when the curve rests on a teleport surface.
            TryTeleport(hand);
            StopAiming(hand);
        }

        private void RotateReticle(TeleportHand hand)
        {
            var reticle = hand.LineVisual != null ? hand.LineVisual.reticle : null;
            if (reticle != null && reticle.activeInHierarchy)
                reticle.transform.rotation = Quaternion.Euler(0f, hand.TargetYaw, 0f);
        }

        private void TryTeleport(TeleportHand hand)
        {
            if (!hand.Ray.TryGetCurrent3DRaycastHit(out var hit)) return;
            var interactable = hit.collider.GetComponentInParent<BaseTeleportationInteractable>();
            if (interactable == null) return;

            var destination = hit.point;
            var yaw = hand.TargetYaw;
            if (interactable is TeleportationAnchor anchor && anchor.teleportAnchorTransform != null)
            {
                // Anchors are authored landings: their transform wins.
                destination = anchor.teleportAnchorTransform.position;
                yaw = anchor.teleportAnchorTransform.eulerAngles.y;
            }

            TeleportRootTo(destination, yaw);
        }

        /// <summary>Moves the player root so the HEAD lands on the destination facing the given yaw.</summary>
        public void TeleportRootTo(Vector3 destination, float yaw)
        {
            if (playerCamera == null) playerCamera = Camera.main;
            var head = playerCamera != null ? playerCamera.transform : transform;

            var ccWasEnabled = characterController != null && characterController.enabled;
            if (ccWasEnabled) characterController.enabled = false;

            // Rotate the rig around the head so the view faces the aimed yaw...
            var deltaYaw = Mathf.DeltaAngle(head.eulerAngles.y, yaw);
            transform.RotateAround(new Vector3(head.position.x, transform.position.y, head.position.z), Vector3.up, deltaYaw);

            // ...then land the head (not the rig origin) on the destination.
            var headOffset = head.position - transform.position;
            transform.position = new Vector3(destination.x - headOffset.x, destination.y, destination.z - headOffset.z);

            if (ccWasEnabled) characterController.enabled = true;
            if (playerMovement != null) playerMovement.OnExternalTeleport();
        }

        private void StopAiming(TeleportHand hand)
        {
            if (!hand.Aiming) return;
            hand.Aiming = false;
            if (hand.Ray != null) hand.Ray.gameObject.SetActive(false);
        }

        private Vector2? ReadStick(XRNode node)
        {
            if (StickProbe != null) return StickProbe(node);
            var device = InputDevices.GetDeviceAtXRNode(node);
            if (!device.isValid) return null;
            device.TryGetFeatureValue(CommonUsages.primary2DAxis, out var value);
            return value;
        }
    }
}
