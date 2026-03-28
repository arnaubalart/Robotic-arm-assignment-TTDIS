using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
[RequireComponent(typeof(PathManager))]
public class PathManipulation : MonoBehaviour
{
    public List<PathNode> nodes = new List<PathNode>();
    public float newNodeYOffset = 0.05f;
    public float labelHeight = 0.03f;

    private PathManager pathManager;
    private PathNode selectedNode;

    private void Awake()
    {
        CacheReferences();
    }

    private void OnEnable()
    {
        CacheReferences();
    }

    private void CacheReferences()
    {
        if (pathManager == null)
            pathManager = GetComponent<PathManager>();
    }

    public void SelectNode(PathNode node)
    {
        selectedNode = node;
    }

    public PathNode GetSelectedNode()
    {
        return selectedNode;
    }

    public void ClearSelection()
    {
        selectedNode = null;
    }

    public void AddNodeButton()
    {
        AddNodeAfterSelected();
    }

    public void DeleteNodeButton()
    {
        DeleteSelectedNode();
    }

    public void AutoButton()
    {
        CacheReferences();
        if (pathManager == null) return;
        pathManager.Play();
    }

    public void RestartButton()
    {
        CacheReferences();
        if (pathManager == null) return;
        pathManager.ResetPath();
        ClearSelection();
    }

    public void AddNodeAfterSelected()
    {
        CacheReferences();

        if (nodes == null)
            nodes = new List<PathNode>();

        if (nodes.Count < 2)
            return;

        int insertIndex;

        if (selectedNode != null)
        {
            insertIndex = nodes.IndexOf(selectedNode);
            insertIndex = insertIndex < 0 ? nodes.Count - 1 : insertIndex + 1;
        }
        else
        {
            insertIndex = nodes.Count - 1;
        }

        insertIndex = Mathf.Clamp(insertIndex, 1, nodes.Count - 1);

        Vector3 newPosition = ComputeNewNodePosition(insertIndex);

        GameObject nodeObject = new GameObject($"Node_{insertIndex}");
        nodeObject.transform.SetParent(transform, worldPositionStays: true);
        nodeObject.transform.position = newPosition;

        PathNode newPathNode = nodeObject.AddComponent<PathNode>();
        newPathNode.angleOfAttack = 90f;
        newPathNode.activateSuction = false;
        newPathNode.deactivateSuction = false;
        newPathNode.waitSeconds = 0f;
        newPathNode.suctionDelay = 0f;

        nodeObject.AddComponent<InteractiveNode>();

        nodes.Insert(insertIndex, newPathNode);
        selectedNode = newPathNode;

        RenumberNodeNames();
    }

    public void DeleteSelectedNode()
    {
        CacheReferences();

        if (selectedNode == null)
            return;

        int index = nodes.IndexOf(selectedNode);
        if (index < 0)
        {
            selectedNode = null;
            return;
        }

        if (index == 0 || index == nodes.Count - 1)
            return;

        PathNode nodeToDelete = selectedNode;
        selectedNode = null;

        bool removed = pathManager != null && pathManager.RemoveNode(index);
        if (!removed)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(nodeToDelete.gameObject);
        else
            Destroy(nodeToDelete.gameObject);
#else
        Destroy(nodeToDelete.gameObject);
#endif

        RenumberNodeNames();
    }

    private Vector3 ComputeNewNodePosition(int insertIndex)
    {
        Vector3 fallback = transform.position + Vector3.up * newNodeYOffset;

        if (nodes == null || nodes.Count == 0)
            return fallback;

        PathNode prev = (insertIndex - 1 >= 0 && insertIndex - 1 < nodes.Count) ? nodes[insertIndex - 1] : null;
        PathNode next = (insertIndex >= 0 && insertIndex < nodes.Count) ? nodes[insertIndex] : null;

        if (prev != null && next != null)
            return Vector3.Lerp(prev.transform.position, next.transform.position, 0.5f);

        if (prev != null)
            return prev.transform.position + Vector3.up * newNodeYOffset;

        if (next != null)
            return next.transform.position + Vector3.up * newNodeYOffset;

        return fallback;
    }

    private void RenumberNodeNames()
    {
        if (nodes == null) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] == null) continue;

            if (i == 0)
                nodes[i].gameObject.name = "StartNode";
            else if (i == nodes.Count - 1)
                nodes[i].gameObject.name = "TargetNode";
            else
                nodes[i].gameObject.name = $"Node_{i}";
        }
    }

    private void OnDrawGizmos()
    {
        if (nodes == null || nodes.Count == 0) return;

        for (int i = 0; i < nodes.Count; i++)
        {
            PathNode node = nodes[i];
            if (node == null) continue;

            Gizmos.color = GetNodeColor(node, i);
            Gizmos.DrawSphere(node.transform.position, 0.01f);

            DrawNodeLabel(node, i);
        }
    }

    private Color GetNodeColor(PathNode node, int index)
    {
        if (index == 0) return Color.green;
        if (index == nodes.Count - 1) return Color.red;
        if (node.activateSuction) return Color.yellow;
        if (node.deactivateSuction) return Color.magenta;
        if (node.waitSeconds > 0f) return new Color(1f, 0.6f, 0.2f, 1f);
        return Color.cyan;
    }

    private void DrawNodeLabel(PathNode node, int index)
    {
#if UNITY_EDITOR
        string title;
        if (index == 0) title = "Start";
        else if (index == nodes.Count - 1) title = "Target";
        else title = $"Node {index}";

        string suctionText = "None";
        if (node.activateSuction) suctionText = "ON";
        if (node.deactivateSuction) suctionText = "OFF";

        string waitText = node.waitSeconds > 0f ? node.waitSeconds.ToString("0.0") + "s" : "0s";
        string label = $"{title}\nAoA {node.angleOfAttack:0}°\nSuction {suctionText}\nWait {waitText}";

        Handles.color = Color.white;
        Handles.Label(node.transform.position + Vector3.up * labelHeight, label);
#endif
    }
}