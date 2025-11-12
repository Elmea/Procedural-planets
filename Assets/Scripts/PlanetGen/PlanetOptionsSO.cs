using UnityEngine;

[CreateAssetMenu(fileName = "PlanetEnvOptions", menuName = "Scriptable Objects/PlanetOptions")]
public class PlanetOptionsSO : ScriptableObject
{
    public float PlanetRadius;

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
    public float SeaCoastLimit = 0.46f;
    public float LandCoastLimit = 0.54f;

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

    [Header("Hill Settings")]
    [Tooltip("soften the start of the hills. Will start after the LandCoastWidth.")]
    public float LandHillRampLimit = 0.6f;
    [Tooltip("This settings is in fraction of the planet radius")]
    public float HillsWavelength = 5f;
    public int HillsOctaves = 4;
    public float HillsPersistence = 0.5f;
    public float HillsLacunarity = 2.0f;
    public float HillsAmplitudeMeters = 25f;

    // Mountains use ridged noise
    [Header("Mountain Settings")]
    [Tooltip("Start of the mountains")]
    public float MountainStart = 0.7f;
    [Tooltip("Threshold for mountain formation on land noise")]
    public float MountainRampLimit = 0.78f;
    [Tooltip("This settings is in fraction of the planet radius")]
    public float MountainWavelength = 0.06f; // smol
    public int MountainOctaves = 5;
    public float MountainGain = 0.5f; // persistence but for ridged noise
    public float MountainLacunarity = 2.0f;
    public float MountainAmplitudeMeters = 180f;  // in meters
}
