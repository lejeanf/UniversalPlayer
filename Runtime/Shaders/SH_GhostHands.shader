Shader "Unlit/GhostHands"
{
    Properties
    {
        //_MainTex ("Main Texture", 2D) = "white" {}
        _Color ("Outside Color", color) = (1,1,1,1)
        _InsideColor ("Inside Color", color) = (1,1,1,1)
        _FresnelIntensity("Fresnel Intensity", Range(0,10)) = 0
        _FresnelRamp("Fresnel Ramp", Range(0,10)) = 0
        _ReversedFresnelIntensity("Inside Fresnel Intensity", Range(0,10)) = 0
        _ReversedFresnelRamp("Inside Fresnel Ramp", Range(1,10)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            ZTest Always
            ZWrite Off
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;  // World position of the vertex
                float3 normal : TEXCOORD2;    // Object space normal
            };

            sampler2D _MainTex;

            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _InsideColor;

            float _FresnelIntensity;
            float _FresnelRamp;

            float _ReversedFresnelIntensity;
            float _ReversedFresnelRamp;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz; // World space position
                o.normal = v.normal; // Pass object space normal unmodified
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Sample the main texture
                fixed4 col = _Color;
                fixed4 insideCol = _InsideColor;

                // Calculate world space view direction
                float3 worldViewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Normalize object space normal
                float3 normalizedNormal = normalize(i.normal);

                // Fresnel effect
                float fresnelAmount = 1.0 - max(0.0, dot(normalizedNormal, worldViewDir));
                fresnelAmount = pow(fresnelAmount, _FresnelRamp) * _FresnelIntensity;

                // Reversed fresnel effect
                float reversedFresnelAmount = max(0.0, dot(normalizedNormal, worldViewDir));
                reversedFresnelAmount = pow(reversedFresnelAmount, _ReversedFresnelRamp) * _ReversedFresnelIntensity;

                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);

                return reversedFresnelAmount * insideCol + fresnelAmount*col;
            }
            ENDCG
        }
    }
}
