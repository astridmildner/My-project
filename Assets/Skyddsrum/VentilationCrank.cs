using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Vev på ventilationsaggregatet. Greppa handtaget (nyp eller knyt handen, eller kontroll)
/// och för handen runt i en cirkel så snurrar veven. Den snurrar vidare en stund när man släpper.
/// </summary>
public class VentilationCrank : MonoBehaviour
{
    [Header("Delar")]
    [Tooltip("Handtaget man greppar")]
    public XRSimpleInteractable handle;
    [Tooltip("Det som roterar (vevaxeln)")]
    public Transform pivot;
    [Tooltip("Axel som veven snurrar runt, i pivotens lokala koordinater")]
    public Vector3 localAxis = Vector3.forward;
    [Tooltip("Valfritt: något som snurrar fort när man vevar (t.ex. fläkthjul)")]
    public Transform fanRotor;
    public float fanGearRatio = 8f;

    [Header("Känsla")]
    public bool keepSpinning = true;
    [Tooltip("Hur snabbt veven stannar när man släpper")]
    public float friction = 1.5f;
    [Tooltip("Varv per minut som räknas som full ventilation")]
    public float targetRpm = 40f;

    [Header("Ljud (valfritt)")]
    public AudioSource sound;

    [Header("Händelser")]
    public UnityEvent onFullTurn;

    /// <summary>Totalt antal varv som vevats.</summary>
    public float TotalTurns { get; private set; }
    /// <summary>Nuvarande varvtal (varv per minut).</summary>
    public float Rpm { get; private set; }
    /// <summary>0–1: hur bra ventilationen går just nu.</summary>
    public float Airflow01 => Mathf.Clamp01(Rpm / Mathf.Max(1f, targetRpm));
    public bool IsGrabbed => _hand != null;

    Transform _hand;
    Vector3 _prevDir;
    float _velocity; // grader per sekund
    float _angleSinceTurn;

    void OnEnable()
    {
        if (handle == null) return;
        handle.selectEntered.AddListener(OnGrab);
        handle.selectExited.AddListener(OnRelease);
    }

    void OnDisable()
    {
        if (handle == null) return;
        handle.selectEntered.RemoveListener(OnGrab);
        handle.selectExited.RemoveListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        _hand = args.interactorObject.GetAttachTransform(handle);
        _prevDir = HandDirection();
    }

    void OnRelease(SelectExitEventArgs args) => _hand = null;

    Vector3 Axis => pivot.TransformDirection(localAxis).normalized;

    Vector3 HandDirection()
    {
        if (_hand == null) return Vector3.zero;
        return Vector3.ProjectOnPlane(_hand.position - pivot.position, Axis).normalized;
    }

    void Update()
    {
        if (pivot == null) return;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float delta = 0f;

        if (_hand != null)
        {
            Vector3 dir = HandDirection();
            if (dir.sqrMagnitude > 0.5f && _prevDir.sqrMagnitude > 0.5f)
            {
                delta = Mathf.Clamp(Vector3.SignedAngle(_prevDir, dir, Axis), -45f, 45f);
                _velocity = Mathf.Lerp(_velocity, delta / dt, 0.3f);
            }
            _prevDir = dir;
        }
        else if (keepSpinning && Mathf.Abs(_velocity) > 1f)
        {
            delta = _velocity * dt;
            _velocity = Mathf.Lerp(_velocity, 0f, friction * dt);
        }
        else _velocity = 0f;

        if (Mathf.Abs(delta) > 0f)
        {
            pivot.Rotate(Axis, delta, Space.World);
            if (fanRotor != null) fanRotor.Rotate(Axis, delta * fanGearRatio, Space.World);
            TotalTurns += Mathf.Abs(delta) / 360f;
            _angleSinceTurn += Mathf.Abs(delta);
            while (_angleSinceTurn >= 360f) { _angleSinceTurn -= 360f; onFullTurn?.Invoke(); }
        }

        Rpm = Mathf.Abs(_velocity) / 6f; // grader/s → varv/min
        if (sound != null)
        {
            bool on = Rpm > 3f;
            if (on && !sound.isPlaying) sound.Play();
            if (!on && sound.isPlaying) sound.Stop();
            sound.pitch = Mathf.Lerp(0.7f, 1.3f, Airflow01);
        }
    }
}
