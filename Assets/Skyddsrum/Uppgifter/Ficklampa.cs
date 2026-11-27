using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Ficklampa som tänds när man tar tag i den: ljuskägla, ett svagt sken runt omkring
/// och en synlig ljusstråle. Den förblir tänd efter att man släppt den.
/// </summary>
[RequireComponent(typeof(XRGrabInteractable))]
public class Ficklampa : MonoBehaviour
{
    public Light spot;
    [Tooltip("Svagt sken runt lampan så att rummet blir lite ljusare")]
    public Light glow;
    [Tooltip("Synlig ljusstråle")]
    public Renderer beam;
    public Renderer lens;
    public Color lensOnColor = new Color(2.2f, 2.1f, 1.8f);
    public bool startOn = false;
    public AudioSource clickSound;

    public bool IsOn { get; private set; }

    XRGrabInteractable _grab;
    MaterialPropertyBlock _mpb;

    void Awake()
    {
        _grab = GetComponent<XRGrabInteractable>();
        SetOn(startOn);
    }

    void OnEnable() => _grab.selectEntered.AddListener(OnGrab);
    void OnDisable() => _grab.selectEntered.RemoveListener(OnGrab);

    void OnGrab(SelectEnterEventArgs args)
    {
        if (IsOn) return;
        SetOn(true);
        if (clickSound != null) clickSound.Play();
    }

    public void SetOn(bool on)
    {
        IsOn = on;
        if (spot != null) spot.enabled = on;
        if (glow != null) glow.enabled = on;
        if (beam != null) beam.enabled = on;
        if (lens != null)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            lens.GetPropertyBlock(_mpb);
            _mpb.SetColor("_EmissionColor", on ? lensOnColor : Color.black);
            lens.SetPropertyBlock(_mpb);
        }
    }
}
