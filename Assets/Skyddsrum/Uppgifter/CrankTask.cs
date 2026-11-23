using UnityEngine;

/// <summary>Uppgift: veva ventilationen en viss tid.</summary>
public class CrankTask : ShelterTask
{
    public VentilationCrank crank;
    [Tooltip("Hur många sekunder man måste veva totalt")]
    public float requiredCrankSeconds = 27f;
    [Tooltip("Lägsta varvtal (varv/min) som räknas som att man vevar")]
    public float minRpm = 12f;

    public float CrankedSeconds { get; private set; }
    /// <summary>Senaste tidpunkten (Time.time) då man vevade.</summary>
    public float LastCrankTime { get; private set; } = -999f;
    public float Progress01 => Mathf.Clamp01(CrankedSeconds / Mathf.Max(0.01f, requiredCrankSeconds));

    bool _halfShown;

    protected override bool IsWorkingOnIt()
    {
        if (crank == null) return false;
        bool cranking = crank.Rpm >= minRpm;
        if (cranking)
        {
            CrankedSeconds += Time.deltaTime;
            LastCrankTime = Time.time;
            if (!_halfShown && Progress01 >= 0.5f)
            {
                _halfShown = true;
                Message("Bra! Fortsätt veva – halvvägs klart.", new Color(0.7f, 0.9f, 1f), 3f);
            }
        }
        return cranking || crank.IsGrabbed;
    }

    /// <summary>För testning: låtsas att man vevat en stund.</summary>
    public void DebugAddCrankSeconds(float seconds)
    {
        if (!IsStarted) StartTask();
        CrankedSeconds += seconds;
        LastCrankTime = Time.time;
    }

    protected override bool CheckComplete() => CrankedSeconds >= requiredCrankSeconds;
}
