using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using jeanf.EventSystem;
using LitMotion;
using UnityEngine.InputSystem;

namespace jeanf.vrplayer
{
    using jeanf.propertyDrawer;
    [ExecuteAlways]
    public class HandsAppearanceManager : MonoBehaviour
    {
        [SerializeField] private bool isDebug = false;
        
        [Header("Settings")]
        [SerializeField] private float blendTime = 1.0f;
        private float _blendValue = 100.0f;
        [Range(0,100)]
        [SerializeField] private float gender = 100.0f;

        public float Gender
        {
            get => gender;
            set => gender = value;
        }

        [Range(0,100)]
        [SerializeField] private float bodyMass = 100.0f;
        
        public float BodyMass
        {
            get => bodyMass;
            set => bodyMass = value;
        }
        [Range(0,1)]
        [SerializeField] private float skinDarkness = 1.0f;
        public float SkinDarkness
        {
            get => skinDarkness;
            set => skinDarkness = value;
        }
        [SerializeField] private Color lightSkinColor;
        [SerializeField] private Color darkSkinColor;
        [Range(0,1)]
        [SerializeField] private float gloveValue = 1.0f;

        private MotionHandle _gloveHandle;
        
        [Header("Hands detected")]
        [SerializeField] private List<SkinnedMeshRenderer> _hands = new List<SkinnedMeshRenderer>();

        [Header("States")]
        [ReadOnly] [SerializeField] private bool isGlove = false;
        [ReadOnly] [SerializeField] private bool isHandVisible = true;
        [ReadOnly] [SerializeField] private bool lastHandVisibility = true;
        [ReadOnly] [SerializeField] private bool canUpdate = false;

        PlayerInput playerInput;
        private float tolerance = 0.01f;

        [Header("Action binding")]
        [SerializeField] private InputActionReference shiftTypeHandAction;
        
        //[SerializeField] private Material skin;
        //[SerializeField] private Material nail;
        private static readonly int SkinBaseColor = Shader.PropertyToID("_BaseColor");
        //private static readonly int SkinDarness = Shader.PropertyToID("_SkinDarkness");
        private static readonly int _gloveValue = Shader.PropertyToID("_Switch_Gloves");

        [SerializeField] private readonly int _genderValue = Shader.PropertyToID("_Switch_Woman");
        //[Range(0,100)]
        //[SerializeField] private float nailDarkness = 10f;


        [Header("Listening on:")]
        [SerializeField] private BoolEventChannelSO gloveStateChannel;

        [SerializeField] private BoolEventChannelSO hmdStateChannel;
        private void OnEnable()
        {
            BlendableHand.AddHand += AddHand;
            BlendableHand.RemoveHand += RemoveHand;
            gloveStateChannel.OnEventRaised += SetGloveState;
            hmdStateChannel.OnEventRaised += SetUpdateState;
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            _hands.Clear();
            _hands.TrimExcess();
            BlendableHand.AddHand -= AddHand;
            BlendableHand.RemoveHand -= RemoveHand;
            gloveStateChannel.OnEventRaised -= SetGloveState;
            hmdStateChannel.OnEventRaised -= SetUpdateState;
        }

        private void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();
        }

        private void Update()
        {
            if(playerInput.currentControlScheme == "XR")
            {
                SetHandsVisibility(true);
            }
            else
            {
                SetHandsVisibility(false);
            }
            if(!canUpdate)return;
            //SetBlendValueFromGender(gender);
            SetHandMaterials(_hands, gender * 0.01f);
            SetGloveValue(_hands, gloveValue);
            SetGender(_hands, gender);
            SetBodyMass(_hands, bodyMass);
            SetSkinDarkness(_hands, skinDarkness);
        }
        private void AddHand(SkinnedMeshRenderer hand)
        {
            if (!_hands.Contains(hand))
            {
                _hands.Add(hand);
                SetGender(_hands, gender);
                SetHandsVisibility(isHandVisible);
            }

        }
        private void RemoveHand(SkinnedMeshRenderer hand)
        {
            if(_hands.Count > 0 && _hands.Contains(hand)) _hands.Remove(hand);
        }

        
        private void SetBlendValueFromGender(bool gender)
        {
            _blendValue = gender ? 100f : 0f;
        }

        
        private void SetGender(List<SkinnedMeshRenderer> hands, float value)
        {
            foreach (var hand in hands)
            {
                hand.SetBlendShapeWeight(0, value);
            }
        }
        
        private void SetBodyMass(List<SkinnedMeshRenderer> hands, float value)
        {
            foreach (var hand in hands)
            {
                hand.SetBlendShapeWeight(2, value);
            }
        }


        private void SetHandMaterials(List<SkinnedMeshRenderer> hands, float value)
        {
            if(hands.Count < 1) return;
            foreach (var hand in hands)
            {
                hand.sharedMaterial.SetFloat(_genderValue, value);
            }
        }

        private void SetSkinDarkness(List<SkinnedMeshRenderer> hands, float skinDarness)
        {
            if(hands.Count < 1) return;
            var blend = Color.Lerp(lightSkinColor, darkSkinColor, skinDarkness);
            foreach (var hand in hands)
            {
                hand.sharedMaterials[0].SetColor(SkinBaseColor, blend);
            }
        }

        private void SetGloveValue(List<SkinnedMeshRenderer> hands, float value)
        {
            if(hands.Count < 1) return;
            foreach (var mat in hands.SelectMany(hand => hand.sharedMaterials))
            {
                mat.SetFloat(_gloveValue, value);
            }
        }
        
        private void LerpGloveTowardsValue(float goalValue, float blendTime)
        {
            _gloveHandle = LMotion.Create(gloveValue,goalValue,blendTime)
                .Bind(x => gloveValue = x);
        }

        public void SetGloveState(bool state)
        {
            if(isDebug) Debug.Log($"glove state {state}");
            isGlove = state;
            var goalValue = isGlove ? 1 : 0;
            LerpGloveTowardsValue(goalValue, blendTime);
        }

        public void SetUpdateState(bool updateState)
        {
            isHandVisible = updateState;
            canUpdate = updateState;
        }

        public void SetHandsVisibility(bool state)
        {
            foreach (var hand in _hands)
            {
                hand.enabled = state;
            }

            lastHandVisibility = state;
        }
    }
}