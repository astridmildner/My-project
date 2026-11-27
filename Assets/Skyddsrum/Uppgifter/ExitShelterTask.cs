using UnityEngine;

/// <summary>
/// Sista uppgiften: signalen "Faran över" spelas och man ska öppna dörren och gå ut till förrummet.
/// Klar när spelarens huvud är inne i förrummet. Ingen synpåverkan – bara ledtråd (handtagen lyser).
/// </summary>
public class ExitShelterTask : ShelterTask
{
    public ShelterDoor door;
    [Tooltip("Området man ska gå till (förrummet)")]
    public BoxCollider targetZone;
    [Tooltip("Signalen \"Faran över\"")]
    public AudioSource signal;

    bool _played;

    protected override bool CheckComplete()
    {
        var cam = Camera.main;
        return cam != null && targetZone != null && ForrumUtil.InBox(targetZone, cam.transform.position);
    }

    protected override bool IsWorkingOnIt() => door != null && Time.time - door.LastHandleTime < 3f;

    void LateUpdate()
    {
        if (!IsStarted || _played) return;
        _played = true;
        if (signal != null) signal.Play();
    }

    protected override string StartMessage =>
        "Faran över!\nÖppna dörren – lyft båda handtagen till Ö – och gå ut till förrummet.";

    protected override string DoneMessage =>
        "Bra jobbat! Du tog dig ut när faran var över.\nDu har klarat skyddsrummet!";

    /// <summary>Ingen synpåverkan för den här uppgiften – bara ledtråd.</summary>
    public override void SetTimes(float hint, float consequence) => base.SetTimes(hint, 1000000f);
}
