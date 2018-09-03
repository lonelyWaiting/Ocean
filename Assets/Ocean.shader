Shader "Unlit/Ocean"
{
	Properties
	{
		displacementMap("BumpMap", 2D) = "black" {}
		normalMap("NormalMap", 2D) = "black"{}
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

			sampler2D displacementMap;
			sampler2D normalMap;
			
			v2f vert (appdata v)
			{
				float4 vertex_displace = tex2Dlod(displacementMap, float4(v.uv, 0, 0));

				v2f o;
				o.uv         = v.uv;
				v.vertex.xz += vertex_displace.xz;
				v.vertex.y   = vertex_displace.y;
				o.vertex     = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 color = fixed4(1.0f,0.0f,0.0f,1.0f);
				return color;
			}
			ENDCG
		}
	}
}
