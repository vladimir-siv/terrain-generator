Shader "TerrainGenerator/TerrainDisplay"
{
	Properties
	{
		_Scale ("_Scale", float) = 1.0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" "LightMode" = "ForwardBase" }

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "UnityLightingCommon.cginc"

			struct vertex
			{
				float4 position: POSITION;
				float3 normal: NORMAL;
			};
			
			struct fragment
			{
				float4 position: SV_POSITION;
				float3 normal: NORMAL;
				float3 world_position: TEXCOORD0;
				float3 lightdir: TEXCOORD1;
			};

			float _Scale;

			float3 hsv2rgb(float3 c)
			{
				float4 K = float4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
				float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
				return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
			}

			fragment vert(vertex v)
			{
				fragment f;
				f.position = UnityObjectToClipPos(v.position);
				f.normal = UnityObjectToWorldNormal(v.normal);
				f.world_position = mul(unity_ObjectToWorld, v.position).xyz;
				f.lightdir = normalize(UnityWorldSpaceLightDir(f.world_position));
				return f;
			}

			fixed4 frag(fragment f) : SV_Target
			{
				fixed3 col = fixed3(hsv2rgb(float3(0.75 * f.world_position.y / _Scale, 1.0, 1.0)).xyz);
				
				float3 normal = normalize(f.normal);
				float3 lightdir = normalize(f.lightdir);

				float ambient = 0.25;
				float diffuse = max(0, dot(normal, lightdir));
				return fixed4(col * _LightColor0.rgb * (ambient + diffuse), 1.0);
			}
			
			ENDCG
		}
	}
}
