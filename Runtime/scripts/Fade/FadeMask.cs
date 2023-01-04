using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using DG.Tweening;
using UnityEngine.Serialization;

namespace jeanf.vrplayer
{
    [ExecuteAlways]
    public class FadeMask : MonoBehaviour
    {
        private CustomPassVolume _customPassVolume;

        [FormerlySerializedAs("_inputBinding")]
        [Header("Manual Switch Input")]
        [Tooltip("Input to manually switch the fade state")]
        [SerializeField] private InputAction inputBinding = new InputAction();
        [FormerlySerializedAs("fadeTime")]
        [Space(10)]
        [Header("Fade Settings")]
        [SerializeField] private static float _fadeTime = .2f;
        [SerializeField] private Color color = new Color(0, 0, 0, 1f);
        [Tooltip("This value will be used onAwake, if you want to start the game with a black screen, set alpha to 1")]
        [Range(0,1)]
        [SerializeField] private float alpha = 1f;

        private static readonly int FadeColor = Shader.PropertyToID("_Color");
        private static readonly int FadeAlpha = Shader.PropertyToID("_Alpha");

        private static Material _shaderMaterial;
        private static bool _isFaded = false;

        public delegate void SetColor(Color color);
        public static SetColor SetFadeColor;
        public delegate void SetAlpha(float alpha);
        public static SetAlpha SetFadeAlpha;
        public delegate void FadeTime(float alpha);
        public static FadeTime SetFadeTime;
        public delegate void FadeToValue(bool value);
        public static FadeToValue FadeTo;
        public delegate void FadeToValueInTime(bool value, float fadeTime);
        public static FadeToValueInTime FadeToInSpecificTime;

        private void Awake()
        {
            alpha = 1;
            FadeValue(false);
            
            if (!_customPassVolume) _customPassVolume = GetComponent<CustomPassVolume>();
            foreach (var pass in _customPassVolume.customPasses)
            {
                if (pass is FullScreenCustomPass f) 
                {
                    _shaderMaterial = f.fullscreenPassMaterial;
                    _shaderMaterial.SetColor(FadeColor, color);
                    _shaderMaterial.SetFloat(FadeAlpha, alpha);
                }
            }
        }
        
        private void OnEnable()
        {   
            inputBinding.Enable();
            inputBinding.performed += _ => SwitchFadeState();

            SetFadeTime += ctx => _fadeTime = ctx;
            SetFadeColor += ctx => color = ctx;
            SetFadeAlpha += ctx => alpha = ctx;
            FadeTo += FadeValue; // using bool
            FadeToInSpecificTime += FadeValue; // using bool + float
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            inputBinding.performed -= null;
            inputBinding.Disable();

            SetFadeTime -= ctx => _fadeTime = ctx;
            SetFadeColor -= ctx => color = ctx;
            SetFadeAlpha -= ctx => alpha = ctx;
            FadeTo -= FadeValue;
            FadeToInSpecificTime -= FadeValue;
        }

        public static void SwitchFadeState()
        {
            _isFaded = !_isFaded;
            FadeValue(_isFaded);
        }
        public static void FadeValue(bool value)
        {
            float tmpAlpha = value ? 1 : 0;
            var forwardTween = DOTween.To(
                () => _shaderMaterial.GetFloat(FadeAlpha),
                (val) => _shaderMaterial.SetFloat(FadeAlpha, val),
                tmpAlpha,
                _fadeTime);
        }
        public static void FadeValue(bool value, float fadeTime)
        {
            float tmpAlpha = value ? 1 : 0;
            DOTween.To(
                () => _shaderMaterial.GetFloat(FadeAlpha),
                (val) => _shaderMaterial.SetFloat(FadeAlpha, val),
                tmpAlpha,
                fadeTime);
        }
    }
}