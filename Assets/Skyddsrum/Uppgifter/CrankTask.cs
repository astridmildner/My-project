using UnityEngine;

/// <summary>
/// Uppgift: veva ventilationen. Med strömavbrott (outage satt): håll ventilationen igång genom att
/// veva tills strömmen kommer tillbaka. Utan strömavbrott: veva en viss tid.
/// </summary>
public class CrankTask : ShelterTask
{
    public VentilationCrank crank;
    [Tooltip("Strömavbrottet som styr uppgiften (tomt = veva en viss tid i stället)")]
    public PowerOutage outage;
    [Tooltip("Används bara utan strömavbrott: hur många sekunder man måste veva totalt")]
    public float requiredCrankSeconds = 27f;
    [Tooltip("Lägsta varvtal (varv/min) som räknas som att man vevar")]
    public float minRpm = 12f;

    [Header("Under strömavbrottet: sekunder utan att veva innan …")]
    public float outageHintSeconds = 8f;
    public float outageConsequenceSeconds = 15f;

    public float CrankedSeconds { get; private set; }
    /// <summary>Senaste tidpunkten (Time.time) då man vevade.</summary>
    public float LastCrankTime { get; private set; } = -999f;

    public float Progress01 => outage != null
        ? outage.CrankProgress01
        : Mathf.Clamp01(CrankedSeconds / Mathf.Max(0.01f, requiredCrankSeconds));

    /// <summary>Sekunder kvar att veva (med strömavbrott: tills strömmen kommer tillbaka).</summary>
    public float RemainingSeconds => outage != null
        ? outage.SecondsUntilPowerBack
        : Mathf.Max(0f, requiredCrankSeconds - CrankedSeconds);

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
                Message("Bra! Fortsätt veva – halvvägs.", new Color(0.7f, 0.9f, 1f), 3f);
            }
        }
        return cranking || crank.IsGrabbed;
    }

    protected override bool CheckComplete() => outage != null ? outage.IsRestored : CrankedSeconds >= requiredCrankSeconds;

    protected override string StartMessage => outage != null
        ? "Fläkten står still utan ström.\nVeva ventilationen så att ni får luft!"
        : base.StartMessage;

    protected override string DoneMessage => outage != null
        ? "Strömmen är tillbaka!\nFläkten går på el igen – du kan sluta veva."
        : base.DoneMessage;

    /// <summary>Under strömavbrott blir luften fort dålig om man inte vevar, så tiderna är kortare.</summary>
    public override void SetTimes(float hint, float consequence)
    {
        if (outage != null) base.SetTimes(Mathf.Min(hint, outageHintSeconds), Mathf.Min(consequence, outageConsequenceSeconds));
        else base.SetTimes(hint, consequence);
    }

    /// <summary>För testning: låtsas att man vevat en stund.</summary>
    public void DebugAddCrankSeconds(float seconds)
    {
        if (!IsStarted) StartTask();
        _debugAdd += seconds;
    }
    float _debugAdd;

    void LateUpdate()
    {
        if (_debugAdd <= 0f) return;
        CrankedSeconds += _debugAdd;
        LastCrankTime = Time.time;
        _debugAdd = 0f;
    }
}
