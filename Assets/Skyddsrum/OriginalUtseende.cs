using System.Collections.Generic;
using UnityEngine;

/// <summary>Sparar rummodellens ursprungliga mesher och material så att de kan återställas.</summary>
public class OriginalUtseende : MonoBehaviour
{
    public List<MeshRenderer> renderers = new List<MeshRenderer>();
    public List<Mesh> meshes = new List<Mesh>();
    public List<Material> materials = new List<Material>();
}
