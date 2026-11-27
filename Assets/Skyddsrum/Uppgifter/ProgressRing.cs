using TMPro;
using UnityEngine;

/// <summary>
/// Förloppscirkel som svävar framför spelaren (som uppgiftstexten) medan man vevar.
/// Fylls medurs och går från rött via gult till grönt. Visar sekunder kvar i mitten.
/// </summary>
public class ProgressRing : MonoBehaviour
{
    public CrankTask task;
    [Tooltip("Material utan ljussättning (URP Unlit)")]
    public Material ringMaterial;

    [Header("Placering (som uppgiftstexten, fast lite lägre)")]
    public float distance = 1.2f;
    public float heightOffset = -0.32f;
    public float followSpeed = 3f;

    [Header("Utseende")]
    public float outerRadius = 0.09f;
    public float innerRadius = 0.068f;
    public Color backgroundColor = new Color(0.12f, 0.12f, 0.12f);
    [Tooltip("Hur länge cirkeln syns efter att man slutat veva")]
    public float lingerSeconds = 4f;
    public float doneShowSeconds = 2.5f;

    Transform _root;
    MeshFilter _fill;
    Material _fillMat;
    TextMeshPro _text;
    float _shownProgress = -1f;
    float _doneAt = -1f;
    bool _visible;

    void Start()
    {
        _root = new GameObject("Förloppscirkel").transform;
        _root.SetParent(transform, false);

        var bg = MakeRing("Bakgrund", 1f, -0.0005f);
        var bgMat = new Material(ringMaterial); SetColor(bgMat, backgroundColor);
        bg.GetComponent<MeshRenderer>().sharedMaterial = bgMat;

        var fillGo = new GameObject("Fyllning");
        fillGo.transform.SetParent(_root, false);
        fillGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        _fill = fillGo.AddComponent<MeshFilter>();
        _fill.sharedMesh = new Mesh { name = "Fyllning" };
        _fillMat = new Material(ringMaterial);
        fillGo.AddComponent<MeshRenderer>().sharedMaterial = _fillMat;

        var textGo = new GameObject("Sekunder kvar");
        textGo.transform.SetParent(_root, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.003f);
        _text = textGo.AddComponent<TextMeshPro>();
        TaskMessage.ApplyOverlay(_text);
        _text.fontSize = 0.38f;
        _text.alignment = TextAlignmentOptions.Center;
        _text.rectTransform.sizeDelta = new Vector2(0.13f, 0.06f);
        _text.fontStyle = FontStyles.Bold;
        _text.color = Color.white;
        _text.outlineWidth = 0.2f;
        _text.outlineColor = new Color32(0, 0, 0, 255);

        _root.gameObject.SetActive(false);
    }

    GameObject MakeRing(string name, float fraction, float z)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_root, false);
        go.transform.localPosition = new Vector3(0f, 0f, z);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = new Mesh { name = name };
        BuildArc(mf.sharedMesh, fraction);
        go.AddComponent<MeshRenderer>();
        return go;
    }

    void BuildArc(Mesh mesh, float fraction)
    {
        mesh.Clear();
        int seg = Mathf.Max(1, Mathf.CeilToInt(72 * Mathf.Clamp01(fraction)));
        if (fraction <= 0.001f) return;
        var v = new Vector3[(seg + 1) * 2];
        var tri = new int[seg * 12];
        for (int i = 0; i <= seg; i++)
        {
            float a = fraction * 2f * Mathf.PI * i / seg; // medurs från toppen
            var dir = new Vector3(Mathf.Sin(a), Mathf.Cos(a), 0f);
            v[i * 2] = dir * innerRadius;
            v[i * 2 + 1] = dir * outerRadius;
        }
        for (int i = 0; i < seg; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3, t = i * 12;
            // båda sidor så att den syns oavsett vridning
            tri[t] = a; tri[t + 1] = b; tri[t + 2] = c; tri[t + 3] = c; tri[t + 4] = b; tri[t + 5] = d;
            tri[t + 6] = a; tri[t + 7] = c; tri[t + 8] = b; tri[t + 9] = c; tri[t + 10] = d; tri[t + 11] = b;
        }
        mesh.vertices = v;
        mesh.triangles = tri;
        mesh.RecalculateBounds();
    }

    static void SetColor(Material m, Color c)
    {
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    static Color ProgressColor(float p)
    {
        Color red = new Color(0.9f, 0.15f, 0.1f), yellow = new Color(1f, 0.85f, 0.1f), green = new Color(0.2f, 0.85f, 0.3f);
        return p < 0.5f ? Color.Lerp(red, yellow, p / 0.5f) : Color.Lerp(yellow, green, (p - 0.5f) / 0.5f);
    }

    void LateUpdate()
    {
        if (task == null || _root == null) return;

        bool show;
        if (task.IsDone)
        {
            if (_doneAt < 0f) _doneAt = Time.time;
            show = Time.time - _doneAt < doneShowSeconds;
        }
        else show = task.IsStarted && task.CrankedSeconds > 0f && Time.time - task.LastCrankTime < lingerSeconds;

        if (show != _visible)
        {
            _visible = show;
            _root.gameObject.SetActive(show);
            if (show) Snap();
        }
        if (!show) return;

        float p = task.IsDone ? 1f : task.Progress01;
        if (Mathf.Abs(p - _shownProgress) > 0.002f)
        {
            _shownProgress = p;
            BuildArc(_fill.sharedMesh, p);
            SetColor(_fillMat, ProgressColor(p));
            float left = task.RemainingSeconds;
            _text.text = task.IsDone ? "Klart!" : Mathf.CeilToInt(left) + " s";
        }

        var cam = Camera.main;
        if (cam != null)
        {
            transform.position = Vector3.Lerp(transform.position, Target(cam), 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            ForrumUtil.FaceCamera(transform, cam);
        }
    }

    Vector3 Target(Camera cam) =>
        cam.transform.position + ForrumUtil.Flat(cam.transform.forward) * distance + Vector3.up * heightOffset;

    void Snap()
    {
        var cam = Camera.main; if (cam == null) return;
        transform.position = Target(cam);
        ForrumUtil.FaceCamera(transform, cam);
    }
}
