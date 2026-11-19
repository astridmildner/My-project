using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Mäter upp ett rum med strålar och skriver resultatet i Console. (Hjälpverktyg.)</summary>
public static class ForrumProbe
{
    [MenuItem("Tools/Förrum/Mät rummet uppe till höger")]
    public static void Probe() => ProbeAt(new Vector3(2.5f, 0f, 2.6f));

    static bool Ray(Vector3 o, Vector3 d, out RaycastHit hit, Transform rig)
    {
        hit = default;
        float best = float.MaxValue; bool any = false;
        foreach (var h in Physics.RaycastAll(o, d, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (rig != null && h.transform.IsChildOf(rig)) continue;
            if (h.distance < best) { best = h.distance; hit = h; any = true; }
        }
        return any;
    }

    public static void ProbeAt(Vector3 xz)
    {
        Physics.SyncTransforms();
        var cam = Camera.main; Transform rig = cam != null ? cam.transform.root : null;
        var sb = new StringBuilder("[Mät] ");
        float floor = 0f;
        if (Ray(new Vector3(xz.x, 3.6f, xz.z), Vector3.down, out var fh, rig)) floor = fh.point.y;
        float ceil = float.NaN;
        if (Ray(new Vector3(xz.x, floor + 0.5f, xz.z), Vector3.up, out var ch, rig)) ceil = ch.point.y;
        sb.Append($"golv={floor:F2} tak={ceil:F2} ");
        Vector3 c = new Vector3(xz.x, floor + 1.0f, xz.z);
        float xmin = 0, xmax = 0, zmin = 0, zmax = 0;
        if (Ray(c, Vector3.left, out var h1, rig)) xmin = h1.point.x;
        if (Ray(c, Vector3.right, out var h2, rig)) xmax = h2.point.x;
        if (Ray(c, Vector3.back, out var h3, rig)) zmin = h3.point.z;
        if (Ray(c, Vector3.forward, out var h4, rig)) zmax = h4.point.z;
        sb.Append($"x={xmin:F2}..{xmax:F2} z={zmin:F2}..{zmax:F2} ");
        sb.Append($"träff:V={Name(h1)} H={Name(h2)} S={Name(h3)} N={Name(h4)}\n");

        // Leta öppningar längs varje vägg
        sb.Append("Västvägg öppen z: " + Scan(rig, floor, z => new Vector3(xmin + 0.3f, 0, z), Vector3.left, zmin, zmax, 0.6f) + "\n");
        sb.Append("Östvägg öppen z: " + Scan(rig, floor, z => new Vector3(xmax - 0.3f, 0, z), Vector3.right, zmin, zmax, 0.6f) + "\n");
        sb.Append("Sydvägg öppen x: " + Scan(rig, floor, x => new Vector3(x, 0, zmin + 0.3f), Vector3.back, xmin, xmax, 0.6f) + "\n");
        sb.Append("Nordvägg öppen x: " + Scan(rig, floor, x => new Vector3(x, 0, zmax - 0.3f), Vector3.forward, xmin, xmax, 0.6f));
        sb.Append("\n");
        foreach (var n in new[] { "Dörr_V1_vid_rum", "Dörrkarm_1", "Dörrkarm_2", "Fredsdörr_1_ingång", "Vägg_V1_3", "Vägg_V1_2", "Vägg_V1_1" })
        {
            var go = GameObject.Find(n);
            if (go == null) { sb.Append($"{n}: saknas\n"); continue; }
            var r = go.GetComponent<Renderer>();
            var b = r != null ? r.bounds : new Bounds(go.transform.position, Vector3.zero);
            sb.Append($"{n}: min={b.min.ToString("F2")} max={b.max.ToString("F2")} pivot={go.transform.position.ToString("F2")} rot={go.transform.eulerAngles.ToString("F0")} aktiv={go.activeInHierarchy}\n");
        }
        Debug.Log(sb.ToString());
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), sb.ToString());
    }

    static string Name(RaycastHit h) => h.collider != null ? h.collider.name : "-";

    static string Scan(Transform rig, float floor, System.Func<float, Vector3> at, Vector3 dir, float from, float to, float limit)
    {
        var sb = new StringBuilder();
        bool open = false; float start = 0;
        for (float t = from + 0.05f; t <= to - 0.05f; t += 0.05f)
        {
            Vector3 o = at(t); o.y = floor + 1.0f;
            bool isOpen = !Ray(o, dir, out var h, rig) || h.distance > limit;
            if (isOpen && !open) { open = true; start = t; }
            if (!isOpen && open) { open = false; sb.Append($"[{start:F2}-{t:F2}] "); }
        }
        if (open) sb.Append($"[{start:F2}-{to:F2}] ");
        return sb.Length == 0 ? "inga" : sb.ToString();
    }
}
