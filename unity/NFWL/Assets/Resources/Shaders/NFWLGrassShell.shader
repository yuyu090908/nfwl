// NFWL v0.7: short 3D grass near the camera (shell layers). Each layer is a copy of the pitch plane a
// few millimetres higher; texels that are not blades are discarded. Layers fade out with distance,
// so far turf costs nothing extra. Quality settings choose 0 to 4 layers.
Shader "NFWL/GrassShell"
{
    Properties
    {
        _ColorDark ("Stripe Dark", Color) = (0.15,0.38,0.12,1)
        _ColorLight ("Stripe Light", Color) = (0.2,0.47,0.15,1)
        _WearColor ("Wear (A = amount)", Color) = (0.36,0.34,0.18,0.45)
        _NoiseTex ("Noise", 2D) = "gray" {}
        _StripeWidth ("Stripe Width (m)", Float) = 3.5
        _MowSheen ("Mowing Sheen", Range(0,0.5)) = 0.12
        _HalfLength ("Pitch Half Length", Float) = 21
        _HalfWidth ("Pitch Half Width", Float) = 12.5
        _Layer ("Layer (0..1)", Range(0,1)) = 0.5
        _BladeDensity ("Blades per m", Float) = 90
        _FadeEnd ("Visible up to (m)", Float) = 22
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Lambert noforwardadd nolightmap nodynlightmap nodirlightmap
        #pragma target 3.0
        #include "NFWLGrass.cginc"

        half _Layer, _BladeDensity, _FadeEnd;

        struct Input { float3 worldPos; };

        float NFWLHash (float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 v = _WorldSpaceCameraPos - IN.worldPos;
            float dist = length(v);
            float2 cell = floor(IN.worldPos.xz * _BladeDensity);
            float h = NFWLHash(cell);
            // Taller layers keep fewer blades (blade tips), and far away nothing is kept.
            float fade = saturate(1.0 - dist / _FadeEnd);
            clip(h - _Layer - (1.0 - fade));
            half stripeSign;
            fixed3 col = NFWLTurfColor(IN.worldPos, v / max(dist, 1e-3), stripeSign);
            // Blade tips are a little lighter, roots darker (self shadowing between blades).
            o.Albedo = col * lerp(0.75, 1.12, _Layer);
            o.Alpha = 1.0;
        }
        ENDCG
    }
}
