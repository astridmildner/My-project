using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

/// <summary>
/// Meny: Tools > Förrum > Sätt upp teleport med handen
/// - Lägger Unitys "Hand Teleport Interactor" på höger hand (handflatan upp, tumme + pekfinger ut, nyp för att teleportera)
/// - Skapar teleportytor: förrummet (alltid på) och skyddsrummet (slås på när dörren öppnas)
/// - Flyttar spelarens startplats till förrummet
/// </summary>
public static class ForrumLocomotion
{
    const string TeleportPrefabPath = "Assets/Samples/XR Interaction Toolkit/3.5.1/Hands Interaction Demo/Prefabs/HandTeleportInteractor.prefab";
    const string GroupName = "Teleportytor";

    [MenuItem("Tools/Förrum/Sätt upp teleport med handen")]
    public static void Setup()
    {
        var log = new StringBuilder("[Förflyttning] ");
        var rig = GameObject.Find("XR Origin Hands (XR Rig)");
        if (rig == null) { Debug.LogError("[Förflyttning] Hittade inte 'XR Origin Hands (XR Rig)' i scenen."); return; }
        var xrOrigin = rig.GetComponent("XROrigin");

        // ---- 1. Teleport på höger hand ----
        var rightHand = FindDeep(rig.transform, "Right Hand");
        if (rightHand == null) log.Append("Hittade inte 'Right Hand'. ");
        else if (FindDeep(rightHand, "Hand Teleport Interactor") != null) log.Append("Handteleport fanns redan. ");
        else
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TeleportPrefabPath);
            var nearFar = rightHand.GetComponentInChildren<NearFarInteractor>(true);
            if (prefab == null) log.Append("Hittade inte HandTeleportInteractor-prefaben. ");
            else
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rightHand);
                inst.name = "Hand Teleport Interactor";
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(inst, "Lägg till handteleport");
                int wired = 0;
                foreach (var mb in inst.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (mb == null) continue;
                    var so = new SerializedObject(mb);
                    var p = so.FindProperty("m_NearFarInteractor");
                    if (p != null && nearFar != null) { p.objectReferenceValue = nearFar; wired++; }
                    var o = so.FindProperty("m_XROrigin");
                    if (o != null)
                    {
                        o.objectReferenceValue = xrOrigin;
                        if (o.objectReferenceValue == null) o.objectReferenceValue = rig.transform; // fältet kan vara en Transform
                        wired++;
                    }
                    so.ApplyModifiedProperties();
                }
                log.Append($"Handteleport tillagd på höger hand ({wired} kopplingar, NearFar={(nearFar != null ? nearFar.name : "saknas")}). ");
            }
        }

        // ---- 2. Teleportytor ----
        GameObject old;
        while ((old = GameObject.Find(GroupName)) != null) Undo.DestroyObjectImmediate(old);
        var golv = GameObject.Find("Golv");
        Bounds b = golv != null && golv.GetComponent<Renderer>() != null
            ? golv.GetComponent<Renderer>().bounds
            : new Bounds(new Vector3(0f, 2.1f, 0f), new Vector3(12f, 0f, 12f));
        float top = b.max.y;

        var group = new GameObject(GroupName);
        Undo.RegisterCreatedObjectUndo(group, "Teleportytor");
        // Förrummet (x 0.70–3.95, z 0.84–2.83)
        Area(group.transform, "Teleport – Förrum", 0.72f, 3.93f, 0.86f, 2.81f, top);
        // Skyddsrummet: stora rummet väster om väggen + rummet nere till höger
        var shelter = new GameObject("Teleport – Skyddsrum (öppnas med dörren)");
        shelter.transform.SetParent(group.transform, false);
        Area(shelter.transform, "Stora rummet", b.min.x + 0.02f, 0.50f, b.min.z + 0.02f, b.max.z - 0.02f, top);
        Area(shelter.transform, "Rummet nere till höger", 0.72f, b.max.x - 0.02f, b.min.z + 0.02f, 0.82f, top);
        log.Append($"Teleportytor skapade (golv y={top:F2}). ");

        // Koppla skyddsrummets yta till dörren
        var door = Object.FindFirstObjectByType<ShelterDoor>();
        if (door != null)
        {
            Undo.RecordObject(door, "Koppla teleport till dörr");
            door.enableWhenOpen = new[] { shelter };
            EditorUtility.SetDirty(door);
            log.Append("Skyddsrummets teleportyta slås på när dörren öppnas. ");
        }
        else log.Append("Ingen dörr hittades – skyddsrummets yta är alltid på. ");

        // ---- 3. Startplats i förrummet, vänd mot packbordet ----
        Undo.RecordObject(rig.transform, "Flytta startplats");
        rig.transform.SetPositionAndRotation(new Vector3(3.2f, top, 2.45f), Quaternion.Euler(0f, -130f, 0f));
        var dog = GameObject.Find("Hund");
        if (dog != null)
        {
            Undo.RecordObject(dog.transform, "Flytta hund");
            dog.transform.SetPositionAndRotation(new Vector3(3.0f, top + 0.01f, 1.1f), Quaternion.LookRotation(Vector3.left));
        }
        log.Append("Startplatsen flyttad till förrummet.");

        EditorSceneManager.MarkSceneDirty(rig.scene);
        Debug.Log(log.ToString());
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), log.ToString());
    }

    // ---- Spärr: man kan inte gå in i skyddsrummet förrän dörren är öppen ----
    [MenuItem("Tools/Förrum/Spärra skyddsrummet tills dörren öppnas")]
    public static void SetupBlockedZones()
    {
        var door = Object.FindFirstObjectByType<ShelterDoor>();
        if (door == null) { Debug.LogError("[Spärr] Hittade ingen dörr."); return; }
        var golv = GameObject.Find("Golv");
        Bounds b = golv != null && golv.GetComponent<Renderer>() != null
            ? golv.GetComponent<Renderer>().bounds
            : new Bounds(new Vector3(0f, 2.1f, 0f), new Vector3(12f, 0f, 12f));
        float y0 = b.min.y - 1f, y1 = b.max.y + 4f;

        var old = door.transform.Find("Spärrzoner");
        if (old != null) Undo.DestroyObjectImmediate(old.gameObject);
        var parent = new GameObject("Spärrzoner").transform;
        Undo.RegisterCreatedObjectUndo(parent.gameObject, "Spärrzoner");
        parent.SetParent(door.transform, false);
        parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        // Västväggens mitt x≈0.60, mellanväggens mitt z≈0.74 (förrummet är x 0.70–3.95, z 0.84–2.83)
        var a = Zone(parent, "Stora rummet", b.min.x - 1f, 0.60f, y0, y1, b.min.z - 1f, b.max.z + 1f);
        var c = Zone(parent, "Rummet nere till höger", 0.60f, b.max.x + 1f, y0, y1, b.min.z - 1f, 0.74f);

        Undo.RecordObject(door, "Spärrzoner");
        door.blockedZones = new[] { a, c };
        door.pushDirections = new[] { Vector3.right, Vector3.forward };
        EditorUtility.SetDirty(door);
        EditorSceneManager.MarkSceneDirty(door.gameObject.scene);
        EditorSceneManager.SaveOpenScenes();
        string msg = "[Spärr] Två spärrzoner skapade och kopplade till dörren (scenen sparad).";
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }

    static BoxCollider Zone(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) / 2f, (y0 + y1) / 2f, (z0 + z1) / 2f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(x1 - x0, y1 - y0, z1 - z0);
        box.isTrigger = true;
        box.enabled = false; // används bara som område
        return box;
    }

    // ---- Felsökning: slå av/på handteleporten ----
    [MenuItem("Tools/Förrum/Felsökning/Stäng av handteleport")]
    public static void DisableHandTeleport() => SetHandTeleport(false);

    [MenuItem("Tools/Förrum/Felsökning/Slå på handteleport")]
    public static void EnableHandTeleport() => SetHandTeleport(true);

    static void SetHandTeleport(bool on)
    {
        var rig = GameObject.Find("XR Origin Hands (XR Rig)");
        var right = rig != null ? FindDeep(rig.transform, "Right Hand") : null;
        Transform tp = null;
        if (right != null) foreach (Transform c in right) if (c.name == "Hand Teleport Interactor") tp = c;
        string msg;
        if (tp == null) msg = "[Förflyttning] Hittade ingen handteleport på höger hand.";
        else
        {
            Undo.RecordObject(tp.gameObject, "Handteleport av/på");
            tp.gameObject.SetActive(on);
            EditorSceneManager.MarkSceneDirty(tp.gameObject.scene);
            EditorSceneManager.SaveOpenScenes();
            msg = "[Förflyttning] Handteleport " + (on ? "PÅ" : "AV") + " (scenen sparad).";
        }
        Debug.Log(msg);
        System.IO.File.WriteAllText(System.IO.Path.Combine(Application.dataPath, "..", "ForrumMatning.txt"), msg);
    }

    static void Area(Transform parent, string name, float x0, float x1, float z0, float z1, float top)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3((x0 + x1) / 2f, top - 0.01f, (z0 + z1) / 2f);
        var box = go.AddComponent<BoxCollider>();
        box.size = new Vector3(Mathf.Abs(x1 - x0), 0.02f, Mathf.Abs(z1 - z0));
        var area = go.AddComponent<TeleportationArea>();
        // Bara teleport-strålen ska kunna träffa ytan (inte vanliga grepp)
        int teleportLayer = InteractionLayerMask.GetMask("Teleport");
        area.interactionLayers = teleportLayer != 0 ? teleportLayer : (1 << 31); // samma lager som handens teleport-stråle
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var r = FindDeep(c, name); if (r != null) return r; }
        return null;
    }
}
