using UnityEngine;

/// <summary>
/// Uppgift: stäng skyddsrumsdörren. Klar när dörren är stängd och båda handtagen är nere (S).
/// </summary>
public class CloseDoorTask : ShelterTask
{
    public ShelterDoor door;
    [Tooltip("Hur länge (sekunder) efter att man nupit ett handtag det räknas som att man jobbar med uppgiften")]
    public float workingGraceSeconds = 3f;

    protected override bool IsWorkingOnIt()
        => door != null && Time.time - door.LastHandleTime < workingGraceSeconds;

    protected override bool CheckComplete() => door != null && door.IsSealed;
}
