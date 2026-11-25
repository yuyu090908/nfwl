// NFWL v0.7: physically based surface for players, boots, posts and stadium metal (Built-in pipeline).
// Standard (PBR) lighting with real-time shadows from every shadow-casting light, normal map,
// metallic / smoothness, ambient occlusion from the texture alpha-free mask, and emission.
// No shader_feature keywords: every variant the game needs is always compiled, so nothing is
// stripped from a WebGL build when materials are created at runtime.
Shader "NFWL/Lit"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Base Map (A = smoothness mask)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _Metallic ("Metallic", Range(0,1)) = 0
        _Glossiness ("Smoothness", Range(0,1)) = 0.3
        _Occlusion ("Cavity Occlusion (mesh colour alpha)", Range(0,1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,1)
        _EmissionMap ("Emission Mask", 2D) = "white" {}
        _SheenColor ("Cloth Sheen", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow vertex:vert
        #pragma target 3.0
        #pragma multi_compile_instancing

        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _EmissionMap;
        half _BumpScale;
        half _Metallic;
        half _Glossiness;
        half _Occlusion;
        fixed4 _Color;
        half4 _EmissionColor;
        fixed4 _SheenColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float cavity;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // Vertex colour alpha < 1 marks creases (neck, armpits, crotch) baked by the mesh builder.
            o.cavity = v.color.a;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            half3 n = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            n.xy *= _BumpScale;
            o.Normal = normalize(n);
            o.Albedo = c.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness * c.a;
            o.Occlusion = lerp(1.0, IN.cavity, _Occlusion);
            // Cloth sheen: a soft brightening at grazing angles (velvet-like fibres), only on fabric.
            half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
            o.Emission = _EmissionColor.rgb * tex2D(_EmissionMap, IN.uv_MainTex).rgb
                       + _SheenColor.rgb * (rim * rim * rim) * _SheenColor.a * UNITY_LIGHTMODEL_AMBIENT.rgb * 4.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
