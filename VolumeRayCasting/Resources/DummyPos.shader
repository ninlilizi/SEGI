// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Custom/DummyPos" {
	SubShader {
		Tags{ "RenderType" = "RayCastVolume" "ForceNoShadowCasting" = "True" "IgnoreProjector" = "True" }
		Pass {
	        Fog { Mode Off }
			ColorMask 0
			Cull Front
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			// vertex input: position, UV
			struct appdata {
			    float4 vertex : POSITION;
			    float4 texcoord : TEXCOORD0;
			};
			
			struct v2f {
			    float4 pos : SV_POSITION;
			    float4 worldpos : TEXCOORD0;
			};
			//float4 ScaleFactor = float4(1,1,1,1);
			v2f vert (appdata v) {
			    v2f o;
			    o.pos = UnityObjectToClipPos( v.vertex );
			    o.worldpos =  v.vertex + float4(0.5f,0.5f,0.5f,0);//mul( _Object2World, v.vertex  );//ComputeScreenPos(v.vertex);
			    
			    return o;
			}
			half4 frag( v2f i ) : COLOR {
				//i.worldpos.z*=0.5f;
				//i.worldpos.z+=0.5f;
				
			    return i.worldpos;
			}
			ENDCG
	    }
	} 
	FallBack "Diffuse"
}
