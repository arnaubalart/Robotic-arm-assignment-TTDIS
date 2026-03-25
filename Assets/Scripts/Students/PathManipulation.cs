using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// ============================================================
//  STUDENT ASSIGNMENT - Path Manipulation
// ============================================================
//
//  This component owns the editable list of path nodes.
//
//  Add your own path-manipulation techniques here, for example:
//    - create new nodes
//    - reorder or remove nodes
//    - add feedforward path previews
//    - visualize likely collisions or unsafe regions
//
//  Extend that visualization to answer questions like:
//    - how will the robot move?
//    - what path will it follow?
//    - where might it collide?
//    - What type of node is this? (start, intermediate, target)
//    - What happens at the node? (dwell, suction, etc.)
// ============================================================
[DisallowMultipleComponent]
[ExecuteAlways]
public class PathManipulation : MonoBehaviour
{
    [Header("Path Nodes")]
    [Tooltip("Ordered list of path nodes used by the PathManager.")]
    public List<PathNode> nodes = new List<PathNode>();

    private static readonly Color PreviewLineColor = new Color(0.9f, 0.95f, 1f, 0.75f);

    private void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            PathNode node = nodes[i];
            if (node == null) continue;

            Vector3 nodePosition = node.transform.position;

            Gizmos.color = PathNodeColorUtility.GetColor(node, i, nodes.Count);
            Gizmos.DrawSphere(nodePosition, 0.005f);

            if (i >= nodes.Count - 1 || nodes[i + 1] == null) continue;

            Gizmos.color = PreviewLineColor;
            Gizmos.DrawLine(nodePosition, nodes[i + 1].transform.position);
        }

        PathNode startNode = nodes[0];
        if (startNode != null) addTextHandleToNode(startNode, "Start");
        
        PathNode node1 = nodes[1];
        if (node1 != null) addTextHandleToNode(node1, "1");

        PathNode node2 = nodes[2];
        if (node2 != null) addTextHandleToNode(node2, "2");

        PathNode targetNode = nodes[nodes.Count - 1];
        if (targetNode != null) addTextHandleToNode(targetNode, "Target");
    }

    private void addTextHandleToNode(PathNode node, string label)
    {
#if UNITY_EDITOR
        Handles.Label(node.transform.position + Vector3.up * 0.01f, label);
#endif
    }
}
