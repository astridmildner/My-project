using UnityEngine;

public static class ForrumUtil
{
    /// <summary>Är punkten inuti lådan? Fungerar även om collidern är avstängd.</summary>
    public static bool InBox(BoxCollider box, Vector3 worldPoint)
    {
        if (box == null) return false;
        Vector3 local = box.transform.InverseTransformPoint(worldPoint) - box.center;
        Vector3 half = box.size * 0.5f;
        return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
    }

    public static Vector3 Flat(Vector3 v)
    {
        v.y = 0f;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }

    /// <summary>Vrider ett objekt så att texten syns från kameran (bara runt Y-axeln).</summary>
    public static void FaceCamera(Transform t, Camera cam)
    {
        if (cam == null) return;
        t.rotation = Quaternion.LookRotation(Flat(t.position - cam.transform.position), Vector3.up);
    }
}
