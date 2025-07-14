using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using LitMotion;
using System;
using System.Collections;
using System.Linq;
using Volume = UnityEngine.Rendering.Volume;

namespace jeanf.universalplayer
{
    public class FadeMask : MonoBehaviour, IDebugBehaviour
    {
        public enum FadeType 
        { 
            Loading,    // Black fade for scene loading
            HeadInWall  // Saturation fade for collision detection
        }

        public bool isDebug
        {
            get => _isDebug;
            set => _isDebug = value;
        }

        [SerializeField] private bool _isDebug = false;
        private static bool _isDebugSTATIC = false;
        [SerializeField] private bool checkForDebugChangeState = false;

        [FormerlySerializedAs("_inputBinding")]
        [Header("Manual Switch Input")]
        [Tooltip("Input to manually switch the fade state")]
        [SerializeField]
        private InputAction inputBinding = new InputAction();

        [Space(10)] [Header("Fade Settings")] [SerializeField]
        private static float _fadeTime = .2f;

        private static Color color = new Color(0, 0, 0, 0);
        private static MotionHandle motionHandle;

        private static readonly int FadeColor = Shader.PropertyToID("_Color");

        private static Material _shaderMaterial;
        private static bool _isFaded = false;

        [Header("Volume Profiles - Only assign the one for your current pipeline")]
        [SerializeField] private VolumeProfile volumeProfile;
        [SerializeField] private Volume postProcessVolume;
        private static Volume staticPostProcessVolume;
        
        private static object hdrpColorAdjustments;
        private static object urpColorAdjustments;

        private static MotionHandle _fadeHandle;
        private static bool _isCurrentlyFading = false;
        private static bool _targetState = false;
        private static FadeType _currentFadeType = FadeType.Loading;

        [Header("Listening On")] [SerializeField]
        private BoolFloatEventChannelSO fadeOutChannelSO;

        public delegate void TogglePpeDelegate(bool state);

        public static TogglePpeDelegate TogglePPE;

        private enum RenderPipeline
        {
            BuiltIn,
            URP,
            HDRP,
            Unknown
        }

        private static RenderPipeline _currentPipeline = RenderPipeline.Unknown;

        private void Awake()
        {
            DetectRenderPipeline();
            SetupVolumeProfile();
            SetVolumeTo_FadeToBlack();
            FadeValue(false, .5f);
        }

        private void DetectRenderPipeline()
        {
            if (GraphicsSettings.defaultRenderPipeline == null)
            {
                _currentPipeline = RenderPipeline.BuiltIn;
                return;
            }

            var renderingAssetType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
            
            if (renderingAssetType.Contains("HDRenderPipelineAsset"))
            {
                _currentPipeline = RenderPipeline.HDRP;
            }
            else if (renderingAssetType.Contains("UniversalRenderPipelineAsset"))
            {
                _currentPipeline = RenderPipeline.URP;
            }
            else
            {
                _currentPipeline = RenderPipeline.Unknown;
            }

            if (_isDebugSTATIC) Debug.Log($"FadeMask: Detected render pipeline: {_currentPipeline}");
        }

        private void SetupVolumeProfile()
        {
            if (postProcessVolume == null)
            {
                if (_isDebugSTATIC) Debug.LogError("FadeMask: postProcessVolume is not assigned!");
                return;
            }

            if (volumeProfile != null)
            {
                postProcessVolume.profile = volumeProfile;
            }
            else if (postProcessVolume.profile == null)
            {
                if (_isDebugSTATIC) Debug.LogError("FadeMask: No volume profile assigned and postProcessVolume has no profile!");
                return;
            }

            if (_currentPipeline == RenderPipeline.BuiltIn)
            {
                if (_isDebugSTATIC) Debug.LogWarning("FadeMask: Built-in pipeline detected. Volume system may not be available.");
                return;
            }

            postProcessVolume.blendDistance = 10.0f;

            staticPostProcessVolume = postProcessVolume;

            if (TryGetColorAdjustments()) return;
            if (_isDebugSTATIC) Debug.LogError("FadeMask: ColorAdjustments component not found in volume profile!");
        }

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

                    case RenderPipeline.BuiltIn:
                        break;
                    case RenderPipeline.Unknown:
                        break;
                    default:
                        if (_isDebugSTATIC) Debug.LogError($"FadeMask: Unsupported pipeline: {_currentPipeline}");
                        break;
                }
                
                return false;
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
                System.Type hdrpColorAdjustmentsType = System.Type.GetType("UnityEngine.Rendering.HighDefinition.ColorAdjustments, Unity.RenderPipelines.HighDefinition.Runtime");
                
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
                System.Type urpColorAdjustmentsType = System.Type.GetType("UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");
                
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

        private void OnEnable() => Subscribe();
        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Subscribe()
        {
            inputBinding.Enable();
            inputBinding.performed += _ => SwitchFadeState();
            if (fadeOutChannelSO != null)
                fadeOutChannelSO.OnEventRaised += FadeValue;
            TogglePPE += ChangePostProcessing;
        }

        private void Unsubscribe()
        {
            if (_shaderMaterial) _shaderMaterial.SetColor(FadeColor, color);
            inputBinding.performed -= null;
            if (fadeOutChannelSO != null)
                fadeOutChannelSO.OnEventRaised -= FadeValue;
            inputBinding.Disable();
            DisableFadeHandle();
            TogglePPE -= ChangePostProcessing;
        }

        public static void SetVolumeTo_FadeToBlack()
        {
            if (_isDebugSTATIC) Debug.Log($"FadeMask: Setting initial setup (black) for {_currentPipeline}");
            
            switch (_currentPipeline)
            {
                case RenderPipeline.HDRP:
                    if (hdrpColorAdjustments != null)
                    {
                        SetColorAdjustmentProperty(hdrpColorAdjustments, "colorFilter", Color.black);
                        SetColorAdjustmentProperty(hdrpColorAdjustments, "saturation", 0f);
                    }
                    else if (_isDebugSTATIC) Debug.LogWarning("FadeMask: HDRP ColorAdjustments is null.");
                    break;
                
                case RenderPipeline.URP:
                    if (urpColorAdjustments != null)
                    {
                        SetColorAdjustmentProperty(urpColorAdjustments, "colorFilter", Color.black);
                        SetColorAdjustmentProperty(urpColorAdjustments, "saturation", 0f);
                    }
                    else if (_isDebugSTATIC) Debug.LogWarning("FadeMask: URP ColorAdjustments is null.");
                    break;

                case RenderPipeline.BuiltIn:
                    break;
                case RenderPipeline.Unknown:
                    break;
                default:
                    if (_isDebugSTATIC) Debug.LogWarning($"FadeMask: Cannot set initial setup for pipeline: {_currentPipeline}");
                    break;
            }
        }

        public static void SetVolumeTo_FadeSaturation()
        {
            if (_isDebugSTATIC) Debug.Log($"FadeMask: Setting head in wall setup (gray) for {_currentPipeline}");
            
            switch (_currentPipeline)
            {
                case RenderPipeline.HDRP:
                    if (hdrpColorAdjustments != null)
                    {
                        SetColorAdjustmentProperty(hdrpColorAdjustments, "colorFilter", Color.white);
                        SetColorAdjustmentProperty(hdrpColorAdjustments, "saturation", -100f);
                    }
                    else if (_isDebugSTATIC) Debug.LogWarning("FadeMask: HDRP ColorAdjustments is null.");
                    break;
                
                case RenderPipeline.URP:
                    if (urpColorAdjustments != null)
                    {
                        SetColorAdjustmentProperty(urpColorAdjustments, "colorFilter", Color.white);
                        SetColorAdjustmentProperty(urpColorAdjustments, "saturation", -100f);
                    }
                    else if (_isDebugSTATIC) Debug.LogWarning("FadeMask: URP ColorAdjustments is null.");
                    break;

                case RenderPipeline.BuiltIn:
                    break;
                case RenderPipeline.Unknown:
                    break;
                default:
                    if (_isDebugSTATIC) Debug.LogWarning($"FadeMask: Cannot set head in wall setup for pipeline: {_currentPipeline}");
                    break;
            }
        }

        private static void SetColorAdjustmentProperty(object colorAdjustments, string propertyName, object value)
        {
            if (colorAdjustments == null) return;

            try
            {
                var colorAdjustmentsType = colorAdjustments.GetType();
                var parametersProperty = colorAdjustmentsType.GetProperty("parameters");
                if (parametersProperty == null) return;
                var parameters = parametersProperty.GetValue(colorAdjustments);

                if (parameters is not IEnumerable parametersEnumerable) return;
                
                var parametersList = new System.Collections.Generic.List<object>();
                foreach (var param in parametersEnumerable)
                {
                    if (param != null) parametersList.Add(param);
                }
                
                object targetParameter = null;
                
                switch (propertyName)
                {
                    case "colorFilter":
                    {
                        foreach (var param in parametersList)
                        {
                            if (param.GetType().Name != "ColorParameter") continue;
                            targetParameter = param;
                            break;
                        }

                        break;
                    }
                    case "saturation" when parametersList.Count > 4 && parametersList[4].GetType().Name == "ClampedFloatParameter":
                        targetParameter = parametersList[4];
                        break;
                    case "saturation":
                    {
                        foreach (var param in parametersList)
                        {
                            if (param.GetType().Name != "ClampedFloatParameter") continue;
                            var paramType = param.GetType();
                            var minProp = paramType.GetProperty("min");
                            var maxProp = paramType.GetProperty("max");
                            if (minProp?.GetValue(param)?.ToString() != "-100" ||
                                maxProp?.GetValue(param)?.ToString() != "100") continue;
                            targetParameter = param;
                            break;
                        }

                        break;
                    }
                }

                if (targetParameter == null) return;
                var parameterType = targetParameter.GetType();
                var valueProperty = parameterType.GetProperty("value");

                if (valueProperty == null) return;
                valueProperty.SetValue(targetParameter, value);
                if (_isDebugSTATIC) Debug.Log($"FadeMask: Set {propertyName} to {value}");
            }
            catch (Exception e)
            {
                if (_isDebugSTATIC) Debug.LogError($"FadeMask: Error setting {propertyName}: {e.Message}");
            }
        }

        private void Update()
        {
            if (!checkForDebugChangeState) return;
            _isDebugSTATIC = _isDebug;
        }

        public static void SwitchFadeState()
        {
            _isFaded = !_isFaded;
            FadeValue(_isFaded);
        }

        private void ChangePostProcessing(bool isInitComplete)
        {
            SetVolumeTo_FadeToBlack();
        }
        public static void FadeValue(bool value)
        {
            FadeValue(value, FadeType.Loading);
        }

        public static void FadeValue(bool value, float fadeTime)
        {
            FadeValue(value, fadeTime, FadeType.Loading);
        }

        public static void FadeValue(bool value, FadeType fadeType)
        {
            FadeValue(value, _fadeTime, fadeType);
        }

        public static void FadeValue(bool value, float fadeTime, FadeType fadeType)
        {
            if (staticPostProcessVolume == null)
            {
                if (_isDebugSTATIC) Debug.LogWarning("FadeMask: staticPostProcessVolume is null. Make sure FadeMask.Awake() has been called.");
                return;
            }

            if (value)
            {
                _currentFadeType = fadeType;
                switch (fadeType)
                {
                    case FadeType.Loading:
                        SetVolumeTo_FadeToBlack(); // Black fade
                        break;
                    case FadeType.HeadInWall:
                        SetVolumeTo_FadeSaturation(); // Saturation fade
                        break;
                }
                
                if (_isDebugSTATIC) Debug.Log($"FadeMask: Setting up {fadeType} fade");
            }

            if (_isCurrentlyFading && _targetState == value)
                return;

            if (_isDebugSTATIC) Debug.Log($"FadeMask: Fading to {value} in {fadeTime}s with {fadeType} style");

            if (_fadeHandle.IsActive())
            {
                _fadeHandle.Cancel();
            }

            _targetState = value;
            _isCurrentlyFading = true;
            float targetAlpha = value ? 1 : 0;
            var volume = staticPostProcessVolume;

            _fadeHandle = LMotion.Create(volume.weight, targetAlpha, fadeTime)
                .WithOnComplete(() => _isCurrentlyFading = false)
                .Bind(x => volume.weight = x);
        }

        private void DisableFadeHandle()
        {
            if (!_fadeHandle.IsActive()) return;
            _fadeHandle.Complete();
            _fadeHandle.Cancel();
        }

        private void OnValidate()
        {
            if (postProcessVolume == null)
            {
                Debug.LogWarning("FadeMask: postProcessVolume is not assigned!");
            }
        }
    }
}