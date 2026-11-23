using TMPro;
using UnityEngine;

/// <summary>
/// Visar korta meddelanden om uppgifter framför spelaren. Texten följer blicken mjukt
/// (bekvämt i VR) och tonar ut efter några sekunder. Anropa TaskMessage.Show(...).
/// </summary>
public class TaskMessage : MonoBehaviour
{
    public float distance = 1.4f;
    public float heightOffset = -0.12f;
    public float followSpeed = 3f;
    public float fadeSeconds = 0.5f;

    static TaskMessage _instance;
    TextMeshPro _text;
    float _shownAt, _duration;
    bool _visible;

    public static void Show(string message, Color color, float seconds = 6f)
    {
        if (_instance == null)
        {
            var go = new GameObject("Uppgiftsmeddelande");
            _instance = go.AddComponent<TaskMessage>();
            _instance.Build();
        }
        _instance.Display(message, color, seconds);
    }

    void Build()
    {
        var textGo = new GameObject("Text");
        textGo.transform.SetParent(transform, false);
        _text = textGo.AddComponent<TextMeshPro>();
        ApplyOverlay(_text);
        _text.fontSize = 0.6f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.textWrappingMode = TextWrappingModes.Normal;
        _text.rectTransform.sizeDelta = new Vector2(1.1f, 0.4f);
        _text.fontStyle = FontStyles.Bold;
        _text.outlineWidth = 0.25f;
        _text.outlineColor = new Color32(0, 0, 0, 255);
        _text.alpha = 0f;
    }

    /// <summary>Gör att texten ritas ovanpå allt (syns även bakom väggar och möbler).</summary>
    public static void ApplyOverlay(TextMeshPro text)
    {
        var mat = Resources.Load<Material>("UppgiftText_Overlay");
        if (mat != null) text.fontSharedMaterial = mat;
    }

    void Display(string message, Color color, float seconds)
    {
        _text.text = message;
        color.a = 1f;
        _text.color = color;
        _text.alpha = 0f;
        _shownAt = Time.time;
        _duration = seconds;
        // Placera direkt framför spelaren första gången den syns
        if (!_visible) SnapToView();
        _visible = true;
    }

    void SnapToView()
    {
        var cam = Camera.main; if (cam == null) return;
        transform.position = TargetPosition(cam);
        ForrumUtil.FaceCamera(transform, cam);
    }

    Vector3 TargetPosition(Camera cam)
    {
        Vector3 fwd = ForrumUtil.Flat(cam.transform.forward);
        return cam.transform.position + fwd * distance + Vector3.up * heightOffset;
    }

    void LateUpdate()
    {
        if (!_visible) return;
        var cam = Camera.main;
        if (cam != null)
        {
            transform.position = Vector3.Lerp(transform.position, TargetPosition(cam), 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            ForrumUtil.FaceCamera(transform, cam);
        }
        float age = Time.time - _shownAt;
        float a = Mathf.Min(Mathf.Clamp01(age / fadeSeconds), Mathf.Clamp01((_duration - age) / fadeSeconds));
        _text.alpha = a;
        if (age >= _duration) { _visible = false; _text.alpha = 0f; }
    }
}
