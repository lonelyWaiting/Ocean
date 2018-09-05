Shader "Unlit/UpdateDisplacementPS"
{
	Properties
	{
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }
		LOD 100

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
			};

			StructuredBuffer<float2> InputHt;
			StructuredBuffer<float2> InputDx;
			StructuredBuffer<float2> InputDy;

			int width;
			int height;
			float choppyScale;

			v2f vert (uint id : SV_VertexID)
			{
				v2f o;
				o.vertex.x = (float)(id % 2) * 4.0f - 1.0f;
				o.vertex.y = (float)(id / 2) * 4.0f - 1.0f;
				o.vertex.z = 0.0f;
				o.vertex.w = 1.0f;

				o.uv.x = (float)(id % 2) * 2.0f;
				o.uv.y = 1.0f - (float)(id / 2) * 2.0f;

				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				int index_x = (int)(i.uv.x * width);
				int index_y = (int)(i.uv.y * height);
				int addr = index_y * width + index_x;

				int sign_correction = ((index_x + index_y) & 1) ? -1 : 1;

				float dx = InputDx[addr].x * sign_correction * choppyScale;
				float dy = InputHt[addr].x * sign_correction;
				float dz = InputDy[addr].x * sign_correction * choppyScale;

				return fixed4(dx, dy, dz, 1.0f);
			}
			ENDCG
		}
	}
}
