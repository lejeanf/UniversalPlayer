using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;
using DG.Tweening;

[ExecuteAlways]
public class FadeMask : MonoBehaviour
{
    private CustomPassVolume _customPassVolume;

    [Header("Manual Switch Input")]
    [Tooltip("Input to manually switch the fade state")]
    [SerializeField] private InputAction _inputBinding = new InputAction();
    [Space(10)]
    [Header("Fade Settings")]
    [SerializeField] private float fadeTime = .2f;
    [SerializeField] private Color color = new Color(0, 0, 0, 1f);
    [Tooltip("This value will be used onAwake, if you want to start the game with a black screen, set alpha to 1")]
    [Range(0,1)]
    [SerializeField] private float alpha = 1f;

    private static readonly int FadeColor = Shader.PropertyToID("_Color");
    private static readonly int FadeAlpha = Shader.PropertyToID("_Alpha");
    
    private Material _shaderMaterial;
    private bool isFaded = false;

    public delegate void SetColor(Color color);
    public static SetColor SetFadeColor;
    public delegate void SetAlpha(float alpha);
    public static SetAlpha SetFadeAlpha;
    public delegate void FadeToValue(bool value);
    public static FadeToValue FadeTo;

    private void Awake()
    {
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
        _inputBinding.Enable();
        _inputBinding.performed += _ => SwitchFadeState();
        
        SetFadeColor += ctx => color = ctx;
        SetFadeAlpha += ctx => alpha = ctx;
        FadeTo += FadeValue;
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    void Unsubscribe()
    {
        _inputBinding.performed -= null;
        _inputBinding.Disable();
        
        SetFadeColor -= ctx => color = ctx;
        SetFadeAlpha -= ctx => alpha = ctx;
        FadeTo -= FadeValue;
    }

    public void SwitchFadeState()
    {
        isFaded = !isFaded;
        FadeValue(isFaded);
    }
    public void FadeValue(bool value)
    {
        float tmpAlpha = value ? 1 : 0;
        var forwardTween = DOTween.To(
            () => _shaderMaterial.GetFloat(FadeAlpha),
            (val) => _shaderMaterial.SetFloat(FadeAlpha, val),
            tmpAlpha,
            fadeTime);
    }
}