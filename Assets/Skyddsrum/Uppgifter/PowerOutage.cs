using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Strömavbrott: lamporna slocknar (men det blir inte kolsvart), fläkten tystnar och man måste
/// veva ventilationen. Har man packat ficklampa får man ta fram den. Efter en bestämd tid kommer
/// strömmen tillbaka – oberoende av vevandet – och fläkten går på el igen.
/// </summary>
public class PowerOutage : MonoBehaviour
{
    [Header("När strömmen går")]
    [Tooltip("Strömmen går när den här uppgiften är klar (plus fördröjning)")]
    public ShelterTask startAfterTask;
    public float startDelaySeconds = 10f;
    [Tooltip("Sekunder från att strömmen går tills vevuppgiften börjar")]
    public float crankStartDelay = 5f;
    [Tooltip("Hur länge man behöver veva innan strömmen kommer tillbaka")]
    public float crankSeconds = 40f;

    [Header("Belysning")]
    [Tooltip("Lampor som släcks helt")]
    public Light[] lightsOff = new Light[0];
    [Tooltip("Lampor som bara dämpas, så att det inte blir kolsvart")]
    public Light[] lightsDim = new Light[0];
    [Range(0f, 1f)] public float dimFraction = 0.25f;
    [Tooltip("Hur mycket av det förberäknade omgivningsljuset (ljusprober) som finns kvar vid strömavbrott")]
    [Range(0f, 1f)] public float ambientFraction = 0.18f;
    [Tooltip("Tänds vid strömavbrott (t.ex. svagt grönt sken vid utgångsskyltarna)")]
    public Light[] emergencyLights = new Light[0];
    [Tooltip("Gör bilden lite mörkare och gråare under strömavbrottet")]
    public Volume darknessVolume;

    [Header("Ventilation")]
    public VentilationCrank crank;
    public CrankTask crankTask;
    public AudioSource fanSound;
    public float fanVolume = 0.3f;

    [Header("Ficklampa")]
    public Backpack backpack;
    [Tooltip("Ficklampan som dyker upp framför spelaren om den var packad")]
    public GameObject flashlight;
    public string flashlightCategory = "Ficklampa";
    /// <summary>Endast test: -1 = kolla ryggsäcken, 0 = ingen ficklampa, 1 = ficklampa.</summary>
    [System.NonSerialized] public int debugFlashlight = -1;

    public enum State { PowerOn, Outage, Restored }
    public State Current { get; private set; } = State.PowerOn;
    public bool PowerOn => Current != State.Outage;
    public bool IsRestored => Current == State.Restored;
    public bool HadFlashlight { get; private set; }

    /// <summary>0–1: hur stor del av vevtiden som gått.</summary>
    public float CrankProgress01 => IsRestored ? 1f : !_crankStarted ? 0f : Mathf.Clamp01((Time.time - _crankStartAt) / Mathf.Max(0.01f, crankSeconds));
    public float SecondsUntilPowerBack => IsRestored ? 0f : !_crankStarted ? crankSeconds : Mathf.Max(0f, crankSeconds - (Time.time - _crankStartAt));

    float _outageAt = -1f, _crankStartAt;
    bool _crankStarted;
    float[] _offOrig, _dimOrig;
    UnityEngine.Rendering.SphericalHarmonicsL2[] _probeOrig;

    void Start()
    {
        _offOrig = lightsOff.Select(l => l != null ? l.intensity : 0f).ToArray();
        _dimOrig = lightsDim.Select(l => l != null ? l.intensity : 0f).ToArray();
        var lp = LightmapSettings.lightProbes;
        if (lp != null && lp.bakedProbes != null) _probeOrig = (UnityEngine.Rendering.SphericalHarmonicsL2[])lp.bakedProbes.Clone();
        foreach (var l in emergencyLights) if (l != null) l.enabled = false;
        if (darknessVolume != null) darknessVolume.weight = 0f;
        if (flashlight != null)
        {
            flashlight.SetActive(false);
            var grab = flashlight.GetComponent<XRGrabInteractable>();
            if (grab != null) grab.selectExited.AddListener(OnFlashlightReleased);
        }
        if (crank != null) crank.motorRunning = true;
        if (fanSound != null)
        {
            if (fanSound.clip == null) fanSound.clip = MakeHum();
            fanSound.loop = true;
            fanSound.volume = fanVolume;
            fanSound.Play();
        }
    }

    void Update()
    {
        if (_skipRequested) ApplySkip();
        if (Current == State.PowerOn && startAfterTask != null && startAfterTask.IsDone
            && Time.time - startAfterTask.DoneTime >= startDelaySeconds)
            BeginOutage();

        if (Current == State.Outage)
        {
            if (!_crankStarted && Time.time - _outageAt >= crankStartDelay)
            {
                _crankStarted = true;
                _crankStartAt = Time.time;
                if (crankTask != null) crankTask.StartTask();
            }
            if (_crankStarted && Time.time - _crankStartAt >= crankSeconds) Restore();
        }
        UpdateFanSound();
    }

    /// <summary>Startar strömavbrottet direkt (används även för testning).</summary>
    public void BeginOutage()
    {
        if (Current != State.PowerOn) return;
        Current = State.Outage;
        _outageAt = Time.time;
        if (crank != null) crank.motorRunning = false;
        StopAllCoroutines();
        StartCoroutine(Flicker(false));

        HadFlashlight = HasFlashlight();
        if (HadFlashlight)
        {
            SpawnFlashlight();
            TaskMessage.Show("Strömmen har gått!\nDu packade en ficklampa – ta den, den är framför dig.", new Color(1f, 0.93f, 0.7f), crankStartDelay);
        }
        else
        {
            TaskMessage.Show("Strömmen har gått!\nDu har ingen ficklampa – den hade varit bra att packa.", new Color(1f, 0.65f, 0.25f), crankStartDelay);
        }
        Debug.Log($"[Ström] Strömavbrott (ficklampa: {(HadFlashlight ? "ja" : "nej")}). Strömmen kommer tillbaka om {crankStartDelay + crankSeconds:F0} s.");
    }

    void Restore()
    {
        Current = State.Restored;
        if (crank != null) crank.motorRunning = true;
        StopAllCoroutines();
        StartCoroutine(Flicker(true));
        Debug.Log("[Ström] Strömmen är tillbaka.");
    }

    /// <summary>Endast test: hoppar fram så att strömmen kommer tillbaka om ett par sekunder.</summary>
    public void DebugSkipToEnd() => _skipRequested = true;
    bool _skipRequested;

    void ApplySkip()
    {
        _skipRequested = false;
        if (Current == State.PowerOn) BeginOutage();
        if (Current != State.Outage) return;
        if (!_crankStarted) { _crankStarted = true; if (crankTask != null) crankTask.StartTask(); }
        _crankStartAt = Time.time - crankSeconds + 2f;
        Debug.Log($"[Ström] Test: strömmen kommer tillbaka om 2 s (t={Time.time:F1}, start={_crankStartAt:F1}).");
    }

    bool HasFlashlight()
    {
        if (debugFlashlight >= 0) return debugFlashlight == 1;
        if (backpack == null) backpack = FindFirstObjectByType<Backpack>();
        if (backpack == null) return false;
        return backpack.Contents.Any(i => i != null && i.allowed &&
            (i.Category.IndexOf(flashlightCategory, System.StringComparison.OrdinalIgnoreCase) >= 0 ||
             i.displayName.IndexOf(flashlightCategory, System.StringComparison.OrdinalIgnoreCase) >= 0));
    }

    void SpawnFlashlight()
    {
        if (flashlight == null) return;
        var cam = Camera.main;
        flashlight.SetActive(true);
        if (cam != null)
        {
            Vector3 fwd = ForrumUtil.Flat(cam.transform.forward);
            flashlight.transform.SetPositionAndRotation(
                cam.transform.position + fwd * 0.45f + Vector3.down * 0.15f,
                Quaternion.LookRotation(fwd, Vector3.up));
        }
        var rb = flashlight.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }
    }

    void OnFlashlightReleased(SelectExitEventArgs args)
    {
        // Efter att man hållit i den beter den sig som ett vanligt föremål (faller ner)
        var rb = flashlight.GetComponent<Rigidbody>();
        if (rb != null) { rb.isKinematic = false; rb.useGravity = true; }
    }

    IEnumerator Flicker(bool on)
    {
        float[] levels = on ? new[] { 1f, 0f, 1f, 0.3f, 1f } : new[] { 0f, 1f, 0f, 0.6f, 0f };
        float[] waits = { 0.1f, 0.12f, 0.09f, 0.15f, 0f };
        for (int i = 0; i < levels.Length; i++)
        {
            SetLevel(levels[i]);
            if (waits[i] > 0f) yield return new WaitForSeconds(waits[i]);
        }
    }

    /// <summary>1 = full ström, 0 = strömavbrott.</summary>
    void SetLevel(float k)
    {
        for (int i = 0; i < lightsOff.Length; i++) if (lightsOff[i] != null) lightsOff[i].intensity = _offOrig[i] * k;
        for (int i = 0; i < lightsDim.Length; i++) if (lightsDim[i] != null) lightsDim[i].intensity = _dimOrig[i] * Mathf.Lerp(dimFraction, 1f, k);
        foreach (var l in emergencyLights) if (l != null) l.enabled = k < 0.5f;
        if (darknessVolume != null) darknessVolume.weight = 1f - k;
        SetProbes(Mathf.Lerp(ambientFraction, 1f, k));
    }

    void SetProbes(float factor)
    {
        var lp = LightmapSettings.lightProbes;
        if (lp == null || _probeOrig == null || _probeOrig.Length != lp.count) return;
        var arr = new UnityEngine.Rendering.SphericalHarmonicsL2[_probeOrig.Length];
        for (int i = 0; i < arr.Length; i++) arr[i] = _probeOrig[i] * factor;
        lp.bakedProbes = arr;
    }

    // Återställ ljusproberna så att inget ändras permanent (t.ex. när Play stoppas)
    void OnDisable() => SetProbes(1f);

    void UpdateFanSound()
    {
        if (fanSound == null) return;
        float air = crank != null ? crank.Airflow01 : 0f;
        float vol = PowerOn ? fanVolume : fanVolume * 0.8f * air;
        float pitch = PowerOn ? 1f : Mathf.Lerp(0.55f, 0.95f, air);
        float dt = Time.deltaTime;
        fanSound.volume = Mathf.MoveTowards(fanSound.volume, vol, 0.25f * dt);
        fanSound.pitch = Mathf.MoveTowards(fanSound.pitch, pitch, 0.3f * dt);
    }

    /// <summary>Ett lågt fläktbrus (skapas i koden, ingen ljudfil behövs).</summary>
    static AudioClip MakeHum()
    {
        const int rate = 44100;
        int n = rate * 2, xf = rate / 10, outN = n - xf;
        var d = new float[n];
        var rng = new System.Random(7);
        float lp = 0f, lp2 = 0f;
        for (int i = 0; i < n; i++)
        {
            float white = (float)(rng.NextDouble() * 2.0 - 1.0);
            lp += (white - lp) * 0.06f;
            lp2 += (lp - lp2) * 0.12f;
            float t = i / (float)rate;
            d[i] = lp2 * 3f + 0.16f * Mathf.Sin(2f * Mathf.PI * 100f * t) + 0.07f * Mathf.Sin(2f * Mathf.PI * 200f * t) + 0.05f * Mathf.Sin(2f * Mathf.PI * 50f * t);
        }
        var o = new float[outN];
        float peak = 0.001f;
        for (int i = 0; i < outN; i++)
        {
            o[i] = i < xf ? Mathf.Lerp(d[outN + i], d[i], i / (float)xf) : d[i];
            peak = Mathf.Max(peak, Mathf.Abs(o[i]));
        }
        for (int i = 0; i < outN; i++) o[i] *= 0.8f / peak;
        var clip = AudioClip.Create("Fläktbrus", outN, 1, rate, false);
        clip.SetData(o, 0);
        return clip;
    }
}
