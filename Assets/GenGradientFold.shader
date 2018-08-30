Shader "Hidden/GenGradientFold"
{
	Properties
	{
	}
	SubShader
	{
		// No culling or depth
		Cull Off ZWrite Off ZTest Always

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

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			Texture2D _MainTex;
			SamplerState point_clamp_sampler;
			int width;
			float choppyScale;
			float GridLen;

			fixed4 frag (v2f i) : SV_Target
			{
				float2 one_texel = float2(1.0f / (float)width, 1.0f / (float)width);

				float2 tc_left  = float2(i.uv.x - one_texel.x, i.uv.y);
				float2 tc_right = float2(i.uv.x + one_texel.x, i.uv.y);
				float2 tc_back  = float2(i.uv.x, i.uv.y - one_texel.y);
				float2 tc_front = float2(i.uv.x, i.uv.y + one_texel.y);

				float3 displace_left  = _MainTex.Sample(point_clamp_sampler, tc_left).xyz;
				float3 displace_right = _MainTex.Sample(point_clamp_sampler, tc_right).xyz;
				float3 displace_back  = _MainTex.Sample(point_clamp_sampler, tc_back).xyz;
				float3 displace_front = _MainTex.Sample(point_clamp_sampler, tc_front).xyz;

				float2 gradient = float2(-(displace_right.z - displace_left.z), -(displace_front.z - displace_back.z));
				
				float2 Dx = (displace_right.xy - displace_left.xy) * choppyScale * GridLen;
				float2 Dy = (displace_front.xy - displace_back.xy) * choppyScale * GridLen;
				float J = (1.0f + Dx.x) * (1.0f + Dy.y) - Dx.y * Dy.x;

				float fold = max(1.0f - J, 0);

				return fixed4(gradient, 0, fold);
			}
			ENDCG
		}
	}
}
