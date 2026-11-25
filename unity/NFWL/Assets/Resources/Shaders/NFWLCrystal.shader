// NFWL v0.7: Crystalia crystals. Transparent, glossy (reflects the floodlights and the night sky),
// with a Fresnel edge and an inner glow. Glow pulses by rarity (Mythic / Legendary patterns).
// _NFWLGlowScale (global) lets the match dim all crystal glow so the ball and outlines stay readable.
Shader "NFWL/Crystal"
{
    Properties
    {
        _Color ("Tint (A = opacity)", Color) = (0.8,0.9,1,0.75)
        [HDR] _GlowColor ("Inner Glow", Color) = (0.3,0.5,1,1)
        _Glossiness ("Smoothness", Range(0,1)) = 0.95
        _FresnelPower ("Fresnel Power", Range(0.5,8)) = 3
        _FresnelBoost ("Fresnel Boost", Range(0,3)) = 1
        _PulseSpeed ("Pulse Speed (0 = steady)", Float) = 0
        _PulseAmount ("Pulse Amount", Range(0,1)) = 0
        _Sparkle ("Sparkle (Legendary)", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 300
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:premul vertex:vert
        #pragma target 3.0
        #pragma multi_compile_instancing

        fixed4 _Color;
        half4 _GlowColor;
        half _Glossiness, _FresnelPower, _FresnelBoost, _PulseSpeed, _PulseAmount, _Sparkle;
        float _NFWLGlowScale;

        struct Input
        {
            float3 viewDir;
            float3 worldNormal;
            float3 worldPos;
            float3 objPos;
        };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.objPos = v.vertex.xyz;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            half f = pow(1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal))), _FresnelPower);
            o.Albedo = _Color.rgb * 0.6;
            o.Metallic = 0.0;
            o.Smoothness = _Glossiness;
            // Edges become more opaque (like real glass / quartz).
            o.Alpha = saturate(_Color.a + f * 0.5);
            // Pulse runs along the shard from base to tip.
            half pulse = 1.0 + _PulseAmount * sin(_Time.y * _PulseSpeed - IN.objPos.y * 6.0);
            half glowScale = _NFWLGlowScale > 0.0 ? _NFWLGlowScale : 1.0;
            half sparkle = _Sparkle * step(0.985, frac(sin(dot(floor(IN.worldPos * 40.0), float3(12.9898, 78.233, 37.719)) + floor(_Time.y * 6.0)) * 43758.5453));
            o.Emission = _GlowColor.rgb * (0.35 + f * _FresnelBoost) * pulse * glowScale + sparkle * _GlowColor.rgb * 2.0;
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
