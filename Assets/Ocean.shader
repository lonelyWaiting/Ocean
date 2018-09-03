Shader "Unlit/Ocean"
{
	Properties
	{
		/*displacementMap("BumpMap", 2D) = "black" {}
		normalMap("NormalMap", 2D) = "black"{}*/
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
				float2 uv		: TEXCOORD0;
				//float3 WorldPos : TEXCOORD1;
				float4 vertex	: SV_POSITION;
			};

			sampler2D displacementMap;
			sampler2D NormalMap;
			float texelLengthX2;

			v2f vert (appdata v)
			{
				float4 vertex_displace = tex2Dlod(displacementMap, float4(v.uv, 0, 0));

				v2f o;
				o.uv         = v.uv;
				v.vertex.xz += vertex_displace.xz;
				v.vertex.y   = vertex_displace.y;
				//o.WorldPos	 = v.vertex.xyz;
				o.vertex     = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				/*float3 eyeVec = _WorldSpaceCameraPos - i.WorldPos;
				eyeVec = normalize(eyeVec);

				float2 grad = tex2D(NormalMap, i.uv);
				float3 normal = normalize(float3(grad, texelLengthX2));
				float3 reflect_vec = reflect(-eyeVec, normal);
				float cos_angle = dot(normal, eyeVec);*/

				fixed4 color = fixed4(1.0f,0.0f,0.0f,1.0f);
				return color;
			}
			ENDCG
		}
	}
}
