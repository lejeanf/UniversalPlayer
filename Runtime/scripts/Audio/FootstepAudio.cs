using System.Collections.Generic;
using jeanf.validationTools;
using UnityEngine;
using UnityEngine.Audio;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Surface-aware footstep + friction audio, driven directly off <see cref="PlayerMovement"/>
    /// state (grounded, real velocity, crouch, momentum) instead of the boundary event
    /// channels — the old project-side footstep wiring silently died whenever the channel
    /// asset was not hand-wired, and a bool "isMoving" cannot express cadence or friction.
    ///
    /// Two layers:
    /// 1. Footsteps — distance-based stepping (a step every strideLength meters actually
    ///    travelled), so cadence follows walk/sprint/crouch speed for free. The ground
    ///    surface is resolved by raycasting the collider under the feet and matching its
    ///    tag against the surface profiles.
    /// 2. Scuffs — shoe-on-floor friction when momentum is fought: an abrupt turn or hard
    ///    stop shows up as horizontal acceleration that opposes travel; past a threshold
    ///    the surface's scuff resource plays, louder the faster the player was going.
    ///    Desktop modes only: XRI locomotion snaps velocity, so every VR stick reversal
    ///    would squeak.
    /// </summary>
    public class FootstepAudio : MonoBehaviour, IValidatable
    {
        private const string LogPrefix = "[UniversalPlayer]";

        [System.Serializable]
        public class SurfaceProfile
        {
            [Tooltip("Ground collider tag this profile applies to (e.g. 'Concrete').")]
            public string surfaceTag;
            [Tooltip("Played each step — an AudioClip or an Audio Random Container.")]
            public AudioResource footsteps;
            [Tooltip("Played on abrupt turns/stops (shoe friction). Optional — silent when empty.")]
            public AudioResource scuffs;
            [Range(0f, 2f)] public float volume = 1f;
        }

        [Header("Debug")]
        [Tooltip("Log every step, scuff, surface change and why sounds are skipped.")]
        [SerializeField] private bool isDebug = false;

        [Header("Wiring")]
        [Validation("PlayerMovement is required — footsteps are driven off its grounded/velocity state.")]
        [SerializeField] private PlayerMovement movement;
        [Validation("An AudioSource is required — there is nothing to play footsteps through without it.")]
        [SerializeField] private AudioSource footstepSource;
        [Tooltip("Optional second source so a scuff can ring over a footstep; falls back to the footstep source.")]
        [SerializeField] private AudioSource scuffSource;

        [Header("Surfaces (matched by ground collider tag, first match wins)")]
        [SerializeField] private List<SurfaceProfile> surfaces = new List<SurfaceProfile>();
        [Tooltip("Used when no profile matches the ground tag — and the packaged default, so assign at least this one.")]
        [SerializeField] private SurfaceProfile defaultSurface = new SurfaceProfile();

        [Header("Ground detection")]
        [Tooltip("Layers probed for ground. Leave as Nothing for automatic: every layer the player's capsule collides with (the same set it can stand on).")]
        [SerializeField] private LayerMask groundLayerOverride;
        [Tooltip("How far below the feet to look for the ground collider.")]
        [SerializeField] private float rayDistance = 0.5f;
        [Tooltip("How long a detected surface is trusted before raycasting again.")]
        [SerializeField] private float surfaceCacheSeconds = 0.2f;

        [Header("Stepping")]
        [Tooltip("Meters travelled per footstep. Cadence follows speed automatically (sprint = faster steps).")]
        [SerializeField] private float strideLength = 0.75f;
        [Tooltip("Sprinting lengthens the stride a little — real runs take longer strides, not just faster ones.")]
        [SerializeField] private float sprintStrideMultiplier = 1.25f;
        [Tooltip("Crouching shortens the stride.")]
        [SerializeField] private float crouchStrideMultiplier = 0.7f;
        [Tooltip("Footstep volume multiplier when fully crouched (sneaking is quiet).")]
        [Range(0f, 1f)] [SerializeField] private float crouchVolume = 0.4f;
        [Tooltip("Below this speed (m/s) the player is shuffling, not stepping — no footsteps.")]
        [SerializeField] private float minStepSpeed = 0.2f;
        [Tooltip("Each step offsets the source this far (m) left/right of center — the L/R foot alternation. Needs a 3D (spatialized) source.")]
        [SerializeField] private float stepStereoSeparation = 0.25f;

        [Header("Landing")]
        [Tooltip("Play a footstep when the feet hit the ground after a jump or fall.")]
        [SerializeField] private bool stepOnLanding = true;
        [Tooltip("Minimum downward speed (m/s) on touchdown that counts as a landing (a step off a curb should not thump).")]
        [SerializeField] private float minLandingFallSpeed = 3f;

        [Header("Friction scuffs (abrupt turns & hard stops, desktop modes)")]
        [Tooltip("Horizontal acceleration (m/s²) opposing travel that counts as friction. Compare with PlayerMovement.speedChangeRate (default 8): braking/turning at full rate crosses this, gentle corrections do not.")]
        [SerializeField] private float scuffAccelThreshold = 6f;
        [Tooltip("No scuff below this speed (m/s) — sits above a gentle stroll so only committed movement squeaks.")]
        [SerializeField] private float scuffMinSpeed = 1.6f;
        [Tooltip("Minimum seconds between scuffs.")]
        [SerializeField] private float scuffCooldown = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float scuffVolume = 0.8f;

        // gates mirrored from PlayerEvents (locomotion freezes must also silence feet)
        private bool _paused;
        private bool _menuOpen;
        private bool _sceneLoading;

        private float _strideAccumulated;
        private bool _stepIsLeft;
        private float _footstepSourceLocalY;

        private bool _wasGrounded = true;
        private float _prevVerticalVelocity;
        private Vector3 _prevCommandedVelocity;
        private float _lastScuffTime = float.NegativeInfinity;

        private string _cachedSurfaceTag;
        private SurfaceProfile _cachedProfile;
        private float _lastSurfaceCheckTime = float.NegativeInfinity;
        private bool _warnedNoSounds;

        /// <summary>Diagnostics: the tag of the last surface resolved under the feet (null before the first step).</summary>
        public string CurrentSurfaceTag => _cachedSurfaceTag;

        /// <summary>True when at least one footstep resource is assigned (default or any surface) — without one EVERY step is silent.</summary>
        public bool HasAnyFootstepSound
        {
            get
            {
                if (defaultSurface != null && defaultSurface.footsteps != null) return true;
                for (var i = 0; i < surfaces.Count; i++)
                    if (surfaces[i] != null && surfaces[i].footsteps != null) return true;
                return false;
            }
        }

        /// <summary>True when at least one scuff resource is assigned — without one the friction layer (abrupt turns / hard stops) is silent.</summary>
        public bool HasAnyScuffSound
        {
            get
            {
                if (defaultSurface != null && defaultSurface.scuffs != null) return true;
                for (var i = 0; i < surfaces.Count; i++)
                    if (surfaces[i] != null && surfaces[i].scuffs != null) return true;
                return false;
            }
        }

        /// <summary>Every non-empty surface profile tag — validation checks each against the project's tag list (an undefined tag can never match a floor).</summary>
        public IEnumerable<string> ConfiguredSurfaceTags
        {
            get
            {
                for (var i = 0; i < surfaces.Count; i++)
                    if (surfaces[i] != null && !string.IsNullOrEmpty(surfaces[i].surfaceTag))
                        yield return surfaces[i].surfaceTag;
            }
        }

        /// <summary>
        /// Wired AND audible. The packaged prefab intentionally ships without sounds
        /// (they are project audio, assigned on the Player variant), so it reports
        /// invalid until the variant assigns at least one footstep resource — the
        /// validation banner is the loud version of the runtime one-shot warning.
        /// </summary>
        public bool IsValid => movement != null && footstepSource != null && HasAnyFootstepSound;

        private void Awake()
        {
            if (footstepSource != null) _footstepSourceLocalY = footstepSource.transform.localPosition.y;
        }

        private void OnEnable()
        {
            PlayerEvents.PauseRequested += OnPauseRequested;
            PlayerEvents.MenuStateChanged += OnMenuStateChanged;
            PlayerEvents.SceneLoadingChanged += OnSceneLoadingChanged;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            PlayerEvents.PauseRequested -= OnPauseRequested;
            PlayerEvents.MenuStateChanged -= OnMenuStateChanged;
            PlayerEvents.SceneLoadingChanged -= OnSceneLoadingChanged;
        }

        private void OnPauseRequested(bool paused) => _paused = paused;
        private void OnMenuStateChanged(bool open) => _menuOpen = open;
        private void OnSceneLoadingChanged(bool loading) => _sceneLoading = loading;

        private void Update()
        {
            if (movement == null || footstepSource == null) return;

            var grounded = movement.IsGrounded;
            var verticalVelocity = movement.VerticalVelocity;
            var commandedVelocity = movement.PlanarVelocity;

            if (_paused || _menuOpen || _sceneLoading || movement.LocomotionLocked
                || BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.Freecam)
            {
                // Frozen or flying: silence and forget momentum so nothing fires on resume.
                _strideAccumulated = 0f;
                RememberFrame(grounded, verticalVelocity, commandedVelocity);
                return;
            }

            if (stepOnLanding && grounded && !_wasGrounded && _prevVerticalVelocity < -minLandingFallSpeed)
            {
                if (isDebug) Debug.Log($"{LogPrefix} FootstepAudio: landed at {-_prevVerticalVelocity:F1} m/s — landing step.", this);
                _strideAccumulated = 0f;
                PlayFootstep();
            }

            if (grounded)
            {
                DetectScuff(commandedVelocity, Time.deltaTime);

                var speed = movement.ActualPlanarVelocity.magnitude;
                if (speed >= minStepSpeed)
                {
                    _strideAccumulated += speed * Time.deltaTime;
                    if (_strideAccumulated >= CurrentStrideLength())
                    {
                        _strideAccumulated = 0f;
                        PlayFootstep();
                    }
                }
                else
                {
                    // Standing (or pushing a wall): never bank a full step for the next start.
                    _strideAccumulated = Mathf.Min(_strideAccumulated, CurrentStrideLength() * 0.5f);
                }
            }
            else
            {
                _strideAccumulated = 0f; // feet are off the ground
            }

            RememberFrame(grounded, verticalVelocity, commandedVelocity);
        }

        private void RememberFrame(bool grounded, float verticalVelocity, Vector3 commandedVelocity)
        {
            _wasGrounded = grounded;
            _prevVerticalVelocity = verticalVelocity;
            _prevCommandedVelocity = commandedVelocity;
        }

        private float CurrentStrideLength()
        {
            var stride = strideLength;
            if (movement.IsSprinting) stride *= sprintStrideMultiplier;
            stride *= Mathf.Lerp(1f, crouchStrideMultiplier, movement.CrouchBlend);
            return Mathf.Max(0.05f, stride);
        }

        /// <summary>
        /// Friction = horizontal acceleration that fights the current travel direction
        /// (braking or turning), measured on the COMMANDED velocity — it ramps at
        /// PlayerMovement.speedChangeRate, so "player yanked the stick the other way"
        /// reads as a clean, frame-rate-independent spike. Acceleration ALONG travel
        /// (speeding up) is excluded: starts never scuff.
        /// </summary>
        private void DetectScuff(Vector3 commandedVelocity, float dt)
        {
            var scheme = BroadcastControlsStatus.controlScheme;
            if (scheme != BroadcastControlsStatus.ControlScheme.KeyboardMouse
                && scheme != BroadcastControlsStatus.ControlScheme.Gamepad) return;
            if (dt <= 0f) return;

            var referenceSpeed = _prevCommandedVelocity.magnitude;
            if (referenceSpeed < scuffMinSpeed) return;

            var acceleration = (commandedVelocity - _prevCommandedVelocity) / dt;
            var travel = _prevCommandedVelocity / referenceSpeed;
            var along = Vector3.Dot(acceleration, travel);
            var friction = (acceleration - Mathf.Max(0f, along) * travel).magnitude;
            if (friction < scuffAccelThreshold) return;
            if (Time.time - _lastScuffTime < scuffCooldown) return;

            _lastScuffTime = Time.time;
            PlayScuff(referenceSpeed);
        }

        private void PlayFootstep()
        {
            var profile = ResolveSurfaceProfile();
            var resource = profile.footsteps != null ? profile.footsteps : defaultSurface.footsteps;
            if (resource == null)
            {
                WarnOnceNoSounds();
                return;
            }

            if (footstepSource.resource != resource) footstepSource.resource = resource;

            // Fake L/R feet: alternate the (3D) source around the head center each step.
            _stepIsLeft = !_stepIsLeft;
            var local = footstepSource.transform.localPosition;
            footstepSource.transform.localPosition =
                new Vector3(_stepIsLeft ? -stepStereoSeparation : stepStereoSeparation, _footstepSourceLocalY, local.z);

            footstepSource.volume = profile.volume * Mathf.Lerp(1f, crouchVolume, movement.CrouchBlend);
            if (footstepSource.isPlaying) footstepSource.Stop(); // keep the cadence crisp at sprint speed
            footstepSource.Play();

            if (isDebug) Debug.Log($"{LogPrefix} FootstepAudio: step on '{_cachedSurfaceTag ?? "default"}' " +
                $"({(_stepIsLeft ? "L" : "R")}, volume {footstepSource.volume:F2})", this);
        }

        private void PlayScuff(float speed)
        {
            var profile = ResolveSurfaceProfile();
            var resource = profile.scuffs != null ? profile.scuffs : defaultSurface.scuffs;
            if (resource == null)
            {
                if (isDebug) Debug.Log($"{LogPrefix} FootstepAudio: scuff detected on '{_cachedSurfaceTag ?? "default"}' but that surface has no scuff resource.", this);
                return;
            }

            var source = scuffSource != null ? scuffSource : footstepSource;
            if (source.resource != resource) source.resource = resource;
            // The faster the player was going, the harder the shoe bites.
            source.volume = profile.volume * scuffVolume * Mathf.Clamp(speed / 4f, 0.3f, 1f)
                            * Mathf.Lerp(1f, crouchVolume, movement.CrouchBlend);
            if (source.isPlaying) source.Stop();
            source.Play();

            if (isDebug) Debug.Log($"{LogPrefix} FootstepAudio: scuff on '{_cachedSurfaceTag ?? "default"}' at {speed:F1} m/s (volume {source.volume:F2})", this);
        }

        private SurfaceProfile ResolveSurfaceProfile()
        {
            if (Time.time - _lastSurfaceCheckTime < surfaceCacheSeconds && _cachedProfile != null)
                return _cachedProfile;

            _lastSurfaceCheckTime = Time.time;

            var mask = groundLayerOverride.value != 0 ? groundLayerOverride.value : movement.CollidableLayers();
            var origin = movement.FootPosition + Vector3.up * 0.1f;
            if (mask != 0 && Physics.Raycast(origin, Vector3.down, out var hit, rayDistance + 0.1f, mask,
                    QueryTriggerInteraction.Ignore))
            {
                var tag = hit.collider.tag;
                if (tag != _cachedSurfaceTag && isDebug)
                    Debug.Log($"{LogPrefix} FootstepAudio: surface changed '{_cachedSurfaceTag ?? "none"}' -> '{tag}' ('{hit.collider.name}')", this);
                _cachedSurfaceTag = tag;
                _cachedProfile = FindProfile(tag);
            }
            else
            {
                _cachedSurfaceTag = null;
                _cachedProfile = defaultSurface;
            }
            return _cachedProfile;
        }

        private SurfaceProfile FindProfile(string surfaceTag)
        {
            for (var i = 0; i < surfaces.Count; i++)
            {
                var profile = surfaces[i];
                if (profile != null && !string.IsNullOrEmpty(profile.surfaceTag) && profile.surfaceTag == surfaceTag)
                    return profile;
            }
            return defaultSurface;
        }

        private void WarnOnceNoSounds()
        {
            if (_warnedNoSounds) return;
            _warnedNoSounds = true;
            Debug.LogWarning($"{LogPrefix} FootstepAudio on '{name}': no footstep AudioResource is assigned " +
                "(neither the matched surface profile nor the default surface has one) — footsteps are SILENT. " +
                "Assign AudioClips or Audio Random Containers on your Player variant's FootstepAudio " +
                "(the packaged prefab intentionally ships without sounds; sample containers live in the AudioSystems package samples).", this);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void OnDrawGizmosSelected()
        {
            if (movement == null) return;
            Gizmos.color = Color.yellow;
            var origin = movement.FootPosition + Vector3.up * 0.1f;
            Gizmos.DrawRay(origin, Vector3.down * (rayDistance + 0.1f));
        }
    }
}
