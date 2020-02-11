Shader "Unlit/ColorBlend"
{
	Properties
	{
		_Color("_Color", COLOR) = (1,1,1,1)
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			Blend SrcAlpha OneMinusSrcAlpha
			
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			
			fixed4 _Color;
			
			float4 vert(float4 position: POSITION): SV_POSITION
			{
				return UnityObjectToClipPos(position);
			}
			
			fixed4 frag(float4 position: SV_POSITION): SV_Target
			{
				return _Color;
			}
			
			ENDCG
		}
	}
}
