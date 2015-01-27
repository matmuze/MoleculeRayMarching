Shader "Custom/RayMarchMask" 
{	
	CGINCLUDE
	
	#include "UnityCG.cginc"
	#pragma target 5.0
		
	float _StepSize;	
	float _OffsetDist;

	sampler2D _CubeBackTex;
	sampler3D _VolumeTex;

	struct v2f
	{
		float4 pos : SV_POSITION;
		float4 worldPos : COLOR0;
	};

	v2f vert(appdata_base v)  
	{
		v2f output;
		output.pos = mul (UNITY_MATRIX_MVP, v.vertex);
		output.worldPos = v.vertex + 0.5;
        return output;
    }
	
	void frag_mask(v2f i, out float4 color : COLOR0) 
	{
		color = float4(0,0,0,0);

		float2 uv = i.pos.xy / _ScreenParams.xy;

		float3 front_pos = i.worldPos.xyz;
		float3 back_pos = tex2D(_CubeBackTex, uv).xyz;	
		float3 current_pos = front_pos;

		float3 dir = back_pos - front_pos;
		float3 delta_dir = normalize(dir) * _StepSize;	
		
		float delta_dir_len = length(delta_dir);
		float length_max = length(dir.xyz);
		float length_acc = 0;	

		// Do view-space offset here

		float3 planeNormal = normalize(_WorldSpaceCameraPos);
		float3 planePosition = planeNormal * _OffsetDist;

		float dist = abs(dot(planeNormal, (back_pos - 0.5) - planePosition));			
		int numStepOffset = (dist < length_max) ? (length_max - dist) / _StepSize : 0;		

		current_pos += (numStepOffset * delta_dir);
		length_acc += (numStepOffset * delta_dir_len);
				
		[loop]
		[allow_uav_condition]
		for( uint i = 0; i < 512 ; i++ )
		{
			if(length_acc >= length_max) break;		

			float sampleValue = tex3Dlod(_VolumeTex, float4(current_pos, 0)).r;
			if(sampleValue > 0)
			{			
				color = float4(current_pos,1);
				break;
			}

			current_pos += delta_dir;
	 		length_acc += delta_dir_len;				 		
		}
	}	

	ENDCG
	
Subshader 
{
	ZTest Always 
	Cull Back 	
		
	Pass 
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_mask						
		ENDCG
	}						
}

Fallback off
	
} // shader