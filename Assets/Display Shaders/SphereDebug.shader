Shader "Custom/AtomShader" 
{	
	CGINCLUDE

	#include "UnityCG.cginc"
															
	uniform float scale;	
	uniform float volumeSize;	
	uniform	StructuredBuffer<float4> atomPositions;	
	
	struct vs2gs
	{
	    float4 pos : SV_POSITION;
	    float4 info : COLOR0;
    };        	
			
	struct gs2fs
	{
		float4 pos : SV_POSITION;
		float4 info : COLOR0;
		float2 uv : TEXCOORD0;			    
	};
				
	vs2gs VS(uint id : SV_VertexID)
	{
		float4 value = (atomPositions[id] * scale) / volumeSize;

		vs2gs output;
		output.pos = float4(value.xyz, 1);
		output.info = float4(value.w, 0, 0, 0); 	    
		return output;
	}										
						
	[maxvertexcount(4)]
	void GS(point vs2gs input[1], inout TriangleStream<gs2fs> triangleStream)
	{
		float4 pos = mul(UNITY_MATRIX_MVP, float4(input[0].pos.xyz, 1.0));
        float4 offset = mul(UNITY_MATRIX_P, float4(input[0].info.x, input[0].info.x, 0, 1));

		gs2fs output;					
		output.info = float4(input[0].info.x, 0, 0, 0); 	   

		//*****//

		output.uv = float2(1.0f, 1.0f);
		output.pos = pos + float4(output.uv * offset.xy, 0, 0);
		triangleStream.Append(output);

		output.uv = float2(1.0f, -1.0f);
		output.pos = pos + float4(output.uv * offset.xy, 0, 0);
		triangleStream.Append(output);	
								
		output.uv = float2(-1.0f, 1.0f);
		output.pos = pos + float4(output.uv * offset.xy, 0, 0);
		triangleStream.Append(output);

		output.uv = float2(-1.0f, -1.0f);
		output.pos = pos + float4(output.uv * offset.xy, 0, 0);
		triangleStream.Append(output);	
	}
			
	void FS (gs2fs input, out float4 color : SV_Target0) //, out float depth : SV_Depth) 
	{	
		float lensqr = dot(input.uv, input.uv);    			
    	if(lensqr > 1.0) discard;			    
			
		//float depthAttenuationFactor = min(pow(1 - , 3) * 2, 0.65);

		color =  float4( float3(0.5,0,0) * (1-Linear01Depth(input.pos.z)), 0.5);	
	}
						
	ENDCG	
	
	SubShader 	
	{	
		// First pass
	    Pass 
	    {
			//Blend SrcAlpha OneMinusSrcAlpha
			//BlendOp Sub
			//ZWrite Off
			//Blend One One

	    	CGPROGRAM			
	    		
			#include "UnityCG.cginc"
			
			#pragma only_renderers d3d11
			#pragma target 5.0				
			
			#pragma vertex VS				
			#pragma fragment FS
			#pragma geometry GS			
		
			ENDCG
		}		
	}
	Fallback Off
}	