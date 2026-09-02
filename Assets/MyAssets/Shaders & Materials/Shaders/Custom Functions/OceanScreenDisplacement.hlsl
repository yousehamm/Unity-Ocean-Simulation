uniform float _TexelSize;
uniform float _CascadeLs[32];
uniform int _CascadeCount;

void OceanWaterHeight_float(
    float2 WorldXZ,
    float BaseSeaLevel,
    UnityTexture2DArray HeightTexArray,
    UnityTexture2DArray XTexArray,
    UnityTexture2DArray YTexArray,
    UnitySamplerState SampleState,
    out float WaterSurfaceY)
{
    float totalHeight = 0.0;
    float2 totalDisplacement = float2(0.0, 0.0);
    int activeCascades = (_CascadeCount > 0) ? _CascadeCount : 1;

    [loop]
    for (int i = 0; i < activeCascades; i++)
    {
        float L = max(_CascadeLs[i], 0.001);
        float2 cascadeUV = WorldXZ / L;
        
        float dispX = SAMPLE_TEXTURE2D_ARRAY_LOD(XTexArray, SampleState, cascadeUV, i, 0).r;
        float dispY = SAMPLE_TEXTURE2D_ARRAY_LOD(YTexArray, SampleState, cascadeUV, i, 0).r;
        
        totalDisplacement += float2(dispX, dispY);
    }

    float2 shiftedWorldXZ = WorldXZ - totalDisplacement;

    [loop]
    for (int i = 0; i < activeCascades; i++)
    {
        float L = max(_CascadeLs[i], 0.001);
        float2 cascadeUV = shiftedWorldXZ / L;
        
        float dispHeight = SAMPLE_TEXTURE2D_ARRAY_LOD(HeightTexArray, SampleState, cascadeUV, i, 0).r;
        totalHeight += dispHeight;
    }

    WaterSurfaceY = BaseSeaLevel + totalHeight;
}