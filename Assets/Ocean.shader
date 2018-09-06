Shader "Unlit/Ocean"
{
	Properties
	{
		displacementMap("BumpMap", 2D) = "black" {}
		NormalMap("NormalMap", 2D) = "black"{}
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
				float3 WorldPos : TEXCOORD1;
				float4 vertex	: SV_POSITION;
			};

			sampler2D displacementMap;
			sampler2D NormalMap;
			sampler2D FresnelMap;
			samplerCUBE reflectCube;
			float texelLengthX2;
			float3 WaterBodyColor;
			float3 skyColor;
			float3 sunDir;
			float3 sunColor;
			float3 bendParam;
			float shineness;

			v2f vert (appdata v)
			{
				float4 vertex_displace = tex2Dlod(displacementMap, float4(v.uv, 0, 0));

				v2f o;
				o.uv         = v.uv;
				v.vertex.xz += vertex_displace.xz;
				v.vertex.y   = vertex_displace.y;
				o.WorldPos	 = v.vertex.xyz;
				o.vertex     = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				float3 eyeVec = _WorldSpaceCameraPos - i.WorldPos;
				eyeVec = normalize(eyeVec);

				float2 grad = tex2Dlod(NormalMap, float4(i.uv, 0, 0)).xy;
				float3 normal = normalize(float3(grad.x, texelLengthX2, grad.y));
				// reflect(i,n): i - 2 * n * dot(n,i)
				// i: incident ray
				// https://docs.microsoft.com/en-us/windows/desktop/direct3dhlsl/dx-graphics-hlsl-reflect
				float3 reflect_vec = reflect(-eyeVec, normal);
				float cos_angle = dot(normal, eyeVec);

				float3 body_color = WaterBodyColor;

				float4 ramp = tex2D(FresnelMap, float2(cos_angle, 0.0f));

				if (reflect_vec.y < bendParam.x)
					ramp = lerp(ramp, bendParam.z, (bendParam.x - reflect_vec.y) / (bendParam.x - bendParam.y));
				reflect_vec.y = max(0, reflect_vec.y);

				float3 reflection = texCUBE(reflectCube, reflect_vec);
				// making higher contrast
				reflection = reflection * reflection * 2.5f;

				float3 reflection_color = lerp(skyColor, reflection, ramp.y);
				float3 water_color = lerp(body_color, reflection_color, ramp.x);

				float sun_spot = pow(clamp(dot(reflect_vec, sunDir), 0, 1), shineness);
				water_color += sunColor * sun_spot;

				return fixed4(water_color, 1.0f);
			}
			ENDCG
		}
	}
}
