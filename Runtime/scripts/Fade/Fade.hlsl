#ifndef UNIVERSAL_FADE_INPUT_INCLUDED
#define UNIVERSAL_FADE_INPUT_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
CBUFFER_END

#endif