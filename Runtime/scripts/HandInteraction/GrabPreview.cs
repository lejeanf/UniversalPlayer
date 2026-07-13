using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Grab preview for VR (ships wired on the Player prefab, zero setup): hovering a
    /// grabbable highlights it (subtle base-color tint via MaterialPropertyBlock —
    /// pipeline-agnostic, non-destructive) and, when the object carries a
    /// PoseContainer, shows a translucent ghost hand posed exactly the way the grab
    /// will look, at the attach point. Interactors and hand sides are discovered from
    /// the player hierarchy; the ghost rig reuses the pose editor's PreviewHand.
    /// </summary>
    public class GrabPreview : MonoBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [Tooltip("Master switch for the whole grab preview (highlight + ghost hand).")]
        [SerializeField] private bool previewEnabled = true;

        [Header("Highlight")]
        [SerializeField] private bool highlightEnabled = true;
        [SerializeField] private Color highlightColor = new Color(0.55f, 0.85f, 1f);
        [Tooltip("How far the base color is pushed toward the highlight color while hovered.")]
        [Range(0f, 1f)][SerializeField] private float highlightStrength = 0.45f;

        [Header("Ghost hand")]
        [SerializeField] private bool ghostHandEnabled = true;
        [Tooltip("Translucent material for the ghost hands (the packaged GhostHands material). Empty keeps the hands' own materials.")]
        [SerializeField] private Material ghostMaterial;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private sealed class HighlightEntry
        {
            public readonly List<Renderer> Renderers = new List<Renderer>();
            public int Count;
        }

        private readonly Dictionary<XRBaseInteractor, HandType> interactorHands = new Dictionary<XRBaseInteractor, HandType>();
        private readonly Dictionary<XRBaseInteractable, HighlightEntry> highlighted = new Dictionary<XRBaseInteractable, HighlightEntry>();
        private readonly Dictionary<HandType, PreviewHand> ghosts = new Dictionary<HandType, PreviewHand>();
        private readonly Dictionary<HandType, Vector3> ghostNaturalScale = new Dictionary<HandType, Vector3>();
        // Each ghost lives under its own UNIT-SCALE anchor that follows the grabbable's
        // attach transform — parenting under a scaled grabbable would stretch/shear it.
        private readonly Dictionary<HandType, Transform> ghostAnchors = new Dictionary<HandType, Transform>();
        private readonly Dictionary<HandType, Transform> ghostFollowTargets = new Dictionary<HandType, Transform>();
        private readonly Dictionary<HandType, XRBaseInteractable> ghostTargets = new Dictionary<HandType, XRBaseInteractable>();
        private readonly List<XRBaseInteractor> subscribedInteractors = new List<XRBaseInteractor>();
        private bool ghostsBuilt;

        private void OnEnable()
        {
            foreach (var interactor in GetComponentsInChildren<XRBaseInteractor>(true))
            {
                interactor.hoverEntered.AddListener(OnHoverEntered);
                interactor.hoverExited.AddListener(OnHoverExited);
                interactor.selectEntered.AddListener(OnSelectEntered);
                subscribedInteractors.Add(interactor);
            }
            foreach (var poseManager in GetComponentsInChildren<HandPoseManager>(true))
            {
                if (poseManager.targetInteractor != null)
                    interactorHands[poseManager.targetInteractor] = poseManager.HandType;
            }
            BroadcastControlsStatus.SendControlScheme += OnControlSchemeChanged;
        }

        private void OnDisable()
        {
            foreach (var interactor in subscribedInteractors)
            {
                if (interactor == null) continue;
                interactor.hoverEntered.RemoveListener(OnHoverEntered);
                interactor.hoverExited.RemoveListener(OnHoverExited);
                interactor.selectEntered.RemoveListener(OnSelectEntered);
            }
            subscribedInteractors.Clear();
            interactorHands.Clear();
            BroadcastControlsStatus.SendControlScheme -= OnControlSchemeChanged;
            HideAll();
        }

        private void OnControlSchemeChanged(BroadcastControlsStatus.ControlScheme scheme)
        {
            if (scheme != BroadcastControlsStatus.ControlScheme.XR) HideAll();
        }

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (args.interactableObject is XRBaseInteractable interactable)
                ShowPreview(interactable, HandFor(args.interactorObject as XRBaseInteractor));
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (args.interactableObject is XRBaseInteractable interactable)
                HidePreview(interactable, HandFor(args.interactorObject as XRBaseInteractor));
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // The grab happened: the real hand takes over from the preview.
            if (args.interactableObject is XRBaseInteractable interactable)
                HidePreview(interactable, HandFor(args.interactorObject as XRBaseInteractor));
        }

        private HandType HandFor(XRBaseInteractor interactor)
        {
            return interactor != null && interactorHands.TryGetValue(interactor, out var hand) ? hand : HandType.Right;
        }

        /// <summary>Public seam (also used by tests and the bench): preview a grabbable for the given hand.</summary>
        public void ShowPreview(XRBaseInteractable interactable, HandType handType)
        {
            if (!previewEnabled || interactable == null) return;
            if (BroadcastControlsStatus.controlScheme != BroadcastControlsStatus.ControlScheme.XR) return;
            if (!(interactable is XRGrabInteractable grab)) return;

            if (highlightEnabled) AddHighlight(interactable);

            if (ghostHandEnabled && !interactable.isSelected
                && interactable.TryGetComponent(out PoseContainer poseContainer) && poseContainer.pose != null)
            {
                EnsureGhostsBuilt();
                if (ghosts.TryGetValue(handType, out var ghost) && ghost != null
                    && ghostAnchors.TryGetValue(handType, out var anchor))
                {
                    var attach = grab.attachTransform != null ? grab.attachTransform : interactable.transform;
                    ghostFollowTargets[handType] = attach;
                    anchor.SetPositionAndRotation(attach.position, attach.rotation);
                    ghost.gameObject.SetActive(true);
                    ghost.ApplyPoseForSetup(poseContainer.pose);
                    ghostTargets[handType] = interactable;
                }
            }
        }

        /// <summary>Public seam: stop previewing a grabbable for the given hand.</summary>
        public void HidePreview(XRBaseInteractable interactable, HandType handType)
        {
            if (interactable == null) return;
            RemoveHighlight(interactable);
            if (ghostTargets.TryGetValue(handType, out var target) && target == interactable)
            {
                ghostTargets.Remove(handType);
                ghostFollowTargets.Remove(handType);
                if (ghosts.TryGetValue(handType, out var ghost) && ghost != null)
                    ghost.gameObject.SetActive(false);
            }
        }

        private void HideAll()
        {
            foreach (var entry in highlighted.Values)
            {
                foreach (var handRenderer in entry.Renderers)
                {
                    if (handRenderer != null) handRenderer.SetPropertyBlock(null);
                }
            }
            highlighted.Clear();
            ghostTargets.Clear();
            ghostFollowTargets.Clear();
            foreach (var ghost in ghosts.Values)
            {
                if (ghost == null) continue;
                ghost.gameObject.SetActive(false);
            }
        }

        private void LateUpdate()
        {
            // Anchors mirror the attach transforms' position/rotation (never scale),
            // so ghosts stay glued to moving grabbables without inheriting distortion.
            foreach (var pair in ghostFollowTargets)
            {
                if (pair.Value == null) continue;
                if (ghostAnchors.TryGetValue(pair.Key, out var anchor) && anchor != null)
                    anchor.SetPositionAndRotation(pair.Value.position, pair.Value.rotation);
            }
        }

        // ---- highlight (MaterialPropertyBlock: non-destructive, works on URP/HDRP Lit and legacy) ----

        private void AddHighlight(XRBaseInteractable interactable)
        {
            if (!highlighted.TryGetValue(interactable, out var entry))
            {
                entry = new HighlightEntry();
                foreach (var itemRenderer in interactable.transform.GetComponentsInChildren<Renderer>())
                {
                    if (itemRenderer.GetComponentInParent<PreviewHand>() != null) continue; // never tint our own ghost
                    var tinted = Color.Lerp(BaseColorOf(itemRenderer), highlightColor, highlightStrength);
                    var block = new MaterialPropertyBlock();
                    block.SetColor(BaseColorId, tinted);
                    block.SetColor(ColorId, tinted);
                    itemRenderer.SetPropertyBlock(block);
                    entry.Renderers.Add(itemRenderer);
                }
                highlighted[interactable] = entry;
            }
            entry.Count++;
        }

        private void RemoveHighlight(XRBaseInteractable interactable)
        {
            if (!highlighted.TryGetValue(interactable, out var entry)) return;
            entry.Count--;
            if (entry.Count > 0) return;
            foreach (var itemRenderer in entry.Renderers)
            {
                if (itemRenderer != null) itemRenderer.SetPropertyBlock(null);
            }
            highlighted.Remove(interactable);
        }

        private static float SafeDivide(float a, float b) => Mathf.Approximately(b, 0f) ? 1f : a / b;

        private static Color BaseColorOf(Renderer itemRenderer)
        {
            var material = itemRenderer.sharedMaterial;
            if (material != null && material.HasProperty(BaseColorId)) return material.GetColor(BaseColorId);
            if (material != null && material.HasProperty(ColorId)) return material.GetColor(ColorId);
            return Color.white;
        }

        // ---- ghost hands (the pose editor's PreviewHand rig, re-skinned translucent) ----

        private void EnsureGhostsBuilt()
        {
            if (ghostsBuilt) return;
            ghostsBuilt = true;

            var helperPrefab = Resources.Load<GameObject>("PoseHelper");
            if (helperPrefab == null)
            {
                Debug.LogWarning($"{LogPrefix} GrabPreview on '{name}': PoseHelper not found in Resources — ghost hand " +
                    "previews are disabled (highlight still works). Was Runtime/Hands/Prefabs/Resources/PoseHelper.prefab moved?", this);
                return;
            }

            var helper = Instantiate(helperPrefab, transform);
            helper.name = "GrabPreviewGhosts";
            foreach (var hand in helper.GetComponentsInChildren<PreviewHand>(true))
            {
                foreach (var handRenderer in hand.GetComponentsInChildren<Renderer>(true))
                {
                    if (ghostMaterial != null)
                    {
                        var materials = new Material[handRenderer.sharedMaterials.Length];
                        for (var i = 0; i < materials.Length; i++) materials[i] = ghostMaterial;
                        handRenderer.sharedMaterials = materials;
                    }
                    handRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                foreach (var handCollider in hand.GetComponentsInChildren<Collider>(true)) Destroy(handCollider);

                ghostNaturalScale[hand.HandType] = hand.transform.lossyScale;

                // Unit-world-scale anchor per hand: the ghost parents here once and the
                // anchor follows the grabbable's attach transform (position/rotation only).
                var anchor = new GameObject($"GhostAnchor_{hand.HandType}").transform;
                anchor.SetParent(transform, false);
                var rootScale = transform.lossyScale;
                anchor.localScale = new Vector3(SafeDivide(1f, rootScale.x), SafeDivide(1f, rootScale.y), SafeDivide(1f, rootScale.z));
                ghostAnchors[hand.HandType] = anchor;

                hand.transform.SetParent(anchor, false);
                hand.transform.localScale = ghostNaturalScale[hand.HandType];
                hand.gameObject.SetActive(false);
                ghosts[hand.HandType] = hand;
            }
            Destroy(helper);

            if (ghosts.Count == 0)
                Debug.LogWarning($"{LogPrefix} GrabPreview on '{name}': PoseHelper contains no PreviewHand — ghost previews disabled.", this);
        }
    }
}
