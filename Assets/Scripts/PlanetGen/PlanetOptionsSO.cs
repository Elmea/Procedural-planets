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

    [Header("Land/Ocean Separation")]
    [Tooltip("Sea level should be between 0 and 1. 0 being no sea and 1 being full sea")]
    public float SeaLevel = 0.0f;
    public float CoastWidth = 0.08f;
    public float OceanDepth = 200f;
    public float LandHeight = 100f;
}
