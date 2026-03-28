using UnityEngine;

public class MouseInteractor : MonoBehaviour
{
    private InteractiveNode hoveredNode;
    private InteractiveNode grabbedNode;
    private float grabZDistance;

    public GameObject bubble;

    private PathManipulation pathManipulation;

    void Start()
    {
        pathManipulation = FindFirstObjectByType<PathManipulation>();
    }

    void Update()
    {
        if (Camera.main == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        int layerMask = 1 << 4;

        if (grabbedNode == null)
        {
            if (Physics.Raycast(ray, out hit, 100f, layerMask, QueryTriggerInteraction.Collide))
            {
                InteractiveNode node = hit.collider.GetComponent<InteractiveNode>();
                if (node != null && node != hoveredNode)
                {
                    if (hoveredNode != null) hoveredNode.SetHighlight(false);
                    hoveredNode = node;
                    hoveredNode.SetHighlight(true);
                }
            }
            else
            {
                if (hoveredNode != null)
                {
                    hoveredNode.SetHighlight(false);
                    hoveredNode = null;
                }
            }
        }

        if (Input.GetMouseButtonDown(0) && hoveredNode != null)
        {
            grabbedNode = hoveredNode;
            grabZDistance = Camera.main.WorldToScreenPoint(grabbedNode.transform.position).z;

            if (bubble != null) bubble.SetActive(true);

            if (pathManipulation != null)
            {
                PathNode node = grabbedNode.GetComponent<PathNode>();
                if (node != null)
                    pathManipulation.SelectNode(node);
            }
        }

        if (Input.GetMouseButton(0) && grabbedNode != null)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = grabZDistance;
            grabbedNode.transform.position = Camera.main.ScreenToWorldPoint(mousePos);
        }

        if (Input.GetMouseButtonUp(0) && grabbedNode != null)
        {
            grabbedNode = null;

            if (bubble != null) bubble.SetActive(false);
        }
    }

    void OnGUI()
    {
        Vector3 mousePos = Input.mousePosition;
        Rect rect = new Rect(mousePos.x - 10, Screen.height - mousePos.y - 10, 20, 20);
        GUI.color = Color.white;
        GUI.Label(rect, "O");
    }
}