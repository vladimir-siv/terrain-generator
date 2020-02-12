Shader "TerrainGenerator/TerrainDisplay"
{
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			CGPROGRAM

			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct vertex
			{
				float4 position: POSITION;
			};
			
			struct fragment
			{
				float4 position: SV_POSITION;
			};

			fragment vert(vertex v)
			{
				fragment f;
				f.position = UnityObjectToClipPos(v.position);
				return f;
			}

			fixed4 frag(fragment f): SV_Target
			{
				return fixed4(1.0, 1.0, 1.0, 1.0);
			}
			
			ENDCG
		}
	}
}
