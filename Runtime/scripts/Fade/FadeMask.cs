using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using LitMotion;

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
        [SerializeField] private InputAction inputBinding = new InputAction();
        [Space(10)]
        [Header("Fade Settings")]
        [SerializeField] private static float _fadeTime = .2f;
        private static Color color = new Color(0, 0, 0, 0);
        private static MotionHandle motionHandle;

        private static readonly int FadeColor = Shader.PropertyToID("_Color");

        private static Material _shaderMaterial;
        private static bool _isFaded = false;

        [SerializeField] private VolumeProfile HDRPVolumeProfile;
        [SerializeField] private VolumeProfile URPVolumeProfile;
        [SerializeField] private Volume postProcessVolume;
        private static Volume staticPostProcessVolume;

        private static MotionHandle _fadeHandle;
        private static bool _isCurrentlyFading = false;
        private static bool _targetState = false;

        [Header("Listening On")]
        [SerializeField] private BoolFloatEventChannelSO fadeOutChannelSO;
        

        private void Awake()
        {
            if (GraphicsSettings.defaultRenderPipeline == null) return;
            var renderingAssetType = GraphicsSettings.defaultRenderPipeline.GetType().ToString();
            if (renderingAssetType.Contains("HDRenderPipelineAsset")) {
                postProcessVolume.profile = HDRPVolumeProfile;
                postProcessVolume.blendDistance = 10.0f;
            } else if (renderingAssetType.Contains("UniversalRenderPipelineAsset")) {
                postProcessVolume.profile = URPVolumeProfile;
            }
            staticPostProcessVolume = postProcessVolume;
            FadeValue(false, .5f);
        }
        
        private void OnEnable()
        {   
            inputBinding.Enable();
            inputBinding.performed += _ => SwitchFadeState();
            fadeOutChannelSO.OnEventRaised += FadeValue;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_shaderMaterial)_shaderMaterial.SetColor(FadeColor, color);
            inputBinding.performed -= null;
            fadeOutChannelSO.OnEventRaised -= FadeValue;
            inputBinding.Disable();
            DisableFadeHandle();
        }

        private void Update()
        {
            if(!checkForDebugChangeState) return;
            _isDebugSTATIC = _isDebug;
        }

        public static void SwitchFadeState()
        {
            _isFaded = !_isFaded;
            FadeValue(_isFaded);
        }
        
        public static void FadeValue(bool value)
        {
            FadeValue(value, _fadeTime);
            if (_isDebugSTATIC) Debug.Log($"Fading to: {value}, in {_fadeTime}");
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

            if (_isDebugSTATIC) Debug.Log($"Fading to: {value}, in {fadeTime}s");

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
    }
}