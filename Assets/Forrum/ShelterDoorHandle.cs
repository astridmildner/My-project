using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Ett handtag (vred) på insidan av skyddsrumsdörren. Nyp eller greppa handtaget så
/// trycks det ner (S = stängd) eller lyfts upp (Ö = öppen). Själva reglerna (att dörren
/// måste vara stängd, att båda måste vara nere osv.) sköts av ShelterDoor.
/// </summary>
[RequireComponent(typeof(XRSimpleInteractable))]
public class ShelterDoorHandle : MonoBehaviour
{
    public ShelterDoor door;
    [Tooltip("Hävarmen som vrids (runt sin lokala z-axel)")]
    public Transform lever;
    [Tooltip("Vinkel när handtaget är uppe (Ö = öppen)")]
    public float upAngle = -8f;
    [Tooltip("Vinkel när handtaget är nere (S = stängd)")]
    public float downAngle = 90f;
    [Tooltip("Grader per sekund")]
    public float speed = 260f;
    [Tooltip("Lyser upp lite när handen är nära")]
    public Renderer[] highlight = new Renderer[0];
    public AudioSource sound;

    public bool IsDown { get; private set; }
    public bool IsMoving => !Mathf.Approximately(_angle, Target);
    float Target => IsDown ? downAngle : upAngle;

    XRSimpleInteractable _interactable;
    float _angle;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _interactable = GetComponent<XRSimpleInteractable>();
        _angle = Target;
        Apply();
    }

    void OnEnable()
    {
        _interactable.selectEntered.AddListener(OnSelected);
        _interactable.hoverEntered.AddListener(OnHoverIn);
        _interactable.hoverExited.AddListener(OnHoverOut);
    }

    void OnDisable()
    {
        _interactable.selectEntered.RemoveListener(OnSelected);
        _interactable.hoverEntered.RemoveListener(OnHoverIn);
        _interactable.hoverExited.RemoveListener(OnHoverOut);
    }

    void OnSelected(SelectEnterEventArgs args) => Press();

    /// <summary>Samma sak som att nypa handtaget (används även av testmenyn).</summary>
    public void Press()
    {
        if (door != null) door.HandlePressed(this);
        else SetDown(!IsDown);
    }

    public void SetDown(bool down)
    {
        if (IsDown == down) return;
        IsDown = down;
        if (sound != null) sound.Play();
    }

    void Update()
    {
        if (!IsMoving) return;
        _angle = Mathf.MoveTowards(_angle, Target, speed * Time.deltaTime);
        Apply();
    }

    void Apply()
    {
        if (lever != null) lever.localRotation = Quaternion.Euler(0f, 0f, _angle);
    }

    void OnHoverIn(HoverEnterEventArgs a) => Tint(true);
    void OnHoverOut(HoverExitEventArgs a) { if (!_interactable.isHovered) Tint(false); }

    void Tint(bool on)
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        foreach (var r in highlight)
        {
            if (r == null) continue;
            if (!on) { r.SetPropertyBlock(null); continue; }
            var m = r.sharedMaterial;
            Color c = m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
            _mpb.Clear();
            _mpb.SetColor("_BaseColor", Color.Lerp(c, Color.white, 0.35f));
            r.SetPropertyBlock(_mpb);
        }
    }
}
