using UnityEngine;

/// <summary>
/// Håller spelaren (XR Origin) på golvhöjd. Hela spelet är på samma våning, så om spelaren
/// av någon anledning hamnar högre eller lägre (fel teleportyta, faller genom golvet)
/// flyttas den direkt tillbaka till golvet.
/// </summary>
public class FloorGuard : MonoBehaviour
{
    [Tooltip("Golvets höjd (y) i skyddsrummet och förrummet")]
    public float floorY = 2.10f;
    [Tooltip("Hur mycket höjden får avvika innan spelaren flyttas tillbaka")]
    public float tolerance = 0.03f;

    CharacterController _cc;

    void Awake() => _cc = GetComponent<CharacterController>();

    void LateUpdate()
    {
        Vector3 p = transform.position;
        if (Mathf.Abs(p.y - floorY) <= tolerance) return;
        bool ccOn = _cc != null && _cc.enabled;
        if (ccOn) _cc.enabled = false;
        transform.position = new Vector3(p.x, floorY, p.z);
        if (ccOn) _cc.enabled = true;
    }
}
