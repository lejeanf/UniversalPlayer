using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using jeanf.EventSystem;
using UnityEngine.Serialization;
using UnityEngine.Rendering;

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
        [FormerlySerializedAs("fadeTime")]
        [Space(10)]
        [Header("Fade Settings")]
        [SerializeField] private static float _fadeTime = .2f;
        private static Color color = new Color(0, 0, 0, 0);

        private static readonly int FadeColor = Shader.PropertyToID("_Color");

        private static Material _shaderMaterial;
        private static bool _isFaded = false;

        [SerializeField] private VolumeProfile HDRPVolumeProfile;
        [SerializeField] private VolumeProfile URPVolumeProfile;
        [SerializeField] private Volume postProcessVolume;
        private static Volume staticPostProcessVolume;

        private void Awake()
        {
            #if UNITY_PIPELINE_HDRP
                postProcessVolume.profile = HDRPVolumeProfile;
            #elif UNITY_PIPELINE_URP
                postProcessVolume.profile = URPVolumeProfile;
            #endif
            staticPostProcessVolume = postProcessVolume;
            FadeValue(false, .5f);
        }
        
        private void OnEnable()
        {   
            inputBinding.Enable();
            inputBinding.performed += _ => SwitchFadeState();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            if (_shaderMaterial)_shaderMaterial.SetColor(FadeColor, new Color(color.r, color.g, color.b, 0));
            inputBinding.performed -= null;
            inputBinding.Disable();
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
        }

        public static void FadeValue(bool value, float fadeTime)
        {
            if(_isDebugSTATIC) Debug.Log($"Fading to: {value}, in {fadeTime}s");
            float alpha = value ? 1 : 0;
            DOTween.To(
                () => {return staticPostProcessVolume.weight;},
                x => staticPostProcessVolume.weight = x,
                alpha,
                fadeTime
            );
        }
    }
}