// NFWL v0.7: shared turf colour so the ground and the short-grass shells match exactly.
#ifndef NFWL_GRASS_INCLUDED
#define NFWL_GRASS_INCLUDED

sampler2D _NoiseTex;
fixed4 _ColorDark;
fixed4 _ColorLight;
float _StripeWidth;
float _MowSheen;
float _HalfLength;
float _HalfWidth;
fixed4 _WearColor;

// Mown stripes run across the pitch (constant x bands). The grain of a band makes it brighter seen
// with the grain and darker against it, so the pattern shifts with the camera like a real pitch.
fixed3 NFWLTurfColor (float3 worldPos, float3 viewDirWorld, out half stripeSign)
{
    float2 xz = worldPos.xz;
    half large = tex2D(_NoiseTex, xz * 0.031).r;
    half mid = tex2D(_NoiseTex, xz * 0.17 + 0.37).g;
    half fine = tex2D(_NoiseTex, xz * 1.9).b;
    half band = step(0.5, frac(xz.x / _StripeWidth * 0.5));
    stripeSign = band * 2.0 - 1.0;
    fixed3 col = lerp(_ColorDark.rgb, _ColorLight.rgb, band);
    // Viewing angle makes the mowing pattern shift, like a real pitch.
    half grain = dot(normalize(viewDirWorld.xz + 1e-4), float2(stripeSign, 0.0));
    col *= 1.0 + grain * _MowSheen;
    col *= 0.86 + large * 0.24;
    col *= 0.93 + mid * 0.12;
    col *= 0.9 + fine * 0.2;
    // Wear in the goalmouths and the centre circle (worn, slightly yellow-brown turf).
    float2 g1 = (xz - float2(_HalfLength - 2.5, 0.0)) / float2(2.2, 2.8);
    float2 g2 = (xz - float2(-_HalfLength + 2.5, 0.0)) / float2(2.2, 2.8);
    float2 cc = xz / float2(2.5, 2.5);
    half wear = saturate(exp(-dot(g1, g1)) + exp(-dot(g2, g2)) + exp(-dot(cc, cc)) * 0.5);
    wear *= smoothstep(0.35, 0.75, large * 0.6 + mid * 0.6);
    col = lerp(col, _WearColor.rgb, wear * _WearColor.a);
    return col;
}
#endif
