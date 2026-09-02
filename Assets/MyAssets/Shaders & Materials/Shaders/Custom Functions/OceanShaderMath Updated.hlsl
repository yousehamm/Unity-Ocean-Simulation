#ifndef OCEAN_NORMALS_INCLUDED
#define OCEAN_NORMALS_INCLUDED

uniform float _TexelSize;
uniform float _CascadeLs[32];
uniform int _CascadeCount;

void OceanDisplacement_float(
    float2 WorldXZ,

    UnityTexture2DArray HeightTexArray,
    UnityTexture2DArray XTexArray,
    UnityTexture2DArray YTexArray,
    UnitySamplerState SampleState,

    out float3 Displacement)
{
    float3 totalDisplacement = float3(0.0, 0.0, 0.0);

    [loop]
    for (int i = 0; i < _CascadeCount; i++)
    {
        float L = max(_CascadeLs[i], 0.001);
        float2 cascadeUV = WorldXZ / L;
        

        float dispX = SAMPLE_TEXTURE2D_ARRAY_LOD(XTexArray, SampleState, cascadeUV, i, 0).r;
        float dispHeight = SAMPLE_TEXTURE2D_ARRAY_LOD(HeightTexArray, SampleState, cascadeUV, i, 0).r;
        float dispY = SAMPLE_TEXTURE2D_ARRAY_LOD(YTexArray, SampleState, cascadeUV, i, 0).r;

        totalDisplacement += float3(dispX, dispHeight, dispY);
    }

    Displacement = totalDisplacement;
}

void OceanNormals_float(
    float2 WorldXZ,

    UnityTexture2DArray SlopeXTexArray,
    UnityTexture2DArray SlopeZTexArray,
    UnitySamplerState SampleState,

    out float3 Normal)
{
    float totalSlopeX = 0.0;
    float totalSlopeZ = 0.0;

    [loop]
    for (int i = 0; i < _CascadeCount; i++)
    {
        float L = max(_CascadeLs[i], 0.001);
        float2 cascadeUV = WorldXZ / L;

        // Direct read of the analytic height-gradient - already exact, no neighbor
        // sampling or finite differencing needed.
        totalSlopeX += SAMPLE_TEXTURE2D_ARRAY_LOD(SlopeXTexArray, SampleState, cascadeUV, i, 0).r;
        totalSlopeZ += SAMPLE_TEXTURE2D_ARRAY_LOD(SlopeZTexArray, SampleState, cascadeUV, i, 0).r;
    }

    Normal = normalize(float3(-totalSlopeX, 1.0, -totalSlopeZ));
}
void OceanFoam_float(
    float2 WorldXZ,
    UnityTexture2DArray FoamTexArray,
    UnitySamplerState SampleState,
    out float FoamAmount)
{
    float foam = 0.0;
    [loop]
    for (int i = 0; i < _CascadeCount; i++)
    {
        float L = max(_CascadeLs[i], 0.001);
        float2 cascadeUV = WorldXZ / L;
        foam += SAMPLE_TEXTURE2D_ARRAY_LOD(FoamTexArray, SampleState, cascadeUV, i, 0).r;
    }
    FoamAmount = saturate(foam);
}

#endif