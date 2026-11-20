using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Skynke framför ett toalettbås. Nyp (eller greppa) skynket en gång så rullas det upp,
/// nyp igen så rullas det ner. Objektets position är där skynket hänger (pinnen).
/// </summary>
[RequireComponent(typeof(BoxCollider))]
[RequireComponent(typeof(XRSimpleInteractable))]
public class ToiletCurtain : MonoBehaviour
{
    [Header("Delar")]
    [Tooltip("Själva tyget (en tunn kub)")]
    public Transform cloth;
    [Tooltip("Den upprullade delen längst ner (en cylinder)")]
    public Transform roll;

    [Header("Mått")]
    public float width = 0.9f;
    public float fullHeight = 1.85f;
    public float rolledHeight = 0.12f;

    [Header("Läge")]
    public bool isOpen = false;
    [Tooltip("Hur snabbt skynket rullas upp/ner (helt varv per sekund)")]
    public float speed = 1.5f;

    [Header("Ljud (valfritt)")]
    public AudioSource sound;

    XRSimpleInteractable _interactable;
    BoxCollider _box;
    float _t;

    void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        _box = GetComponent<BoxCollider>();
        _t = isOpen ? 1f : 0f;
        Apply(_t);
    }

    void OnEnable() => _interactable.selectEntered.AddListener(OnSelected);
    void OnDisable() => _interactable.selectEntered.RemoveListener(OnSelected);

    void OnSelected(SelectEnterEventArgs args) => Toggle();

    public void Toggle()
    {
        isOpen = !isOpen;
        if (sound != null) sound.Play();
    }

    void Update()
    {
        float target = isOpen ? 1f : 0f;
        if (Mathf.Approximately(_t, target)) return;
        _t = Mathf.MoveTowards(_t, target, speed * Time.deltaTime);
        Apply(_t);
    }

    void Apply(float t)
    {
        // Mjuk rörelse i början och slutet
        float e = t * t * (3f - 2f * t);
        float h = Mathf.Lerp(fullHeight, rolledHeight, e);
        if (cloth != null)
        {
            cloth.localScale = new Vector3(width, h, 0.01f);
            cloth.localPosition = new Vector3(0f, -h / 2f, 0f);
        }
        if (roll != null)
        {
            float d = Mathf.Lerp(0.03f, 0.08f, e); // rullen blir tjockare ju mer som rullas upp
            roll.localScale = new Vector3(d, width / 2f, d);
            roll.localPosition = new Vector3(0f, -h, 0f);
        }
        if (_box != null)
        {
            _box.size = new Vector3(width, h + 0.04f, 0.08f);
            _box.center = new Vector3(0f, -h / 2f, 0f);
        }
    }
}
