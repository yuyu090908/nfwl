// NFWL v0.7: night sky skybox. Deep navy zenith, a warm haze of city light at the horizon,
// and small stars. Also used as the reflection environment, so glossy materials pick up a
// dark sky instead of a daytime one.
Shader "NFWL/NightSky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.01,0.015,0.05,1)
        _Horizon ("Horizon", Color) = (0.07,0.08,0.14,1)
        _Haze ("City Haze", Color) = (0.22,0.17,0.14,1)
        _StarDensity ("Star Density", Range(0,1)) = 0.35
        _StarBrightness ("Star Brightness", Float) = 1.2
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Zenith, _Horizon, _Haze;
            half _StarDensity, _StarBrightness;

            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert (appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float hash3 (float3 p)
            {
                return frac(sin(dot(p, float3(127.1, 311.7, 74.7))) * 43758.5453);
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                fixed3 col = lerp(_Horizon.rgb, _Zenith.rgb, saturate(h * 1.6));
                col += _Haze.rgb * exp(-max(h, 0) * 9.0) * 0.6;
                col = lerp(col, _Horizon.rgb * 0.5, saturate(-h * 4.0));
                // Stars: one candidate per cell of a 3D grid on the sky sphere.
                float3 cell = floor(d * 140.0);
                float r = hash3(cell);
                float3 centre = (cell + 0.5) / 140.0;
                float star = step(1.0 - _StarDensity * 0.02, r) * smoothstep(0.004, 0.0, distance(d, normalize(centre)));
                col += star * _StarBrightness * saturate(h * 4.0) * (0.6 + 0.4 * sin(_Time.y * (2.0 + r * 5.0) + r * 30.0));
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
