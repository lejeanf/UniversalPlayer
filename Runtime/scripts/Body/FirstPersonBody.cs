using System.Collections.Generic;
using System.Linq;
using jeanf.EventSystem;
using jeanf.validationTools;
using UnityEngine;

namespace jeanf.universalplayer
{
    /// <summary>
    /// True first-person body for the M&amp;K / gamepad modes: look down and see your own
    /// torso, legs and feet. Hidden in XR and FreeCam. See Documentation~/true-first-person-body.md.
    ///
    /// Works out of the box with a procedural primitive mannequin (no art assets shipped);
    /// projects assign a rigged humanoid Animator on their Player variant instead, and the
    /// driver switches to feeding that animator's parameters (Speed, NormalizedSpeed, MoveX,
    /// MoveY, IsSprinting, CrouchBlend, IsGrounded) and hiding its head bone.
    /// </summary>
    public class FirstPersonBody : MonoBehaviour, IDebugBehaviour
    {
        private const string LogPrefix = "[UniversalPlayer]";

        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }
        [SerializeField] private bool _isDebug = false;

        [Tooltip("Master switch: disable to skip the body entirely — nothing is instantiated and no per-frame work runs (performance, or projects that simply do not want a visible body). Can be toggled at runtime via BodyEnabled.")]
        [SerializeField] private bool bodyEnabled = true;

        [Tooltip("Source of speed / grounded / crouch state. Required.")]
        [Validation("PlayerMovement is required — it feeds the body's speed / grounded / crouch state. The body cannot animate without it.")]
        [SerializeField] private PlayerMovement playerMovement;
        [Tooltip("The CameraOffset transform — the body yaws toward its look direction.")]
        [Validation("Camera offset is required — the body yaws toward its look direction. Assign the CameraOffset transform.")]
        [SerializeField] private Transform cameraOffset;

        [Header("Project body (assign on your Player variant)")]
        [Tooltip("Rigged character on a child of this object. When assigned, bodyPrefab and the placeholder are ignored.")]
        [SerializeField] private Animator bodyAnimator;
        [Tooltip("Instantiated under this object at startup when no bodyAnimator is assigned. The package ships its template character here; variants can swap or clear it.")]
        [SerializeField] private GameObject bodyPrefab;
        [Tooltip("Assigned to the body's Animator at startup when it has no controller of its own.")]
        [SerializeField] private RuntimeAnimatorController bodyController;
        [Tooltip("Scale the head bone to zero so it never clips the camera. Humanoid rigs use the avatar; Generic rigs fall back to finding a bone named like 'head'.")]
        [SerializeField] private bool hideHeadBone = true;
        [Tooltip("Ground speed (m/s) of the fastest movement cycle in the animator. Moving faster than this scales animation playback up (MotionSpeed parameter) so the feet keep matching the floor. The Setup Template Body tool measures and sets this.")]
        [SerializeField] private float fastestCycleGroundSpeed = 3f;

        [Header("Placeholder mannequin (used while no Animator is assigned)")]
        [SerializeField] private bool showPlaceholder = true;
        [SerializeField] private float bodyHeight = 1.65f;
        [Tooltip("How far the body sits behind the camera axis, so looking down shows the chest instead of the inside of the torso.")]
        [SerializeField] private float bodyBackOffset = 0.08f;
        [Tooltip("Walk cycles per meter — matched to FpsCameraFeel.bobCyclesPerMeter so the steps land on the bob dips.")]
        [SerializeField] private float cyclesPerMeter = 0.45f;
        [SerializeField] private float legSwingDegrees = 35f;
        [SerializeField] private float armSwingDegrees = 20f;
        [Range(0.4f, 1f)][SerializeField] private float crouchBodyScale = 0.65f;

        [Header("Yaw follow")]
        [Tooltip("While standing still the body ignores camera yaw until the twist exceeds this angle.")]
        [SerializeField] private float idleYawDeadZoneDegrees = 45f;
        [SerializeField] private float turnDegreesPerSecond = 360f;

        private Transform placeholderRoot;
        private Transform hipL, hipR, shoulderL, shoulderR;
        private float swingPhase;
        private float swingBlend;
        private bool seated;
        private float seatedBlend;
        private bool visible = true;
        private HashSet<string> animatorParams;
        private bool missingParamsWarned;
        private bool missingRefsWarned;

        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int NormalizedSpeedParam = Animator.StringToHash("NormalizedSpeed");
        private static readonly int MoveXParam = Animator.StringToHash("MoveX");
        private static readonly int MoveYParam = Animator.StringToHash("MoveY");
        private static readonly int IsSprintingParam = Animator.StringToHash("IsSprinting");
        private static readonly int CrouchBlendParam = Animator.StringToHash("CrouchBlend");
        private static readonly int IsGroundedParam = Animator.StringToHash("IsGrounded");
        private static readonly int IsSeatedParam = Animator.StringToHash("IsSeated");
        private static readonly int MotionSpeedParam = Animator.StringToHash("MotionSpeed");

        /// <summary>Set by the SitController: plays the sit pose (placeholder) or drives the IsSeated animator parameter.</summary>
        public void SetSeated(bool value) => seated = value;
        public bool Seated => seated;

        private BodyHandSupportIk _handSupportIk;

        /// <summary>
        /// The real hand bone, for items held in the hand on M&amp;K/gamepad
        /// (PlayerItemAnchors' Animated Bone attach mode). Null when the body is off,
        /// not yet built, or not a humanoid rig — the caller docks to the view instead.
        /// Parenting to the bone is enough to ride the animation; no IK is involved, so
        /// this never contends with the sit-support IK (which owns the only IK goal).
        /// </summary>
        public Transform GetHandBone(HandType hand)
        {
            if (!bodyEnabled || bodyAnimator == null || !bodyAnimator.isHuman || hand == HandType.None) return null;
            return bodyAnimator.GetBoneTransform(hand == HandType.Left
                ? HumanBodyBones.LeftHand
                : HumanBodyBones.RightHand);
        }

        /// <summary>True when <see cref="GetHandBone"/> can actually return a bone — the editor uses it to warn before play.</summary>
        public bool HasHumanoidHands => bodyEnabled && bodyAnimator != null && bodyAnimator.isHuman;

        /// <summary>
        /// Reaches the right hand toward a support point (a chair back) with the
        /// given weight — driven frame-by-frame by the sit/stand transition.
        /// Null anchor or weight 0 releases the hand back to the animation.
        /// </summary>
        public void SetHandSupport(Transform anchor, float weight)
        {
            if (bodyAnimator == null) return; // placeholder mannequin: no IK rig
            if (_handSupportIk == null)
            {
                _handSupportIk = bodyAnimator.GetComponent<BodyHandSupportIk>();
                if (_handSupportIk == null) _handSupportIk = bodyAnimator.gameObject.AddComponent<BodyHandSupportIk>();
            }
            _handSupportIk.SetHandTarget(anchor, weight);
        }

        /// <summary>
        /// Turn the whole body feature on/off. Off before the first frame means the body
        /// prefab is never instantiated at all; turning on later builds it on demand.
        /// </summary>
        public bool BodyEnabled
        {
            get => bodyEnabled;
            set
            {
                if (bodyEnabled == value) return;
                bodyEnabled = value;
                if (value) EnsureBodyBuilt();
                ApplyVisibility(ShouldBeVisible(), force: true);
            }
        }

        private void Start()
        {
            if (!bodyEnabled) return;
            EnsureBodyBuilt();
            ApplyVisibility(ShouldBeVisible(), force: true);
        }

        private void EnsureBodyBuilt()
        {
            if (bodyAnimator != null || placeholderRoot != null) return;
            if (bodyAnimator == null && bodyPrefab != null)
            {
                var instance = Instantiate(bodyPrefab, transform);
                instance.name = bodyPrefab.name;
                bodyAnimator = instance.GetComponentInChildren<Animator>(true);
                if (bodyAnimator == null)
                {
                    Debug.LogWarning($"{LogPrefix} FirstPersonBody on '{name}': bodyPrefab '{bodyPrefab.name}' contains " +
                        "no Animator — falling back to the placeholder mannequin. Add an Animator to the body prefab " +
                        "(the model's rig provides one automatically when it is part of the prefab).", this);
                    Destroy(instance);
                }
            }

            if (bodyAnimator != null && bodyAnimator.runtimeAnimatorController == null && bodyController != null)
            {
                bodyAnimator.runtimeAnimatorController = bodyController;
            }
            if (bodyAnimator != null)
            {
                // The CharacterController owns ALL movement. Root motion from the clips
                // (Mixamo cycles travel) would carry the body away from the player.
                bodyAnimator.applyRootMotion = false;
            }

            if (bodyAnimator == null && showPlaceholder)
            {
                placeholderRoot = PlaceholderMannequin.Build(transform, bodyHeight, bodyBackOffset,
                    out hipL, out hipR, out shoulderL, out shoulderR);
            }
        }

        private void LateUpdate()
        {
            if (playerMovement == null)
            {
                if (!missingRefsWarned)
                {
                    missingRefsWarned = true;
                    Debug.LogWarning($"{LogPrefix} FirstPersonBody on '{name}': playerMovement is not assigned — " +
                        "the body cannot follow locomotion and stays hidden. Wire the PlayerMovement component " +
                        "from the Player prefab (or variant).", this);
                    ApplyVisibility(false, force: true);
                }
                return;
            }

            ApplyVisibility(ShouldBeVisible());
            if (!visible) return;

            var dt = Time.deltaTime;
            if (dt <= 0f) return;

            UpdateYaw(dt);

            if (bodyAnimator != null)
            {
                DriveAnimator();
                if (hideHeadBone) HideHead();
                // Absolute guarantee against drift: root motion is written to the
                // ANIMATOR's transform (not the wrapper), so pin the whole chain from
                // the animator up to the Body node back to identity after it evaluated.
                for (var node = bodyAnimator.transform; node != null && node != transform; node = node.parent)
                {
                    node.localPosition = Vector3.zero;
                    node.localRotation = Quaternion.identity;
                }
            }
            else if (placeholderRoot != null)
            {
                AnimatePlaceholder(dt);
            }
        }

        private bool ShouldBeVisible()
        {
            if (!bodyEnabled) return false;
            var scheme = BroadcastControlsStatus.controlScheme;
            return scheme == BroadcastControlsStatus.ControlScheme.KeyboardMouse
                || scheme == BroadcastControlsStatus.ControlScheme.Gamepad;
        }

        private void ApplyVisibility(bool shouldShow, bool force = false)
        {
            if (!force && shouldShow == visible) return;
            visible = shouldShow;

            var target = bodyAnimator != null ? bodyAnimator.transform : placeholderRoot;
            if (target != null && target.gameObject.activeSelf != shouldShow)
            {
                target.gameObject.SetActive(shouldShow);
                if (isDebug) Debug.Log($"{LogPrefix} first-person body {(shouldShow ? "shown" : "hidden")} " +
                    $"(scheme: {BroadcastControlsStatus.controlScheme})", this);
            }
        }

        private void UpdateYaw(float dt)
        {
            if (cameraOffset == null) return;

            var currentYaw = transform.localEulerAngles.y;
            var targetYaw = cameraOffset.localEulerAngles.y;
            var delta = Mathf.DeltaAngle(currentYaw, targetYaw);

            if (seated)
            {
                // Seated the body stays planted facing the seat; only the head (camera) looks around.
                transform.localRotation = Quaternion.Euler(0f,
                    Mathf.MoveTowardsAngle(currentYaw, 0f, turnDegreesPerSecond * dt), 0f);
                return;
            }

            var moving = playerMovement.PlanarVelocity.magnitude > 0.2f;
            if (!moving && Mathf.Abs(delta) < idleYawDeadZoneDegrees) return;

            var step = turnDegreesPerSecond * dt;
            transform.localRotation = Quaternion.Euler(0f,
                Mathf.MoveTowardsAngle(currentYaw, targetYaw, step), 0f);
        }

        private void DriveAnimator()
        {
            if (bodyAnimator.runtimeAnimatorController == null)
            {
                WarnMissingParamsOnce("it has NO AnimatorController assigned");
                return;
            }

            if (animatorParams == null)
            {
                animatorParams = new HashSet<string>(bodyAnimator.parameters.Select(p => p.name));
                var expected = new[] { "Speed", "NormalizedSpeed", "MoveX", "MoveY", "IsSprinting", "CrouchBlend", "IsGrounded", "IsSeated", "MotionSpeed" };
                var missing = expected.Where(p => !animatorParams.Contains(p)).ToArray();
                if (missing.Length > 0)
                    WarnMissingParamsOnce($"its controller lacks the parameter(s): {string.Join(", ", missing)}");
            }

            if (animatorParams.Contains("Speed")) bodyAnimator.SetFloat(SpeedParam, playerMovement.ActualPlanarVelocity.magnitude);
            if (animatorParams.Contains("NormalizedSpeed")) bodyAnimator.SetFloat(NormalizedSpeedParam, playerMovement.NormalizedSpeed);
            // MoveX/MoveY = planar velocity in the BODY's local space in REAL m/s — the
            // blend tree anchors sit at each clip's natural ground speed, so feet match
            // the floor at any velocity in between. Faster than the fastest cycle, the
            // MotionSpeed multiplier scales playback so the stride keeps up.
            var localVelocity = transform.InverseTransformDirection(playerMovement.ActualPlanarVelocity);
            if (animatorParams.Contains("MoveX")) bodyAnimator.SetFloat(MoveXParam, localVelocity.x);
            if (animatorParams.Contains("MoveY")) bodyAnimator.SetFloat(MoveYParam, localVelocity.z);
            if (animatorParams.Contains("MotionSpeed"))
            {
                var planarSpeed = playerMovement.ActualPlanarVelocity.magnitude;
                var reference = Mathf.Max(0.5f, fastestCycleGroundSpeed);
                bodyAnimator.SetFloat(MotionSpeedParam, planarSpeed <= reference ? 1f : planarSpeed / reference);
            }
            if (animatorParams.Contains("IsSprinting")) bodyAnimator.SetBool(IsSprintingParam, playerMovement.IsSprinting);
            if (animatorParams.Contains("CrouchBlend")) bodyAnimator.SetFloat(CrouchBlendParam, playerMovement.CrouchBlend);
            if (animatorParams.Contains("IsGrounded")) bodyAnimator.SetBool(IsGroundedParam, DebouncedGrounded(Time.deltaTime));
            if (animatorParams.Contains("IsSeated")) bodyAnimator.SetBool(IsSeatedParam, seated);
        }

        private void WarnMissingParamsOnce(string problem)
        {
            if (missingParamsWarned) return;
            missingParamsWarned = true;
            Debug.LogWarning($"{LogPrefix} FirstPersonBody on '{name}': bodyAnimator '{bodyAnimator.name}' is assigned but {problem}. " +
                "The body will not animate (or only partially). Expected float/bool parameters: Speed, NormalizedSpeed, " +
                "MoveX, MoveY, IsSprinting, CrouchBlend, IsGrounded — see Documentation~/true-first-person-body.md.", this);
        }

        private Transform headBone;
        private bool headSearchDone;
        private float ungroundedSeconds;

        /// <summary>
        /// CharacterController.isGrounded flickers false for single frames while walking
        /// (steps, slopes) — raw, it made the jump/airborne animation fire at random.
        /// Grounded is reported immediately; airborne only after a short debounce.
        /// </summary>
        private bool DebouncedGrounded(float dt)
        {
            if (playerMovement.IsGrounded)
            {
                ungroundedSeconds = 0f;
                return true;
            }
            ungroundedSeconds += dt;
            return ungroundedSeconds < 0.15f;
        }

        private void HideHead()
        {
            if (!headSearchDone)
            {
                headSearchDone = true;
                headBone = bodyAnimator.isHuman
                    ? bodyAnimator.GetBoneTransform(HumanBodyBones.Head)
                    : FindHeadByName(bodyAnimator.transform);
                if (headBone == null)
                {
                    Debug.LogWarning($"{LogPrefix} FirstPersonBody on '{name}': no head bone found on " +
                        $"'{bodyAnimator.name}' (Generic rig without a child named like 'head') — the head will clip " +
                        "the camera. Import the model as Humanoid, or make sure the head bone name contains 'head'.", this);
                }
            }
            if (headBone != null) headBone.localScale = Vector3.one * 0.0001f;
        }

        private static Transform FindHeadByName(Transform root)
        {
            Transform fallback = null;
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                var childName = child.name.ToLowerInvariant();
                if (childName == "cc_base_head" || childName == "head") return child;
                if (fallback == null && childName.EndsWith("head")) fallback = child;
            }
            return fallback;
        }

        private void AnimatePlaceholder(float dt)
        {
            var speed = playerMovement.ActualPlanarVelocity.magnitude;
            var walking = !seated && playerMovement.IsGrounded && speed > 0.1f;

            seatedBlend = Mathf.MoveTowards(seatedBlend, seated ? 1f : 0f, dt * 4f);
            swingBlend = Mathf.MoveTowards(swingBlend, walking ? 1f : 0f, dt * 6f);
            if (walking) swingPhase += speed * cyclesPerMeter * dt * Mathf.PI * 2f;
            else if (swingBlend <= 0f) swingPhase = 0f;

            var swing = Mathf.Sin(swingPhase) * swingBlend * Mathf.Lerp(0.7f, 1.3f, playerMovement.NormalizedSpeed);

            // Seated: thighs fold forward (hip pivots to -90°), arms rest slightly forward.
            hipL.localRotation = Quaternion.Euler(Mathf.Lerp(swing * legSwingDegrees, -90f, seatedBlend), 0f, 0f);
            hipR.localRotation = Quaternion.Euler(Mathf.Lerp(-swing * legSwingDegrees, -90f, seatedBlend), 0f, 0f);
            shoulderL.localRotation = Quaternion.Euler(Mathf.Lerp(-swing * armSwingDegrees, -20f, seatedBlend), 0f, 0f);
            shoulderR.localRotation = Quaternion.Euler(Mathf.Lerp(swing * armSwingDegrees, -20f, seatedBlend), 0f, 0f);

            // Crouch: squash the whole mannequin from the feet up. Crude, but it is a placeholder.
            var scaleY = Mathf.Lerp(1f, crouchBodyScale, playerMovement.CrouchBlend);
            placeholderRoot.localScale = new Vector3(1f, scaleY, 1f);

            // Seated the player root sits AT the seat (hips), so the mannequin sinks until
            // its hips coincide with the root and the legs stretch out at seat height.
            var hipHeight = 0.85f * (bodyHeight / 1.65f);
            var basePosition = new Vector3(0f, 0f, -bodyBackOffset);
            placeholderRoot.localPosition = basePosition + Vector3.down * (hipHeight * seatedBlend);
        }

        /// <summary>
        /// Headless primitive mannequin: torso + two swinging legs and arms, feet at the
        /// node origin. No colliders (they would fight the CharacterController and the
        /// crouch headroom check), pipeline-appropriate Lit material found at runtime.
        /// </summary>
        internal static class PlaceholderMannequin
        {
            internal static Transform Build(Transform parent, float height, float backOffset,
                out Transform hipL, out Transform hipR, out Transform shoulderL, out Transform shoulderR)
            {
                var s = height / 1.65f;
                var material = CreateBodyMaterial();

                var root = new GameObject("PlaceholderBody").transform;
                root.SetParent(parent, false);
                root.localPosition = new Vector3(0f, 0f, -backOffset);

                // Torso: shoulders at ~1.4, hips at ~0.85
                Part(root, material, "Torso",
                    new Vector3(0f, 1.125f * s, 0f), new Vector3(0.34f, 0.62f, 0.22f) * s);

                hipL = Pivot(root, "Hip_L", new Vector3(-0.09f * s, 0.85f * s, 0f));
                hipR = Pivot(root, "Hip_R", new Vector3(0.09f * s, 0.85f * s, 0f));
                Part(hipL, material, "Leg_L", new Vector3(0f, -0.425f * s, 0f), new Vector3(0.13f, 0.85f, 0.13f) * s);
                Part(hipR, material, "Leg_R", new Vector3(0f, -0.425f * s, 0f), new Vector3(0.13f, 0.85f, 0.13f) * s);

                shoulderL = Pivot(root, "Shoulder_L", new Vector3(-0.23f * s, 1.4f * s, 0f));
                shoulderR = Pivot(root, "Shoulder_R", new Vector3(0.23f * s, 1.4f * s, 0f));
                Part(shoulderL, material, "Arm_L", new Vector3(0f, -0.3f * s, 0f), new Vector3(0.09f, 0.6f, 0.09f) * s);
                Part(shoulderR, material, "Arm_R", new Vector3(0f, -0.3f * s, 0f), new Vector3(0.09f, 0.6f, 0.09f) * s);

                return root;
            }

            private static Transform Pivot(Transform parent, string name, Vector3 localPosition)
            {
                var pivot = new GameObject(name).transform;
                pivot.SetParent(parent, false);
                pivot.localPosition = localPosition;
                return pivot;
            }

            private static void Part(Transform parent, Material material, string name, Vector3 localPosition, Vector3 localScale)
            {
                var part = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                part.name = name;
                // Immediate: a deferred Destroy would leave the collider alive for one frame,
                // long enough to shove the CharacterController it spawns inside of.
                Object.DestroyImmediate(part.GetComponent<Collider>());
                part.transform.SetParent(parent, false);
                part.transform.localPosition = localPosition;
                part.transform.localScale = localScale;
                part.GetComponent<Renderer>().sharedMaterial = material;
            }

            private static Material CreateBodyMaterial()
            {
                var pipeline = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var shaderName = pipeline == null ? "Standard"
                    : pipeline.GetType().FullName.Contains("Universal") ? "Universal Render Pipeline/Lit"
                    : "HDRP/Lit";
                var shader = Shader.Find(shaderName);
                if (shader == null)
                {
                    Debug.LogWarning($"{LogPrefix} FirstPersonBody: shader '{shaderName}' not found for the placeholder " +
                        "mannequin — it will render with Unity's error shader. Assign a real body Animator on your variant.");
                    return new Material(Shader.Find("Hidden/InternalErrorShader"));
                }
                var material = new Material(shader) { name = "PlaceholderBody (runtime)" };
                var grey = new Color(0.45f, 0.47f, 0.5f);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", grey);
                else if (material.HasProperty("_Color")) material.SetColor("_Color", grey);
                return material;
            }
        }
    }
}
