#ifndef OCEAN_NORMALS_INCLUDED
#define OCEAN_NORMALS_INCLUDED

uniform float _TexelSize;
uniform float _CascadeLs[32];
uniform int _CascadeCount;

void ComputeCascadeDerivatives(
    float2 UV,
    float TexelSize,
    float L,
    int sliceIndex,

    UnityTexture2DArray HeightTexArray,
    UnityTexture2DArray XTexArray,
    UnityTexture2DArray YTexArray,
    UnitySamplerState SampleState,

    out float3 dD_dx,
    out float3 dD_dz)
{
    float safeL = max(L, 0.001);
    float2 texelX = float2(TexelSize, 0.0);
    float2 texelY = float2(0.0, TexelSize);

    float worldTexelStep = TexelSize * safeL;
    float deltaWorldSpace = max(2.0 * worldTexelStep, 0.00001);

    float3 dispR = float3(
        SAMPLE_TEXTURE2D_ARRAY_LOD(XTexArray, SampleState, UV + texelX, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(HeightTexArray, SampleState, UV + texelX, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(YTexArray, SampleState, UV + texelX, sliceIndex, 0).r);

    float3 dispL = float3(
        SAMPLE_TEXTURE2D_ARRAY_LOD(XTexArray, SampleState, UV - texelX, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(HeightTexArray, SampleState, UV - texelX, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(YTexArray, SampleState, UV - texelX, sliceIndex, 0).r);

    float3 dispU = float3(
        SAMPLE_TEXTURE2D_ARRAY_LOD(XTexArray, SampleState, UV + texelY, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(HeightTexArray, SampleState, UV + texelY, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(YTexArray, SampleState, UV + texelY, sliceIndex, 0).r);

    float3 dispD = float3(
        SAMPLE_TEXTURE2D_ARRAY_LOD(XTexArray, SampleState, UV - texelY, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(HeightTexArray, SampleState, UV - texelY, sliceIndex, 0).r,
        SAMPLE_TEXTURE2D_ARRAY_LOD(YTexArray, SampleState, UV - texelY, sliceIndex, 0).r);

    dD_dx = (dispR - dispL) / deltaWorldSpace;
    dD_dz = (dispU - dispD) / deltaWorldSpace;
}

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

    UnityTexture2DArray HeightTexArray,
    UnityTexture2DArray XTexArray,
    UnityTexture2DArray YTexArray,
    UnitySamplerState SampleState,

    out float3 Normal,
    out float3 JacobianDx,
    out float3 JacobianDz)
{
    float3 total_dD_dx = float3(0.0, 0.0, 0.0);
    float3 total_dD_dz = float3(0.0, 0.0, 0.0);

    [loop]
    for (int i = 0; i < _CascadeCount; i++)
    {
        float3 dD_dx, dD_dz;

        float L = max(_CascadeLs[i], 0.001);
        float2 cascadeUV = WorldXZ / L;

        ComputeCascadeDerivatives(
            cascadeUV,
            _TexelSize,
            L,
            i,
            HeightTexArray,
            XTexArray,
            YTexArray,
            SampleState,
            dD_dx,
            dD_dz
        );

        total_dD_dx += dD_dx;
        total_dD_dz += dD_dz;
    }

    JacobianDx = total_dD_dx;
    JacobianDz = total_dD_dz;

    float3 finalTangentX = float3(1.0 + total_dD_dx.x, total_dD_dx.y, total_dD_dx.z);
    float3 finalTangentZ = float3(total_dD_dz.x, total_dD_dz.y, 1.0 + total_dD_dz.z);

    Normal = normalize(cross(finalTangentZ, finalTangentX));
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