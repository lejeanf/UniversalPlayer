using System;
using System.Collections.Generic;
using UnityEngine;
using jeanf.EventSystem;
using LitMotion;
using UnityEngine.InputSystem;

namespace jeanf.universalplayer
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

        private float tolerance = 0.01f;
        
        private List<int> _nullHandIndices = new List<int>(4); 
        private int _frameCounter = 0;
        private const int CleanupInterval = 300; 

        [Header("Action binding")]
        [SerializeField] private InputActionReference shiftTypeHandAction;
        
        private static readonly int SkinBaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int _gloveValue = Shader.PropertyToID("_Switch_Gloves");
        [SerializeField] private readonly int _genderValue = Shader.PropertyToID("_Switch_Woman");

        [Header("Listening on:")]
        [SerializeField] private BoolEventChannelSO gloveStateChannel;

        private void OnEnable()
        {
            BlendableHand.AddHand += AddHand;
            BlendableHand.RemoveHand += RemoveHand;
            if (gloveStateChannel != null)
            {
                gloveStateChannel.OnEventRaised += SetGloveState;
            }
        }

        private void OnDisable() => Unsubscribe();
        private void OnDestroy() => Unsubscribe();

        private void Unsubscribe()
        {
            _hands.Clear();
            _hands.TrimExcess();
            BlendableHand.AddHand -= AddHand;
            BlendableHand.RemoveHand -= RemoveHand;
            if (gloveStateChannel != null)
            {
                gloveStateChannel.OnEventRaised -= SetGloveState;
            }
        }

        private void Update()
        {
            _frameCounter++;
            if (_frameCounter >= CleanupInterval)
            {
                _frameCounter = 0;
                CleanupNullHands_GCFree();
            }
            
            if(BroadcastControlsStatus.controlScheme == BroadcastControlsStatus.ControlScheme.XR)
            {
                SetHandsVisibility(true);
                SetHandMaterials(_hands, gender * 0.01f);
                SetGloveValue(_hands, gloveValue);
                SetGender(_hands, gender);
                SetBodyMass(_hands, bodyMass);
                SetSkinDarkness(_hands, skinDarkness);
            }
        }
        
        private void CleanupNullHands_GCFree()
        {
            if (_hands is null || _hands.Count is 0) return;
            
            _nullHandIndices.Clear();
            
            for (int i = 0; i < _hands.Count; i++)
            {
                if (_hands[i] is null)
                {
                    _nullHandIndices.Add(i);
                }
            }
            
            for (int i = _nullHandIndices.Count - 1; i >= 0; i--)
            {
                _hands.RemoveAt(_nullHandIndices[i]);
            }
        }
        
        private void AddHand(SkinnedMeshRenderer hand)
        {
            if (hand is null)
            {
                if (isDebug) Debug.LogWarning("[HandsAppearanceManager] Attempted to add null hand!");
                return;
            }
            
            if (hand.sharedMesh is null)
            {
                if (isDebug) 
                {
                    Debug.LogWarning("[HandsAppearanceManager] Hand has no mesh: " + hand.name);
                }
                return;
            }

            if (!_hands.Contains(hand))
            {
                _hands.Add(hand);
                
                if (ValidateBlendShapes(hand))
                {
                    SetGender(_hands, gender);
                }
                else
                {
                    if (isDebug) 
                    {
                        Debug.LogWarning("[HandsAppearanceManager] Hand missing blend shapes: " + hand.name);
                    }
                }
                
                SetHandsVisibility(isHandVisible);
            }
        }
        
        private bool ValidateBlendShapes(SkinnedMeshRenderer hand)
        {
            if (hand is null || hand.sharedMesh is null) return false;
            
            int blendShapeCount = hand.sharedMesh.blendShapeCount;
            
            if (blendShapeCount < 1)
            {
                if (isDebug)
                {
                    Debug.LogWarning("[HandsAppearanceManager] Hand has no blend shapes: " + hand.name);
                }
                return false;
            }
            
            if (blendShapeCount < 3)
            {
                if (isDebug)
                {
                    Debug.LogWarning("[HandsAppearanceManager] Hand missing body mass blend shape: " + hand.name);
                }
            }
            
            return true;
        }
        
        private void RemoveHand(SkinnedMeshRenderer hand)
        {
            if(_hands != null && _hands.Count > 0 && _hands.Contains(hand))
            {
                _hands.Remove(hand);
            }
        }

        private void SetBlendValueFromGender(bool gender)
        {
            _blendValue = gender ? 100f : 0f;
        }

        private void SetGender(List<SkinnedMeshRenderer> hands, float value)
        {
            if (hands is null || hands.Count is 0) return;
            
            for (int i = 0; i < hands.Count; i++)
            {
                var hand = hands[i];
                if (hand is null || hand.sharedMesh is null) continue;
                
                if (hand.sharedMesh.blendShapeCount < 1)
                {
                    if (isDebug)
                    {
                        Debug.LogWarning("[HandsAppearanceManager] Hand has no blend shapes for gender: " + hand.name);
                    }
                    continue;
                }
                
                try
                {
                    hand.SetBlendShapeWeight(0, value);
                }
                catch (Exception e)
                {
                    Debug.LogError("[HandsAppearanceManager] Error setting gender blend shape on " + hand.name + ": " + e.Message);
                }
            }
        }
        
        private void SetBodyMass(List<SkinnedMeshRenderer> hands, float value)
        {
            if (hands is null || hands.Count is 0) return;
            
            for (int i = 0; i < hands.Count; i++)
            {
                var hand = hands[i];
                if (hand is null || hand.sharedMesh is null) continue;
                
                if (hand.sharedMesh.blendShapeCount < 3)
                {
                    if (isDebug)
                    {
                        Debug.LogWarning("[HandsAppearanceManager] Hand doesn't have body mass blend shape: " + hand.name);
                    }
                    continue;
                }
                
                try
                {
                    hand.SetBlendShapeWeight(2, value);
                }
                catch (Exception e)
                {
                    Debug.LogError("[HandsAppearanceManager] Error setting body mass blend shape on " + hand.name + ": " + e.Message);
                }
            }
        }

        private void SetHandMaterials(List<SkinnedMeshRenderer> hands, float value)
        {
            if (hands is null || hands.Count < 1) return;
            
            for (int i = 0; i < hands.Count; i++)
            {
                var hand = hands[i];
                if (hand is null || hand.sharedMaterial is null) continue;
                
                try
                {
                    hand.sharedMaterial.SetFloat(_genderValue, value);
                }
                catch (Exception e)
                {
                    Debug.LogError("[HandsAppearanceManager] Error setting material on " + hand.name + ": " + e.Message);
                }
            }
        }

        private void SetSkinDarkness(List<SkinnedMeshRenderer> hands, float skinDarness)
        {
            if (hands is null || hands.Count < 1) return;
            
            var blend = Color.Lerp(lightSkinColor, darkSkinColor, skinDarkness);
            
            for (int i = 0; i < hands.Count; i++)
            {
                var hand = hands[i];
                if (hand is null || hand.sharedMaterials is null || hand.sharedMaterials.Length is 0) continue;
                
                if (hand.sharedMaterials[0] is null) continue;
                
                try
                {
                    hand.sharedMaterials[0].SetColor(SkinBaseColor, blend);
                }
                catch (Exception e)
                {
                    Debug.LogError("[HandsAppearanceManager] Error setting skin darkness on " + hand.name + ": " + e.Message);
                }
            }
        }

        private void SetGloveValue(List<SkinnedMeshRenderer> hands, float value)
        {
            if (hands is null || hands.Count < 1) return;
            
            for (int i = 0; i < hands.Count; i++)
            {
                var hand = hands[i];
                if (hand is null || hand.sharedMaterials is null) continue;
                
                for (int j = 0; j < hand.sharedMaterials.Length; j++)
                {
                    var mat = hand.sharedMaterials[j];
                    if (mat is null) continue;
                    
                    try
                    {
                        mat.SetFloat(_gloveValue, value);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[HandsAppearanceManager] Error setting glove value on " + hand.name + ": " + e.Message);
                    }
                }
            }
        }
        
        private void LerpGloveTowardsValue(float goalValue, float blendTime)
        {
            _gloveHandle = LMotion.Create(gloveValue, goalValue, blendTime)
                .Bind(x => gloveValue = x);
        }

        public void SetGloveState(bool state)
        {
            if(isDebug)
            {
                Debug.Log("[HandsAppearanceManager] Glove state: " + state);
            }
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
            if (_hands is null) return;
            
            for (int i = 0; i < _hands.Count; i++)
            {
                var hand = _hands[i];
                if (hand is null) continue;
                
                try
                {
                    hand.enabled = state;
                }
                catch (Exception e)
                {
                    Debug.LogError("[HandsAppearanceManager] Error setting hand visibility: " + e.Message);
                }
            }

            lastHandVisibility = state;
        }
    }
}