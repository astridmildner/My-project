using UnityEngine;

/// <summary>Uppgift: fyll baljorna med vatten.</summary>
public class FillBasinsTask : ShelterTask
{
    public WaterBasin[] basins = new WaterBasin[0];
    [Range(0f, 1f)]
    [Tooltip("Hur fulla baljorna måste vara (0.9 = 90 %)")]
    public float requiredFill = 0.9f;

    [Tooltip("Hur länge (sekunder) det räknas som att man fyller efter att vattnet slutat öka")]
    public float workingGraceSeconds = 2f;

    float _lastTotal = -1f;
    float _lastProgressTime = -999f;

    protected override bool IsWorkingOnIt()
    {
        float total = 0f;
        foreach (var b in basins) if (b != null) total += b.Liters;
        if (_lastTotal >= 0f && total > _lastTotal + 0.0001f) _lastProgressTime = Time.time;
        _lastTotal = total;
        return Time.time - _lastProgressTime < workingGraceSeconds;
    }

    protected override bool CheckComplete()
    {
        if (basins == null || basins.Length == 0) return false;
        foreach (var b in basins)
            if (b == null || b.Fill01 < requiredFill) return false;
        return true;
    }
}
