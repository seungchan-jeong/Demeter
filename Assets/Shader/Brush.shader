Shader "Brush/Brush"
{
    Properties
    {
        _BrushTex("Brush Texture", 2D) = "white" {}

        [HideInInspector]_BrushPos("Mask Position", Vector) = (0.5,0.5,0.0,0.0)
        _BrushTint("Brush Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL
            #pragma multi_compile_fragment _ DEBUG_DISPLAY
            #pragma multi_compile_fragment _ CLEAR

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Fullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Debug/DebuggingFullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            TEXTURE2D_X(_TestTex);
            SAMPLER(sampler_TestTex);

            TEXTURE2D_X(_BrushTex);
            SAMPLER(sampler_BrushTex);

			float4 _BrushPos;
			float4 _BrushTint;
            float _BrushScale;

			float4 _Brush_TexelSize;
			float4 _TestTex_TexelSize;
            
            half4 Fragment(Varyings input) : SV_Target
            {
                #if defined(CLEAR)
                return half4(0.0f, 0.0f, 0.0f, 1.0f);
                #endif
                
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.uv;

                half4 col = SAMPLE_TEXTURE2D_X(_TestTex, sampler_TestTex, uv);
                
				float2 pos = float2(0.5f, 0.5f) - ((input.uv - _BrushPos.xy) * _TestTex_TexelSize.xy / (_Brush_TexelSize.xy * _BrushScale));
				float4 brushCol = float4(0,0,0,0);
				if(pos.x > 0 && pos.x < 1 && pos.y > 0 && pos.y < 1)
					brushCol = SAMPLE_TEXTURE2D_X(_BrushTex, sampler_BrushTex, pos) * _BrushTint;

                col = lerp(col, float4(brushCol.rgb,1.0), brushCol.a);
                
                #ifdef _LINEAR_TO_SRGB_CONVERSION
                col = LinearToSRGB(col);
                #endif

                #if defined(DEBUG_DISPLAY)
                half4 debugColor = 0;

                if(CanDebugOverrideOutputColor(col, uv, debugColor))
                {
                    return debugColor;
                }
                #endif
                
                return col;
            }
            ENDHLSL
        }
    }
}
