using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace jeanf.universalplayer
{
    /// <summary>
    /// The package must work in both URP and HDRP, but a material authored for one
    /// pipeline renders as the pink error shader in the other (e.g. White.mat is
    /// HDRP/Lit). This guard sweeps each loaded scene for materials whose shader is
    /// broken in the active pipeline and switches them to the pipeline's Lit shader,
    /// preserving the base color, with a console entry naming every swap.
    /// Runs automatically; package assets stay untouched on disk (in-memory swap).
    /// </summary>
    public static class PipelineMaterialGuard
    {
        private static readonly HashSet<Material> Processed = new HashSet<Material>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            Processed.Clear();
            SceneManager.sceneLoaded += (scene, _) => FixBrokenMaterials(scene);
            FixBrokenMaterials(SceneManager.GetActiveScene());
        }

        /// <summary>Swaps broken-shader materials on all renderers of the scene to the active pipeline's Lit shader.</summary>
        public static void FixBrokenMaterials(Scene scene)
        {
            var lit = ActivePipelineLitShader();
            if (lit == null) return; // built-in or unknown pipeline — nothing sensible to swap to

            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (var material in renderer.sharedMaterials)
                    {
                        if (material == null || Processed.Contains(material)) continue;
                        Processed.Add(material);
                        if (!IsBrokenInActivePipeline(material)) continue;

                        var baseColor = ReadBaseColor(material);
                        var brokenShaderName = material.shader != null ? material.shader.name : "<null>";
                        material.shader = lit;
                        WriteBaseColor(material, baseColor);

                        Debug.LogWarning($"{XrStartupDiagnostics.LogPrefix} Material '{material.name}' used shader " +
                            $"'{brokenShaderName}' which does not work in the active pipeline — switched to '{lit.name}' for this session. " +
                            "To fix it permanently, author the material for this pipeline (or keep relying on this guard).");
                    }
                }
            }
        }

        private static bool IsBrokenInActivePipeline(Material material)
        {
            var shader = material.shader;
            if (shader == null) return true;
            if (shader.name == "Hidden/InternalErrorShader") return true;
            return !shader.isSupported;
        }

        /// <summary>True when the material's shader cannot render in the active pipeline (pink).</summary>
        public static bool IsBroken(Material material) => material == null || IsBrokenInActivePipeline(material);

        /// <summary>
        /// A vertex-colored unlit material that renders in URP, HDRP and built-in —
        /// for runtime-built lines and gizmo-like visuals (LineRenderer gradients).
        /// </summary>
        public static Material VertexColorUnlit()
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null || !shader.isSupported) shader = Shader.Find("Hidden/Internal-Colored");
            return new Material(shader);
        }

        /// <summary>
        /// A plain opaque material in the active pipeline's Lit shader (unlit fallback)
        /// — for editor previews whose authored materials belong to the other pipeline.
        /// </summary>
        public static Material SafeOpaque(Color color)
        {
            var shader = ActivePipelineLitShader();
            if (shader == null || !shader.isSupported) shader = Shader.Find("Sprites/Default");
            var material = new Material(shader);
            WriteBaseColor(material, color);
            return material;
        }

        private static Shader ActivePipelineLitShader()
        {
            var pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null) return null;

            var pipelineTypeName = pipeline.GetType().Name;
            if (pipelineTypeName.Contains("Universal")) return Shader.Find("Universal Render Pipeline/Lit");
            if (pipelineTypeName.Contains("HDRenderPipeline")) return Shader.Find("HDRP/Lit");
            return null;
        }

        private static Color ReadBaseColor(Material material)
        {
            if (material.HasProperty("_BaseColor")) return material.GetColor("_BaseColor");
            if (material.HasProperty("_Color")) return material.GetColor("_Color");
            return Color.white;
        }

        private static void WriteBaseColor(Material material, Color color)
        {
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            else if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }
    }
}
