using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Grund för en uppgift i skyddsrummet. Gör man inte uppgiften i tid kommer först en
/// ledtråd (saker lyser) och sedan en konsekvens (synen blir sämre, se VisionEffect).
/// När uppgiften är klar försvinner både ledtråd och konsekvens.
/// </summary>
public abstract class ShelterTask : MonoBehaviour
{
    [Header("Uppgift")]
    public string title = "Uppgift";
    [Tooltip("Uppgiften startar när den här dörren öppnas – eller när spelaren går in i skyddsrummet. Tom = startar direkt.")]
    public ShelterDoor startWhenDoorOpens;
    [Tooltip("Uppgiften startar också när spelarens huvud kommer in i något av de här områdena. Tomt = dörrens spärrzoner används.")]
    public BoxCollider[] startWhenPlayerInside = new BoxCollider[0];
    [Tooltip("Om satt: uppgiften startar när den här uppgiften är klar (plus fördröjning) i stället för vid dörren.")]
    public ShelterTask startAfterTask;
    [Tooltip("Starta först när spelaren är inne i skyddsrummet (inte redan när dörren öppnas)")]
    public bool requirePlayerInside = false;
    public float startDelaySeconds = 10f;

    [Header("Tider (sekunder efter start)")]
    public float hintAfterSeconds = 60f;
    public float consequenceAfterSeconds = 120f;
    [Tooltip("Hur lång tid det tar innan synen är som sämst")]
    public float consequenceRampSeconds = 45f;

    [Header("Ledtråd: det här lyser")]
    public Renderer[] glowTargets = new Renderer[0];
    public Light hintLight;
    public Color glowColor = new Color(1f, 0.8f, 0.25f);
    [Tooltip("Hur många blinkningar per sekund (0.5 = en blinkning varannan sekund)")]
    public float pulsesPerSecond = 0.5f;

    [Header("Meddelanden")]
    public bool showMessages = true;

    public bool IsStarted { get; private set; }
    public bool IsDone { get; private set; }
    /// <summary>När uppgiften blev klar (Time.time), -1 om den inte är klar.</summary>
    public float DoneTime { get; private set; } = -1f;
    /// <summary>0 = ingen påverkan, 1 = som sämst.</summary>
    public float Severity { get; private set; }
    public float ElapsedSeconds => _t;

    float _t;
    bool _glowing;
    bool _hintShown, _consequenceShown;
    readonly List<Material> _glowMats = new List<Material>();

    protected abstract bool CheckComplete();

    /// <summary>Sant medan spelaren håller på med uppgiften. Då blinkar inget och klockan står still.</summary>
    protected virtual bool IsWorkingOnIt() => false;

    protected virtual void Start()
    {
        if (hintLight != null) hintLight.enabled = false;
    }

    void Update()
    {
        if (IsDone) return;
        if (!IsStarted)
        {
            if (ShouldStart()) StartTask();
            else return;
        }

        if (CheckComplete())
        {
            IsDone = true;
            DoneTime = Time.time;
            Severity = 0f;
            SetGlow(false);
            Message("Bra jobbat! Uppgiften är klar.", new Color(0.35f, 1f, 0.45f), 4f);
            Debug.Log($"[Uppgift] Klar: {title} efter {_t:F0} s");
            return;
        }

        bool working = IsWorkingOnIt();
        if (!working) _t += Time.deltaTime; // klockan står still medan man jobbar med uppgiften
        bool hint = _t >= hintAfterSeconds && !working;
        SetGlow(hint);
        if (hint)
        {
            Pulse();
            if (!_hintShown) { _hintShown = true; Message("Kom ihåg: " + title, new Color(1f, 0.85f, 0.3f), 6f); }
        }
        if (_t >= consequenceAfterSeconds && !_consequenceShown)
        {
            _consequenceShown = true;
            Message("Du mår sämre – " + LowerFirst(title) + "!", new Color(1f, 0.55f, 0.2f), 6f);
        }
        Severity = _t >= consequenceAfterSeconds
            ? Mathf.Clamp01((_t - consequenceAfterSeconds) / Mathf.Max(0.01f, consequenceRampSeconds))
            : 0f;
    }

    bool ShouldStart()
    {
        if (startAfterTask != null)
            return startAfterTask.IsDone && Time.time - startAfterTask.DoneTime >= startDelaySeconds;
        if (startWhenDoorOpens == null) return true;
        if (startWhenDoorOpens.IsOpen && !requirePlayerInside) return true;
        var cam = Camera.main;
        if (cam == null) return false;
        var zones = startWhenPlayerInside != null && startWhenPlayerInside.Length > 0
            ? startWhenPlayerInside
            : startWhenDoorOpens.blockedZones;
        if (zones == null) return false;
        foreach (var z in zones)
            if (z != null && ForrumUtil.InBox(z, cam.transform.position)) return true;
        return false;
    }

    /// <summary>Startar uppgiften direkt (används även för testning).</summary>
    public void StartTask()
    {
        if (IsStarted || IsDone) return;
        IsStarted = true;
        Message("Uppgift: " + title, Color.white, 7f);
        Debug.Log($"[Uppgift] Startad: {title} (ledtråd efter {hintAfterSeconds:F0} s, synpåverkan efter {consequenceAfterSeconds:F0} s)");
    }

    protected void Message(string text, Color color, float seconds)
    {
        if (showMessages) TaskMessage.Show(text, color, seconds);
    }

    static string LowerFirst(string s) => string.IsNullOrEmpty(s) ? s : char.ToLower(s[0]) + s.Substring(1);

    void SetGlow(bool on)
    {
        if (_glowing == on) return;
        _glowing = on;
        if (hintLight != null) hintLight.enabled = on;
        if (on)
        {
            _glowMats.Clear();
            foreach (var r in glowTargets)
            {
                if (r == null) continue;
                foreach (var m in r.materials) { m.EnableKeyword("_EMISSION"); _glowMats.Add(m); }
            }
        }
        else
        {
            foreach (var m in _glowMats)
            {
                if (m == null) continue;
                m.SetColor("_EmissionColor", Color.black);
                m.DisableKeyword("_EMISSION");
            }
        }
    }

    void Pulse()
    {
        float p = 0.5f + 0.5f * Mathf.Sin(Time.time * pulsesPerSecond * 2f * Mathf.PI);
        Color c = glowColor * Mathf.Lerp(0.1f, 1.3f, p);
        foreach (var m in _glowMats) if (m != null) m.SetColor("_EmissionColor", c);
        if (hintLight != null) hintLight.intensity = Mathf.Lerp(0.2f, 1.6f, p);
    }

    /// <summary>För testning: ändra tiderna på alla uppgifter.</summary>
    public void SetTimes(float hint, float consequence)
    {
        hintAfterSeconds = hint;
        consequenceAfterSeconds = consequence;
    }
}
