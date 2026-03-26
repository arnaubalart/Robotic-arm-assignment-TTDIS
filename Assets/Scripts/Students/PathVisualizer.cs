using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathVisualizer : MonoBehaviour
{
    private PathManipulation pathManip;
    private LineRenderer line;

    void Start()
    {
        pathManip = GetComponent<PathManipulation>();
        line = GetComponent<LineRenderer>();

        // FORZA LO SPAZIO GLOBALE (Risolve il problema del lenzuolo gigante)
        line.useWorldSpace = true;

        // Spessore sottile ed elegante
        line.startWidth = 0.008f;
        line.endWidth = 0.008f;

        // Usa un materiale base pre-incluso in Unity per evitare il fucsia
        line.material = new Material(Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply"));
        line.startColor = Color.cyan;
        line.endColor = Color.blue;
    }

    void Update()
    {
        if (pathManip != null && pathManip.nodes != null && pathManip.nodes.Count > 0)
        {
            line.positionCount = pathManip.nodes.Count;
            for (int i = 0; i < pathManip.nodes.Count; i++)
            {
                if (pathManip.nodes[i] != null)
                {
                    line.SetPosition(i, pathManip.nodes[i].transform.position);
                }
            }
        }
    }
}