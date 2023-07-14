using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.Build;

[InitializeOnLoad]
public class RenderingPipelineDefines
{
    static RenderingPipelineDefines()
    {
        if (GraphicsSettings.renderPipelineAsset == null) return;
        var renderingAssetType = GraphicsSettings.renderPipelineAsset.GetType().ToString();
        if (renderingAssetType.Contains("HDRenderPipelineAsset")) {
            AddDefine("UNITY_PIPELINE_HDRP");
        } else if (renderingAssetType.Contains("UniversalRenderPipelineAsset")) {
            AddDefine("UNITY_PIPELINE_URP");
        }
    }

    public static void AddDefine(string define)
    {
        var target = EditorUserBuildSettings.activeBuildTarget;
        var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(target);
        var defines = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup));
        var definesList = new List<string>(defines.Split(';'));
        if (!definesList.Contains(define))
        {
            definesList.Add(define);
            Debug.Log(string.Join(";",definesList));
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, string.Join(";",definesList));
        }
    }
}