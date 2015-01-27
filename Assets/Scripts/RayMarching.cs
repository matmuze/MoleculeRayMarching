using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class RayMarching : MonoBehaviour
{
    public Shader compositeShader;
    public Shader renderBackDepthShader;
    public Shader rayMarchShader;
    public Shader rayMarchMaskShader;
    public Shader sphereDebugShader;
    
    //public Shader volumeMaskFrontShader;
    //public Shader volumeMaskBackShader;

    public ComputeShader InitVolume;
    public ComputeShader InvertBlitVolume;
    public ComputeShader FillVolume;
    public ComputeShader SkimVolume;
    public ComputeShader BlitVolume;
    public ComputeShader MaskVolume;
    public ComputeShader InvertVolume;
    public ComputeShader EnlargeVolume;
    public ComputeShader ReadbackTexture3D;
    public ComputeShader TrimVolume;

    public Mesh CubeMesh;

    [Range(1, 64)]
    public float Scale = 1;

    [Range(0, 100)]
    public float Opacity = 100;
    
    [Range(32, 512)]
    public int NumSteps = 256;

    [Range(128, 1024)]
    public int VolumeSize = 256;

    [Range(0, 1)]
    public float DepthThreshold = 0;

    [Range(-2, 2)]
    public float RayOffset = 0;

	private Material _rayMarchMaterial;
    private Material _rayMarchMaskMaterial;
	private Material _compositeMaterial;
    private Material _backDepthMaterial;
    private Material _sphereDebugMaterial;
    
    private ComputeBuffer atomBuffer;
    private ComputeBuffer flatVolumeBuffer;

    private RenderTexture _volumeTexture_1;
    private RenderTexture _volumeTexture_2;
    private RenderTexture _volumeTexture_3;
    
    private RenderTexture _cameraDepthBuffer;
    private RenderTexture _drawVolumeTexture;
    private RenderTexture _maskVolumeTexture;

	private void OnEnable()
	{
		_rayMarchMaterial = new Material(rayMarchShader);
		_compositeMaterial = new Material(compositeShader);
        _backDepthMaterial = new Material(renderBackDepthShader);
        _sphereDebugMaterial = new Material(sphereDebugShader);
        _rayMarchMaskMaterial = new Material(rayMarchMaskShader);
    }

    private void OnDisable()
    {
        if (_volumeTexture_1 != null) _volumeTexture_1.Release(); _volumeTexture_1 = null;
        if (_volumeTexture_2 != null) _volumeTexture_2.Release(); _volumeTexture_2 = null;
        if (_volumeTexture_3 != null) _volumeTexture_3.Release(); _volumeTexture_3 = null;
        if (_cameraDepthBuffer != null) _cameraDepthBuffer.Release(); _cameraDepthBuffer = null;

        if (atomBuffer != null) atomBuffer.Release(); atomBuffer = null;
        if (flatVolumeBuffer != null) flatVolumeBuffer.Release(); flatVolumeBuffer = null;
    }

	private void Start()
	{
		CreateResources();

        FillWithData();
	}

    private List<Vector4> spheres;
    
	private void CreateResources()
	{
		_volumeTexture_1 = new RenderTexture(VolumeSize, VolumeSize, 0, RenderTextureFormat.RFloat);
        _volumeTexture_1.volumeDepth = VolumeSize;
        _volumeTexture_1.isVolume = true;
        _volumeTexture_1.enableRandomWrite = true;
        _volumeTexture_1.filterMode = FilterMode.Trilinear;
        _volumeTexture_1.wrapMode = TextureWrapMode.Clamp;
        _volumeTexture_1.Create();

        _volumeTexture_2 = new RenderTexture(VolumeSize, VolumeSize, 0, RenderTextureFormat.RFloat);
        _volumeTexture_2.volumeDepth = VolumeSize;
        _volumeTexture_2.isVolume = true;
        _volumeTexture_2.enableRandomWrite = true;
        _volumeTexture_2.filterMode = FilterMode.Trilinear;
        _volumeTexture_2.wrapMode = TextureWrapMode.Clamp;
        _volumeTexture_2.Create();

        _volumeTexture_3 = new RenderTexture(VolumeSize, VolumeSize, 0, RenderTextureFormat.RFloat);
        _volumeTexture_3.volumeDepth = VolumeSize;
        _volumeTexture_3.isVolume = true;
        _volumeTexture_3.enableRandomWrite = true;
        _volumeTexture_3.filterMode = FilterMode.Trilinear;
        _volumeTexture_3.wrapMode = TextureWrapMode.Clamp;
        _volumeTexture_3.Create();

        //flatVolumeBuffer = new ComputeBuffer(VolumeSize * VolumeSize * VolumeSize, sizeof(float), ComputeBufferType.Default);

        string pdbPath = Application.dataPath + "/Molecules/" + "1.pdb";
        var atoms = PdbReader.ReadPdbFile(pdbPath);

        atomBuffer = new ComputeBuffer(atoms.Count, sizeof(float) * 4, ComputeBufferType.Default);
        atomBuffer.SetData(atoms.ToArray());

        //string csvPath = @"D:\Projects\Unity\unity-ray-marching-volumetric-textures\unity-ray-marching-volumetric-textures\tunnel\out\analysis\tunnel_profiles.csv";
        //spheres = new List<Vector4>();
        
        //foreach (var line in File.ReadAllLines(csvPath))
        //{
        //    if(line.StartsWith("Snapshot")) continue;

        //    var split = line.Split(new [] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        //    if (String.CompareOrdinal(split[12].Trim(), "X") == 0)
        //    {
        //        for (int i = 13, j = 0; i < split.Length; i++, j ++)
        //        {
        //            spheres.Add(new Vector4(float.Parse(split[i]), 0, 0, 0));
        //        }
        //    }
        //    else
        //    {
        //        if (String.CompareOrdinal(split[12].Trim(), "Y") == 0)
        //        {
        //            for (int i = 13, j = 0; i < split.Length; i++, j++)
        //            {
        //                var v = new Vector4(spheres[j].x, float.Parse(split[i]), 0, 0);
        //                spheres[j] = v;
        //            }
        //        }
        //        if (String.CompareOrdinal(split[12].Trim(), "Z") == 0)
        //        {
        //            for (int i = 13, j = 0; i < split.Length; i++, j++)
        //            {
        //                var v = new Vector4(spheres[j].x, spheres[j].y, float.Parse(split[i]), 0);
        //                spheres[j] = v;
        //            }
        //        }
        //        if (String.CompareOrdinal(split[12].Trim(), "R") == 0)
        //        {
        //            for (int i = 13, j = 0; i < split.Length; i++, j++)
        //            {
        //                var v = new Vector4(spheres[j].x, spheres[j].y, spheres[j].z, float.Parse(split[i]));
        //                spheres[j] = v;
        //            }
        //        }
        //    }
        //}
	}

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Space)) FillWithData();
        FillWithData();
    }

    private void FillWithData()
    {
        // Init the volume data with zeros 
        InitVolume.SetFloat("_VolumeSize", VolumeSize);
        InitVolume.SetTexture(0, "_VolumeTexture", _volumeTexture_1);
        InitVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);
        
        // Fill the volume data with atom values
        FillVolume.SetFloat("_Scale", Scale);
        FillVolume.SetFloat("_VolumeSize", VolumeSize);
        FillVolume.SetInt("_DrawCount", atomBuffer.count);
        FillVolume.SetBuffer(0, "_AtomBuffer", atomBuffer);
        FillVolume.SetTexture(0, "_VolumeTexture", _volumeTexture_1);
        FillVolume.Dispatch(0, (int)Mathf.Ceil((atomBuffer.count) / 128.0f), 1, 1);

        //// Invert volume
        //InvertVolume.SetFloat("_VolumeSize", VolumeSize);
        //InvertVolume.SetTexture(0, "_VolumeTexture", _volumeTexture_1);
        //InvertVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        // Invert + blit volume
        InvertBlitVolume.SetFloat("_VolumeSize", VolumeSize);
        InvertBlitVolume.SetTexture(0, "_VolumeTextureIn", _volumeTexture_1);
        InvertBlitVolume.SetTexture(0, "_VolumeTextureOut", _volumeTexture_2);
        InvertBlitVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        //// Enlarge volume
        //InvertVolume.SetFloat("_VolumeSize", VolumeSize);
        //EnlargeVolume.SetTexture(0, "_VolumeTextureIn", _volumeTexture_1);
        //EnlargeVolume.SetTexture(0, "_VolumeTextureOut", _volumeTexture_3);
        //EnlargeVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        //// Mask volume
        //MaskVolume.SetTexture(0, "_VolumeTextureIn", _volumeTexturePung);
        //MaskVolume.SetTexture(0, "_VolumeTextureOut", _volumeTexturePong);
        //MaskVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        // Trim
        TrimVolume.SetFloat("_VolumeSize", VolumeSize);
        TrimVolume.SetTexture(0, "_VolumeTextureIn", _volumeTexture_2);
        TrimVolume.SetTexture(0, "_VolumeTextureOut", _volumeTexture_3);
        TrimVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        // Skim
        SkimVolume.SetFloat("_VolumeSize", VolumeSize);
        SkimVolume.SetTexture(0, "_VolumeTextureIn", _volumeTexture_3);
        SkimVolume.SetTexture(0, "_VolumeTextureOut", _volumeTexture_2);
        SkimVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        //// Invert volume
        //InvertVolume.SetFloat("_VolumeSize", VolumeSize);
        //InvertVolume.SetTexture(0, "_VolumeTexture", _volumeTexture_1);
        //InvertVolume.Dispatch(0, VolumeSize / 8, VolumeSize / 8, VolumeSize / 8);

        
        _maskVolumeTexture = _volumeTexture_1;
        _drawVolumeTexture = _volumeTexture_2;
    }

    private bool _drawAtoms = false;

    [ImageEffectOpaque]
    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (_cameraDepthBuffer != null && (_cameraDepthBuffer.width != Screen.width || _cameraDepthBuffer.height != Screen.height))
        {
            _cameraDepthBuffer.Release(); _cameraDepthBuffer = null;
        }

        if (_cameraDepthBuffer == null)
        {
            _cameraDepthBuffer = new RenderTexture(source.width, source.height, 24, RenderTextureFormat.Depth);
            _cameraDepthBuffer.anisoLevel = 9;
            _cameraDepthBuffer.filterMode = FilterMode.Trilinear;
        }

        if (_drawAtoms)
        {
            _sphereDebugMaterial.SetFloat("scale", Scale);
            _sphereDebugMaterial.SetFloat("volumeSize", VolumeSize);
            _sphereDebugMaterial.SetBuffer("atomPositions", atomBuffer);
            _sphereDebugMaterial.SetPass(0);

            Graphics.SetRenderTarget(source);
            Graphics.DrawProcedural(MeshTopology.Points, atomBuffer.count);
        }

        // Render cube back depth
        var backDepth = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
        Graphics.SetRenderTarget(backDepth);
        GL.Clear(true, true, new Color(0, 0, 0, 0));

        _backDepthMaterial.SetPass(0);
        Graphics.DrawMeshNow(CubeMesh, GetComponent<MouseOrbit>().target, Quaternion.identity);

        // Render mask volume
        var volumeMask = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGBFloat);
        Graphics.SetRenderTarget(volumeMask.colorBuffer, _cameraDepthBuffer.depthBuffer);
        GL.Clear(true, true, new Color(0, 0, 0, 0));

        _rayMarchMaskMaterial.SetFloat("_OffsetDist", RayOffset);
        _rayMarchMaskMaterial.SetFloat("_StepSize", 1.0f / NumSteps);
        _rayMarchMaskMaterial.SetTexture("_CubeBackTex", backDepth);
        _rayMarchMaskMaterial.SetTexture("_VolumeTex", _maskVolumeTexture);
        _rayMarchMaskMaterial.SetPass(0);

        Graphics.DrawMeshNow(CubeMesh, GetComponent<MouseOrbit>().target, Quaternion.identity);


        // Render volume
        var volumeTarget = RenderTexture.GetTemporary(source.width, source.height, 0, source.format);
        Graphics.SetRenderTarget(volumeTarget.colorBuffer, _cameraDepthBuffer.depthBuffer);
        GL.Clear(true, true, new Color(0, 0, 0, 0));

        _rayMarchMaterial.SetInt("_UseMask", 1);
        _rayMarchMaterial.SetFloat("_StepSize", 1.0f / NumSteps);
        _rayMarchMaterial.SetFloat("_OffsetDist", RayOffset);
        _rayMarchMaterial.SetFloat("_DepthThreshold", DepthThreshold);
        _rayMarchMaterial.SetTexture("_TextureMask", volumeMask);
        _rayMarchMaterial.SetTexture("_CubeBackTex", backDepth);
        _rayMarchMaterial.SetTexture("_VolumeTex", _drawVolumeTexture);
        _rayMarchMaterial.SetPass(0);

        Graphics.DrawMeshNow(CubeMesh, GetComponent<MouseOrbit>().target, Quaternion.identity);

        Shader.SetGlobalTexture("_CameraDepthTexture", _cameraDepthBuffer);

        // Composite pass
        _compositeMaterial.SetTexture("_BlendTex", volumeTarget);
        Graphics.Blit(source, destination, _compositeMaterial);

        RenderTexture.ReleaseTemporary(volumeMask);
        RenderTexture.ReleaseTemporary(volumeTarget);
        RenderTexture.ReleaseTemporary(backDepth);
    }
}