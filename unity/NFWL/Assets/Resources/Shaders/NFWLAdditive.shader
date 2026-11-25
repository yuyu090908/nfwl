// NFWL v0.7: unlit additive glow for decoration only (floodlight halos, light shafts, LED boards).
// It never lights players or grass: real lighting comes from the few real-time lights.
// _FadeNear hides a shaft as the camera gets close so it never fogs the play.
Shader "NFWL/Additive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        [HDR] _Color ("Color", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1
        _ScrollX ("Scroll X (LED)", Float) = 0
        _FadeNear ("Fade Near Camera (m)", Float) = 0
        _SoftEdge ("Soft Edge (view angle)", Range(0,1)) = 0
        _Flicker ("Flicker", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            half4 _Color;
            half _Intensity, _ScrollX, _FadeNear, _SoftEdge, _Flicker;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fade : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert (appdata_full v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex) + float2(_Time.y * _ScrollX, 0);
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 wn = UnityObjectToWorldNormal(v.normal);
                float3 vd = normalize(_WorldSpaceCameraPos - wp);
                float near = _FadeNear > 0 ? saturate((distance(wp, _WorldSpaceCameraPos) - _FadeNear) / _FadeNear) : 1;
                // Light shafts: faces seen edge-on fade out, which hides the cone's polygon outline.
                float edge = lerp(1, saturate(abs(dot(wn, vd)) * 1.5), _SoftEdge);
                o.fade = near * edge * v.color.a;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                half flicker = 1 - _Flicker * 0.5 * (1 + sin(_Time.y * 23.0 + i.uv.x * 40.0));
                half4 c = tex2D(_MainTex, i.uv) * _Color * (_Intensity * i.fade * flicker);
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, fixed4(0,0,0,0));
                return c;
            }
            ENDCG
        }
    }
}
