using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using System.Collections.Generic;
using Unity.VisualScripting;
using Unity.Collections;
using System;

public class OceanGenerator : MonoBehaviour
{
    [Header("Settings & Debug")]
    [SerializeField, Tooltip("Enables time affecting waves.")] bool enableTime = true;
    [SerializeField, Tooltip("Enables IFFT displacement calculations.")] bool enableIFFT = true;
    [SerializeField, Tooltip("When toggled resets ocean generation.")] bool resetGeneration = false;
    public Camera mainCam;
    [SerializeField] Material displacementMaterial;
    [SerializeField] Material screenMaterial;
    [Range(0, 5)] public int oceanPreset = 0;
    int currentPreset;
    [Range(0, 4), Tooltip("Swell comes from a distant storm decoupled from local wind.")]
    public int swellPreset = 0;
    int currentSwellPreset = -1;

    [Header("Global Ocean Variables")]
    [Tooltip("Constant that scales the speed of time.")] public float timeScale = 1f;
    [Tooltip("The current time.")] public float currentTime = 0;
    [Tooltip("Gravitational constant.")] public float gravity = 9.81f;

    [Header("Cascade Parameters")]
    [Tooltip("Ocean simulation texture resolution per cascade.")] public int N = 512;
    [Tooltip("Lengths in meters for each cascade determining cascade count.")] public List<float> cascadeLengths = new List<float>() { 0.1f, 1f, 10f, 100f, 1000f, 10000f };

    [Header("Main Spectrum Layer")]
    [Tooltip("Wind speed.")] public float windSpeed = 20f;
    [Tooltip("Wind direction.")] public Vector2 windDir = new(1, 1);
    [Tooltip("Distance the wind has blown over the water.")] public float Fetch = 100000;
    [Tooltip("Ocean depth parameter.")] public float Depth = 1000;
    [Tooltip("Peak enhancement constant ranging from one to seven.")] public float Gamma = 3.3f;
    [Tooltip("Wave spread power from the wind direction.")] public float SpreadPower = 12;
    [Tooltip("Choppiness scale multiplying horizontal displacement.")] public List<float> ChoppinessScale = new List<float>() { 0.3f, 0.5f, 0.7f, 1.0f, 1.3f, 1.5f };

    [Header("Swell Layer (dual JONSWAP)")]
    [Tooltip("Adds a second JONSWAP spectrum representing distant storm swell.")] public bool enableSwellLayer = true;
    [Tooltip("Wind speed of the distant storm generating the swell.")] public float swellWindSpeed = 12f;
    [Tooltip("Swell direction decoupled from local wind.")] public Vector2 swellWindDir = new(1, 0.4f);
    [Tooltip("Distance the swell has traveled over water.")] public float swellFetch = 800000f;
    [Tooltip("Gamma parameter for swell spectra.")] public float swellGamma = 1.0f;
    [Tooltip("Directional spread power for the swell layer.")] public float swellSpreadPower = 40f;
    [Range(0f, 3f), Tooltip("Independent energy dial for the swell layer.")] public float swellEnergyScale = 1.0f;

    [Header("Foam Settings")]
    [Tooltip("Foam simulation texture resolution.")] public int foamResolution = 512;
    public float foamThreshold = 0.2f;
    public float foamGenStrength = 3.0f;
    public float foamDecayRate = 1.0f;

    [Header("Cascade Spectrum Textures")]
    public RenderTexture initSpectrumTexArray;
    [Tooltip("Packed complex spectra containing two real fields per channel.")]
    public RenderTexture packedDisplacementSpectrumArray;
    [Tooltip("Packed complex spectra containing slope and Jacobian terms.")]
    public RenderTexture packedSlopeSpectrumArray;

    [Header("Cascade IFFT Textures")]
    public RenderTexture twiddleFactorTex;
    public RenderTexture pingPongAArray;
    public RenderTexture pingPongBArray;

    [Header("Cascade Displacement Textures")]
    public RenderTexture displacementTexHeightArray;
    public RenderTexture displacementTexXArray;
    public RenderTexture displacementTexYArray;

    [Header("Cascade Slope Textures (Analytic Normals)")]
    [Tooltip("Final height gradient textures sampled directly for normals.")]
    public RenderTexture slopeTexXArray;
    public RenderTexture slopeTexZArray;

    [Header("Cascade Jacobian Textures (Analytic Foam)")]
    [Tooltip("Final horizontal displacement Jacobian textures sampled directly for foam.")]
    public RenderTexture jacobianTexXXArray;
    public RenderTexture jacobianTexZZArray;
    public RenderTexture jacobianTexXZArray;

    [Header("Foam Textures")]
    public RenderTexture foamGenTexArray;
    public RenderTexture foamAccumTexArrayA;
    public RenderTexture foamAccumTexArrayB;
    private bool foamInA = true;

    [Header("Shaders")]
    [SerializeField] ComputeShader SpectrumShader;
    [SerializeField] ComputeShader IFFTShader;
    [SerializeField] ComputeShader FoamShader;

    //Store compute buffer for cascade lengths
    private ComputeBuffer cascadeLBuffer;
    //Store compute buffer for cascade choppiness scales
    private ComputeBuffer cascadeChoppinessBuffer;
    //Store compute buffer for lower wave number cutoffs
    private ComputeBuffer cascadeKLowBuffer;
    //Store compute buffer for higher wave number cutoffs
    private ComputeBuffer cascadeKHighBuffer;

    private int lastCascadeCount = -1;
    private void OnValidate()
    {
        //Ensure list sizes match cascade configurations
        EnsureChoppinessListSize();

        //Flag generation reset when cascade count changes at runtime
        if (Application.isPlaying && cascadeLengths != null && cascadeLengths.Count != lastCascadeCount)
        {
            resetGeneration = true;
        }
    }

    private void EnsureChoppinessListSize()
    {
        //Validate list initialization and sizing
        if (cascadeLengths == null)
        {
            return;
        }
        if (ChoppinessScale == null)
        {
            ChoppinessScale = new List<float>();
        }

        //Adjust list count to match cascade lengths
        while (ChoppinessScale.Count < cascadeLengths.Count)
        {
            ChoppinessScale.Add(1.0f);
        }
        while (ChoppinessScale.Count > cascadeLengths.Count)
        {
            ChoppinessScale.RemoveAt(ChoppinessScale.Count - 1);
        }
    }

    //Retrieve choppiness value for specific cascade index safely
    private float GetChoppinessForCascade(int index)
    {
        //Check bounds before returning choppiness scale
        if (ChoppinessScale == null || index < 0 || index >= ChoppinessScale.Count)
        {
            return 1.0f;
        }
        return ChoppinessScale[index];
    }
    public void Start()
    {
        //Initialize generator state and reset simulation textures
        EnsureChoppinessListSize();

        resetGeneration = true;
        currentPreset = -1;
        currentSwellPreset = -1;

        //Configure debug cascade length arrays globally
        float[] debugCascadeLengths = new float[32];
        Shader.SetGlobalFloatArray("SampleWaterHeight", debugCascadeLengths);
    }
    public void Update()
    {
        //Handle texture generation reset sequence when triggered
        if (resetGeneration)
        {
            ResetTextures();
            PrecomputeTwiddleFactor();

            RunningInitialSpectrum();

            displacementMaterial.SetTexture("_Displacement_Texture_Height_Array", displacementTexHeightArray);
            displacementMaterial.SetTexture("_Displacement_Texture_X_Array", displacementTexXArray);
            displacementMaterial.SetTexture("_Displacement_Texture_Y_Array", displacementTexYArray);
            displacementMaterial.SetTexture("_Slope_Texture_X_Array", slopeTexXArray);
            displacementMaterial.SetTexture("_Slope_Texture_Z_Array", slopeTexZArray);

            screenMaterial.SetTexture("_Displacement_Texture_Height_Array", displacementTexHeightArray);

            Shader.SetGlobalFloat("_TexelSize", 1.0f / N);
            Shader.SetGlobalFloatArray("_CascadeLs", cascadeLengths);
            Shader.SetGlobalInt("_CascadeCount", cascadeLengths.Count);

            resetGeneration = !resetGeneration;
        }

        //Apply configured ocean and swell preset parameters
        OceanPresets();
        SwellPresets();

        //Advance simulation time and update spectrum textures per frame
        if (enableTime && !resetGeneration)
        {
            currentTime += Time.deltaTime * timeScale;

            RunningSpectrumUpdate();

            //Execute IFFT passes when enabled
            if (enableIFFT)
            {
                RunningIFFT_Displacement();
                RunningIFFT_Slope();
                RunningFoam();

                displacementTexHeightArray.GenerateMips();
                displacementTexXArray.GenerateMips();
                displacementTexYArray.GenerateMips();

                slopeTexXArray.GenerateMips();
                slopeTexZArray.GenerateMips();
            }

            foamInA = !foamInA;
            displacementMaterial.SetTexture("_Foam_Texture_Array", foamInA ? foamAccumTexArrayA : foamAccumTexArrayB);
        }

        //Update screen materials and underwater camera states
        if (screenMaterial != null && displacementTexHeightArray != null)
        {
            screenMaterial.SetTexture("HeightTexArray", displacementTexHeightArray);

            if (mainCam.transform != null)
            {
                bool isUnderwater = mainCam.transform.position.y < 0;
                screenMaterial.SetFloat("_CameraUnderwater", isUnderwater ? 1f : 0f);
            }
        }
    }
    public void ResetTextures()
    {
        //Release existing render textures and compute buffers from memory
        if (initSpectrumTexArray != null)
        {
            initSpectrumTexArray.Release();

            packedDisplacementSpectrumArray.Release();
            packedSlopeSpectrumArray.Release();

            twiddleFactorTex.Release();
            pingPongAArray.Release();
            pingPongBArray.Release();

            displacementTexHeightArray.Release();
            displacementTexXArray.Release();
            displacementTexYArray.Release();
            slopeTexXArray.Release();
            slopeTexZArray.Release();
            jacobianTexXXArray.Release();
            jacobianTexZZArray.Release();
            jacobianTexXZArray.Release();
        }

        cascadeLBuffer?.Release();
        cascadeChoppinessBuffer?.Release();
        cascadeKLowBuffer?.Release();
        cascadeKHighBuffer?.Release();
        BuildCascadeBuffers();

        //Allocate fresh render textures for ocean simulation channels
        initSpectrumTexArray = CreateTextures(N, N, cascadeLengths.Count, false, false, false);

        packedDisplacementSpectrumArray = CreateTextures(N, N, cascadeLengths.Count, false, false, false);
        packedSlopeSpectrumArray = CreateTextures(N, N, cascadeLengths.Count, false, false, false);

        twiddleFactorTex = CreateTextures((int)Mathf.Log(N, 2), N, 0, false, false, false);
        pingPongAArray = CreateTextures(N, N, cascadeLengths.Count, false, false, false);
        pingPongBArray = CreateTextures(N, N, cascadeLengths.Count, false, false, false);

        displacementTexHeightArray = CreateTextures(N, N, cascadeLengths.Count, true, true, true);
        displacementTexXArray = CreateTextures(N, N, cascadeLengths.Count, true, true, true);
        displacementTexYArray = CreateTextures(N, N, cascadeLengths.Count, true, true, true);

        slopeTexXArray = CreateTextures(N, N, cascadeLengths.Count, true, true, true);
        slopeTexZArray = CreateTextures(N, N, cascadeLengths.Count, true, true, true);

        jacobianTexXXArray = CreateTextures(N, N, cascadeLengths.Count, true, true, false);
        jacobianTexZZArray = CreateTextures(N, N, cascadeLengths.Count, true, true, false);
        jacobianTexXZArray = CreateTextures(N, N, cascadeLengths.Count, true, true, false);

        foamGenTexArray = CreateTextures(foamResolution, foamResolution, cascadeLengths.Count, true, true, false);
        foamAccumTexArrayA = CreateTextures(foamResolution, foamResolution, cascadeLengths.Count, true, true, false);
        foamAccumTexArrayB = CreateTextures(foamResolution, foamResolution, cascadeLengths.Count, true, true, false);
    }
    //Upload cascade parameters into compute buffers
    private void BuildCascadeBuffers()
    {
        int count = cascadeLengths.Count;
        float[] lArr = new float[count];
        float[] choppinessArr = new float[count];
        float[] kLowArr = new float[count];
        float[] kHighArr = new float[count];

        //Calculate wave number cutoffs and length parameters for each cascade
        for (int i = 0; i < count; i++)
        {
            lArr[i] = cascadeLengths[i];
            choppinessArr[i] = GetChoppinessForCascade(i);

            float kNyquistThis = Mathf.PI * N / cascadeLengths[i];
            float kNyquistLarger = (i < count - 1) ? Mathf.PI * N / cascadeLengths[i + 1] : 0f;
            kLowArr[i] = kNyquistLarger;
            kHighArr[i] = kNyquistThis;
        }

        cascadeLBuffer = new ComputeBuffer(count, sizeof(float));
        cascadeChoppinessBuffer = new ComputeBuffer(count, sizeof(float));
        cascadeKLowBuffer = new ComputeBuffer(count, sizeof(float));
        cascadeKHighBuffer = new ComputeBuffer(count, sizeof(float));

        cascadeLBuffer.SetData(lArr);
        cascadeChoppinessBuffer.SetData(choppinessArr);
        cascadeKLowBuffer.SetData(kLowArr);
        cascadeKHighBuffer.SetData(kHighArr);
    }

    private void OnDisable()
    {
        //Release compute buffers safely when component is disabled
        cascadeLBuffer?.Release();
        cascadeChoppinessBuffer?.Release();
        cascadeKLowBuffer?.Release();
        cascadeKHighBuffer?.Release();
    }

    public static RenderTexture CreateTextures(int sizeX, int sizeY, int depth, bool repeatTex, bool useBilinear, bool useMipMaps)
    {
        RenderTexture texture = new RenderTexture(sizeX, sizeY, 0);
        texture.enableRandomWrite = true;
        texture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16G16B16A16_SFloat;

        //Configure volume depth for texture array support
        if (depth > 0)
        {
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex2DArray;
            texture.volumeDepth = depth;
        }
        else
        {
            texture.dimension = UnityEngine.Rendering.TextureDimension.Tex2D;
        }

        //Enable mipmaps when requested
        if (useMipMaps)
        {
            texture.useMipMap = true;
            texture.autoGenerateMips = false;
        }

        texture.filterMode = useBilinear ? FilterMode.Bilinear : FilterMode.Point;
        texture.wrapMode = repeatTex ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;

        texture.Create();
        return texture;
    }
    void PrecomputeTwiddleFactor()
    {
        //Dispatch twiddle factor kernel for IFFT computations
        int twiddleFactorIndex = IFFTShader.FindKernel("TwiddleFactor");
        IFFTShader.SetInt("N", N);
        IFFTShader.SetTexture(twiddleFactorIndex, "TwiddleFactorTex", twiddleFactorTex);
        IFFTShader.Dispatch(twiddleFactorIndex, Mathf.CeilToInt((float)Mathf.Log(N, 2) / 16), N / 16, 1);
    }
    void RunningInitialSpectrum()
    {
        //Find JONSWAP spectrum compute kernel
        int spectrumKernalIndex = SpectrumShader.FindKernel("JonswapSpectrum");

        SpectrumShader.SetTexture(spectrumKernalIndex, "Spectrum", initSpectrumTexArray);

        //Dispatch local wind sea layer
        DispatchJonswapLayer(spectrumKernalIndex, windDir, windSpeed, Fetch, Gamma, SpreadPower,
            layerIndex: 0, accumulate: false, energyScale: 1f);

        //Dispatch optional distant swell layer
        if (enableSwellLayer)
        {
            DispatchJonswapLayer(spectrumKernalIndex, swellWindDir, swellWindSpeed, swellFetch, swellGamma, swellSpreadPower,
                layerIndex: 1, accumulate: true, energyScale: swellEnergyScale);
        }
    }

    void DispatchJonswapLayer(int kernelIndex, Vector2 dir, float speed, float fetch, float gamma, float spread,
        int layerIndex, bool accumulate, float energyScale)
    {
        //Pass spectrum generation parameters to compute shader
        SpectrumShader.SetInt("N", N);
        SpectrumShader.SetVector("Omega", dir);
        SpectrumShader.SetFloat("V", speed);
        SpectrumShader.SetFloat("g", gravity);
        SpectrumShader.SetFloat("Time", currentTime);

        SpectrumShader.SetFloat("Fetch", fetch);
        SpectrumShader.SetFloat("Depth", Depth);
        SpectrumShader.SetFloat("Gamma", gamma);
        SpectrumShader.SetFloat("SpreadPower", spread);

        SpectrumShader.SetBuffer(kernelIndex, "CascadeL", cascadeLBuffer);
        SpectrumShader.SetBuffer(kernelIndex, "CascadeChoppinessScale", cascadeChoppinessBuffer);
        SpectrumShader.SetBuffer(kernelIndex, "CascadeKLow", cascadeKLowBuffer);
        SpectrumShader.SetBuffer(kernelIndex, "CascadeKHigh", cascadeKHighBuffer);

        SpectrumShader.SetInt("LayerIndex", layerIndex);
        SpectrumShader.SetInt("Accumulate", accumulate ? 1 : 0);
        SpectrumShader.SetFloat("LayerEnergyScale", energyScale);

        SpectrumShader.Dispatch(kernelIndex, N / 16, N / 16, cascadeLengths.Count);
    }
    void RunningSpectrumUpdate()
    {
        //Find packed runtime spectrum compute kernel
        int packedKernelIndex = SpectrumShader.FindKernel("RuntimeSpectrumPacked");

        SpectrumShader.SetInt("N", N);
        SpectrumShader.SetVector("Omega", windDir);
        SpectrumShader.SetFloat("V", windSpeed);
        SpectrumShader.SetFloat("g", gravity);
        SpectrumShader.SetFloat("Time", currentTime);

        SpectrumShader.SetFloat("Fetch", Fetch);
        SpectrumShader.SetFloat("Depth", Depth);
        SpectrumShader.SetFloat("Gamma", Gamma);
        SpectrumShader.SetFloat("SpreadPower", SpreadPower);

        SpectrumShader.SetBuffer(packedKernelIndex, "CascadeL", cascadeLBuffer);
        SpectrumShader.SetBuffer(packedKernelIndex, "CascadeChoppinessScale", cascadeChoppinessBuffer);

        SpectrumShader.SetTexture(packedKernelIndex, "Spectrum", initSpectrumTexArray);
        SpectrumShader.SetTexture(packedKernelIndex, "PackedDisplacementSpectrum", packedDisplacementSpectrumArray);
        SpectrumShader.SetTexture(packedKernelIndex, "PackedSlopeSpectrum", packedSlopeSpectrumArray);

        SpectrumShader.Dispatch(packedKernelIndex, N / 16, N / 16, cascadeLengths.Count);
    }

    void RunningIFFT_Displacement()
    {
        //Execute packed IFFT for displacement outputs
        RunPackedIFFT(packedDisplacementSpectrumArray, "FinalOutputDisplacement",
            ("DisplacementXOut", displacementTexXArray),
            ("DisplacementZOut", displacementTexYArray),
            ("HeightOut", displacementTexHeightArray),
            ("JacobianXZOut", jacobianTexXZArray));
    }

    void RunningIFFT_Slope()
    {
        //Execute packed IFFT for slope outputs
        RunPackedIFFT(packedSlopeSpectrumArray, "FinalOutputSlope",
            ("SlopeXOut", slopeTexXArray),
            ("SlopeZOut", slopeTexZArray),
            ("JacobianXXOut", jacobianTexXXArray),
            ("JacobianZZOut", jacobianTexZZArray));
    }

    void RunPackedIFFT(RenderTexture packedSpectrum, string finalKernelName,
        params (string uavName, RenderTexture target)[] outputs)
    {
        int rowIndex = IFFTShader.FindKernel("SharedMemoryFFTRow");
        int columnIndex = IFFTShader.FindKernel("SharedMemoryFFTColumn");
        int finalOutputIndex = IFFTShader.FindKernel(finalKernelName);

        IFFTShader.SetInt("N", N);
        int cascadeCount = cascadeLengths.Count;

        //Perform row butterfly pass using shared memory
        IFFTShader.SetTexture(rowIndex, "TwiddleFactorTex", twiddleFactorTex);
        IFFTShader.SetTexture(rowIndex, "BufferIn", packedSpectrum);
        IFFTShader.SetTexture(rowIndex, "BufferOut", pingPongAArray);
        IFFTShader.Dispatch(rowIndex, 1, N, cascadeCount);

        //Perform column butterfly pass using shared memory
        IFFTShader.SetTexture(columnIndex, "TwiddleFactorTex", twiddleFactorTex);
        IFFTShader.SetTexture(columnIndex, "BufferIn", pingPongAArray);
        IFFTShader.SetTexture(columnIndex, "BufferOut", pingPongBArray);
        IFFTShader.Dispatch(columnIndex, N, 1, cascadeCount);

        //Unpack and store final spatial results
        IFFTShader.SetTexture(finalOutputIndex, "BufferIn", pingPongBArray);
        foreach (var (uavName, target) in outputs)
        {
            IFFTShader.SetTexture(finalOutputIndex, uavName, target);
        }

        IFFTShader.Dispatch(finalOutputIndex, N / 16, N / 16, cascadeCount);
    }
    void RunningFoam()
    {
        //Find foam generation and accumulation kernels
        int genIndex = FoamShader.FindKernel("FoamGenerate");
        int accIndex = FoamShader.FindKernel("FoamAccumulate");

        FoamShader.SetInt("FoamN", foamResolution);
        FoamShader.SetInt("DisplacementN", N);
        FoamShader.SetFloat("FoamThreshold", foamThreshold);
        FoamShader.SetFloat("FoamGenStrength", foamGenStrength);
        FoamShader.SetFloat("FoamDecayRate", foamDecayRate);
        FoamShader.SetFloat("DeltaTime", Time.deltaTime * timeScale);
        FoamShader.SetBuffer(accIndex, "CascadeL", cascadeLBuffer);

        int cascadeCount = cascadeLengths.Count;

        FoamShader.SetTexture(genIndex, "JacobianXX", jacobianTexXXArray);
        FoamShader.SetTexture(genIndex, "JacobianZZ", jacobianTexZZArray);
        FoamShader.SetTexture(genIndex, "JacobianXZ", jacobianTexXZArray);
        FoamShader.SetTexture(genIndex, "FoamGenTexArray", foamGenTexArray);
        FoamShader.Dispatch(genIndex, foamResolution / 16, foamResolution / 16, cascadeCount);

        RenderTexture accIn = foamInA ? foamAccumTexArrayA : foamAccumTexArrayB;
        RenderTexture accOut = foamInA ? foamAccumTexArrayB : foamAccumTexArrayA;

        FoamShader.SetTexture(accIndex, "FoamGenTexArray", foamGenTexArray);
        FoamShader.SetTexture(accIndex, "DisplacementX", displacementTexXArray);
        FoamShader.SetTexture(accIndex, "DisplacementZ", displacementTexYArray);
        FoamShader.SetTexture(accIndex, "FoamAccumIn", accIn);
        FoamShader.SetTexture(accIndex, "FoamAccumOut", accOut);
        FoamShader.Dispatch(accIndex, foamResolution / 16, foamResolution / 16, cascadeCount);
    }
    void OceanPresets()
    {
        //Apply preset wind and sea parameters when preset index changes
        if (oceanPreset != currentPreset)
        {
            if (oceanPreset == 0)
            {
                windSpeed = 3;
                windDir = new(1, 0);
                Fetch = 10000;
                Depth = 1000;
                Gamma = 1.0f;
                SpreadPower = 20;

            }
            if (oceanPreset == 1)
            {
                windSpeed = 3;
                windDir = new(1, 0);
                Fetch = 10000;
                Depth = 1000;
                Gamma = 1.0f;
                SpreadPower = 6;

            }
            else if (oceanPreset == 2)
            {
                windSpeed = 7;
                windDir = new(1, 0);
                Fetch = 50000;
                Depth = 1000;
                Gamma = 2.0f;
                SpreadPower = 10;
            }
            else if (oceanPreset == 3)
            {
                windSpeed = 12;
                windDir = new(1, 0);
                Fetch = 150000;
                Depth = 1000;
                Gamma = 3.3f;
                SpreadPower = 8;
            }
            else if (oceanPreset == 4)
            {
                windSpeed = 18;
                windDir = new(1, 0);
                Fetch = 300000;
                Depth = 1000;
                Gamma = 4.5f;
                SpreadPower = 6;
            }
            else if (oceanPreset == 5)
            {
                windSpeed = 25;
                windDir = new(1, 0);
                Fetch = 500000;
                Depth = 1000;
                Gamma = 6.0f;
                SpreadPower = 3;
            }

            currentPreset = oceanPreset;
            resetGeneration = true;
        }
    }
    void SwellPresets()
    {
        //Apply preset swell parameters when swell preset index changes
        if (swellPreset != currentSwellPreset)
        {

            if (swellPreset == 0)
            {
                swellWindSpeed = 8;
                swellFetch = 100000;
                swellGamma = 1.0f;
                swellSpreadPower = 30;
                swellEnergyScale = 0f;
            }
            else if (swellPreset == 1)
            {
                swellWindSpeed = 10;
                swellFetch = 300000;
                swellGamma = 1.0f;
                swellSpreadPower = 35;
                swellEnergyScale = 0.25f;
            }
            else if (swellPreset == 2)
            {
                swellWindSpeed = 16;
                swellFetch = 500000;
                swellGamma = 1.0f;
                swellSpreadPower = 40;
                swellEnergyScale = 0.6f;
            }
            else if (swellPreset == 3)
            {
                swellWindSpeed = 22;
                swellFetch = 900000;
                swellGamma = 1.2f;
                swellSpreadPower = 45;
                swellEnergyScale = 1.0f;
            }
            else if (swellPreset == 4)
            {
                swellWindSpeed = 30;
                swellFetch = 1300000;
                swellGamma = 1.3f;
                swellSpreadPower = 50;
                swellEnergyScale = 1.5f;
            }

            currentSwellPreset = swellPreset;
            resetGeneration = true;
        }
    }
}