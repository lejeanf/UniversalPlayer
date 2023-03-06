using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapMaterials : MonoBehaviour
{
    public List<Material> materials;
    private static List<Material> _materials = new List<Material>();
    private int currentMaterialIndex = 0;
    public static Renderer rend;
    public enum HandMaterial
    {
        Default = 0,
        Glove = 1
    }
    private HandMaterial _handMaterial = HandMaterial.Default;

    private void Awake()
    {
        rend = GetComponent<Renderer>();
        if(materials.Count > 0) _materials = materials;
    }

    private void Start()
    {
        rend = GetComponent<Renderer>();
        rend.enabled = true;
        rend.sharedMaterial = materials[currentMaterialIndex];
    }
    
    public static void SwapMaterial(HandMaterial handMaterial)
    {
        var index = (int) handMaterial;
        if (index >= 0 && index < _materials.Count) rend.material = _materials[index];
    }
}
