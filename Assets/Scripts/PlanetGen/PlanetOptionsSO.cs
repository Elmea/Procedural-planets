using UnityEngine;

[CreateAssetMenu(fileName = "PlanetEnvOptions", menuName = "Scriptable Objects/PlanetOptions")]
public class PlanetOptionsSO : ScriptableObject
{
    [Header("Continent Noise Settings")]
    public float ContinentWavelength = 2.5f;
    public float ContinentLacunarity = 2f;
    public float ContinentPersistence = 0.5f;
    public int ContinentOctaves = 4;
    public float ContinentWarpAmplitude = 0.2f;
    public float ContinentWarpFrequency = 0.15f;

    [Header("Land/Ocean Settings")]
    [Tooltip("Sea level should be between 0 and 1. 0 being no sea and 1 being full sea")]
    public float SeaLevel = 0.0f;
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
}
