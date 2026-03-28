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
    [Header("Path Nodes")]
    [Tooltip("Ordered list of path nodes used by the PathManager.")]
    public List<PathNode> nodes = new List<PathNode>();

    [Header("Node Creation")]
    [Tooltip("Vertical offset used when creating a new node if no midpoint is available.")]
    public float newNodeYOffset = 0.05f;

    private static readonly Color PreviewLineColor = new Color(0.9f, 0.95f, 1f, 0.75f);

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

    // ------------------------------------------------------------
    // UI BUTTON METHODS
    // ------------------------------------------------------------

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

    // ------------------------------------------------------------
    // NODE EDITING
    // ------------------------------------------------------------

    public void AddNodeAfterSelected()
    {
        CacheReferences();

        if (nodes == null)
            nodes = new List<PathNode>();

        if (nodes.Count < 2)
        {
            Debug.LogWarning("[PathManipulation] You need at least Start and Target nodes before adding intermediate nodes.");
            return;
        }

        int insertIndex;

        if (selectedNode != null)
        {
            insertIndex = nodes.IndexOf(selectedNode);
            if (insertIndex < 0)
                insertIndex = nodes.Count - 1;
            else
                insertIndex += 1;
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
        {
            Debug.LogWarning("[PathManipulation] No node selected to delete.");
            return;
        }

        int index = nodes.IndexOf(selectedNode);
        if (index < 0)
        {
            Debug.LogWarning("[PathManipulation] Selected node is not in the path.");
            selectedNode = null;
            return;
        }

        if (index == 0 || index == nodes.Count - 1)
        {
            Debug.LogWarning("[PathManipulation] Cannot delete Start or Target node.");
            return;
        }

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

            Vector3 nodePosition = node.transform.position;

            Gizmos.color = PathNodeColorUtility.GetColor(node, i, nodes.Count);
            Gizmos.DrawSphere(nodePosition, 0.005f);

            if (i >= nodes.Count - 1 || nodes[i + 1] == null) continue;

            Gizmos.color = PreviewLineColor;
            Gizmos.DrawLine(nodePosition, nodes[i + 1].transform.position);
        }

        if (nodes.Count > 0 && nodes[0] != null)
            AddTextHandleToNode(nodes[0], "Start");

        for (int i = 1; i < nodes.Count - 1; i++)
        {
            if (nodes[i] != null)
                AddTextHandleToNode(nodes[i], i.ToString());
        }

        if (nodes.Count > 1 && nodes[nodes.Count - 1] != null)
            AddTextHandleToNode(nodes[nodes.Count - 1], "Target");
    }

    private void AddTextHandleToNode(PathNode node, string label)
    {
#if UNITY_EDITOR
        Handles.Label(node.transform.position + Vector3.up * 0.01f, label);
#endif
    }
}