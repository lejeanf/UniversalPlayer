using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using LitMotion;
using System;
using System.Linq;
using Volume = UnityEngine.Rendering.Volume;

namespace jeanf.universalplayer
{
    public class FadeMask : MonoBehaviour, IDebugBehaviour
    {
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
        
        // Use objects to handle both URP and HDRP ColorAdjustments via reflection
        private static object hdrpColorAdjustments;
        private static object urpColorAdjustments;

        private static MotionHandle _fadeHandle;
        private static bool _isCurrentlyFading = false;
        private static bool _targetState = false;

        [Header("Listening On")] [SerializeField]
        private BoolFloatEventChannelSO fadeOutChannelSO;

        public delegate void TogglePpeDelegate(bool state);

        public static TogglePpeDelegate TogglePPE;

        // Pipeline detection
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
            SetVolumeTo_InitialSetup();
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

            // Use the assigned volume profile if available
            if (volumeProfile != null)
            {
                postProcessVolume.profile = volumeProfile;
            }
            else if (postProcessVolume.profile == null)
            {
                if (_isDebugSTATIC) Debug.LogError("FadeMask: No volume profile assigned and postProcessVolume has no profile!");
                return;
            }

            // Handle built-in pipeline
            if (_currentPipeline == RenderPipeline.BuiltIn)
            {
                if (_isDebugSTATIC) Debug.LogWarning("FadeMask: Built-in pipeline detected. Volume system may not be available.");
                return;
            }

            // Set blend distance (same for all pipelines that support volumes)
            postProcessVolume.blendDistance = 10.0f;

            staticPostProcessVolume = postProcessVolume;
            
            // Try to get ColorAdjustments component based on current pipeline
            if (!TryGetColorAdjustments())
            {
                if (_isDebugSTATIC) Debug.LogError("FadeMask: ColorAdjustments component not found in volume profile!");
            }
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
                // Find HDRP ColorAdjustments type
                System.Type hdrpColorAdjustmentsType = System.Type.GetType("UnityEngine.Rendering.HighDefinition.ColorAdjustments, Unity.RenderPipelines.HighDefinition.Runtime");
                
                // Fallback: search through loaded assemblies
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
                
                // Use reflection to call TryGet<T> method
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
                // Find URP ColorAdjustments type
                System.Type urpColorAdjustmentsType = System.Type.GetType("UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");
                
                // Fallback: search through loaded assemblies
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
                
                // Use reflection to call TryGet<T> method
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

        public static void SetVolumeTo_InitialSetup()
        {
            if (_isDebugSTATIC) Debug.Log($"FadeMask: Setting initial setup for {_currentPipeline}");
            
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
                
                default:
                    if (_isDebugSTATIC) Debug.LogWarning($"FadeMask: Cannot set initial setup for pipeline: {_currentPipeline}");
                    break;
            }
        }

        public static void SetVolumeTo_HeadInWallSetup()
        {
            if (_isDebugSTATIC) Debug.Log($"FadeMask: Setting head in wall setup for {_currentPipeline}");
            
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
                if (parameters == null) return;
                
                var parametersEnumerable = parameters as System.Collections.IEnumerable;
                if (parametersEnumerable == null) return;
                
                var parametersList = new System.Collections.Generic.List<object>();
                foreach (var param in parametersEnumerable)
                {
                    if (param != null) parametersList.Add(param);
                }
                
                object targetParameter = null;
                
                if (propertyName == "colorFilter")
                {
                    // Find ColorParameter
                    foreach (var param in parametersList)
                    {
                        if (param.GetType().Name == "ColorParameter")
                        {
                            targetParameter = param;
                            break;
                        }
                    }
                }
                else if (propertyName == "saturation")
                {
                    // Find saturation parameter - typically index 4 or by range check
                    if (parametersList.Count > 4 && parametersList[4].GetType().Name == "ClampedFloatParameter")
                    {
                        targetParameter = parametersList[4];
                    }
                    else
                    {
                        // Fallback: find ClampedFloatParameter with range -100 to 100
                        foreach (var param in parametersList)
                        {
                            if (param.GetType().Name == "ClampedFloatParameter")
                            {
                                var paramType = param.GetType();
                                var minProp = paramType.GetProperty("min");
                                var maxProp = paramType.GetProperty("max");
                                if (minProp?.GetValue(param)?.ToString() == "-100" && 
                                    maxProp?.GetValue(param)?.ToString() == "100")
                                {
                                    targetParameter = param;
                                    break;
                                }
                            }
                        }
                    }
                }
                
                if (targetParameter != null)
                {
                    var parameterType = targetParameter.GetType();
                    var valueProperty = parameterType.GetProperty("value");
                    
                    if (valueProperty != null)
                    {
                        valueProperty.SetValue(targetParameter, value);
                        if (_isDebugSTATIC) Debug.Log($"FadeMask: Set {propertyName} to {value}");
                    }
                }
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
            SetVolumeTo_InitialSetup();
            
            if (!isInitComplete) return;

            SetVolumeTo_HeadInWallSetup();
        }

        public static void FadeValue(bool value)
        {
            FadeValue(value, _fadeTime);
            if (_isDebugSTATIC) Debug.Log($"FadeMask: Fading to {value} in {_fadeTime}s");
        }

        public static void FadeValue(bool value, float fadeTime)
        {
            // Check if volume is initialized
            if (staticPostProcessVolume == null)
            {
                if (_isDebugSTATIC) Debug.LogWarning("FadeMask: staticPostProcessVolume is null. Make sure FadeMask.Awake() has been called.");
                return;
            }

            // Don't start a new fade if we're already fading to the same target
            if (_isCurrentlyFading && _targetState == value)
                return;

            if (_isDebugSTATIC) Debug.Log($"FadeMask: Fading to {value} in {fadeTime}s");

            // Only cancel if the handle is valid and active
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