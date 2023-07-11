using System;
using System.Collections.Generic;
using UnityEngine;

public class SwapHands : MonoBehaviour
{
    [SerializeField] public HandType handType;
    [Space(20)]
    [SerializeField] private List<SkinnedMeshRenderer> hands;
    [SerializeField] private List<Hand> handTypes;
    
    [Serializable]
    public struct Hand
    {   
        public HandType handType;
        public Mesh mesh;
        public Material skinMaterial;
        public Material nailMaterial;
    }
    public enum HandType
    {
        WhiteSkin_Male = 0,
        WhiteSkin_Female = 1,
        LightBrownSkin_Male = 2,
        LightBrownSkin_Female = 3,
        DarkBrownSkin_Male = 4,
        DarkBrownSkin_Female = 5,
        DarkSkin_Male = 6,
        DarkSkin_Female = 7,
        HandWithGlove_Male = 8,
        HandWithGlove_Female = 9
    }
    private HandType _handType
    {
        get => handType;
        set
        {
            handType = value;
            SetHands(value); 
        }
    }
    public delegate void SwapHandEvent(HandType handType);
    public static event SwapHandEvent OnSwapHand;

    private void OnEnable()
    {
        OnSwapHand += ctx => SetHands(ctx);
    }

    private void OnDisable() => Unsubscribe();
    private void OnDestroy() => Unsubscribe();

    private void Unsubscribe()
    {
        OnSwapHand -= null;
    }
    public void OnValidate ()
    {
        _handType = handType;
    }
    
    private void SetHands(HandType handType)
    {
        foreach (var h in handTypes)
        {
            if (h.handType != handType) continue;
            foreach (var hand in hands)
            {
                hand.sharedMesh = h.mesh;
                hand.sharedMaterial = h.skinMaterial;
                Debug.Log($"Setting {handType} to {hand.gameObject.name}");
            }
        }
    }
}

