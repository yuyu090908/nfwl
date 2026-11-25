// NFWL v0.7: low-poly spectators, animated entirely on the GPU (no Animator, no scripts per fan).
// Vertex colour = clothes / skin, uv2.x = random phase, uv2.y = part (0 body, 1 arm, 2 flag).
// Globals set by the match: _NFWLCrowdExcite (0 calm .. 1 goal), _NFWLCrowdWave (0/1 wave around the stadium).
Shader "NFWL/Crowd"
{
    Properties
    {
        _Brightness ("Brightness", Range(0,2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 150

        CGPROGRAM
        #pragma surface surf Lambert vertex:vert addshadow noforwardadd nolightmap nodynlightmap nodirlightmap
        #pragma target 3.0

        half _Brightness;
        float _NFWLCrowdExcite;
        float _NFWLCrowdWave;

        struct Input { fixed4 color : COLOR; };

        void vert (inout appdata_full v)
        {
            float t = _Time.y;
            float ph = v.texcoord1.x * 6.2831;
            float part = v.texcoord1.y;
            float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
            float excite = saturate(_NFWLCrowdExcite);
            // Idle: a little sway and the odd clap.
            float idle = sin(t * (1.5 + v.texcoord1.x) + ph) * 0.012;
            // A wave travelling around the stadium.
            float wave = _NFWLCrowdWave * saturate(sin(t * 2.2 - atan2(wp.z, wp.x) * 3.0) * 2.0 - 1.0);
            // Stand up and jump when excited (each fan with their own rhythm).
            float jump = max(0.0, sin(t * (7.0 + v.texcoord1.x * 3.0) + ph));
            float rise = max(excite, wave) * (0.28 + jump * 0.1 * excite);
            v.vertex.y += idle + rise;
            // Arms go up: lifted above the head when excited, a light clap otherwise.
            float armUp = part > 0.5 ? max(excite, wave) * 0.5 + max(0.0, sin(t * 4.0 + ph)) * 0.03 : 0.0;
            v.vertex.y += armUp;
            // Flags wave side to side.
            if (part > 1.5)
            {
                v.vertex.x += sin(t * 4.0 + ph + v.vertex.y * 3.0) * (0.06 + excite * 0.12);
            }
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            o.Albedo = IN.color.rgb * _Brightness;
            o.Alpha = 1;
        }
        ENDCG
    }
}
