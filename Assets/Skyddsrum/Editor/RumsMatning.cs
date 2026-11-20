using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>Hjälpverktyg: mäter stora rummet och listar vad som redan finns där.</summary>
public static class RumsMatning
{
    [MenuItem("Tools/Skyddsrum/Mät stora rummet")]
    public static void Measure()
    {
        Physics.SyncTransforms();
        var sb = new StringBuilder();
        var cam = Camera.main; Transform rig = cam != null ? cam.transform.root : null;
        float floor = 2.1f;
        foreach (var p in new[] { new Vector3(-1.7f, 0f, 0f), new Vector3(-1.7f, 0f, -1.8f), new Vector3(-1.7f, 0f, 1.8f), new Vector3(-3.0f, 0f, 0f) })
        {
            Vector3 c = new Vector3(p.x, floor + 1.0f, p.z);
            string R(Vector3 d) { return Ray(c, d, rig, out var h) ? $"{(d.x != 0 ? h.point.x : h.point.z):F2}({h.collider.name})" : "-"; }
            sb.AppendLine($"Från ({p.x},{p.z}): V={R(Vector3.left)} Ö={R(Vector3.right)} S={R(Vector3.back)} N={R(Vector3.forward)}");
        }
        void B(string n) { var g = GameObject.Find(n); var r = g != null ? g.GetComponent<Renderer>() : null; if (r) sb.AppendLine($"{n}: {r.bounds.min.ToString("F2")}..{r.bounds.max.ToString("F2")}"); }
        B("Golv"); B("Tak");
        var layout = GameObject.Find("Skyddssrum_layout");
        string[] ours = { "Förrum", "Toaletter", "Skyltar", "Teleportytor", "Belysning (dämpad)", "Vatten" };
        sb.AppendLine("--- Övriga objekt (bounds) ---");
        foreach (var root in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
        {
            if (!root.activeInHierarchy) continue;
            if (layout != null && root == layout) continue;
            if (rig != null && root.transform == rig) continue;
            var rs = root.GetComponentsInChildren<Renderer>().Where(r => r.enabled && !(r is ParticleSystemRenderer)).ToArray();
            if (rs.Length == 0) continue;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            sb.AppendLine($"{root.name}{(ours.Contains(root.name) ? " (vårt)" : "")}: {b.min.ToString("F2")}..{b.max.ToString("F2")} ({rs.Length} st)");
            if (root.name == "Interactables" || root.name == "Environment")
                foreach (Transform ch in root.transform)
                {
                    var crs = ch.GetComponentsInChildren<Renderer>(); if (crs.Length == 0 || !ch.gameObject.activeInHierarchy) continue;
                    var cb = crs[0].bounds; foreach (var r in crs) cb.Encapsulate(r.bounds);
                    sb.AppendLine($"   {ch.name}: {cb.min.ToString("F2")}..{cb.max.ToString("F2")}");
                }
        }
        if (layout != null)
        {
            sb.AppendLine("--- Rummodellens delar ---");
            foreach (var r in layout.GetComponentsInChildren<Renderer>(true))
                sb.AppendLine($"{r.name}{(r.gameObject.activeInHierarchy ? "" : " (dold)")}: {r.bounds.min.ToString("F2")}..{r.bounds.max.ToString("F2")} mat={(r.sharedMaterial ? r.sharedMaterial.name : "-")} uv={(r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh ? r.GetComponent<MeshFilter>().sharedMesh.uv.Length : -1)}");
        }
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), sb.ToString());
        Debug.Log("[Mätning] Klar.");
    }

    static bool Ray(Vector3 o, Vector3 d, Transform rig, out RaycastHit hit)
    {
        hit = default; float best = float.MaxValue; bool any = false;
        foreach (var h in Physics.RaycastAll(o, d, 20f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (rig != null && h.transform.IsChildOf(rig)) continue;
            if (h.distance < best) { best = h.distance; hit = h; any = true; }
        }
        return any;
    }
}
