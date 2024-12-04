Shader "HDRP/FresnelGhostShader"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Texture", 2D) = "white" {}
        _FresnelPower ("Fresnel Power", Float) = 3.0
        _GlowColor ("Glow Color", Color) = (0, 1, 1, 1) // Cyan glow for example
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
            Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            // Transparency settings
            ZWrite Off
            ZTest Always

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 viewDir : TEXCOORD2;
            };

            // Vertex shader
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Pass normal and view direction for fresnel calculation
                o.worldNormal = mul((float3x3)unity_ObjectToWorld, v.normal);
                o.viewDir = normalize(_WorldSpaceCameraPos - mul(unity_ObjectToWorld, v.vertex).xyz);

                o.uv = v.uv;
                return o;
            }

            // Fresnel function
            float Fresnel(float3 viewDir, float3 normal, float power)
            {
                float cosTheta = dot(viewDir, normal);
                return pow(1.0 - cosTheta, power);
            }

            // Fragment shader
            sampler2D _MainTex;
            float _FresnelPower;
            float4 _GlowColor;

            fixed4 frag(v2f i) : SV_Target
            {
                // Sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                
                // Compute the Fresnel effect
                float fresnel = Fresnel(i.viewDir, i.worldNormal, _FresnelPower);

                // Combine texture color with the glow based on fresnel
                fixed4 finalColor = lerp(col, _GlowColor, fresnel);

                // Return the final color with transparency
                finalColor.a = col.a; // Maintain original alpha for transparency
                return finalColor;
            }
            ENDCG
        }
    }
}
