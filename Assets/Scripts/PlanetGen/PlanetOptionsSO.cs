using PlasticPipe.PlasticProtocol.Messages;
using UnityEngine;

[CreateAssetMenu(fileName = "PlanetEnvOptions", menuName = "Scriptable Objects/PlanetOptions")]
public class PlanetOptionsSO : ScriptableObject
{
    [Header("Continent Noise Settings")]
    [Tooltip("This settings is in fraction of the planet radius")]
    public float ContinentWavelength = 2.5f;
    public float ContinentLacunarity = 2f;
    public float ContinentPersistence = 0.5f;
    public int ContinentOctaves = 4;
    public float ContinentWarpAmplitude = 0.2f;
    public float ContinentWarpFrequency = 0.15f;

    [Header("Land/Ocean Basic Settings")]
    [Tooltip("Sea level should be between 0 and 1. 0 being no sea and 1 being full sea")]
    public float SeaLevel = 0.5f;
    public float SeaCoastWidth = 0.08f;
    public float LandCoastWidth = 0.08f;
    
    [Tooltip("Height of where the land starts. It's used to delimit the coast shelf from the land")]
    public float BaseLandLevel = 10f;
    [Tooltip("Maximum height of the land.")]
    public float LandMaxHeight = 100f;

    [Tooltip("Height of where the ocean starts. It's used to delimit the coast shelf from the ocean")]
    public float ShelfDepth = 10f;
    [Tooltip("Portion of the ocean depth that will be the shelf")]
    public float ShelfPortion = 0.30f;
    [Tooltip("Sharpness of the shelf transition to the ocean plateau")]
    public float ShelfSharpness = 3.0f;
    [Tooltip("Depth of the ocean plateau. The plateau starts after the shelf")]
    public float OceanPlateauDepth = 75f;
    [Tooltip("Maximum depth of the ocean.")]
    public float OceanMaxDepth = 150f;

    [Header("Inland Settings")]
    [Tooltip("begin adding hills after this much land-ness")]
    public float DetailStartMask = 0.35f;
    [Tooltip("soften the start")]
    public float DetailRampMask = 0.15f;

    [Tooltip("This settings is in fraction of the planet radius")]
    public float HillsWavelength = 0.20f;
    public int HillsOctaves = 4;
    public float HillsPersistence = 0.5f;
    public float HillsLacunarity = 2.0f;
    public float HillsWarpAmplitude = 0.20f;
    public float HillsWarpFrequency = 1.7f;
    public float HillsAmplitudeMeters = 25f;

    // Mountains use ridged noise
    [Tooltip("Threshold for mountain formation on land noise (0-0.5)")]
    public float MountainThreshold = 0.35f;
    public float MountainRamp = 0.05f;
    [Tooltip("This settings is in fraction of the planet radius")]
    public float MountainWavelength = 0.06f; // smol
    public int MountainOctaves = 5;
    public float MountainGain = 0.5f; // persistence but for ridged noise
    public float MountainLacunarity = 2.0f;
    public float MountainWarpAmplitude = 0.15f;
    public float MountainWarpFrequency = 2.3f;
    public float MountainAmplitudeMeters = 180f;  // in meters
}
