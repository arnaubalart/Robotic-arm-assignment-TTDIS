using UnityEngine;

public class MouseInteractor : MonoBehaviour
{
    private InteractiveNode hoveredNode;
    private InteractiveNode grabbedNode;
    private float grabZDistance;
    public GameObject bubble;
    void Update()
    {
        

        // mouse ray
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // we tell the raycast to only hit objects in the "Interactive" layer (layer 4) and to also consider trigger colliders
        int layerMask = 1 << 4;

        // illumination and hover logic only if we're not currently grabbing a node
        if (grabbedNode == null)
        {
            
            if (Physics.Raycast(ray, out hit, 100f, layerMask, QueryTriggerInteraction.Collide))
            {
                // if we hit something, we check if it's an InteractiveNode and highlight it
                //Debug.Log("🎯 the laser has hitted: " + hit.collider.gameObject.name);

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
                // to remove highlight if we move the mouse away from any node
                if (hoveredNode != null)
                {
                    hoveredNode.SetHighlight(false);
                    hoveredNode = null;
                }
            }
        }

        // grab logic (click)
        if (Input.GetMouseButtonDown(0) && hoveredNode != null)
        {
            grabbedNode = hoveredNode;
            grabZDistance = Camera.main.WorldToScreenPoint(grabbedNode.transform.position).z;
            //Debug.Log("node grabbed");
            //for the bubble to apper when we click on the node
            bubble.SetActive(true);
        }

        // move logic (while holding the click)
        if (Input.GetMouseButton(0) && grabbedNode != null)
        {
            Vector3 mousePos = Input.mousePosition;
            mousePos.z = grabZDistance;
            grabbedNode.transform.position = Camera.main.ScreenToWorldPoint(mousePos);
        }

        // release logic (release click)
        if (Input.GetMouseButtonUp(0) && grabbedNode != null)
        {
            grabbedNode = null;
            //for the bubble to disapper when we click on the node
            bubble.SetActive(false);
            //Debug.Log("node relesed");
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