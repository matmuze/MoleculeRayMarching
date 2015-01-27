using UnityEngine;

[ExecuteInEditMode, AddComponentMenu("Image Effects/SSAO Pro")]
[RequireComponent(typeof(Camera))]
public class SSAOPro : MonoBehaviour
{
	public enum BlurMode
	{
		None,
		Gaussian,
		Bilateral
	}

	public enum SampleCount
	{
		VeryLow,
		Low,
		Medium,
		High
	}

	public Texture2D NoiseTexture;

	public SampleCount Samples = SampleCount.Medium;

	[Range(1, 4)]
	public int Downsampling = 1;

	[Range(0.005f, 2f)]
	public float Radius = 0.45f;

	[Range(0f, 16f)]
	public float Intensity = 2f;

	[Range(0f, 10f)]
	public float Distance = 1f;

	[Range(0f, 1f)]
	public float Bias = 0.025f;

	[Range(0f, 1f)]
	public float LumContribution = 0.7f;

	public Color OcclusionColor = Color.black;

	public bool CutoffEnabled = false;
	public float CutoffDistance = 500f;
	public float CutoffFalloff = 75f;

	public BlurMode Blur = BlurMode.None;
	public bool BlurDownsampling = false;

	public bool DebugAO = false;

	public Shader Shader;
	protected Material m_Material;
	protected Camera m_Camera;

	public Material Material
	{
		get
		{
			if (m_Material == null)
			{
				m_Material = new Material(Shader);
				m_Material.hideFlags = HideFlags.HideAndDontSave;
			}

			return m_Material;
		}
	}

	void Start()
	{
		// Disable if we don't support image effects
		if (!SystemInfo.supportsImageEffects)
		{
			Debug.LogWarning("Image Effects are not supported on this platform.");
			enabled = false;
			return;
		}

		// Disable if we don't support render textures
		if (!SystemInfo.supportsRenderTextures)
		{
			Debug.LogWarning("RenderTextures are not supported on this platform.");
			enabled = false;
			return;
		}

		// Disable the image effect if the shader can't
		// run on the users graphics card
		if (Shader != null && !Shader.isSupported)
		{
			Debug.LogWarning("Unsupported shader.");
			enabled = false;
			return;
		}

		CheckShaderStates(true);
	}

	void OnEnable()
	{
		m_Camera = GetComponent<Camera>();

		// We need both textures, the depth encoded in DepthNormals is low precision which
		// results in ugly depth artifacts
		m_Camera.depthTextureMode |= DepthTextureMode.Depth;
		m_Camera.depthTextureMode |= DepthTextureMode.DepthNormals;
	}

	void OnDestroy()
	{
		if (m_Material)
			DestroyImmediate(m_Material);
	}

	[ImageEffectOpaque]
	void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		if (Shader == null)
		{
			Graphics.Blit(source, destination);
			return;
		}

		CheckShaderStates(false);

		if (NoiseTexture != null)
			Material.SetTexture("_NoiseTex", NoiseTexture);

		Material.SetMatrix("_InverseProj", m_Camera.projectionMatrix.inverse);
		Material.SetVector("_Params1", new Vector4(NoiseTexture == null ? 0f : NoiseTexture.width, Radius, Intensity, Distance));
		Material.SetVector("_Params2", new Vector4(Bias, LumContribution, CutoffDistance, CutoffFalloff));
		Material.SetColor("_OcclusionColor", OcclusionColor);

		if (Blur == BlurMode.None)
		{
			RenderTexture rt = RenderTexture.GetTemporary(source.width / Downsampling, source.height / Downsampling, 0);

			if (DebugAO)
			{
				Graphics.Blit(source, rt, Material, 0);
				Graphics.Blit(rt, destination);
				RenderTexture.ReleaseTemporary(rt);
				return;
			}

			Graphics.Blit(source, rt, Material, 0);
			Material.SetTexture("_SSAOTex", rt);
			Graphics.Blit(source, destination, Material, 3);
			RenderTexture.ReleaseTemporary(rt);
		}
		else
		{
			int pass = (Blur == BlurMode.Bilateral) ? 2 : 1;

			int d = BlurDownsampling ? Downsampling : 1;
			RenderTexture rt1 = RenderTexture.GetTemporary(source.width / d, source.height / d, 0);
			RenderTexture rt2 = RenderTexture.GetTemporary(source.width / Downsampling, source.height / Downsampling, 0);

			// SSAO
			Graphics.Blit(source, rt1, Material, 0);

			// Horizontal blur
			Material.SetVector("_Direction", new Vector2(1f / source.width, 0f));
			Graphics.Blit(rt1, rt2, Material, pass);

			// Vertical blur
			Material.SetVector("_Direction", new Vector2(0f, 1f / source.height));
			Graphics.Blit(rt2, DebugAO ? destination : rt1, Material, pass);

			if (!DebugAO)
			{
				Material.SetTexture("_SSAOTex", rt1);
				Graphics.Blit(source, destination, Material, 3);
			}

			RenderTexture.ReleaseTemporary(rt1);
			RenderTexture.ReleaseTemporary(rt2);
		}
	}

	// State switching... Gah, ugly but does the job until I refactor it.
	bool __useNoise = false;
	bool __cutoffEnabled = false;
	float __lumContribution = 0f;
	Color __occlusionColor = Color.black;
	SampleCount __samples = SampleCount.Medium;

	void CheckShaderStates(bool force)
	{
		if (!force)
		{
			if (__useNoise == (NoiseTexture != null) &&
				__cutoffEnabled == CutoffEnabled &&
				__lumContribution == LumContribution &&
				__occlusionColor == OcclusionColor &&
				__samples == Samples)
			{
				return;
			}
		}

		Material.shaderKeywords = new string[]
		{
			(NoiseTexture != null) ? "NOISE_ON" : "NOISE_OFF",
			(CutoffEnabled) ? "DISTANCE_CUTOFF_ON" : "DISTANCE_CUTOFF_OFF",
			(LumContribution > 0.0001f) ? "LUM_CONTRIB_ON" : "LUM_CONTRIB_OFF",
			(OcclusionColor == Color.black) ? "CUSTOM_COLOR_OFF" : "CUSTOM_COLOR_ON",
			(Samples == SampleCount.Low) ? "SAMPLES_LOW"
				: (Samples == SampleCount.Medium) ? "SAMPLES_MEDIUM"
				: (Samples == SampleCount.High) ? "SAMPLES_HIGH"
				: "SAMPLES_VERY_LOW"
		};

		__useNoise = (NoiseTexture != null);
		__cutoffEnabled = CutoffEnabled;
		__lumContribution = LumContribution;
		__occlusionColor = OcclusionColor;
		__samples = Samples;
	}
}
