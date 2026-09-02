Shader "Custom/PhysicalSkyCRT"
{
    Properties
    {
        _SunDirection ("Sun Direction", Vector) = (0, 1, 0, 0)
        _SunIntensity ("Sun Intensity", Float) = 22.0
        _PlanetRadius ("Planet Radius", Float) = 6371
        _AtmosphereRadius ("Atmosphere Radius", Float) = 6471
        _RayleighCoeff ("Rayleigh Coefficient", Vector) = (0.0055, 0.013, 0.0224, 0)
        _MieCoeff ("Mie Coefficient", Float) = 0.021
        _RayleighScaleHeight ("Rayleigh Scale Height", Float) = 8
        _MieScaleHeight ("Mie Scale Height", Float) = 1.2
        _MieG ("Mie Anisotropy", Float) = 0.758
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "PhysicalSkyCRT"

            CGPROGRAM
            #pragma vertex CustomRenderTextureVertexShader
            #pragma fragment frag
            #include "UnityCustomRenderTexture.cginc"

            float3 _SunDirection;
            float _SunIntensity;
            float _PlanetRadius;
            float _AtmosphereRadius;
            float3 _RayleighCoeff;
            float _MieCoeff;
            float _RayleighScaleHeight;
            float _MieScaleHeight;
            float _MieG;

            #define PRIMARY_STEPS 32
            #define LIGHT_STEPS 16
            #define PI 3.14159265359

            bool RaySphereIntersect(float3 rayOrigin, float3 rayDir, float radius, out float2 t)
            {
                float b = dot(rayOrigin, rayDir);
                float c = dot(rayOrigin, rayOrigin) - radius * radius;
                float disc = b * b - c;
                if (disc < 0.0)
                {
                    t = float2(0, 0);
                    return false;
                }
                disc = sqrt(disc);
                t = float2(-b - disc, -b + disc);
                return true;
            }

            float3 ComputePhysicalSky(
                float3 ViewDir,
                float3 SunDir,
                float SunIntensity,
                float PlanetRadius,
                float AtmosphereRadius,
                float3 RayleighCoeff,
                float MieCoeff,
                float RayleighScaleHeight,
                float MieScaleHeight,
                float MieG)
            {
                float3 rayOrigin = float3(0.0, PlanetRadius + 0.0001, 0.0); // was +1.0 meter, now +0.0001 km (=10cm)

                float2 atmoHit;
                if (!RaySphereIntersect(rayOrigin, ViewDir, AtmosphereRadius, atmoHit) || atmoHit.y < 0.0)
                {
                    return float3(0, 0, 0);
                }

                float rayStart = max(atmoHit.x, 0.0);
                float rayEnd = atmoHit.y;

                float2 groundHit;
                float raymarchFloor = PlanetRadius * 0.995; 
                
                bool hitsGround = RaySphereIntersect(rayOrigin, ViewDir, raymarchFloor, groundHit) && groundHit.x > 0.0;
                if (hitsGround)
                {
                    rayEnd = min(rayEnd, groundHit.x);
                }

                float segmentLength = (rayEnd - rayStart) / float(PRIMARY_STEPS);
                float3 pos = rayOrigin + ViewDir * rayStart;

                float opticalDepthR = 0.0;
                float opticalDepthM = 0.0;

                float3 totalRayleigh = float3(0, 0, 0);
                float3 totalMie = float3(0, 0, 0);

                float mu = dot(ViewDir, SunDir);
                float phaseR = 3.0 / (16.0 * PI) * (1.0 + mu * mu);
                float g2 = MieG * MieG;
                float phaseM = 3.0 / (8.0 * PI) * ((1.0 - g2) * (1.0 + mu * mu)) /
                               ((2.0 + g2) * pow(abs(1.0 + g2 - 2.0 * MieG * mu), 1.5));

                for (int i = 0; i < PRIMARY_STEPS; i++)
                {
                    float3 samplePos = pos + ViewDir * (segmentLength * (float(i) + 0.5));
                    float height = length(samplePos) - PlanetRadius;
                    if (height < 0.0) break;

                    float hR = exp(-height / RayleighScaleHeight) * segmentLength;
                    float hM = exp(-height / MieScaleHeight) * segmentLength;
                    opticalDepthR += hR;
                    opticalDepthM += hM;

                    float2 sunHit;
                    RaySphereIntersect(samplePos, SunDir, AtmosphereRadius, sunHit);
                    float sunSegLength = sunHit.y / float(LIGHT_STEPS);

                    float sunOpticalDepthR = 0.0;
                    float sunOpticalDepthM = 0.0;
                    bool sampleValid = true;

                    for (int j = 0; j < LIGHT_STEPS; j++)
                    {
                        float3 sunSamplePos = samplePos + SunDir * (sunSegLength * (float(j) + 0.5));
                        float sunHeight = length(sunSamplePos) - PlanetRadius;
                        if (sunHeight < 0.0)
                        {
                            sampleValid = false;
                            break;
                        }
                        sunOpticalDepthR += exp(-sunHeight / RayleighScaleHeight) * sunSegLength;
                        sunOpticalDepthM += exp(-sunHeight / MieScaleHeight) * sunSegLength;
                    }

                    if (sampleValid)
                    {
                        float3 tau = RayleighCoeff * (opticalDepthR + sunOpticalDepthR) +
                                     MieCoeff * 1.1 * (opticalDepthM + sunOpticalDepthM);
                        float3 attenuation = exp(-tau);

                        totalRayleigh += attenuation * hR;
                        totalMie += attenuation * hM;
                    }
                }

                return SunIntensity * (totalRayleigh * RayleighCoeff * phaseR + totalMie * MieCoeff * phaseM);
            }

            float4 frag(v2f_customrendertexture IN) : SV_Target
            {
                float3 viewDir = normalize(IN.direction);
                float3 sunDir = normalize(_SunDirection);
            
                float3 skyColor = ComputePhysicalSky(
                    viewDir, sunDir, _SunIntensity,
                    _PlanetRadius, _AtmosphereRadius,
                    _RayleighCoeff, _MieCoeff,
                    _RayleighScaleHeight, _MieScaleHeight, _MieG);
            
                // Sun disc: angular radius of the real sun is ~0.53 degrees (~0.00465 rad half-angle)
                float cosSunAngle = dot(viewDir, sunDir);
                float sunAngularSize = 0.9998; // cos of ~1.1 degree half-angle, tweak for a slightly bigger/softer disc
                float sunDisc = smoothstep(sunAngularSize, 0.99995, cosSunAngle);
                float3 sunColor = sunDisc * _SunIntensity * 2.0; // bright, separate from atmosphere intensity
            
                return float4(skyColor + sunColor, 1.0);
            }
            ENDCG
        }
    }
}