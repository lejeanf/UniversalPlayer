using UnityEngine;
using UnityEngine.Rendering;

namespace jeanf.universalplayer
{
    /// <summary>
    /// Picks the material authored for the ACTIVE render pipeline. Shader Graphs and
    /// hand-written shaders are pipeline-specific (an HDRP graph is magenta under URP and
    /// vice versa), so components that ship a look for both pipelines hold one material
    /// per pipeline and resolve it here at runtime — no project-side wiring.
    /// </summary>
    public static class PipelineMaterials
    {
        /// <summary>The material for the active pipeline: <paramref name="hdrp"/> under HDRP, otherwise <paramref name="urpOrBuiltIn"/>. Falls back to whichever is assigned.</summary>
        public static Material Pick(Material urpOrBuiltIn, Material hdrp) => Pick(urpOrBuiltIn, hdrp, ActivePipelineTypeName());

        /// <summary>Testable core: <paramref name="pipelineTypeName"/> is the active RenderPipelineAsset's type name (null/empty = Built-in).</summary>
        public static Material Pick(Material urpOrBuiltIn, Material hdrp, string pipelineTypeName)
        {
            var isHdrp = !string.IsNullOrEmpty(pipelineTypeName) && pipelineTypeName.Contains("HDRenderPipelineAsset");
            var preferred = isHdrp ? hdrp : urpOrBuiltIn;
            var other = isHdrp ? urpOrBuiltIn : hdrp;
            return preferred != null ? preferred : other;
        }

        /// <summary>Type name of the pipeline asset actually rendering (quality override first, then the graphics default); null under Built-in.</summary>
        public static string ActivePipelineTypeName()
        {
            var asset = GraphicsSettings.currentRenderPipeline != null ? GraphicsSettings.currentRenderPipeline : GraphicsSettings.defaultRenderPipeline;
            return asset != null ? asset.GetType().ToString() : null;
        }

        /// <summary>Assigns <paramref name="material"/> to every slot of every renderer under <paramref name="root"/> (inactive included) and turns their shadows off.</summary>
        public static void ApplyToRenderers(GameObject root, Material material)
        {
            if (root == null || material == null) return;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (var i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }
    }
}
