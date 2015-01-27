Shader "Custom/RayMarchVolume" 
{	
	CGINCLUDE
	
	#include "UnityCG.cginc"
	#pragma target 5.0
	
	sampler2D _TextureMask;
	sampler2D _CubeBackTex;
	sampler3D _VolumeTex;

	int _UseMask;
	int _NumStepsMax;
	float _Opacity;	
	float _StepSize;	
	float _OffsetDist;	
	float _DepthThreshold;
	float3 _VoxelColor;

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

	// TODO: Correct depth
	float get_depth( float3 current_pos )
	{
		float4 pos = mul (UNITY_MATRIX_MVP, float4(current_pos - 0.5, 1));
		return (pos.z / pos.w) ;
	}

	// Raycast with depth 
	void frag_depth(v2f i, out float4 color : COLOR0, out float depth : DEPTH) 
	{
		depth = 1;
		color = float4(0,0,0,0.3);

		float2 uv = i.pos.xy / _ScreenParams.xy;	
		
		float3 front_pos = i.worldPos.xyz;
		
		if(_UseMask == 1)
		{
			float4 maskSample = tex2D(_TextureMask, uv);
			if(maskSample.a == 0) return;	
			front_pos = maskSample.xyz;
		}			
						
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

		float dist = (dot(-planeNormal, (back_pos - 0.5) - planePosition));			
		
		if(dist < 0) return;
		
		int numStepOffset = (dist < length_max) ? (length_max - dist) / _StepSize : 0;		

		numStepOffset += 10;

		current_pos += (numStepOffset * delta_dir);
		length_acc += (numStepOffset * delta_dir_len);

		// Do ray casting here

		[loop]
		[allow_uav_condition]
		for( uint i = 0; i < 512 ; i++ )
		{
			if(length_acc >= length_max) break;		

			float sampleValue = tex3Dlod(_VolumeTex, float4(current_pos, 0)).r;
			if(sampleValue > _DepthThreshold)
			{			
				depth = get_depth(current_pos);
				color = float4(1,1,1,1);
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
	ZWrite On
	Cull Back 	

	Pass 
	{
		CGPROGRAM
		#pragma vertex vert
		#pragma fragment frag_depth		
		ENDCG
	}				
}

Fallback off
	
} // shader