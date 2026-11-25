// NFWL v0.7: the pitch. Colour variation, mowing stripes that change with the viewing angle,
// a turf normal map whose detail fades with distance (close: fine blades, far: smooth and cheap),
// low smoothness with a soft sheen, and real-time shadows from the floodlights.
Shader "NFWL/Grass"
{
    Properties
    {
        _ColorDark ("Stripe Dark", Color) = (0.15,0.38,0.12,1)
        _ColorLight ("Stripe Light", Color) = (0.2,0.47,0.15,1)
        _WearColor ("Wear (A = amount)", Color) = (0.36,0.34,0.18,0.45)
        _NoiseTex ("Noise (RGB = 3 octaves)", 2D) = "gray" {}
        [Normal] _BumpMap ("Turf Normal", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 0.9
        _DetailTiling ("Blade Tiling (per m)", Float) = 2.2
        _DetailFadeStart ("Detail Fade Start (m)", Float) = 12
        _DetailFadeEnd ("Detail Fade End (m)", Float) = 45
        _StripeWidth ("Stripe Width (m)", Float) = 3.5
        _MowSheen ("Mowing Sheen", Range(0,0.5)) = 0.12
        _Glossiness ("Smoothness", Range(0,1)) = 0.18
        _HalfLength ("Pitch Half Length", Float) = 21
        _HalfWidth ("Pitch Half Width", Float) = 12.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry-10" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #include "NFWLGrass.cginc"

        sampler2D _BumpMap;
        half _BumpScale, _DetailTiling, _DetailFadeStart, _DetailFadeEnd, _Glossiness;

        struct Input
        {
            float3 worldPos;
            float2 uv_BumpMap;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 v = _WorldSpaceCameraPos - IN.worldPos;
            float dist = length(v);
            half stripeSign;
            o.Albedo = NFWLTurfColor(IN.worldPos, v / max(dist, 1e-3), stripeSign);
            half detail = 1.0 - saturate((dist - _DetailFadeStart) / max(_DetailFadeEnd - _DetailFadeStart, 1.0));
            float2 uv = IN.worldPos.xz * _DetailTiling;
            half3 n1 = UnpackNormal(tex2D(_BumpMap, uv));
            half3 n2 = UnpackNormal(tex2D(_BumpMap, uv * 0.37 + 0.5));
            half3 n = half3(n1.xy + n2.xy * 0.6, 1.0);
            n.xy *= _BumpScale * (0.25 + 0.75 * detail);
            // Blades lean along the mowing direction.
            n.x += stripeSign * 0.06;
            o.Normal = normalize(n);
            o.Metallic = 0.0;
            o.Smoothness = _Glossiness * (0.7 + 0.3 * detail);
            o.Occlusion = 1.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
