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

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

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

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv     = v.uv;
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				int index_x = (int)(i.uv.x * width);
				int index_y = (int)(i.uv.y * height);
				int addr = index_y * width + index_x;

				int sign_correction = ((index_x + index_y) & 1) ? -1 : 1;

				float dx = InputDx[addr].x * sign_correction;
				float dy = InputDy[addr].x * sign_correction;
				float dz = InputHt[addr].x * sign_correction;

				return fixed4(dx, dy, dz, 1.0f);
			}
			ENDCG
		}
	}
}
