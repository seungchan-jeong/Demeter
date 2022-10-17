Shader "Custom/IndirectDrawMaterial"
{
    SubShader {
        Tags { "RenderType" = "Opaque" }

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex   : POSITION;
            };

            struct v2f {
                float4 vertex   : SV_POSITION;
            }; 

            struct FoliagePoint {
                float3 worldPosition;
                float3 worldNormal;
                int foliageType;
            };

            StructuredBuffer<FoliagePoint> _PointCloudBuffer;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = float4(_PointCloudBuffer[instanceID].worldPosition, 1.0f) + i.vertex;
                o.vertex = UnityObjectToClipPos(pos);
                
                return o;
            }

            fixed4 frag(v2f i) : SV_Target {
                return fixed4(1.0f, 0.0f, 0.0f, 1.0f);
            }

            ENDCG
        }
    }
}
