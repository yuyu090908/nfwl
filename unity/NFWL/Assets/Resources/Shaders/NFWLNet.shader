// NFWL v0.7: goal net. A deforming mesh with a procedural knotted mesh pattern (alpha cut),
// lit by the floodlights and casting a net-patterned shadow onto the grass.
Shader "NFWL/Net"
{
    Properties
    {
        _Color ("Color", Color) = (0.92,0.94,0.96,1)
        _Cells ("Cells per m", Float) = 8
        _Thickness ("String Thickness", Range(0.02,0.4)) = 0.1
        _Glossiness ("Smoothness", Range(0,1)) = 0.2
        _Cutoff ("Cutoff", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        Cull Off
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard addshadow alphatest:_Cutoff vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        half _Cells, _Thickness, _Glossiness;

        struct Input { float2 netUV; };

        void vert (inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            // texcoord holds the rest position of the vertex on the panel in metres.
            o.netUV = v.texcoord.xy * _Cells;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Diamond mesh: two families of diagonal strings.
            float2 uv = IN.netUV;
            float a = abs(frac(uv.x + uv.y) - 0.5);
            float b = abs(frac(uv.x - uv.y) - 0.5);
            float s = max(step(0.5 - _Thickness, a), step(0.5 - _Thickness, b));
            o.Albedo = _Color.rgb;
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
            o.Alpha = s;
        }
        ENDCG
    }
}
