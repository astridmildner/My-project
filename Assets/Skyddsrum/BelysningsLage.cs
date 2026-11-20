using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Sparar hur belysningen var innan den dämpades, så att den kan återställas.
/// (Används av menyn Tools > Skyddsrum > Belysning.)
/// </summary>
public class BelysningsLage : MonoBehaviour
{
    public List<Light> dimmedLights = new List<Light>();
    public List<float> originalIntensities = new List<float>();
    public Color originalAmbient;
    public float originalAmbientIntensity = 1f;
}
