using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// En sak som kan packas i ryggsäcken. Bestämmer om saken får tas med in i skyddsrummet.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class PackItem : MonoBehaviour
{
    public string displayName = "Sak";
    [Tooltip("Får saken tas med in i skyddsrummet?")]
    public bool allowed = true;
    [Tooltip("Saker med samma kategori räknas som en rätt sak (t.ex. vattenflaska och mugg).")]
    public string category = "";
    [Tooltip("Varför saken inte får följa med (för framtida feedback).")]
    [TextArea] public string whyNot = "";

    [Header("Etikett")]
    public bool showLabel = true;
    public float labelOffset = 0.08f;

    public XRGrabInteractable Grab { get; private set; }
    public Rigidbody Body { get; private set; }
    public string Category => string.IsNullOrEmpty(category) ? displayName : category;
    public bool IsHeld => Grab != null && Grab.isSelected;
    public bool CanBePacked => Time.time >= _noPackUntil;

    float _noPackUntil;
    TextMeshPro _label;
    Renderer[] _renderers;

    public void BlockPacking(float seconds) => _noPackUntil = Time.time + seconds;

    void Awake()
    {
        Grab = GetComponent<XRGrabInteractable>();
        Body = GetComponent<Rigidbody>();
        _renderers = GetComponentsInChildren<Renderer>();

        if (showLabel)
        {
            var go = new GameObject("Etikett");
            go.transform.SetParent(transform, false);
            _label = go.AddComponent<TextMeshPro>();
            _label.text = displayName;
            _label.fontSize = 0.35f;
            _label.fontStyle = FontStyles.Bold;
            _label.alignment = TextAlignmentOptions.Center;
            _label.textWrappingMode = TextWrappingModes.NoWrap;
            _label.rectTransform.sizeDelta = new Vector2(0.8f, 0.08f);
            _label.color = Color.white;
            _label.outlineWidth = 0.25f;
            _label.outlineColor = new Color32(0, 0, 0, 255);
            go.SetActive(false);
        }
    }

    void LateUpdate()
    {
        if (_label == null) return;
        bool show = Grab.isHovered || Grab.isSelected;
        if (_label.gameObject.activeSelf != show) _label.gameObject.SetActive(show);
        if (!show) return;

        // Placera etiketten ovanför saken, oavsett hur den är vriden.
        float top = transform.position.y;
        foreach (var r in _renderers)
            if (r != null && r.gameObject != _label.gameObject) top = Mathf.Max(top, r.bounds.max.y);
        _label.transform.position = new Vector3(transform.position.x, top + labelOffset, transform.position.z);
        ForrumUtil.FaceCamera(_label.transform, Camera.main);
    }
}
