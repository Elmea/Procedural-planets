using Unity.Burst;
using Unity.Mathematics;

// functions that we also are using to generate basic tree positions on the planet surface
[BurstCompile]
public static class BurstUtils
{
    public static float ContinentField(float3 posMeters, 
                                       float planetRadius, float continentWavelength,
                                       float warpAmplitude, float warpFrequency,
                                       float continentLacunarity, int continentOctaves, float continentPersistence)
    {
        float continentWavelengthFactor = planetRadius * continentWavelength;
        float baseFreq = 1f / continentWavelengthFactor;

        float3 pt = posMeters * baseFreq;
        float3 ptWarped = Warp(pt, warpAmplitude, warpFrequency);
        float height = FBM(ptWarped, continentLacunarity, continentOctaves, continentPersistence);

        return height;
    }

    // attempt to make a noise that looks like coastlines
    public static float CoastBreaker(float3 posMeters, float planetRadiusMeters)
    {
        float wavelength = planetRadiusMeters * 0.10f;
        float freq = 1f / wavelength;

        float3 p = posMeters * freq;
        float3 pw = Warp(p, 0.5f, 2.0f);
        float r = FBM(pw, 2.0f, 4, 0.5f);
        r = 0.5f * (r + 1f);
        return r;
    }

    public static float CoastLandProfile(float landMask, float baseLandLevel)
    {
        float gradient = math.smoothstep(0f, 1f, landMask); // smoother 0 to 1
        return baseLandLevel * gradient; // meters above sea
    }

    public static float HillsField(float3 posMeters, float coastValue, float hillsMask, float baseLandLevel,
                                   float planetRadius, float hillsWavelength,
                                   float hillsLacunarity, int hillsOctaves, float hillsPersistence,
                                   float hillsAmplitudeMeters)
    {
        hillsWavelength = planetRadius * hillsWavelength;
        float baseFreq = 1f / math.max(hillsWavelength, 1e-6f);
        float3 pt = posMeters * baseFreq;

        float hillsValue = FBM(pt, hillsLacunarity, hillsOctaves, hillsPersistence) * hillsAmplitudeMeters + baseLandLevel;
        return math.lerp(coastValue, hillsValue, hillsMask);
    }


    // FBM between 0 and 1
    public static float FBM(float3 pt, float lacunarity, int octaves, float persistence)
    {
        float a = 1f;
        float amplitude = 0f;
        float sum = 0f;
        float3 q = pt;
        for (int i = 0; i < octaves; i++)
        {
            sum += a * noise.snoise(q);
            amplitude += a;
            q *= lacunarity;
            a *= persistence;
        }
        return (sum / math.max(amplitude, 1e-6f)) * 0.5f + 0.5f;
    }

    public static float RidgedFBM(float3 pt, float lacunarity, int octaves, float gain)
    {
        float a = 1f;
        float amplitude = 0f;
        float sum = 0f;
        float3 q = pt;
        for (int i = 0; i < octaves; i++)
        {
            float n = 1f - math.abs(noise.snoise(q));
            sum += a * n;
            amplitude += a;
            q *= lacunarity;
            a *= gain;
        }
        return sum / math.max(amplitude, 1e-6f);
    }

    public static float3 Warp(float3 pt, float amplitude, float frequency)
    {
        float3 w;
        w.x = noise.snoise(pt * frequency + new float3(37.2f, 15.7f, 91.1f));
        w.y = noise.snoise(pt * frequency + new float3(-12.3f, 44.5f, 7.9f));
        w.z = noise.snoise(pt * frequency + new float3(9.4f, -55.6f, 23.3f));
        return pt + amplitude * w;
    }
}
