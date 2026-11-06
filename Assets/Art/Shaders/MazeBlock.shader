// The 3D menu diorama's only shader: unlit, because the URP renderer this project ships is a 2D one
// and no light ever travels through the menu. Faces read their own brightness from their normal —
// tops catch a little more than sides, which is the whole shading model of a model railroad.
//
// The echo wave arrives through _Wave, a per-renderer MaterialPropertyBlock value the wave component
// pushes past 1 so Bloom catches a lit wall and a dark one cannot be confused. The same wave is what
// the trim wears permanently, as a plain material value: the frame of the diorama is always humming.
//
// _Wave is deliberately NOT declared inside CBUFFER_START(UnityPerMaterial), unlike the two colours.
// Properties that sit in that buffer are fed to the SRP batcher's per-material constant buffer, and a
// MaterialPropertyBlock override of a batched property is silently dropped — in this project it was
// dropped for some walls and honoured for others, which is worse than not rendering at all, because it
// looks like a bad wave shape rather than a missing one. A value that varies per renderer has to be
// addressed per renderer.
//
// Two passes, one body: "Universal2D" because the 2D renderer only draws passes it recognises, and
// "UniversalForward" so the geometry survives being handed a forward renderer on the menu camera.
Shader "EchoMaze/MazeBlock"
{
    Properties
    {
        _BaseColor("Side Colour", Color) = (0.043, 0.055, 0.078, 1)
        _TopColor("Top Colour", Color) = (0.10, 0.14, 0.19, 1)
        _Wave("Wave (rgb x a)", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex MazeVert
            #pragma fragment MazeFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TopColor;
            CBUFFER_END

            // Outside the cbuffer on purpose — see the header.
            float4 _Wave;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings MazeVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 MazeFrag(Varyings input) : SV_Target
            {
                float up = saturate(input.normalWS.y);
                float3 base = lerp(_BaseColor.rgb, _TopColor.rgb, up);

                // The wave replaces rather than adds: a lit wall is the wave's colour at the wave's
                // intensity, so the dark slate never mutates the cyan on its way to Bloom.
                float glow = _Wave.a;
                float3 lit = max(base, _Wave.rgb * glow);
                return float4(lit, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex MazeVert
            #pragma fragment MazeFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _TopColor;
            CBUFFER_END

            // Outside the cbuffer on purpose — see the header.
            float4 _Wave;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD0;
            };

            Varyings MazeVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float4 MazeFrag(Varyings input) : SV_Target
            {
                float up = saturate(input.normalWS.y);
                float3 base = lerp(_BaseColor.rgb, _TopColor.rgb, up);

                float glow = _Wave.a;
                float3 lit = max(base, _Wave.rgb * glow);
                return float4(lit, 1.0);
            }
            ENDHLSL
        }
    }
}
