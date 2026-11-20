using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Dörren in till skyddsrummet. Öppnas när spelaren står vid dörren med en godkänd
/// ryggsäck på ryggen och tomma händer. Försöker man gå in med något i handen
/// tappar man det och det hamnar utanför dörren.
/// </summary>
public class ShelterDoor : MonoBehaviour
{
    public Backpack backpack;
    public CheckShelf checkShelf;

    [Header("Dörr")]
    [Tooltip("Gångjärnet – det som vrids när dörren öppnas")]
    public Transform hinge;
    public float openAngle = -95f;
    public float openSpeed = 60f;

    [Header("Zoner")]
    [Tooltip("Där spelaren ska stå för att dörren ska öppnas")]
    public BoxCollider approachZone;
    [Tooltip("Själva dörröppningen. Håller man något här tappar man det.")]
    public BoxCollider doorwayZone;
    [Tooltip("Hit flyttas saker som man försökte ta med in")]
    public Transform returnPoint;

    [Header("Feedback")]
    public TextMeshPro sign;
    public Renderer lamp;
    public AudioSource unlockSound;

    [Header("Spärr när dörren är låst")]
    [Tooltip("Områden spelaren inte får gå in i förrän dörren är öppen (skyddsrummet)")]
    public BoxCollider[] blockedZones = new BoxCollider[0];
    [Tooltip("Åt vilket håll spelaren skjuts ut ur respektive område (mot förrummet)")]
    public Vector3[] pushDirections = new Vector3[0];
    [Tooltip("Hur långt utanför området spelaren hamnar (meter)")]
    public float pushMargin = 0.15f;

    [Header("När dörren öppnas")]
    [Tooltip("T.ex. teleporteringsytorna inne i skyddsrummet")]
    public GameObject[] enableWhenOpen;
    public GameObject[] disableWhenOpen;
    public UnityEvent onOpened;

    public bool IsOpen { get; private set; }

    static readonly Color Locked = new Color(0.85f, 0.1f, 0.08f);
    static readonly Color Open = new Color(0.1f, 0.8f, 0.2f);

    float _angle;
    float _nextScan;
    readonly List<XRBaseInteractor> _interactors = new List<XRBaseInteractor>();
    readonly List<IXRSelectInteractable> _tmp = new List<IXRSelectInteractable>();

    void Start()
    {
        foreach (var go in enableWhenOpen) if (go != null) go.SetActive(false);
        Show(Locked, "<b>Dörren är låst</b>");
    }

    void Update()
    {
        var cam = Camera.main;
        if (IsOpen)
        {
            _angle = Mathf.MoveTowards(_angle, openAngle, openSpeed * Time.deltaTime);
            if (hinge != null) hinge.localRotation = Quaternion.Euler(0f, _angle, 0f);
            if (cam != null && ForrumUtil.InBox(doorwayZone, cam.transform.position)) DropHeldItems();
            return;
        }

        if (cam != null && BlockWalkingIn(cam)) return;

        bool near = cam != null && ForrumUtil.InBox(approachZone, cam.transform.position);
        if (backpack == null || !backpack.IsOnBack)
        {
            Show(Locked, checkShelf != null && checkShelf.Approved
                ? "<b>Dörren är låst</b>\nTa din ryggsäck från incheckningshyllan."
                : "<b>Dörren är låst</b>\nPacka ryggsäcken och ställ den på\nincheckningshyllan.");
        }
        else if (HandsBusy())
        {
            Show(Locked, "<b>Dörren är låst</b>\nDu måste ha tomma händer för att gå in.");
        }
        else if (!near)
        {
            Show(Locked, "<b>Dörren är låst</b>\nGå fram till dörren.");
        }
        else
        {
            OpenDoor();
        }
    }

    /// <summary>Skjuter tillbaka spelaren om huvudet hamnar i skyddsrummet medan dörren är låst.</summary>
    bool BlockWalkingIn(Camera cam)
    {
        Vector3 head = cam.transform.position;
        for (int i = 0; i < blockedZones.Length; i++)
        {
            var zone = blockedZones[i];
            // Avbockade spärrzoner (eller "Spärrzoner"-objektet) räknas inte – praktiskt under utveckling
            if (zone == null || !zone.gameObject.activeInHierarchy || !ForrumUtil.InBox(zone, head)) continue;
            Vector3 dir = i < pushDirections.Length && pushDirections[i].sqrMagnitude > 0.0001f
                ? pushDirections[i].normalized
                : -transform.forward;
            float d = DistanceToExit(zone, head, dir) + pushMargin;
            MoveRig(cam, dir * d);
            Show(Locked, "<b>Dörren är låst</b>\nDu kommer inte in förrän du\npackat rätt och har tomma händer.", true);
            _holdMessageUntil = Time.time + 3f;
            return true;
        }
        return false;
    }

    static float DistanceToExit(BoxCollider box, Vector3 worldPoint, Vector3 worldDir)
    {
        Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 ld = box.transform.InverseTransformDirection(worldDir);
        Vector3 half = box.size * 0.5f;
        float t = float.MaxValue;
        for (int a = 0; a < 3; a++)
        {
            if (ld[a] > 0.0001f) t = Mathf.Min(t, (half[a] - local[a]) / ld[a]);
            else if (ld[a] < -0.0001f) t = Mathf.Min(t, (-half[a] - local[a]) / ld[a]);
        }
        return t == float.MaxValue ? 0f : Mathf.Max(0f, t);
    }

    static void MoveRig(Camera cam, Vector3 delta)
    {
        var rig = cam.transform.root;
        var cc = rig.GetComponentInChildren<CharacterController>();
        bool ccOn = cc != null && cc.enabled;
        if (ccOn) cc.enabled = false;
        rig.position += new Vector3(delta.x, 0f, delta.z);
        if (ccOn) cc.enabled = true;
    }

    void OpenDoor()
    {
        IsOpen = true;
        Show(Open, "<b>Välkommen in i skyddsrummet</b>", true);
        foreach (var go in enableWhenOpen) if (go != null) go.SetActive(true);
        foreach (var go in disableWhenOpen) if (go != null) go.SetActive(false);
        if (unlockSound != null) unlockSound.Play();
        onOpened?.Invoke();
    }

    void ScanInteractors()
    {
        if (Time.time < _nextScan) return;
        _nextScan = Time.time + 1f;
        _interactors.Clear();
        _interactors.AddRange(FindObjectsByType<XRBaseInteractor>(FindObjectsSortMode.None));
    }

    bool HandsBusy()
    {
        ScanInteractors();
        foreach (var i in _interactors)
        {
            if (i == null || i is XRSocketInteractor) continue;
            if (i.hasSelection) return true;
        }
        return false;
    }

    void DropHeldItems()
    {
        ScanInteractors();
        foreach (var i in _interactors)
        {
            if (i == null || i is XRSocketInteractor || !i.hasSelection) continue;
            _tmp.Clear();
            _tmp.AddRange(i.interactablesSelected);
            i.interactionManager.CancelInteractorSelection((IXRSelectInteractor)i);
            foreach (var held in _tmp)
            {
                var t = held.transform;
                if (returnPoint != null) t.position = returnPoint.position;
                var rb = t.GetComponent<Rigidbody>();
                if (rb != null && !rb.isKinematic) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            }
            Show(Locked, "Du kan inte ta med dig saker i händerna.\nDet du höll i ligger kvar utanför.");
        }
    }

    float _holdMessageUntil;

    void Show(Color c, string text, bool force = false)
    {
        if (!force && Time.time < _holdMessageUntil) return;
        if (sign != null) sign.text = text;
        if (lamp != null) lamp.material.color = c;
    }
}
