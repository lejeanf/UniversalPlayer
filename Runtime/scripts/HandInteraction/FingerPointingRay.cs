using System.Collections.Generic;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// VR pointing ray (ships on the Player root, zero wiring): while a hand is in the
    /// POINT pose (trigger released — see ControllerHandPoseDriver), a short straight
    /// ray fades out of the fingertip. It changes color over anything usable — the
    /// same sources the desktop reticle reacts to (XRI interactables, seats,
    /// pickables, project layers) — and pressing the Interact button (A / X) performs
    /// the interact action: PerformAction raycasts along this ray in XR.
    /// </summary>
    public class FingerPointingRay : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [SerializeField] private float rayLength = 2.5f;
        [SerializeField] private float rayWidth = 0.006f;
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.55f);
        [SerializeField] private Color usableColor = new Color(0.35f, 0.95f, 0.6f, 0.9f);
        [Tooltip("Project raycast-interactable layers (same mask as the desktop reticle / click handler). Empty = only XRI interactables, seats and pickables change the ray state.")]
        [SerializeField] private LayerMask physicsHoverMask;

        private sealed class HandRay
        {
            public HandPoseManager Manager;
            public Transform Origin;
            public LineRenderer Line;
            public bool Pointing;
        }

        private readonly List<HandRay> hands = new List<HandRay>();
        private ControllerHandPoseDriver poseDriver;
        private Material lineMaterial;
        private float nextHandScan;

        private static FingerPointingRay activeInstance;

        /// <summary>The ray of the first hand currently pointing (PerformAction's XR origin).</summary>
        public static bool TryGetPointingRay(out Ray ray)
        {
            ray = default;
            if (activeInstance == null) return false;
            foreach (var hand in activeInstance.hands)
            {
                if (!hand.Pointing || hand.Origin == null) continue;
                ray = new Ray(hand.Origin.position, hand.Origin.forward);
                return true;
            }
            return false;
        }

        private void OnEnable()
        {
            activeInstance = this;
            poseDriver = GetComponentInChildren<ControllerHandPoseDriver>(true);
            if (poseDriver == null)
            {
                Debug.LogWarning($"{LogPrefix} FingerPointingRay on '{name}': no ControllerHandPoseDriver found — " +
                    "without it there is no POINT pose state, so the pointing ray never shows.", this);
            }
        }

        private void OnDisable()
        {
            if (activeInstance == this) activeInstance = null;
            foreach (var hand in hands)
            {
                if (hand.Line != null) Destroy(hand.Line.gameObject);
            }
            hands.Clear();
            if (lineMaterial != null) Destroy(lineMaterial);
        }

        private void Update()
        {
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR)
            {
                HideAll();
                return;
            }

            if (hands.Count == 0 && Time.unscaledTime >= nextHandScan) RefreshHands();

            foreach (var hand in hands)
            {
                if (hand.Manager == null) continue;
                hand.Pointing = poseDriver != null
                    && hand.Manager.gameObject.activeInHierarchy
                    && poseDriver.StateOf(hand.Manager.HandType) == ControllerHandPoseDriver.PoseState.Point;
                UpdateRay(hand);
            }
        }

        private void HideAll()
        {
            foreach (var hand in hands)
            {
                hand.Pointing = false;
                if (hand.Line != null) hand.Line.enabled = false;
            }
        }

        /// <summary>Re-discovers the hands (they spawn at runtime); also a test seam.</summary>
        public void RefreshHands()
        {
            nextHandScan = Time.unscaledTime + 1f;
            foreach (var hand in hands)
            {
                if (hand.Line != null) Destroy(hand.Line.gameObject);
            }
            hands.Clear();
            foreach (var manager in GetComponentsInChildren<HandPoseManager>(true))
            {
                var origin = manager.targetInteractor != null && manager.targetInteractor.attachTransform != null
                    ? manager.targetInteractor.attachTransform
                    : manager.transform;
                hands.Add(new HandRay { Manager = manager, Origin = origin, Line = BuildLine(manager.HandType) });
            }
        }

        private LineRenderer BuildLine(HandType handType)
        {
            if (lineMaterial == null)
            {
                // Vertex-colored unlit: renders the fade in URP and HDRP alike.
                lineMaterial = PipelineMaterialGuard.VertexColorUnlit();
            }
            var go = new GameObject($"PointingRay_{handType}");
            go.transform.SetParent(transform, false);
            var line = go.AddComponent<LineRenderer>();
            line.material = lineMaterial;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = rayWidth;
            line.endWidth = rayWidth * 0.4f;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.enabled = false;
            return line;
        }

        private void UpdateRay(HandRay hand)
        {
            if (hand.Line == null) return;
            hand.Line.enabled = hand.Pointing;
            if (!hand.Pointing) return;

            var origin = hand.Origin.position;
            var direction = hand.Origin.forward;
            var distance = rayLength;
            var usable = false;

            if (Physics.Raycast(origin, direction, out var hit, rayLength))
            {
                distance = hit.distance;
                usable = (physicsHoverMask.value & 1 << hit.collider.gameObject.layer) != 0
                         || hit.collider.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.IXRInteractable>() != null
                         || hit.collider.GetComponentInParent<Seat>() != null
                         || hit.collider.GetComponentInParent<PickableObject>() != null;
            }

            var color = usable ? usableColor : idleColor;
            hand.Line.startColor = color;
            hand.Line.endColor = new Color(color.r, color.g, color.b, 0f); // fades out with distance
            hand.Line.SetPosition(0, origin);
            hand.Line.SetPosition(1, origin + direction * distance);
        }
    }
}
