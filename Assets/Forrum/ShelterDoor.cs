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

    [Header("Handtag på insidan (verklighetstrogen dörr)")]
    [Tooltip("Båda måste vara nere (S) för att dörren ska vara stängd och tät")]
    public ShelterDoorHandle[] handles = new ShelterDoorHandle[0];
    public float closeSpeed = 70f;
    public UnityEvent onSealed;

    /// <summary>Dörren är upplåst (man har packat rätt). Förblir sann även om man stänger den inifrån.</summary>
    public bool IsOpen { get; private set; }
    /// <summary>Dörrbladet står stängt i karmen (efter att man varit inne).</summary>
    public bool IsLeafClosed => IsOpen && !_leafOpen && Mathf.Abs(_angle) < 0.5f;
    /// <summary>Dörren är stängd och båda handtagen är nere (S).</summary>
    public bool IsSealed => IsLeafClosed && AllHandles(true);

    /// <summary>När spelaren senast nöp ett handtag (Time.time).</summary>
    public float LastHandleTime { get; private set; } = -999f;

    bool _leafOpen;
    bool HasHandles => handles != null && handles.Length > 0;

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
            UpdatePendingHandle();
            float target = (_leafOpen || !HasHandles) ? openAngle : 0f;
            _angle = Mathf.MoveTowards(_angle, target, (target == 0f ? closeSpeed : openSpeed) * Time.deltaTime);
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
        _leafOpen = true;
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

    // ---------------- Handtagen på insidan ----------------

    bool AllHandles(bool down)
    {
        if (!HasHandles) return false;
        foreach (var h in handles) if (h != null && h.IsDown != down) return false;
        return true;
    }

    ShelterDoorHandle _pendingHandle;

    /// <summary>Anropas när spelaren nyper/greppar ett av handtagen.</summary>
    public void HandlePressed(ShelterDoorHandle handle)
    {
        if (handle == null) return;
        LastHandleTime = Time.time;

        if (!IsOpen)
        {
            // Handtagen sitter på insidan – står man där ska de alltid fungera
            // (t.ex. om man kom in med spärrzonerna avstängda under utveckling).
            UnlockFromInside();
        }

        if (_leafOpen)
        {
            // Dörren står öppen: den slår igen och handtaget trycks ner när den är stängd
            _leafOpen = false;
            _pendingHandle = handle;
            Debug.Log("[Dörr] Handtag nypt – dörren stängs.");
            return;
        }
        if (Mathf.Abs(_angle) > 0.5f) { _pendingHandle = handle; return; } // håller på att stängas

        handle.SetDown(!handle.IsDown);
        AfterHandleChanged(handle);
    }

    void UpdatePendingHandle()
    {
        if (_pendingHandle == null || _leafOpen || Mathf.Abs(_angle) > 0.5f) return;
        var h = _pendingHandle;
        _pendingHandle = null;
        h.SetDown(true);
        AfterHandleChanged(h);
    }

    void AfterHandleChanged(ShelterDoorHandle handle)
    {
        var white = new Color(1f, 0.95f, 0.8f);
        Debug.Log($"[Dörr] Handtag {handle.name}: {(handle.IsDown ? "nere (S)" : "uppe (Ö)")}");
        if (AllHandles(true))
        {
            TaskMessage.Show("Bra! Dörren är stängd och tät.", new Color(0.3f, 1f, 0.4f), 4f);
            onSealed?.Invoke();
        }
        else if (AllHandles(false))
        {
            _leafOpen = true;
            TaskMessage.Show("Båda handtagen är uppe (Ö) – dörren öppnas.", white, 4f);
        }
        else if (handle.IsDown)
        {
            TaskMessage.Show("Tryck ner det andra handtaget också.", white, 4f);
        }
        else
        {
            TaskMessage.Show("Ett handtag är uppe – dörren är inte tät.", new Color(1f, 0.8f, 0.2f), 4f);
        }
    }

    void UnlockFromInside()
    {
        IsOpen = true;
        _leafOpen = false; // dörren står kvar stängd
        foreach (var go in enableWhenOpen) if (go != null) go.SetActive(true);
        foreach (var go in disableWhenOpen) if (go != null) go.SetActive(false);
        Show(Open, "<b>Dörren är upplåst</b>", true);
        Debug.Log("[Dörr] Upplåst inifrån via handtaget.");
    }

    /// <summary>Endast för test i editorn: låser upp dörren direkt.</summary>
    public void DebugUnlock() { if (!IsOpen) OpenDoor(); }

    /// <summary>Endast för test i editorn: stänger dörren och trycker ner båda handtagen.</summary>
    public void DebugSeal()
    {
        if (!IsOpen) OpenDoor();
        _leafOpen = false;
        LastHandleTime = Time.time;
        if (HasHandles) foreach (var h in handles) if (h != null) h.SetDown(true);
    }

    float _holdMessageUntil;

    void Show(Color c, string text, bool force = false)
    {
        if (!force && Time.time < _holdMessageUntil) return;
        if (sign != null) sign.text = text;
        if (lamp != null) lamp.material.color = c;
    }
}
