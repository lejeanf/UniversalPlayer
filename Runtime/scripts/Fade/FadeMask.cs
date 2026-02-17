using UnityEngine;
using UnityEngine.Rendering;
using LitMotion;
using System;
using System.Collections;
using System.Linq;
using Volume = UnityEngine.Rendering.Volume;

namespace jeanf.universalplayer
{
    public class FadeMask : MonoBehaviour
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
        [SerializeField] private Volume postProcessVolume;
        
        private static Volume staticPostProcessVolume;
        private static object hdrpColorAdjustments;
        private static object urpColorAdjustments;
        
        private enum RenderPipeline { BuiltIn, URP, HDRP, Unknown }
        private static RenderPipeline _currentPipeline = RenderPipeline.Unknown;

        private enum VisualState { Loading, Clear, HeadInWall }
        private static VisualState _currentState = VisualState.Loading;
        
        private static MotionHandle _colorFilterHandle;
        private static MotionHandle _saturationHandle;

        private void Awake()
        {
            DetectRenderPipeline();
            SetupVolumeProfile();
            _fadeTime = _fadeTimeInstance;
            SetStateLoadingImmediate();
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
                if (_isDebugSTATIC) Debug.LogError("FadeMask: postProcessVolume is not assigned!");
                return;
            }

            if (volumeProfile != null)
                postProcessVolume.profile = Instantiate(volumeProfile);
            else if (postProcessVolume.profile == null)
            {
                if (_isDebugSTATIC) Debug.LogError("FadeMask: No volume profile assigned!");
                return;
            }

            if (_currentPipeline == RenderPipeline.BuiltIn)
            {
                if (_isDebugSTATIC) Debug.LogWarning("FadeMask: Built-in pipeline detected. Volume system may not be available.");
                return;
            }

            postProcessVolume.weight = 1f;
            postProcessVolume.blendDistance = 10.0f;
            staticPostProcessVolume = postProcessVolume;

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

        #endregion

        #region Public State Methods

        public static void SetStateLoading()
        {
            if (staticPostProcessVolume == null) return;
            TransitionToState(VisualState.Loading);
        }

        public static void SetStateClear()
        {
            if (staticPostProcessVolume == null) return;
            TransitionToState(VisualState.Clear);
        }

        public static void SetStateHeadInWall()
        {
            if (staticPostProcessVolume == null) return;
            TransitionToState(VisualState.HeadInWall);
        }

        private static void SetStateLoadingImmediate()
        {
            if (staticPostProcessVolume == null) return;
            SetColorAndSaturation(Color.black, 0f);
            _currentState = VisualState.Loading;
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
                        int floatParamIndex = 0;
    
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