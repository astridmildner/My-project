using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Ryggsäcken. Släpp en sak i öppningen så packas den. Listan ovanför väskan visar
/// vad som ligger i den, och man nyper på en rad för att ta ut saken igen.
/// När väskan godkänts på incheckningshyllan stängs den, och nästa gång man
/// lyfter den hamnar den automatiskt på ryggen.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Backpack : MonoBehaviour
{
    [Header("Delar")]
    [Tooltip("Trigger-collider i väskans öppning")]
    public Collider opening;
    [Tooltip("Lock som visas när väskan är stängd")]
    public GameObject closedLid;
    [Tooltip("Material för listans rader")]
    public Material panelMaterial;

    [Header("Lista")]
    public float panelHeightAboveBag = 1.0f;
    public float rowWidth = 0.24f;
    public float rowHeight = 0.04f;
    public int rowsPerColumn = 11;

    [Header("På ryggen")]
    [Tooltip("Var väskan sitter i förhållande till huvudet (bakåt = negativ z)")]
    public Vector3 backOffset = new Vector3(0f, -0.7f, -0.25f);

    [Header("Ljud (valfritt)")]
    public AudioSource packSound;

    public event Action ContentsChanged;

    public IReadOnlyList<PackItem> Contents => _contents;
    public bool IsClosed { get; private set; }
    public bool IsOnBack { get; private set; }
    public bool HighlightWrong { get; set; }
    public bool IsHeld => _grab != null && _grab.isSelected;
    public float Speed => _rb != null && !_rb.isKinematic ? _rb.linearVelocity.magnitude : 0f;
    public int TotalCategories { get; private set; }
    public int CorrectCount => _contents.Where(i => i.allowed).Select(i => i.Category).Distinct().Count();
    public int WrongCount => _contents.Count(i => !i.allowed);

    readonly List<PackItem> _contents = new List<PackItem>();
    readonly Dictionary<PackItem, Transform> _originalParent = new Dictionary<PackItem, Transform>();
    XRGrabInteractable _grab;
    Rigidbody _rb;
    bool _attachPending;
    PackItem _pendingRemove;

    // Listan
    Transform _panel;
    TextMeshPro _title;
    readonly List<Row> _rows = new List<Row>();
    bool _dirty = true;

    class Row
    {
        public GameObject go;
        public PackItem item;
        public XRSimpleInteractable button;
        public Renderer bg;
    }

    static readonly Color RowColor = new Color(0.15f, 0.18f, 0.22f);
    static readonly Color RowHover = new Color(0.25f, 0.45f, 0.7f);
    static readonly Color RowWrong = new Color(0.7f, 0.12f, 0.1f);

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        _rb = GetComponent<Rigidbody>();
        if (closedLid != null) closedLid.SetActive(false);
        BuildPanel();
    }

    void Start()
    {
        TotalCategories = FindObjectsByType<PackItem>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(i => i.allowed).Select(i => i.Category).Distinct().Count();
    }

    void OnEnable() => _grab.selectEntered.AddListener(OnGrabbed);
    void OnDisable() => _grab.selectEntered.RemoveListener(OnGrabbed);

    void OnDestroy()
    {
        if (_panel != null) Destroy(_panel.gameObject);
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        if (IsClosed && !IsOnBack) _attachPending = true;
    }

    // ---------- Packa ----------
    void OnTriggerStay(Collider other)
    {
        if (IsClosed || IsOnBack) return;
        if (other.isTrigger) return;
        var item = other.GetComponentInParent<PackItem>();
        if (item == null || item.IsHeld || !item.CanBePacked || _contents.Contains(item)) return;
        // Bara om saken faktiskt är i öppningen (inte bara nuddar väskans utsida)
        if (opening != null && !opening.bounds.Intersects(other.bounds)) return;
        Pack(item);
    }

    public void Pack(PackItem item)
    {
        _contents.Add(item);
        _originalParent[item] = item.transform.parent;
        item.transform.SetParent(transform, true);
        item.gameObject.SetActive(false);
        if (packSound != null) packSound.Play();
        Changed();
    }

    public void Unpack(PackItem item)
    {
        if (!_contents.Remove(item)) return;
        _originalParent.TryGetValue(item, out var parent);
        item.transform.SetParent(parent, true);

        // Lägg saken bredvid väskan, på sidan mot spelaren
        var cam = Camera.main;
        Vector3 toPlayer = cam != null ? ForrumUtil.Flat(cam.transform.position - transform.position) : -transform.forward;
        item.transform.position = transform.position + toPlayer * 0.3f + Vector3.up * 0.25f;
        item.transform.rotation = Quaternion.LookRotation(toPlayer, Vector3.up);
        item.BlockPacking(1.5f);
        item.gameObject.SetActive(true);
        if (item.Body != null && !item.Body.isKinematic)
        {
            item.Body.linearVelocity = Vector3.zero;
            item.Body.angularVelocity = Vector3.zero;
        }
        Changed();
    }

    void Changed()
    {
        _dirty = true;
        ContentsChanged?.Invoke();
    }

    // ---------- Stäng och ta på ----------
    public void Close()
    {
        IsClosed = true;
        if (closedLid != null) closedLid.SetActive(true);
        _dirty = true;
    }

    void AttachToBack()
    {
        _attachPending = false;
        if (_grab.interactionManager != null)
            _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
        _grab.enabled = false;
        _rb.isKinematic = true;
        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
        IsOnBack = true;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    void Update()
    {
        if (_attachPending) AttachToBack();
        if (_pendingRemove != null)
        {
            var item = _pendingRemove;
            _pendingRemove = null;
            Unpack(item);
        }
        if (_dirty) RebuildRows();
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (IsOnBack)
        {
            if (cam == null) return;
            Quaternion yaw = Quaternion.LookRotation(ForrumUtil.Flat(cam.transform.forward), Vector3.up);
            transform.SetPositionAndRotation(cam.transform.position + yaw * backOffset, yaw);
            return;
        }

        if (_panel != null)
        {
            _panel.position = transform.position + Vector3.up * panelHeightAboveBag;
            ForrumUtil.FaceCamera(_panel, cam);
            foreach (var r in _rows)
            {
                Color c = r.button.isHovered ? RowHover
                        : (HighlightWrong && !r.item.allowed) ? RowWrong
                        : RowColor;
                r.bg.material.color = c;
            }
        }
    }

    // ---------- Listan ----------
    void BuildPanel()
    {
        _panel = new GameObject(name + " – lista").transform;
        var titleGo = new GameObject("Rubrik");
        titleGo.transform.SetParent(_panel, false);
        titleGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        _title = titleGo.AddComponent<TextMeshPro>();
        _title.fontSize = 0.3f;
        _title.alignment = TextAlignmentOptions.Bottom;
        _title.rectTransform.sizeDelta = new Vector2(0.6f, 0.1f);
        _title.color = Color.white;
        _title.outlineWidth = 0.2f;
        _title.outlineColor = new Color32(0, 0, 0, 255);
    }

    void RebuildRows()
    {
        _dirty = false;
        foreach (var r in _rows) Destroy(r.go);
        _rows.Clear();

        if (IsClosed)
        {
            _title.text = "<b>Ryggsäcken är packad och stängd!</b>";
            return;
        }

        _title.text = _contents.Count == 0
            ? "<b>Ryggsäcken är tom</b>\nSläpp saker i öppningen för att packa"
            : $"<b>I ryggsäcken ({_contents.Count})</b>\nNyp på en sak för att ta ut den";

        int columns = Mathf.Max(1, Mathf.CeilToInt(_contents.Count / (float)rowsPerColumn));
        float gap = 0.01f;
        float totalWidth = columns * rowWidth + (columns - 1) * gap;

        for (int i = 0; i < _contents.Count; i++)
        {
            var item = _contents[i];
            int col = i / rowsPerColumn;
            int row = i % rowsPerColumn;

            var go = new GameObject("Rad – " + item.displayName);
            go.transform.SetParent(_panel, false);
            go.transform.localPosition = new Vector3(
                -totalWidth / 2f + rowWidth / 2f + col * (rowWidth + gap),
                -rowHeight / 2f - row * (rowHeight + 0.005f),
                0f);

            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(rowWidth, rowHeight, 0.03f);
            var button = go.AddComponent<XRSimpleInteractable>();
            button.selectEntered.AddListener(_ => _pendingRemove = item);

            var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Destroy(bg.GetComponent<Collider>());
            bg.transform.SetParent(go.transform, false);
            bg.transform.localScale = new Vector3(rowWidth, rowHeight, 1f);
            var rend = bg.GetComponent<Renderer>();
            if (panelMaterial != null) rend.sharedMaterial = panelMaterial;

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(go.transform, false);
            textGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
            var text = textGo.AddComponent<TextMeshPro>();
            text.text = item.displayName;
            text.fontSize = 0.2f;
            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.rectTransform.sizeDelta = new Vector2(rowWidth - 0.01f, rowHeight);
            text.color = Color.white;

            _rows.Add(new Row { go = go, item = item, button = button, bg = rend });
        }
    }
}
