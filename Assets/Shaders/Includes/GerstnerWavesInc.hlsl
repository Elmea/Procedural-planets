#ifndef GERSTNERWAVESINC_HLSL
#define GERSTNERWAVESINC_HLSL


static float Height(float lat, float lon, float time,
                    float angDir, float amplitude, float frequency, float speed)
{
    float2 dir  = float2(cos(angDir), sin(angDir));
    float  heightPos = (dir.x * lon + dir.y * lat) * frequency + time * speed;
    return amplitude * sin(heightPos);
}

static float3 LatLonToWorld(float radius, float lon, float lat, float h)
{
    float cLat = cos(lat);
    float sLat = sin(lat);
    float sLon = sin(lon);
    float cLon = cos(lon);
    float3 n = float3(sLon * cLat, sLat, cLon * cLat);
    return n * (radius + h);
}

static float3 NormalFromFiniteDiff(
    float radius, float lon, float lat, float h, float time,
    float angDir, float amplitude, float frequency, float speed)
{
    const float baseStep = 0.00075;

    float cosLat = max(1e-3, cos(lat));
    float dLon = baseStep * cosLat;
    float dLat = baseStep;

    float lonP = lon + dLon, lonM = lon - dLon;
    float latP = lat + dLat, latM = lat - dLat;

    float hLonPlus = Height(lonP, lat,  time, amplitude, frequency, speed, angDir);
    float hLonMin = Height(lonM, lat,  time, amplitude, frequency, speed, angDir);
    float hLatPlus = Height(lon,  latP, time, amplitude, frequency, speed, angDir);
    float hLatMin = Height(lon,  latM, time, amplitude, frequency, speed, angDir);

    float3 pt = LatLonToWorld(radius, lon,  lat,  h);
    float3 ptLonPlus = LatLonToWorld(radius, lonP, lat,  hLonPlus);
    float3 ptLonMin = LatLonToWorld(radius, lonM, lat,  hLonMin);
    float3 ptLatPlus = LatLonToWorld(radius, lon,  latP, hLatPlus);
    float3 ptLatMin = LatLonToWorld(radius, lon,  latM, hLatMin);

    float3 tangentLon = (ptLonPlus - ptLonMin) / (2.0 * dLon);
    float3 tangentLat = (ptLatPlus - ptLatMin) / (2.0 * dLat);

    float3 normal = normalize(cross(tangentLon, tangentLat));

    return normal;
}

void OceanDisplacement_float(float3 worldPos, float3 planetPos, float time, float normalStrength,
                             float4 dirAngAmpFreqSpeed,
                             out float3 outWorldPos, out float3 outWorldNormal)
{
    float3 localPos = worldPos - planetPos;
    float radius = length(localPos);
    float3 normal = normalize(localPos);

    float lat = asin(normal.y);
    float lon = atan2(normal.x, normal.z);

    float h = Height(lon, lat, time,
                      dirAngAmpFreqSpeed.x, dirAngAmpFreqSpeed.y, dirAngAmpFreqSpeed.z, dirAngAmpFreqSpeed.w);

    outWorldPos = normal * (h + radius) + planetPos;

    float3 waveNormal = NormalFromFiniteDiff(radius, lon, lat, h,
                                  time,
                                  dirAngAmpFreqSpeed.x, dirAngAmpFreqSpeed.y, dirAngAmpFreqSpeed.z, dirAngAmpFreqSpeed.w);

    normal = normalize(lerp(normal, waveNormal, saturate(normalStrength)));

    outWorldNormal = normal;
}
#endif
