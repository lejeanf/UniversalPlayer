
using System;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    public class HandsAppearanceManager : MonoBehaviour
{
    [SerializeField] private bool isDebug = false;
    [SerializeField] private bool gender = true;
    [SerializeField] private float blendTime = 1.0f;
    [SerializeField] private List<SkinnedMeshRenderer> _hands = new List<SkinnedMeshRenderer>();
    private float _blendValue = 100.0f;
    [Range(0,100)]
    [SerializeField] private float blendValue = 100.0f;
    [Range(0,1)]
    [SerializeField] private float skinDarkness = 1.0f;
    [SerializeField] private Color lightSkinColor;
    [SerializeField] private Color darkSkinColor;

    private float tolerance = 0.01f;

    [SerializeField] private InputActionReference shiftTypeHandAction;
    
    [SerializeField] private Material skinF;
    [SerializeField] private Material skinM;
    [SerializeField] private Material nailF;
    [SerializeField] private Material nailM;
    private static readonly int SkinBaseColor = Shader.PropertyToID("_BaseColor");
    //[Range(0,100)]
    //[SerializeField] private float nailDarkness = 10f;


    private void OnEnable()
    {
        BlendableHand.AddHand += AddHand;
        BlendableHand.RemoveHand += RemoveHand;
        
        shiftTypeHandAction.action.Enable();
        shiftTypeHandAction.action.performed += ctx => SetBlendValueFromGender(gender = !gender);
    }


    private void AddHand(SkinnedMeshRenderer hand)
    {
        if (!_hands.Contains(hand))
        {
            _hands.Add(hand);
            SetBlendShapeWeight(_hands, blendValue);
        }

    }
    private void RemoveHand(SkinnedMeshRenderer hand)
    {
        if(_hands.Count > 0 && _hands.Contains(hand)) _hands.Remove(hand);
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        _hands.Clear();
        _hands.TrimExcess();
        
        BlendableHand.AddHand -= null;
        BlendableHand.RemoveHand -= null;

        shiftTypeHandAction.action.performed -= null;
        shiftTypeHandAction.action.Disable();
    }
    private void SetBlendValueFromGender(bool gender)
    {
        _blendValue = gender ? 100f : 0f;
    }

    private void Update()
    {
        SetBlendValueFromGender(gender);
        SedBlendValue();
        SetBlendShapeWeight(_hands, blendValue);
        SetSkinDarkness(_hands);
    }
    
    private void SetBlendShapeWeight(List<SkinnedMeshRenderer> hands, float value)
    {
        if (_hands.Count <= 0) return;
        foreach (var hand in hands)
        {
            hand.SetBlendShapeWeight(0, value);
            var materialBlendValue = MathF.Round(value * .01f, 1);
            SetHandMaterials(hand, materialBlendValue);
        }
    }

    private void SedBlendValue()
    {
        if (Math.Abs(blendValue - _blendValue) < tolerance) return;
        DOTween.To(() => blendValue, x=> blendValue = x, _blendValue, blendTime)
            .OnUpdate(() => {
                if(isDebug) Debug.Log($"blendValue: {blendValue}");
            });
    }

    private void SetHandMaterials(Renderer hand, float value)
    {
        hand.materials[0].Lerp(skinF, skinM, value);
        hand.materials[1].Lerp(nailF, nailM, value);
    }

    private void SetSkinDarkness(List<SkinnedMeshRenderer> hands)
    {
        var blend = Color.Lerp(lightSkinColor, darkSkinColor, skinDarkness);
        //var blendNail = Color.Lerp(lightSkinColor, new Color(darkSkinColor.r * nailDarkness, darkSkinColor.g *nailDarkness, darkSkinColor.b *nailDarkness, darkSkinColor.a), skinDarkness);
        foreach (var hand in hands)
        {
            hand.materials[0].SetColor(SkinBaseColor, blend);
           //hand.materials[1].SetColor(SkinBaseColor, blendNail);
        }
    }
}

}
