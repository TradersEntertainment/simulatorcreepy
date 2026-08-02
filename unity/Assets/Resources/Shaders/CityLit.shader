// CityLit — the only surface shader the city uses.
//
// Every piece of city geometry is baked into a handful of big meshes with per-vertex colour,
// so the whole low-poly skyline draws in a few calls with no materials to author. Lighting is
// deliberately blunt: one directional light, a cool ambient fill, hard-ish shadows. That is
// the toy-city look the design asks for, and it is cheap enough to leave headroom for the
// crowd and traffic simulation later.
//
// Vertex colour alpha is an emissive channel: a = 1 is a normal lit surface, a = 0 glows at
// full colour. Lit windows are just quads with alpha 0, which is why dusk reads warm without
// a single extra light.

Shader "Mesruiyet/CityLit"
{
    Properties
    {
        _AmbientSky    ("Ambient sky",    Color) = (0.34, 0.40, 0.52, 1)
        _AmbientGround ("Ambient ground", Color) = (0.10, 0.11, 0.15, 1)
        _ShadowTint    ("Shadow tint",    Color) = (0.16, 0.20, 0.30, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientSky;
                half4 _AmbientGround;
                half4 _ShadowTint;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
                half4  color      : COLOR;
                float  fogCoord   : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS   = TransformObjectToWorldNormal(input.normalOS);
                o.color      = input.color;
                o.fogCoord   = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 albedo = input.color.rgb;

                // Alpha 0 means "this surface is its own light source" — lit windows, signs.
                half emissive = 1.0h - input.color.a;

                float3 n = normalize(input.normalWS);

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light main = GetMainLight(shadowCoord);

                // Half-lambert keeps the shadowed faces readable instead of crushing to black,
                // which matters a lot when every building is a flat-coloured box.
                half ndl = saturate(dot(n, main.direction)) * 0.75h + 0.25h;
                half shadow = lerp(1.0h, main.shadowAttenuation, 0.7h);

                // Hemispheric fill: sky from above, bounce from the ground plane below.
                half up = n.y * 0.5h + 0.5h;
                half3 ambient = lerp(_AmbientGround.rgb, _AmbientSky.rgb, up);

                half3 lit = albedo * (main.color * ndl * shadow + ambient);
                lit = lerp(lit * _ShadowTint.rgb + lit * 0.55h, lit, saturate(shadow + 0.35h));

                half3 rgb = lerp(lit, albedo, emissive);
                rgb = MixFog(rgb, input.fogCoord);
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientSky;
                half4 _AmbientGround;
                half4 _ShadowTint;
            CBUFFER_END

            float3 _LightDirection;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct ShadowVaryings
            {
                float4 positionCS : SV_POSITION;
            };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS   = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));

            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif

                o.positionCS = positionCS;
                return o;
            }

            half4 ShadowFrag(ShadowVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _AmbientSky;
                half4 _AmbientGround;
                half4 _ShadowTint;
            CBUFFER_END

            struct DepthAttributes { float4 positionOS : POSITION; };
            struct DepthVaryings   { float4 positionCS : SV_POSITION; };

            DepthVaryings DepthVert(DepthAttributes input)
            {
                DepthVaryings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half4 DepthFrag(DepthVaryings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
