// NFWL v0.7: contact shadow (ambient occlusion where boots and the ball touch the grass).
// Multiplies the ground colour with a soft radial falloff; the real shadow from the floodlights
// is drawn by the shadow maps, this only adds the dark contact that shadow maps are too coarse for.
Shader "NFWL/ContactShadow"
{
    Properties
    {
        _Strength ("Strength", Range(0,1)) = 0.5
        _Softness ("Softness", Range(0.1,4)) = 1.6
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="AlphaTest+20" "IgnoreProjector"="True" }
        Blend DstColor Zero
        ZWrite Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half, _Strength)
            UNITY_INSTANCING_BUFFER_END(Props)
            half _Softness;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv * 2 - 1;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half d = saturate(1 - dot(i.uv, i.uv));
                half s = UNITY_ACCESS_INSTANCED_PROP(Props, _Strength) * pow(d, _Softness);
                return fixed4(1 - s, 1 - s, 1 - s, 1);
            }
            ENDCG
        }
    }
}
