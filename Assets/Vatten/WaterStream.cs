using UnityEngine;

/// <summary>
/// En vattenstråle som rinner rakt nedåt från det här objektets position.
/// Visar en cylinder ner till det den träffar och talar om ifall den träffar en balja.
/// </summary>
public class WaterStream : MonoBehaviour
{
    [Tooltip("Cylinder (utan collider) som visar strålen")]
    public Transform visual;
    public float width = 0.02f;
    public float maxLength = 4f;
    public LayerMask hitMask = ~0;

    Transform _ignoreRoot;

    public void SetIgnoreRoot(Transform root) => _ignoreRoot = root;

    void Awake()
    {
        // Strålen ska alltid peka rakt ner, även om baljan lutar, så den får inte ärva rotation.
        if (visual != null)
        {
            visual.SetParent(null, true);
            visual.gameObject.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (visual != null) Destroy(visual.gameObject);
    }

    void OnDisable()
    {
        if (visual != null) visual.gameObject.SetActive(false);
    }

    /// <summary>Anropas varje bildruta. Returnerar baljan som strålen träffar (eller null).</summary>
    public WaterBasin Flow(bool on)
    {
        if (visual != null && visual.gameObject.activeSelf != on) visual.gameObject.SetActive(on);
        if (!on) return null;

        Vector3 origin = transform.position;
        float length = maxLength;
        WaterBasin target = null;

        var hits = Physics.RaycastAll(origin, Vector3.down, maxLength, hitMask, QueryTriggerInteraction.Ignore);
        float best = float.MaxValue;
        foreach (var h in hits)
        {
            if (_ignoreRoot != null && h.transform.IsChildOf(_ignoreRoot)) continue;
            if (h.distance < best)
            {
                best = h.distance;
                length = h.distance;
                target = h.collider.GetComponentInParent<WaterBasin>();
            }
        }

        // Stanna strålen vid vattenytan om den träffar en balja med vatten i.
        if (target != null && target.Liters > 0f)
        {
            float toSurface = origin.y - target.SurfaceWorldY;
            if (toSurface > 0f) length = Mathf.Min(length, toSurface);
        }

        if (visual != null)
        {
            visual.position = origin + Vector3.down * (length * 0.5f);
            visual.rotation = Quaternion.identity;
            visual.localScale = new Vector3(width, length * 0.5f, width); // Unitys cylinder är 2 m hög
        }
        return target;
    }
}
