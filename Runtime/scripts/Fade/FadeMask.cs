using UnityEngine;
using UnityEngine.Rendering;
using LitMotion;
using System;
using System.Collections;
using System.Linq;
using jeanf.validationTools;
using Volume = UnityEngine.Rendering.Volume;

namespace jeanf.universalplayer
{
    public class FadeMask : MonoBehaviour, IValidatable
    {
        [Header("Debug")]
        [SerializeField] private bool _isDebug = false;
        private static bool _isDebugSTATIC = false;
        [SerializeField] private bool checkForDebugChangeState = false;

        [Header("Fade Settings")]
        [SerializeField] private float _fadeTimeInstance = 0.2f;
        private static float _fadeTime = 0.2f;

        [Header("Volume Setup")]
        [SerializeField] private VolumeProfile volumeProfile;
        [Validation("The Volume used for fades is required — without it every fade (loading, head-in-wall) silently no-ops.")]
        [SerializeField] private Volume postProcessVolume;

        /// <summary>
        /// Editor validation: the volume + a profile must be assigned, and the
        /// profile must contain the ColorAdjustments of the ACTIVE pipeline —
        /// a URP profile in an HDRP project (or vice-versa) fades nothing.
        /// Invalid = orange in the hierarchy/inspector AND the build FAILS
        /// (SceneValidationOnBuild throws on invalid IValidatable components).
        /// </summary>
        public bool IsValid
        {
            get
            {
                if (postProcessVolume == null) return false;
                var profile = volumeProfile != null ? volumeProfile : postProcessVolume.sharedProfile;
                return ProfileMatchesActivePipeline(profile);
            }
        }

        /// <summary>The volume profile this FadeMask will fade with (explicit field first, else the Volume's own).</summary>
        public VolumeProfile EffectiveProfile => volumeProfile != null
            ? volumeProfile
            : postProcessVolume != null ? postProcessVolume.sharedProfile : null;

        /// <summary>
        /// True when the profile carries the ColorAdjustments of the ACTIVE
        /// render pipeline. The single source of truth for every guardrail
        /// (IsValid, ValidateSetup, the inspector's one-click fix).
        /// </summary>
        public static bool ProfileMatchesActivePipeline(VolumeProfile profile)
        {
            if (profile == null) return false;
            var pipelineAsset = GraphicsSettings.defaultRenderPipeline;
            if (pipelineAsset == null) return true; // built-in: nothing further to judge
            var expectedNamespace = pipelineAsset.GetType().ToString().Contains("HDRenderPipelineAsset")
                ? "UnityEngine.Rendering.HighDefinition"
                : "UnityEngine.Rendering.Universal";

            foreach (var component in profile.components)
                if (component != null && component.GetType().FullName == $"{expectedNamespace}.ColorAdjustments")
                    return true;
            return false; // profile belongs to the other pipeline (or has no ColorAdjustments)
        }

        /// <summary>
        /// True when the camera's SRP volume mask includes the volume's layer.
        /// The classic silent killer: HDRP/URP cameras default to the 'Default'
        /// layer only, and a fade volume on e.g. the 'Player' layer is simply
        /// ignored — no fade, edit mode or play mode, with zero errors.
        /// </summary>
        public static bool CameraSeesFadeVolume(Camera camera, Volume volume)
        {
            if (!TryGetCameraVolumeMask(camera, volume, false, out _, out var mask)) return true; // unknown API: don't accuse
            return (mask & (1 << volume.gameObject.layer)) != 0;
        }

        /// <summary>
        /// Adds the volume's layer to the camera's SRP volume mask (adding the
        /// SRP camera-data component when missing). Returns the modified
        /// component so editor callers can persist it, or null when nothing
        /// needed changing.
        /// </summary>
        public static Component RepairCameraVolumeMask(Camera camera, Volume volume)
        {
            if (!TryGetCameraVolumeMask(camera, volume, true, out var data, out var mask)) return null;
            var bit = 1 << volume.gameObject.layer;
            if ((mask & bit) != 0) return null;

            var dataType = data.GetType();
            var field = dataType.GetField("volumeLayerMask");
            if (field != null) field.SetValue(data, (LayerMask)(mask | bit));
            else dataType.GetProperty("volumeLayerMask")?.SetValue(data, (LayerMask)(mask | bit));

            Debug.LogWarning($"FadeMask: the camera '{camera.name}' volume mask did not include layer " +
                $"'{LayerMask.LayerToName(volume.gameObject.layer)}' where the fade volume lives — every fade was being " +
                "ignored. The mask was repaired; apply/save it on your Player variant to make it permanent " +
                "(Tools/UniversalPlayer/ValidateSetup verifies it).", camera);
            return data;
        }

        private static bool TryGetCameraVolumeMask(Camera camera, Volume volume, bool addIfMissing, out Component data, out int mask)
        {
            data = null;
            mask = 0;
            if (camera == null || volume == null) return false;

            var pipelineAsset = GraphicsSettings.defaultRenderPipeline;
            if (pipelineAsset == null) return false; // built-in: volumes are not camera-masked
            var typeName = pipelineAsset.GetType().ToString().Contains("HDRenderPipelineAsset")
                ? "UnityEngine.Rendering.HighDefinition.HDAdditionalCameraData, Unity.RenderPipelines.HighDefinition.Runtime"
                : "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime";
            var dataType = Type.GetType(typeName);
            if (dataType == null) return false;

            data = camera.GetComponent(dataType);
            if (data == null)
            {
                if (!addIfMissing) { mask = 1; return true; } // no data component = default mask ('Default' layer only)
                data = camera.gameObject.AddComponent(dataType) as Component;
                if (data == null) return false;
            }

            var field = dataType.GetField("volumeLayerMask");
            if (field != null) { mask = ((LayerMask)field.GetValue(data)).value; return true; }
            var property = dataType.GetProperty("volumeLayerMask");
            if (property != null) { mask = ((LayerMask)property.GetValue(data)).value; return true; }
            data = null;
            return false;
        }

        /// <summary>Name prefix of the bundled profile matching the active pipeline ("URP" or "HDRP").</summary>
        public static string ActivePipelinePrefix()
        {
            var pipelineAsset = GraphicsSettings.defaultRenderPipeline;
            if (pipelineAsset == null) return null;
            return pipelineAsset.GetType().ToString().Contains("HDRenderPipelineAsset") ? "HDRP" : "URP";
        }
        
        private static Volume staticPostProcessVolume;
        private static object hdrpColorAdjustments;
        private static object urpColorAdjustments;
        
        private enum RenderPipeline { BuiltIn, URP, HDRP, Unknown }
        private static RenderPipeline _currentPipeline = RenderPipeline.Unknown;

        private enum VisualState { Loading, Clear, HeadInWall }
        private static VisualState _currentState = VisualState.Loading;

        // The main menu fades the world to black in EVERY mode. It is an overlay
        // on top of the requested base state: the base (loading/head-in-wall/
        // clear) is remembered and restored when the menu closes.
        private static bool _menuOpen;
        private static VisualState _requestedState = VisualState.Loading;
        private static bool _lastRaisedFade = true;

        /// <summary>True while the world is faded to black by loading/teleporting (menu-caused black excluded).</summary>
        public static bool ScreenFaded => _requestedState == VisualState.Loading;

        // Tells listeners (the cursor, notably) that the world went black or came
        // back — driven by the BASE state only: the menu overlay also blacks the
        // screen but its UI still needs a visible cursor.
        private static void RaiseFadeSignal()
        {
            var faded = ScreenFaded;
            if (faded == _lastRaisedFade) return;
            _lastRaisedFade = faded;
            PlayerEvents.RaiseScreenFade(faded);
        }
        
        private static MotionHandle _colorFilterHandle;
        private static MotionHandle _saturationHandle;

        private void Awake()
        {
            DetectRenderPipeline();
            SetupVolumeProfile();
            _fadeTime = _fadeTimeInstance;
            // Statics survive when Enter Play Mode skips the domain reload —
            // start every session from the canonical "loading, black, no menu"
            // state or a menu left open last session would pin the screen black.
            _menuOpen = false;
            _currentVisualColor = Color.black;
            _currentVisualSaturation = 0f;
            _lastRaisedFade = true; // subscribers pull ScreenFaded on their own init
            SetStateLoadingImmediate();
        }

        private void OnEnable() => PlayerEvents.MenuStateChanged += OnMenuStateChanged;
        private void OnDisable() => PlayerEvents.MenuStateChanged -= OnMenuStateChanged;

        private static void OnMenuStateChanged(bool open)
        {
            _menuOpen = open;
            if (staticPostProcessVolume == null) return;
            ApplyState();
        }

        private void Update()
        {
            if (!checkForDebugChangeState) return;
            _isDebugSTATIC = _isDebug;
        }

        #region Pipeline Detection & Setup

        private void DetectRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                _currentPipeline = RenderPipeline.BuiltIn;
                return;
            }

            var renderingAssetType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
            
            if (renderingAssetType.Contains("HDRenderPipelineAsset"))
                _currentPipeline = RenderPipeline.HDRP;
            else if (renderingAssetType.Contains("UniversalRenderPipelineAsset"))
                _currentPipeline = RenderPipeline.URP;
            else
                _currentPipeline = RenderPipeline.Unknown;

            if (_isDebugSTATIC) Debug.Log($"FadeMask: Detected render pipeline: {_currentPipeline}");
        }

        private void SetupVolumeProfile()
        {
            if (postProcessVolume == null)
            {
                Debug.LogError("FadeMask: postProcessVolume is not assigned — every fade (loading, head-in-wall) will silently no-op. " +
                    "Assign the Volume on the FadeMask component of the Player prefab (variant).", this);
                return;
            }

            if (volumeProfile != null)
                postProcessVolume.profile = Instantiate(volumeProfile);
            else if (postProcessVolume.profile == null)
            {
                Debug.LogError("FadeMask: no volume profile assigned (neither FadeMask.volumeProfile nor the Volume's profile) — " +
                    "fades will silently no-op. Assign the URP/HDRP FadeGlobalVolume Profile from Runtime/scripts/Fade/.", this);
                return;
            }

            if (_currentPipeline == RenderPipeline.BuiltIn)
            {
                if (_isDebugSTATIC) Debug.LogWarning("FadeMask: Built-in pipeline detected. Volume system may not be available.");
                return;
            }

            postProcessVolume.weight = 1f;
            postProcessVolume.blendDistance = 10.0f;
            // The fade must WIN: a scene color-grading volume at default priority
            // would otherwise fight the black screen with equal say.
            postProcessVolume.priority = 100f;
            staticPostProcessVolume = postProcessVolume;

            // Self-repair the other silent killer: a camera whose volume mask
            // excludes the fade volume's layer ignores it entirely.
            var fadeCamera = GetComponentInParent<Camera>();
            if (fadeCamera == null) fadeCamera = Camera.main;
            RepairCameraVolumeMask(fadeCamera, postProcessVolume);

            if (!TryGetColorAdjustments())
            {
                Debug.LogError($"FadeMask: no {_currentPipeline} ColorAdjustments found in profile '{postProcessVolume.profile.name}' — fades will silently no-op. " +
                    $"Most likely the profile belongs to the other pipeline: assign the {_currentPipeline} FadeGlobalVolume Profile " +
                    "(Runtime/scripts/Fade/) on this project's Player prefab variant.", this);
#if UNITY_EDITOR
                TryEditorProfileRecovery();
#endif
            }

            EnsureUrpPostProcessingEnabled();
        }

#if UNITY_EDITOR
        // EDITOR-ONLY safety net: when the assigned profile belongs to the other
        // pipeline, load the bundled profile for the ACTIVE pipeline so fades
        // still work this session. The prefab is NOT modified — the error above
        // (and the orange validation) still point at the permanent fix; builds
        // are protected separately (SceneValidationOnBuild fails the build).
        private void TryEditorProfileRecovery()
        {
            var prefix = ActivePipelinePrefix();
            if (prefix == null) return;

            foreach (var guid in UnityEditor.AssetDatabase.FindAssets($"{prefix} FadeGlobalVolume t:VolumeProfile"))
            {
                var path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var bundled = UnityEditor.AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (bundled == null || !ProfileMatchesActivePipeline(bundled)) continue;

                postProcessVolume.profile = Instantiate(bundled);
                if (!TryGetColorAdjustments()) continue;

                Debug.LogWarning($"FadeMask: EDITOR RECOVERY — fades run on the bundled '{bundled.name}' for this session only. " +
                    $"For a permanent fix (and for builds), assign that profile on the FadeMask of the Player variant " +
                    "(one-click button on the component).", this);
                return;
            }
        }
#endif

        private bool TryGetColorAdjustments()
        {
            try
            {
                switch (_currentPipeline)
                {
                    case RenderPipeline.HDRP:
                        return TryGetHDRPColorAdjustments();
                    case RenderPipeline.URP:
                        return TryGetURPColorAdjustments();
                    default:
                        return false;
                }
            }
            catch (Exception e)
            {
                if (_isDebugSTATIC) Debug.LogError($"FadeMask: Error getting ColorAdjustments: {e.Message}");
                return false;
            }
        }

        private bool TryGetHDRPColorAdjustments()
        {
            try
            {
                var hdrpColorAdjustmentsType = System.Type.GetType("UnityEngine.Rendering.HighDefinition.ColorAdjustments, Unity.RenderPipelines.HighDefinition.Runtime");
                
                if (hdrpColorAdjustmentsType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name.Contains("HighDefinition"))
                        {
                            hdrpColorAdjustmentsType = assembly.GetType("UnityEngine.Rendering.HighDefinition.ColorAdjustments");
                            if (hdrpColorAdjustmentsType != null) break;
                        }
                    }
                }
                
                if (hdrpColorAdjustmentsType == null)
                {
                    if (_isDebugSTATIC) Debug.LogError("FadeMask: HDRP ColorAdjustments type not found.");
                    return false;
                }
                
                var profileType = staticPostProcessVolume.profile.GetType();
                var tryGetMethods = profileType.GetMethods().Where(m => m.Name == "TryGet" && m.IsGenericMethodDefinition).ToArray();
                
                if (tryGetMethods.Length == 0)
                {
                    if (_isDebugSTATIC) Debug.LogError("FadeMask: No generic TryGet methods found on VolumeProfile");
                    return false;
                }
                
                var tryGetMethod = tryGetMethods[0].MakeGenericMethod(hdrpColorAdjustmentsType);
                var parameters = new object[] { null };
                var result = (bool)tryGetMethod.Invoke(staticPostProcessVolume.profile, parameters);
                
                if (result)
                {
                    hdrpColorAdjustments = parameters[0];
                    if (_isDebugSTATIC) Debug.Log("FadeMask: Successfully got HDRP ColorAdjustments");
                    return true;
                }
                else
                {
                    if (_isDebugSTATIC) Debug.LogError("FadeMask: Failed to get HDRP ColorAdjustments from volume profile");
                    return false;
                }
            }
            catch (Exception e)
            {
                if (_isDebugSTATIC) Debug.LogError($"FadeMask: Error getting HDRP ColorAdjustments: {e.Message}");
                return false;
            }
        }

        private bool TryGetURPColorAdjustments()
        {
            try
            {
                var urpColorAdjustmentsType = System.Type.GetType("UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");
                
                if (urpColorAdjustmentsType == null)
                {
                    foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
                    {
                        if (assembly.GetName().Name.Contains("Universal"))
                        {
                            urpColorAdjustmentsType = assembly.GetType("UnityEngine.Rendering.Universal.ColorAdjustments");
                            if (urpColorAdjustmentsType != null) break;
                        }
                    }
                }
                
                if (urpColorAdjustmentsType == null)
                {
                    if (_isDebugSTATIC) Debug.LogError("FadeMask: URP ColorAdjustments type not found.");
                    return false;
                }
                
                var profileType = staticPostProcessVolume.profile.GetType();
                var tryGetMethods = profileType.GetMethods().Where(m => m.Name == "TryGet" && m.IsGenericMethodDefinition).ToArray();
                
                if (tryGetMethods.Length == 0)
                {
                    if (_isDebugSTATIC) Debug.LogError("FadeMask: No generic TryGet methods found on VolumeProfile");
                    return false;
                }
                
                var tryGetMethod = tryGetMethods[0].MakeGenericMethod(urpColorAdjustmentsType);
                var parameters = new object[] { null };
                var result = (bool)tryGetMethod.Invoke(staticPostProcessVolume.profile, parameters);
                
                if (result)
                {
                    urpColorAdjustments = parameters[0];
                    if (_isDebugSTATIC) Debug.Log("FadeMask: Successfully got URP ColorAdjustments");
                    return true;
                }
                else
                {
                    if (_isDebugSTATIC) Debug.LogError("FadeMask: Failed to get URP ColorAdjustments from volume profile");
                    return false;
                }
            }
            catch (Exception e)
            {
                if (_isDebugSTATIC) Debug.LogError($"FadeMask: Error getting URP ColorAdjustments: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// URP only renders post-processing (and therefore the fade) when the camera has
        /// 'Post Processing' ticked, and a camera without a serialized
        /// UniversalAdditionalCameraData defaults to OFF — making every fade invisible
        /// while the volume data changes correctly. Reflection keeps this class free of a
        /// hard URP reference (HDRP needs no such switch).
        /// </summary>
        private void EnsureUrpPostProcessingEnabled()
        {
            if (_currentPipeline != RenderPipeline.URP) return;
            var mainCamera = Camera.main;
            if (mainCamera == null) return; // headless tests / camera spawns later

            try
            {
                var dataType = System.Type.GetType(
                    "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
                if (dataType == null) return;

                var cameraData = mainCamera.GetComponent(dataType);
                if (cameraData == null) cameraData = mainCamera.gameObject.AddComponent(dataType);
                var renderPostProcessing = dataType.GetProperty("renderPostProcessing");
                if (renderPostProcessing == null || (bool)renderPostProcessing.GetValue(cameraData)) return;

                renderPostProcessing.SetValue(cameraData, true);
                Debug.LogWarning($"FadeMask: 'Post Processing' was disabled on camera '{mainCamera.name}' so fades would be invisible — " +
                    "enabled it at runtime. Tick Rendering > Post Processing on the camera (prefab variant) to make this permanent.", mainCamera);
            }
            catch (Exception e)
            {
                Debug.LogError($"FadeMask: could not verify the camera's post-processing setting: {e.Message}");
            }
        }

        #endregion

        #region Public State Methods

        private static bool _warnedNoActiveFadeMask = false;
        private static bool FadeMaskNotReady()
        {
            if (staticPostProcessVolume != null) return false;
            if (!_warnedNoActiveFadeMask)
            {
                _warnedNoActiveFadeMask = true;
                Debug.LogWarning("FadeMask: a fade was requested but no FadeMask is set up in this scene — the screen will not fade. " +
                    "Add the Player prefab (variant), or a FadeMask with its Volume, to the scene.");
            }
            return true;
        }

        public static void SetStateLoading()
        {
            if (FadeMaskNotReady()) return;
            _requestedState = VisualState.Loading;
            ApplyState();
            RaiseFadeSignal();
        }

        public static void SetStateClear()
        {
            if (FadeMaskNotReady()) return;
            _requestedState = VisualState.Clear;
            ApplyState();
            RaiseFadeSignal();
        }

        public static void SetStateHeadInWall()
        {
            if (FadeMaskNotReady()) return;
            _requestedState = VisualState.HeadInWall;
            ApplyState();
            RaiseFadeSignal();
        }

        // An open menu wins over every base state; the base is re-applied when
        // the menu closes (e.g. still loading -> stays black; head in wall ->
        // back to desaturated; otherwise -> clear).
        private static void ApplyState() => TransitionToState(_menuOpen ? VisualState.Loading : _requestedState);

        private static void SetStateLoadingImmediate()
        {
            if (staticPostProcessVolume == null) return;
            SetColorAndSaturation(Color.black, 0f);
            _currentState = VisualState.Loading;
            _requestedState = VisualState.Loading;
        }

        #endregion

        #region State Transitions

        private static Color _currentVisualColor = Color.black;
        private static float _currentVisualSaturation = 0f;

        private static void TransitionToState(VisualState targetState)
        {
            if (_currentState == targetState) return;

            if (_isDebugSTATIC) Debug.Log($"FadeMask: Transitioning from {_currentState} to {targetState}");

            if (_colorFilterHandle.IsActive()) _colorFilterHandle.Cancel();
            if (_saturationHandle.IsActive()) _saturationHandle.Cancel();

            Color targetColor = targetState == VisualState.Loading ? Color.black : Color.white;
            float targetSaturation = targetState == VisualState.HeadInWall ? -100f : 0f;

            _colorFilterHandle = LMotion.Create(_currentVisualColor, targetColor, _fadeTime)
                .Bind(color => {
                    _currentVisualColor = color;
                    SetColorAndSaturation(color, null);
                });

            _saturationHandle = LMotion.Create(_currentVisualSaturation, targetSaturation, _fadeTime)
                .Bind(saturation => {
                    _currentVisualSaturation = saturation;
                    SetColorAndSaturation(null, saturation);
                });

            _currentState = targetState;
        }

        private static void SetColorAndSaturation(Color? color, float? saturation)
        {
            var colorAdjustments = _currentPipeline == RenderPipeline.URP ? urpColorAdjustments : hdrpColorAdjustments;
            if (colorAdjustments == null) return;

            if (color.HasValue)
                SetColorAdjustmentProperty(colorAdjustments, "colorFilter", color.Value);
            
            if (saturation.HasValue)
                SetColorAdjustmentProperty(colorAdjustments, "saturation", saturation.Value);
        }

        #endregion

        #region Property Getters & Setters

        private static Color GetCurrentColorFilter()
        {
            try
            {
                var colorAdjustments = _currentPipeline == RenderPipeline.URP ? urpColorAdjustments : hdrpColorAdjustments;
                if (colorAdjustments == null) return Color.white;
                
                var colorAdjustmentsType = colorAdjustments.GetType();
                var parametersProperty = colorAdjustmentsType.GetProperty("parameters");
                var parameters = parametersProperty?.GetValue(colorAdjustments);
                
                if (parameters is IEnumerable parametersEnumerable)
                {
                    foreach (var param in parametersEnumerable)
                    {
                        if (param?.GetType().Name == "ColorParameter")
                        {
                            var valueProperty = param.GetType().GetProperty("value");
                            return (Color)(valueProperty?.GetValue(param) ?? Color.white);
                        }
                    }
                }
            }
            catch { }
            
            return Color.white;
        }

        private static float GetCurrentSaturation()
        {
            try
            {
                var colorAdjustments = _currentPipeline == RenderPipeline.URP ? urpColorAdjustments : hdrpColorAdjustments;
                if (colorAdjustments == null) return 0f;
                
                var colorAdjustmentsType = colorAdjustments.GetType();
                var parametersProperty = colorAdjustmentsType.GetProperty("parameters");
                var parameters = parametersProperty?.GetValue(colorAdjustments);
                
                if (parameters is IEnumerable parametersEnumerable)
                {
                    foreach (var param in parametersEnumerable)
                    {
                        if (param?.GetType().Name == "ClampedFloatParameter")
                        {
                            var minProp = param.GetType().GetProperty("min");
                            var maxProp = param.GetType().GetProperty("max");
                            if (minProp?.GetValue(param)?.ToString() == "-100" &&
                                maxProp?.GetValue(param)?.ToString() == "100")
                            {
                                var valueProperty = param.GetType().GetProperty("value");
                                return (float)(valueProperty?.GetValue(param) ?? 0f);
                            }
                        }
                    }
                }
            }
            catch { }
            
            return 0f;
        }

        private static void SetColorAdjustmentProperty(object colorAdjustments, string propertyName, object value)
        {
            if (colorAdjustments == null)
            {
                Debug.LogError($"FadeMask: colorAdjustments is null for {propertyName}");
                return;
            }

            try
            {
                var colorAdjustmentsType = colorAdjustments.GetType();
                var parametersProperty = colorAdjustmentsType.GetProperty("parameters");
                if (parametersProperty == null)
                {
                    Debug.LogError($"FadeMask: parameters property not found");
                    return;
                }
                var parameters = parametersProperty.GetValue(colorAdjustments);

                if (parameters is not IEnumerable parametersEnumerable)
                {
                    Debug.LogError($"FadeMask: parameters is not enumerable");
                    return;
                }
                
                var parametersList = new System.Collections.Generic.List<object>();
                foreach (var param in parametersEnumerable)
                {
                    if (param != null) parametersList.Add(param);
                }
                
                object targetParameter = null;
                
                switch (propertyName)
                {
                    case "colorFilter":
                        foreach (var param in parametersList)
                        {
                            if (param.GetType().Name == "ColorParameter")
                            {
                                targetParameter = param;
                                break;
                            }
                        }
                        break;
                        
                    case "saturation":
                        if (parametersList.Count > 4 && parametersList[4].GetType().Name == "ClampedFloatParameter")
                        {
                            targetParameter = parametersList[4];
                        }
                        break;
                }

                if (targetParameter == null)
                {
                    Debug.LogError($"FadeMask: targetParameter not found for {propertyName}");
                    return;
                }
                
                var parameterType = targetParameter.GetType();

                var valueProperty = parameterType.GetProperty("value");
                if (valueProperty == null)
                {
                    Debug.LogError($"FadeMask: value property not found");
                    return;
                }
                
                valueProperty.SetValue(targetParameter, value);

                var overrideStateProperty = parameterType.GetProperty("overrideState");
                if (overrideStateProperty != null)
                {
                    overrideStateProperty.SetValue(targetParameter, true);
                }

                if (_currentPipeline == RenderPipeline.URP)
                {
                    var activeProperty = colorAdjustmentsType.GetProperty("active");
                    if (activeProperty != null)
                    {
                        activeProperty.SetValue(colorAdjustments, true);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"FadeMask: Error setting {propertyName}: {e.Message}\n{e.StackTrace}");
            }
        }

        #endregion

        private void OnValidate()
        {
            if (postProcessVolume == null)
            {
                Debug.LogWarning("FadeMask: postProcessVolume is not assigned!");
            }
        }
    }
}