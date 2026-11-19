using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// En vattenkran. Greppa ratten (med hand eller kontroll) för att sätta på eller stänga av vattnet.
/// </summary>
public class WaterTap : MonoBehaviour
{
    [Header("Delar")]
    public XRBaseInteractable handle;
    [Tooltip("Det som ska vridas när kranen öppnas")]
    public Transform handleVisual;
    public WaterStream stream;

    [Header("Inställningar")]
    public bool isOn = false;
    [Tooltip("Liter per sekund")]
    public float flowLitersPerSecond = 3f;
    public float handleOpenAngle = 90f;
    public float handleTurnSpeed = 360f;

    [Header("Ljud (valfritt)")]
    public AudioSource waterSound;

    [Header("Händelser")]
    public UnityEvent onTurnedOn;
    public UnityEvent onTurnedOff;

    float _angle;

    void OnEnable()
    {
        if (handle != null) handle.selectEntered.AddListener(OnHandleSelected);
        if (stream != null) stream.SetIgnoreRoot(transform);
    }

    void OnDisable()
    {
        if (handle != null) handle.selectEntered.RemoveListener(OnHandleSelected);
    }

    void OnHandleSelected(SelectEnterEventArgs args) => Toggle();

    public void Toggle() => SetOn(!isOn);

    public void SetOn(bool on)
    {
        if (isOn == on) return;
        isOn = on;
        if (waterSound != null)
        {
            if (on) waterSound.Play(); else waterSound.Stop();
        }
        if (on) onTurnedOn?.Invoke(); else onTurnedOff?.Invoke();
    }

    void Update()
    {
        if (stream != null)
        {
            var basin = stream.Flow(isOn);
            if (basin != null) basin.AddWater(flowLitersPerSecond * Time.deltaTime);
        }

        if (handleVisual != null)
        {
            float target = isOn ? handleOpenAngle : 0f;
            _angle = Mathf.MoveTowards(_angle, target, handleTurnSpeed * Time.deltaTime);
            handleVisual.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }
    }
}
