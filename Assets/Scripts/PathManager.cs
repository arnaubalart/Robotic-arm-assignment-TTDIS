using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PathManipulation))]
[ExecuteAlways]
public class PathManager : InternalPathManager
{
    [Tooltip("Student-owned component for defining nodes and feedforward path visuals.")]
    public PathManipulation pathManipulation;

    protected override List<PathNode> PathNodes
    {
        get
        {
            if (pathManipulation != null && pathManipulation.nodes != null)
                return pathManipulation.nodes;

            return base.PathNodes;
        }
    }

    private void Reset()
    {
        EnsurePathManipulationReference();
    }

    private void OnValidate()
    {
        EnsurePathManipulationReference();
        SyncLegacyNodesToPathManipulation();
    }

    private void EnsurePathManipulationReference()
    {
        if (pathManipulation == null)
            pathManipulation = GetComponent<PathManipulation>();
    }

    private void SyncLegacyNodesToPathManipulation()
    {
        if (pathManipulation == null || pathManipulation.nodes == null || pathManipulation.nodes.Count > 0 || nodes == null || nodes.Count == 0)
            return;

        pathManipulation.nodes = new List<PathNode>(nodes);
    }

    private void OnDrawGizmos()
    {
        SuctionGizmoUtility.DrawDetectionRadius(endEffectorTransform, suctionDetectionRadius);
    }
}
