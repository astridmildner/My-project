using UnityEngine;

/// <summary>
/// En balja som kan fyllas med vatten. Lutar man den så rinner vattnet ut
/// (och kan hällas över i en annan balja).
/// </summary>
public class WaterBasin : MonoBehaviour
{
    [Header("Vatten")]
    public float capacityLiters = 45f;
    [Range(0f, 1f)] public float startFill = 0f;
    [Tooltip("Platt kub som visar vattnet")]
    public Transform waterSurface;
    [Tooltip("Lokal höjd för insidan av botten")]
    public float innerBottomY = 0.02f;
    [Tooltip("Lokal höjd för kanten")]
    public float innerTopY = 0.24f;

    [Header("Hälla ut")]
    [Tooltip("Liter per sekund när baljan lutas")]
    public float pourRate = 8f;
    [Tooltip("Hur många grader baljan måste lutas innan det börjar rinna ut")]
    public float minPourAngle = 25f;
    public WaterStream pourStream;
    [Tooltip("Punkter längs kanten. Vattnet rinner ut vid den som är lägst.")]
    public Transform[] rimPoints;

    public float Liters { get; private set; }
    public float Fill01 => capacityLiters > 0f ? Liters / capacityLiters : 0f;
    public bool IsFull => Liters >= capacityLiters - 0.001f;

    float CurrentLocalY => Mathf.Lerp(innerBottomY, innerTopY, Fill01);
    public float SurfaceWorldY => transform.TransformPoint(new Vector3(0f, CurrentLocalY, 0f)).y;

    void Start()
    {
        Liters = capacityLiters * startFill;
        if (pourStream != null) pourStream.SetIgnoreRoot(transform);
        UpdateVisual();
    }

    public void AddWater(float liters)
    {
        Liters = Mathf.Clamp(Liters + liters, 0f, capacityLiters);
    }

    public void Empty() => Liters = 0f;

    void Update()
    {
        HandlePouring();
        UpdateVisual();
    }

    void HandlePouring()
    {
        bool pouring = false;
        float tilt = Vector3.Angle(transform.up, Vector3.up);

        if (Liters > 0f && tilt > minPourAngle && rimPoints != null && rimPoints.Length > 0)
        {
            Transform lowest = rimPoints[0];
            foreach (var p in rimPoints)
                if (p != null && p.position.y < lowest.position.y) lowest = p;

            // Rinner ut när vattenytan når över den lägsta kanten (eller baljan är upp och ner).
            if (lowest.position.y <= SurfaceWorldY || tilt > 90f)
            {
                pouring = true;
                float amount = Mathf.Min(Liters, pourRate * Mathf.InverseLerp(minPourAngle, 120f, tilt) * Time.deltaTime);
                amount = Mathf.Max(amount, Mathf.Min(Liters, 0.5f * Time.deltaTime));
                Liters -= amount;

                if (pourStream != null)
                {
                    pourStream.transform.position = lowest.position;
                    var other = pourStream.Flow(true);
                    if (other != null && other != this) other.AddWater(amount);
                }
            }
        }

        if (!pouring && pourStream != null) pourStream.Flow(false);
    }

    void UpdateVisual()
    {
        if (waterSurface == null) return;
        bool show = Liters > 0.01f;
        if (waterSurface.gameObject.activeSelf != show) waterSurface.gameObject.SetActive(show);
        if (!show) return;

        float h = Mathf.Max(0.002f, CurrentLocalY - innerBottomY);
        var s = waterSurface.localScale;
        waterSurface.localScale = new Vector3(s.x, h, s.z);
        var p = waterSurface.localPosition;
        waterSurface.localPosition = new Vector3(p.x, innerBottomY + h * 0.5f, p.z);
    }
}
