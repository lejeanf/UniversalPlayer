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
        private static Color color = new Color(0, 0, 0, 0);

        private static readonly int FadeColor = Shader.PropertyToID("_Color");

        private static Material _shaderMaterial;
        private static bool _isFaded = false;
        private void Awake()
        {
            if (!_customPassVolume) _customPassVolume = GetComponent<CustomPassVolume>();
            foreach (var pass in _customPassVolume.customPasses)
            {
                if (pass is FullScreenCustomPass f) 
                {
                    _shaderMaterial = f.fullscreenPassMaterial;
                    color = _shaderMaterial.GetColor(FadeColor);
                }
            }

            if (!_shaderMaterial) return;
            _shaderMaterial.SetColor(FadeColor, new Color(color.r, color.g, color.b, 1));
            FadeValue(false, .5f);
        }
        
        private void OnEnable()
        {   
            inputBinding.Enable();
            inputBinding.performed += _ => SwitchFadeState();
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        void Unsubscribe()
        {
            if (_shaderMaterial)_shaderMaterial.SetColor(FadeColor, new Color(color.r, color.g, color.b, 0));
            inputBinding.performed -= null;
            inputBinding.Disable();
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
            Debug.Log($"Fading to: {value}, in {fadeTime}s");
            float alpha = value ? 1 : 0;
            _shaderMaterial.DOColor(new Color(color.r, color.g, color.b, alpha), fadeTime);
        }
    }
}