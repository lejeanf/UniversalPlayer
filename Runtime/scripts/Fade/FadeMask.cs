using UnityEngine;
using UnityEngine.InputSystem;
using jeanf.EventSystem;
using UnityEngine.Serialization;
using UnityEngine.Rendering;
using LitMotion;

namespace jeanf.vrplayer
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
            if (_shaderMaterial)_shaderMaterial.SetColor(FadeColor, new Color(color.r, color.g, color.b, 0));
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

            if (_isDebugSTATIC) Debug.Log($"Fading to: {value}, in {fadeTime}s");
            float alpha = value ? 1 : 0;
            
            _fadeHandle = LMotion.Create(staticPostProcessVolume.weight,alpha,fadeTime)
                .Bind(x => staticPostProcessVolume.weight = x);
        }

        private void DisableFadeHandle()
        {
            if (!_fadeHandle.IsActive()) return;
            _fadeHandle.Complete();
            _fadeHandle.Cancel();
        }
    }
}