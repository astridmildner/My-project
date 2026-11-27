using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Meny: Tools > Skyddsrum > Uppgifter > Skapa uppgift: faran över – gå ut till förrummet
/// </summary>
public static class UtgangBuilder
{
    const string SoundPath = "Assets/Skyddsrum/Ljud/Faran_over.mp3";

    [MenuItem("Tools/Skyddsrum/Uppgifter/Skapa uppgift: faran över – gå ut till förrummet (sista)")]
    public static void Create()
    {
        if (EditorApplication.isPlaying) { Log("[Uppgifter] Stoppa Play först."); return; }
        var crankTask = Object.FindFirstObjectByType<CrankTask>(FindObjectsInactive.Include);
        var door = Object.FindFirstObjectByType<ShelterDoor>();
        var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SoundPath);
        if (crankTask == null || door == null) { Log("[Uppgifter] Behöver vevuppgiften och skyddsrumsdörren i scenen."); return; }
        if (clip == null) { Log("[Uppgifter] Hittade inte ljudet " + SoundPath); return; }

        GameObject root = GameObject.Find("Uppgifter");
        if (root == null) { root = new GameObject("Uppgifter"); Undo.RegisterCreatedObjectUndo(root, "Uppgifter"); }
        var old = root.transform.Find("Faran över");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);

        var go = new GameObject("Faran över");
        Undo.RegisterCreatedObjectUndo(go, "Uppgift");
        go.transform.SetParent(root.transform, false);
        go.transform.SetAsLastSibling();
        var task = go.AddComponent<ExitShelterTask>();
        task.title = "Gå ut till förrummet";
        task.door = door;
        task.startAfterTask = crankTask;
        task.startDelaySeconds = 5f;
        task.startWhenDoorOpens = door;
        var water = Object.FindFirstObjectByType<FillBasinsTask>(FindObjectsInactive.Include);
        task.SetTimes(water != null ? water.hintAfterSeconds : 60f, 0f);

        // Signalen – hörs lika starkt överallt
        var a = go.AddComponent<AudioSource>();
        a.clip = clip; a.playOnAwake = false; a.loop = false; a.spatialBlend = 0f; a.volume = 0.8f;
        task.signal = a;

        // Förrummet (golv y=2.10, x 0.70–3.95, z 0.84–2.83)
        var zoneGo = new GameObject("Förrummet (mål)");
        zoneGo.transform.SetParent(go.transform, false);
        zoneGo.transform.SetPositionAndRotation(new Vector3(2.33f, 2.10f + 1.3f, 1.835f), Quaternion.identity);
        var zone = zoneGo.AddComponent<BoxCollider>();
        zone.size = new Vector3(3.25f, 2.6f, 1.99f);
        zone.isTrigger = true; zone.enabled = false;
        task.targetZone = zone;

        // Ledtråd: handtagen lyser
        var glow = new List<Renderer>();
        Vector3 mid = door.transform.position + Vector3.up * 1f; int n = 0;
        if (door.handles != null)
        {
            Vector3 sum = Vector3.zero;
            foreach (var h in door.handles)
            {
                if (h == null) continue;
                glow.AddRange(h.GetComponentsInChildren<Renderer>(true).Where(r => r.GetComponent<TMPro.TextMeshPro>() == null && !r.name.StartsWith("Pil")));
                sum += h.transform.position; n++;
            }
            if (n > 0) mid = sum / n;
        }
        task.glowTargets = glow.ToArray();
        var lightGo = new GameObject("Ledtrådslampa");
        lightGo.transform.SetParent(go.transform, false);
        lightGo.transform.position = mid + door.transform.forward * 0.6f;
        var l = lightGo.AddComponent<Light>();
        l.type = LightType.Point; l.color = new Color(1f, 0.8f, 0.3f); l.range = 1.6f; l.intensity = 1f; l.shadows = LightShadows.None;
        task.hintLight = l;

        EditorSceneManager.MarkSceneDirty(go.scene);
        EditorSceneManager.SaveOpenScenes();
        Selection.activeGameObject = go;
        Log($"[Uppgifter] 'Faran över – gå ut till förrummet' skapad: startar 5 s efter vevuppgiften, ljud {clip.length:F0} s, {glow.Count} delar lyser, ledtråd efter {task.hintAfterSeconds} s, ingen synpåverkan. (scenen sparad)");
    }

    [MenuItem("Tools/Skyddsrum/Uppgifter/Test under Play: faran över nu")]
    static void TestNow()
    {
        if (!EditorApplication.isPlaying) { Log("[Uppgifter] Tryck Play först."); return; }
        var t = Object.FindFirstObjectByType<ExitShelterTask>();
        if (t == null) { Log("[Uppgifter] Uppgiften saknas – skapa den först."); return; }
        var d = Object.FindFirstObjectByType<ShelterDoor>();
        if (d != null) d.DebugSeal();   // dörren stängd och tät, som när man varit inne ett tag
        t.StartTask();
        Log("[Uppgifter] Faran över startad (test).");
    }

    static void Log(string msg)
    {
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }
}
