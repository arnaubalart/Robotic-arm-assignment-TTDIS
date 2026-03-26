using UnityEngine;

[RequireComponent(typeof(PathNode))]
public class InteractiveNode : MonoBehaviour
{
    private Renderer rendGlobal;
    private Color originalColor;

    void Start()
    {
        SphereCollider col = gameObject.GetComponent<SphereCollider>();
        if (col == null) col = gameObject.AddComponent<SphereCollider>();
        col.radius = 0.15f;
        col.isTrigger = true;

        foreach (Transform child in transform)
        {
            if (child.name == "VisualSphere") Destroy(child.gameObject);
        }

        GameObject visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualSphere.name = "VisualSphere";
        visualSphere.transform.SetParent(this.transform);
        visualSphere.transform.localPosition = Vector3.zero;
        visualSphere.transform.localScale = Vector3.one * 0.08f;
        Destroy(visualSphere.GetComponent<Collider>());

        rendGlobal = visualSphere.GetComponent<Renderer>();
        PathManipulation pathManip = FindFirstObjectByType<PathManipulation>();

        if (pathManip != null && pathManip.nodes != null)
        {
            int myIndex = pathManip.nodes.IndexOf(GetComponent<PathNode>());
            if (myIndex == 0) rendGlobal.material.color = Color.green;
            else if (myIndex == pathManip.nodes.Count - 1) rendGlobal.material.color = Color.red;
            else rendGlobal.material.color = Color.cyan;
        }

        originalColor = rendGlobal.material.color;

        // Layer 4 (Water) per farsi ignorare dalla ventosa ma NON dal nostro nuovo raggio!
        gameObject.layer = 4;
        visualSphere.layer = 4;

        if (pathManip != null)
        {
            InternalPathManager ipm = pathManip.GetComponent<InternalPathManager>();
            if (ipm != null) ipm.suctionGraspMask &= ~(1 << 4);
        }
    }

    // NUOVI COMANDI PULITI
    public void SetHighlight(bool isHighlighted)
    {
        if (rendGlobal != null)
        {
            rendGlobal.material.color = isHighlighted ? Color.yellow : originalColor;
        }
    }
}