struct WaterChunkInstanceData
{
    float4x4 localToWorld;
    float lodLevel;
};

StructuredBuffer<WaterChunkInstanceData> _InstanceBuffer;

void InstancingSetup_float(float3 In, out float3 Out)
{
#ifdef PROCEDURAL_INSTANCING_ON
    WaterChunkInstanceData data = _InstanceBuffer[unity_InstanceID];

    unity_ObjectToWorld = data.localToWorld;
    unity_WorldToObject = unity_ObjectToWorld;
    unity_WorldToObject._14_24_34 *= -1;
    unity_WorldToObject._11_22_33 = 1.0f / unity_WorldToObject._11_22_33;
#endif
    Out = In;
}