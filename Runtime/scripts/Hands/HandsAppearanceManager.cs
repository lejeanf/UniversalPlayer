using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    [ExecuteAlways]
    public class HandsAppearanceManager : MonoBehaviour
    {
        [SerializeField] private bool isDebug = false;
        [SerializeField] private float blendTime = 1.0f;
        [SerializeField] private List<SkinnedMeshRenderer> _hands = new List<SkinnedMeshRenderer>();
        private float _blendValue = 100.0f;
        [Range(0,100)]
        [SerializeField] private float blendValue = 100.0f;
        [Range(0,1)]
        [SerializeField] private float skinDarkness = 1.0f;
        [SerializeField] private Color lightSkinColor;
        [SerializeField] private Color darkSkinColor;
        [Range(0,1)]
        [SerializeField] private float gloveValue = 1.0f;

        [SerializeField] private bool isGlove = false;

        private float tolerance = 0.01f;

        [SerializeField] private InputActionReference shiftTypeHandAction;
        
        //[SerializeField] private Material skin;
        //[SerializeField] private Material nail;
        private static readonly int SkinBaseColor = Shader.PropertyToID("_BaseColor");
        //private static readonly int SkinDarness = Shader.PropertyToID("_SkinDarkness");
        private static readonly int _gloveValue = Shader.PropertyToID("_Switch_Gloves");

        [SerializeField] private readonly int _genderValue = Shader.PropertyToID("_Switch_Woman");
        //[Range(0,100)]
        //[SerializeField] private float nailDarkness = 10f;


        private void OnEnable()
        {
            BlendableHand.AddHand += AddHand;
            BlendableHand.RemoveHand += RemoveHand;
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
        }
        private void SetBlendValueFromGender(bool gender)
        {
            _blendValue = gender ? 100f : 0f;
        }

        private void Update()
        {
            //SetBlendValueFromGender(gender);
            SetHandMaterials(_hands, blendValue * 0.01f);
            SetGloveValue(_hands, gloveValue);
            SetBlendShapeWeight(_hands, blendValue);
            SetSkinDarkness(_hands, skinDarkness);
        }
        
        private void SetBlendShapeWeight(List<SkinnedMeshRenderer> hands, float value)
        {
            if (_hands.Count <= 0) return;
            foreach (var hand in hands)
            {
                hand.SetBlendShapeWeight(0, value);
                var materialBlendValue = MathF.Round(value * .01f, 1);
            }
        }


        private void SetHandMaterials(List<SkinnedMeshRenderer> hands, float value)
        {
            foreach (var hand in hands)
            {
                hand.sharedMaterial.SetFloat(_genderValue, value);
            }
        }

        private void SetSkinDarkness(List<SkinnedMeshRenderer> hands, float skinDarness)
        {
            var blend = Color.Lerp(lightSkinColor, darkSkinColor, skinDarkness);
            //var blendNail = Color.Lerp(lightSkinColor, new Color(darkSkinColor.r * nailDarkness, darkSkinColor.g *nailDarkness, darkSkinColor.b *nailDarkness, darkSkinColor.a), skinDarkness);
            foreach (var hand in hands)
            {
                hand.sharedMaterials[0].SetColor(SkinBaseColor, blend);
                //hand.materials[1].SetColor(SkinBaseColor, blendnew);
            }
        }

        private void SetGloveValue(List<SkinnedMeshRenderer> hands, float value)
        {
            foreach (var mat in hands.SelectMany(hand => hand.sharedMaterials))
            {
                mat.SetFloat(_gloveValue, value);
            }
        }
        
        private void LerpGloveTowardsValue(float goalValue, float tolerance, float blendTime)
        {
            if (Math.Abs(gloveValue - goalValue) < tolerance) return;
            DOTween.To(() => gloveValue, x=> gloveValue = x, goalValue, blendTime)
                .OnUpdate(() => {
                    if(isDebug) Debug.Log($"{nameof(gloveValue)}: {gloveValue}");
                });
        }

        public void SetGloveState(bool state)
        {
            if(isDebug) Debug.Log($"glove state {state}");
            isGlove = state;
            var goalValue = isGlove ? 1 : 0;
            LerpGloveTowardsValue(goalValue, tolerance, blendTime);
        }
    }
}
