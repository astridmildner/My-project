using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Försämrar synen (mörka kanter, bleka färger, suddighet) när en uppgift inte görs.
/// Styr vikten på en Volume med efterbehandlingseffekter. Blir snabbt bra igen när uppgiften är klar.
/// </summary>
[RequireComponent(typeof(Volume))]
public class VisionEffect : MonoBehaviour
{
    [Tooltip("Hur snabbt synen får försämras (andel per sekund)")]
    public float worsenSpeed = 0.5f;
    [Tooltip("Hur snabbt synen blir bra igen (andel per sekund)")]
    public float recoverSpeed = 1.2f;
    /// <summary>För testning under Play: 0–1 tvingar fram effekten, -1 = av. Sparas aldrig i scenen.</summary>
    [System.NonSerialized] public float debugSeverity = -1f;

    public float Current => _volume != null ? _volume.weight : 0f;

    Volume _volume;
    ShelterTask[] _tasks = new ShelterTask[0];

    void Awake()
    {
        _volume = GetComponent<Volume>();
        _volume.weight = 0f;
    }

    void Start()
    {
        _tasks = FindObjectsByType<ShelterTask>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    void Update()
    {
        float target = 0f;
        foreach (var t in _tasks)
            if (t != null && t.isActiveAndEnabled) target = Mathf.Max(target, t.Severity);
        if (debugSeverity >= 0f) target = debugSeverity;

        float speed = target < _volume.weight ? recoverSpeed : worsenSpeed;
        _volume.weight = Mathf.MoveTowards(_volume.weight, target, speed * Time.deltaTime);
    }
}
