// One unlit, premultiplied-additive pass for the whole wave field.
//
// Additive rather than alpha-blended because the world is pure black: overlapping arcs must sum,
// and a bright corner dot has to be able to exceed the Bloom threshold without any texture behind it.
// ZTest Always because nothing else is drawn in this game, so depth ordering would only ever
// hide a wave behind an invisible collider.
// LightMode Universal2D is required: the URP 2D renderer only draws passes tagged SRPDefaultUnlit
// or Universal2D, so an untagged pass renders as nothing at all.
Shader "EchoMaze/EchoArc"
{
    Properties
    {
        _Hdr("HDR Gain", Range(0.5, 8)) = 2.5
        _Core("Core Width", Range(0.02, 0.6)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+50"
            "RenderPipeline" = "UniversalPipeline"
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend One One, One One

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex EchoArcVert
            #pragma fragment EchoArcFrag
            #pragma target 3.5

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Hdr;
                float _Core;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            Varyings EchoArcVert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }

            float4 EchoArcFrag(Varyings input) : SV_Target
            {
                float alpha = input.color.a;
                if (alpha <= 0.001)
                {
                    return float4(0.0, 0.0, 0.0, 0.0);
                }

                // uv.y runs across the ribbon: a tight bright core plus a soft skirt, so a thin
                // line still survives at low resolution instead of aliasing into a dotted stroke.
                float across = abs(input.uv.y - 0.5) * 2.0;
                float core = 1.0 - smoothstep(_Core, 1.0, across);
                float skirt = saturate(1.0 - across);
                float shape = saturate(core * 0.8 + skirt * skirt * 0.4);

                float3 rgb = input.color.rgb * (_Hdr * shape * alpha);
                return float4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
