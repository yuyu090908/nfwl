// NFWL v0.7: broadcast-style image processing for the Built-in pipeline (driven by NFWLPostProcess.cs).
// Pass 0 bloom prefilter, 1 downsample, 2 upsample + combine, 3 SSAO, 4 SSAO blur, 5 final composite.
// No motion blur, depth of field or lens flares (they hurt playability).
Shader "Hidden/NFWL/Post"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    sampler2D _BaseTex;
    sampler2D _BloomTex;
    sampler2D _AOTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    half4 _Threshold;      // x threshold, y knee, z clamp
    half _BloomIntensity;
    half _Exposure;
    half _Contrast;
    half _Saturation;
    half4 _WhiteBalance;   // rgb multipliers
    half4 _ShadowTint;     // rgb, a = amount
    half4 _HighlightTint;  // rgb, a = amount
    half _Vignette;
    half _AOIntensity;
    half _AORadius;
    half _UseAO;
    half _Tonemap;
    float2 _BlurDir;

    half3 ToLinear (half3 c)
    {
    #ifdef UNITY_COLORSPACE_GAMMA
        return GammaToLinearSpace(c);
    #else
        return c;
    #endif
    }

    half3 ToOutput (half3 c)
    {
    #ifdef UNITY_COLORSPACE_GAMMA
        return LinearToGammaSpace(c);
    #else
        return c;
    #endif
    }

    half Luma (half3 c) { return dot(c, half3(0.2126, 0.7152, 0.0722)); }

    half4 FragPrefilter (v2f_img i) : SV_Target
    {
        half3 c = ToLinear(tex2D(_MainTex, i.uv).rgb);
        c = min(c, _Threshold.z);
        half br = max(c.r, max(c.g, c.b));
        // Soft knee so the bloom grows smoothly instead of switching on.
        half soft = clamp(br - _Threshold.x + _Threshold.y, 0, 2 * _Threshold.y);
        soft = soft * soft / (4 * _Threshold.y + 1e-4);
        half contrib = max(soft, br - _Threshold.x) / max(br, 1e-4);
        return half4(c * contrib, 1);
    }

    half4 FragDown (v2f_img i) : SV_Target
    {
        float4 o = _MainTex_TexelSize.xyxy * float4(-1, -1, 1, 1);
        half3 s = tex2D(_MainTex, i.uv + o.xy).rgb + tex2D(_MainTex, i.uv + o.zy).rgb
                + tex2D(_MainTex, i.uv + o.xw).rgb + tex2D(_MainTex, i.uv + o.zw).rgb;
        return half4(s * 0.25, 1);
    }

    half4 FragUp (v2f_img i) : SV_Target
    {
        // 3x3 tent filter on the smaller level, added to the larger level.
        float4 d = _MainTex_TexelSize.xyxy * float4(1, 1, -1, 0);
        half3 s = tex2D(_MainTex, i.uv - d.xy).rgb;
        s += tex2D(_MainTex, i.uv - d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv - d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.zw).rgb * 2;
        s += tex2D(_MainTex, i.uv).rgb * 4;
        s += tex2D(_MainTex, i.uv + d.xw).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.zy).rgb;
        s += tex2D(_MainTex, i.uv + d.wy).rgb * 2;
        s += tex2D(_MainTex, i.uv + d.xy).rgb;
        return half4(tex2D(_BaseTex, i.uv).rgb + s * (1.0 / 16.0), 1);
    }

    float InvDepth (float2 uv)
    {
        return 1.0 / max(LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv)), 1e-3);
    }

    // Crease ambient occlusion without normals: for pairs of samples on opposite sides of the pixel,
    // 1/depth is linear across any flat surface, so (w1 + w2 - 2 w0) is zero on flat ground and walls
    // and positive in concave corners (boot on grass, arm against the body, seat steps).
    // Large depth jumps (a player in front of the stands) are ignored, so there are no dark halos.
    half4 FragAO (v2f_img i) : SV_Target
    {
        float w0 = InvDepth(i.uv);
        float z0 = 1.0 / w0;
        if (z0 > 90.0) return half4(1, 1, 1, 1);
        float radiusUV = _AORadius * unity_CameraProjection._m11 * 0.5 / z0;
        radiusUV = min(radiusUV, 0.08);
        float aspect = _ScreenParams.y / _ScreenParams.x;
        float occ = 0;
        const int DIRS = 6;
        UNITY_UNROLL
        for (int k = 0; k < DIRS; k++)
        {
            float a = (k + frac(sin(dot(i.uv, float2(12.9898, 78.233))) * 43758.5453) * 0.5) * (3.14159265 / DIRS);
            float2 dir = float2(cos(a) * aspect, sin(a));
            UNITY_UNROLL
            for (int s = 1; s <= 2; s++)
            {
                float2 o = dir * radiusUV * (s * 0.5);
                float w1 = InvDepth(i.uv + o);
                float w2 = InvDepth(i.uv - o);
                float crease = (w1 + w2 - 2.0 * w0) * z0 * z0 * 0.5; // metres
                float range = step(crease, _AORadius * 2.5);
                occ += saturate(crease / (_AORadius * 0.6)) * range;
            }
        }
        occ /= DIRS * 2;
        return half4(saturate(1.0 - occ * 1.6).xxx, 1);
    }

    half4 FragBlur (v2f_img i) : SV_Target
    {
        float z0 = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, i.uv));
        half sum = 0, wsum = 0;
        UNITY_UNROLL
        for (int k = -2; k <= 2; k++)
        {
            float2 uv = i.uv + _BlurDir * _MainTex_TexelSize.xy * k * 1.5;
            float z = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
            half w = exp(-abs(z - z0) * 4.0 / max(z0 * 0.05, 0.05)) * (3 - abs(k));
            sum += tex2D(_MainTex, uv).r * w;
            wsum += w;
        }
        return half4((sum / max(wsum, 1e-4)).xxx, 1);
    }

    // ACES filmic curve (Narkowicz fit): keeps black kits from crushing and white kits from clipping.
    half3 ACES (half3 x)
    {
        const half a = 2.51, b = 0.03, c = 2.43, d = 0.59, e = 0.14;
        return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
    }

    half4 FragComposite (v2f_img i) : SV_Target
    {
        half3 c = ToLinear(tex2D(_MainTex, i.uv).rgb);
        if (_UseAO > 0.5)
        {
            half ao = tex2D(_AOTex, i.uv).r;
            c *= lerp(1.0, ao, _AOIntensity);
        }
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity;
        c *= _Exposure * _WhiteBalance.rgb;
        if (_Tonemap > 0.5) c = ACES(c * 0.8);
        else c = saturate(c);
        // Split toning: cool shadows, warm highlights (floodlight look).
        half l = Luma(c);
        c = lerp(c, c * _ShadowTint.rgb * 2.0, _ShadowTint.a * (1.0 - smoothstep(0.0, 0.5, l)));
        c = lerp(c, c * _HighlightTint.rgb * 2.0, _HighlightTint.a * smoothstep(0.5, 1.0, l));
        // Contrast around mid grey (in perceptual space) and saturation.
        half3 p = sqrt(max(c, 0));
        p = (p - 0.5) * _Contrast + 0.5;
        c = max(p, 0) * max(p, 0);
        l = Luma(c);
        c = lerp(l.xxx, c, _Saturation);
        // Very light vignette, never over the play area.
        float2 v = (i.uv - 0.5) * float2(1.0, 0.8);
        c *= 1.0 - _Vignette * smoothstep(0.35, 0.85, dot(v, v) * 2.0);
        return half4(ToOutput(saturate(c)), 1);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragPrefilter
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragDown
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragUp
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragAO
            #pragma target 3.0
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragBlur
            ENDCG }
        Pass { CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment FragComposite
            ENDCG }
    }
    Fallback Off
}
