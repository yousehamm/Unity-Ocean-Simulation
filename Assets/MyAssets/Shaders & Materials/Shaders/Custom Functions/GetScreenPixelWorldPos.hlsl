//Calculate camera ray plane intersection
void GetCameraRayPlaneIntersection_float(float2 UV, float PlaneHeight,
    out float3 Position, out float3 RayOrigin, out float3 RayDir, out float Valid, out float3 PixelOnLens)
{
    float2 ndc = UV * 2.0 - 1.0;

    float tanHalfFovY = 1.0 / unity_CameraProjection._m11;
    float tanHalfFovX = 1.0 / unity_CameraProjection._m00;

    float3 viewDir = float3(ndc.x * tanHalfFovX, ndc.y * tanHalfFovY, -1.0);
    float3 worldDir = normalize(mul((float3x3) UNITY_MATRIX_I_V, viewDir));

    RayOrigin = _WorldSpaceCameraPos;
    RayDir = worldDir;

    float t = (PlaneHeight - RayOrigin.y) / RayDir.y;
    Valid = (t > 0.0 && RayDir.y < 0.0) ? 1.0 : 0.0;
    t = max(t, 0.0);

    Position = RayOrigin + RayDir * t;
    
    PixelOnLens = RayOrigin + RayDir * _ProjectionParams.y;
}

//Intersect ray with height plane
void IntersectRayWithHeight_float(float3 RayOrigin, float3 RayDir, float PlaneHeight, out float3 Position)
{
    float t = (PlaneHeight - RayOrigin.y) / RayDir.y;
    t = max(t, 0.0);
    Position = RayOrigin + RayDir * t;
}

//Smooth water mask calculation
void SmoothWaterMask_float(float SurfaceY, float PointY, out float Mask)
{
    float diff = PointY - SurfaceY;
    float aa = max(fwidth(diff), 1e-5);
    Mask = 1.0 - smoothstep(-aa, aa, diff);
}

//Calculate camera submergence mask
void CameraSubmergenceMask_float(float WaterHeight, float LensSize, float2 UV, out float Mask)
{
    float2 ndc = UV * 2.0 - 1.0;

    float tanHalfFovY = 1.0 / unity_CameraProjection._m11;
    float tanHalfFovX = 1.0 / unity_CameraProjection._m00;

    float3 viewLensPos = float3(ndc.x * tanHalfFovX * LensSize, ndc.y * tanHalfFovY * LensSize, LensSize);
    float3 worldLensPos = mul(UNITY_MATRIX_I_V, float4(viewLensPos, 1.0)).xyz;

    Mask = (worldLensPos.y < WaterHeight) ? 1.0 : 0.0;
}

//Get pixel lens world coordinates
void GetPixelLensXZ_float(float LensSize, float2 UV, out float2 WorldXZ)
{
    float2 ndc = UV * 2.0 - 1.0;

    float tanHalfFovY = 1.0 / unity_CameraProjection._m11;
    float tanHalfFovX = 1.0 / unity_CameraProjection._m00;

    float3 viewLensPos = float3(ndc.x * tanHalfFovX * LensSize, ndc.y * tanHalfFovY * LensSize, LensSize);
    float3 worldLensPos = mul(UNITY_MATRIX_I_V, float4(viewLensPos, 1.0)).xyz;

    WorldXZ = worldLensPos.xz;
}