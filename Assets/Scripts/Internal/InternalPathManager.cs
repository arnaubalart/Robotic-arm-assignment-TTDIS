using System.Collections.Generic;
using UnityEngine;

public abstract class InternalPathManager : MonoBehaviour, IPathManager
{
    [Header("Path Nodes")]
    [Tooltip("Ordered list of PathNode components. Index 0 = start, last = target.")]
    public List<PathNode> nodes = new List<PathNode>();

    [HideInInspector] public Transform endEffectorTransform;
    [HideInInspector] public Transform trackingReference;
    [HideInInspector] public Renderer stabilizerRenderer;
    [HideInInspector] public float suctionDetectionRadius = 0.1f;
    [HideInInspector] public LayerMask suctionGraspMask = ~0;
    [HideInInspector] public int suctionOutputPin = 31;

    protected RobotController robotController;
    protected int _currentNodeIndex;
    protected bool _isRunning;
    protected bool _isStepping;

    private float _dwellTimer = -1f;
    private bool _dwellActionFired;
    private float _postSuctionDelay;
    private float _doutResendTimer;
    private int _pendingSuctionPin = -1;
    private bool _pendingSuctionState;

    private Vector3 _lastTrackingPosition;
    private bool _trackingInitialized;

    private Transform _grabbedObject;
    private Transform _grabbedOriginalParent;
    private bool _suctionActive;

    private Color _stabilizerOriginalColor;

    protected static readonly Color SuctionActiveColor = new Color(0.68f, 0.85f, 0.90f, 1f);
    protected virtual List<PathNode> PathNodes => nodes;

    public virtual bool IsRunning  => _isRunning;
    public virtual bool IsStepping => _isStepping;
    public virtual bool IsReadyToMove => true;

    protected bool DefaultIsReadyToMove()
    {
        return true;
    }

    public virtual PathNode GetCurrentTargetNode()
    {
        return GetDefaultCurrentTargetNode();
    }

    protected PathNode GetDefaultCurrentTargetNode()
    {
        if (PathNodes == null || PathNodes.Count == 0) return null;
        int idx = Mathf.Clamp(_currentNodeIndex, 0, PathNodes.Count - 1);
        return PathNodes[idx];
    }

    public virtual int GetCurrentNodeIndex() => GetDefaultCurrentNodeIndex();

    protected int GetDefaultCurrentNodeIndex()
    {
        return _currentNodeIndex;
    }

    public virtual bool IsPathComplete()
    {
        return IsDefaultPathComplete();
    }

    protected bool IsDefaultPathComplete()
    {
        return PathNodes == null || PathNodes.Count == 0 || _currentNodeIndex >= PathNodes.Count - 1;
    }

    public virtual void AdvanceToNextNode()
    {
        AdvanceToNextNodeDefault();
    }

    protected void AdvanceToNextNodeDefault()
    {
        if (PathNodes == null || _currentNodeIndex >= PathNodes.Count - 1) return;

        if (_currentNodeIndex < PathNodes.Count && PathNodes[_currentNodeIndex] != null)
            PathNodes[_currentNodeIndex].isReached = true;

        _currentNodeIndex++;
        ResetDwellState();

        if (!_isRunning)
            _isStepping = true;
    }

    protected int GetNodeCount()
    {
        return PathNodes?.Count ?? 0;
    }

    protected PathNode GetNodeAt(int index)
    {
        if (PathNodes == null || index < 0 || index >= PathNodes.Count) return null;
        return PathNodes[index];
    }

    public IReadOnlyList<PathNode> GetPathNodes()
    {
        return PathNodes;
    }

    public bool HandleNodeDwell()
    {
        PathNode node = GetCurrentTargetNode();
        if (node == null) return true;

        bool hasAction = node.activateSuction || node.deactivateSuction;

        if (_dwellTimer < 0f)
            _dwellTimer = 0f;

        _dwellTimer += Time.deltaTime;

        if (!_dwellActionFired && hasAction && _dwellTimer >= node.suctionDelay)
        {
            FireSuctionAction(node);
            _dwellActionFired = true;
        }

        if (_dwellActionFired && _postSuctionDelay > 0f)
        {
            _postSuctionDelay -= Time.deltaTime;
            _doutResendTimer -= Time.deltaTime;

            if (_doutResendTimer <= 0f && _pendingSuctionPin >= 0 && robotController != null)
            {
                robotController.SetDigitalOutput(_pendingSuctionPin, _pendingSuctionState);
                _doutResendTimer = 0.3f;
            }
        }

        if (_dwellActionFired && _postSuctionDelay <= 0f)
            _pendingSuctionPin = -1;

        bool waitComplete = _dwellTimer >= node.waitSeconds;
        bool actionComplete = !hasAction || (_dwellActionFired && _postSuctionDelay <= 0f);
        return waitComplete && actionComplete;
    }

    public virtual void SetRobotController(RobotController controller) => robotController = controller;

    protected virtual void Awake()
    {
        if (stabilizerRenderer != null)
            _stabilizerOriginalColor = stabilizerRenderer.sharedMaterial.GetColor("_BaseColor");
    }

    protected virtual void Update()
    {
        SyncTrackingReference();
    }

    protected virtual void Start()
    {
        if (!Application.isPlaying) return;

        ResetPath();
    }

    public bool RemoveNode(int index)
    {
        if (PathNodes == null || index <= 0 || index >= PathNodes.Count - 1)
        {
            Debug.LogWarning("[PathManager] Cannot remove start or end node.");
            return false;
        }

        PathNodes.RemoveAt(index);
        if (index <= _currentNodeIndex) _currentNodeIndex--;
        _currentNodeIndex = Mathf.Clamp(_currentNodeIndex, 0, Mathf.Max(PathNodes.Count - 1, 0));
        return true;
    }

    protected void DrawNodeGizmos()
    {
        SuctionGizmoUtility.DrawDetectionRadius(endEffectorTransform, suctionDetectionRadius);

        if (PathNodes == null) return;

        for (int i = 0; i < PathNodes.Count; i++)
        {
            if (PathNodes[i] == null) continue;

            Gizmos.color = PathNodeColorUtility.GetColor(PathNodes[i], i, PathNodes.Count);
            Gizmos.DrawSphere(PathNodes[i].position, 0.005f);
        }
    }

    public virtual void Play()
    {
        if (_currentNodeIndex <= 1 && PathNodes != null && PathNodes.Count > 0 && !PathNodes[0].isReached)
            _currentNodeIndex = 0;

        _isStepping = false;
        _isRunning  = true;
    }

    public virtual void EmergencyStop()
    {
        _isRunning  = false;
        _isStepping = false;
    }

    public virtual void GoToPreviousNode()
    {
        if (_currentNodeIndex <= 0) return;

        _currentNodeIndex--;
        ResetDwellState();
        _isStepping = true;

        if (_currentNodeIndex < GetNodeCount() && GetNodeAt(_currentNodeIndex) != null)
            GetNodeAt(_currentNodeIndex).isReached = false;
    }

    // Called by IK_Solver once the arm has settled at a manually stepped node.
    public virtual void NotifyArmSettled()
    {
        if (_isStepping)
            _isStepping = false;
    }

    public virtual void ResetPath()
    {
        _isRunning  = false;
        _isStepping = false;
        _currentNodeIndex = 0;
        ResetDwellState();

        if (PathNodes != null)
        {
            foreach (var node in PathNodes)
            {
                if (node != null)
                    node.isReached = false;
            }
        }

        ReleaseObject();
        SetStabilizerColor(_stabilizerOriginalColor);
    }

    private void SyncTrackingReference()
    {
        if (trackingReference == null || !Application.isPlaying || PathNodes == null) return;

        if (!_trackingInitialized)
        {
            _lastTrackingPosition = trackingReference.position;
            _trackingInitialized = true;
            return;
        }

        Vector3 delta = trackingReference.position - _lastTrackingPosition;
        if (delta.sqrMagnitude > 1e-6f)
        {
            foreach (var node in PathNodes)
            {
                if (node != null)
                    node.transform.position += delta;
            }
        }

        _lastTrackingPosition = trackingReference.position;
    }

    private void FireSuctionAction(PathNode node)
    {
        bool hasAction = node.activateSuction || node.deactivateSuction;

        if (node.activateSuction)
        {
            SetStabilizerColor(SuctionActiveColor);
            GrabNearestObject();
            robotController?.SetDigitalOutput(suctionOutputPin, true);
        }

        if (node.deactivateSuction)
        {
            SetStabilizerColor(_stabilizerOriginalColor);
            ReleaseObject();
            robotController?.SetDigitalOutput(suctionOutputPin, false);
        }

        if (hasAction && robotController != null)
        {
            _postSuctionDelay = 2f;
            _doutResendTimer = 0.3f;
            _pendingSuctionPin = suctionOutputPin;
            _pendingSuctionState = node.activateSuction;
        }
    }

    private void GrabNearestObject()
    {
        if (_suctionActive || endEffectorTransform == null) return;

        Vector3 center = endEffectorTransform.position;
        Transform obj = FindNearestGraspableObject(center);

        if (obj == null)
        {
            Debug.LogWarning(
                $"[PathManager] Suction activated but no graspable object found nearby. " +
                $"radius={suctionDetectionRadius:F3}, mask={suctionGraspMask.value}");
            return;
        }

        _grabbedOriginalParent = obj.parent;

        var rb = obj.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        obj.SetParent(endEffectorTransform, worldPositionStays: true);
        _grabbedObject = obj;
        _suctionActive = true;
        Debug.Log($"[PathManager] Grabbed '{obj.name}'.");
    }

    private Transform FindNearestGraspableObject(Vector3 center)
    {
        Transform best = FindNearestGraspableCollider(center);
        if (best != null) return best;

        return FindNearestGraspableRenderer(center);
    }

    private Transform FindNearestGraspableCollider(Vector3 center)
    {
        Collider[] nearby = Physics.OverlapSphere(center, suctionDetectionRadius, suctionGraspMask);

        float bestDist = float.MaxValue;
        Transform best = null;

        foreach (var col in nearby)
        {
            Transform candidate = GetGraspTarget(col.transform);
            if (candidate == null) continue;

            float distance = Vector3.Distance(center, col.ClosestPoint(center));
            if (distance < bestDist)
            {
                bestDist = distance;
                best = candidate;
            }
        }

        return best;
    }

    private Transform FindNearestGraspableRenderer(Vector3 center)
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);

        float bestDist = float.MaxValue;
        Transform best = null;

        foreach (var renderer in renderers)
        {
            if (renderer == null || !renderer.enabled) continue;

            Transform candidate = GetGraspTarget(renderer.transform);
            if (candidate == null) continue;

            Vector3 closestPoint = renderer.bounds.ClosestPoint(center);
            float distance = Vector3.Distance(center, closestPoint);
            if (distance > suctionDetectionRadius) continue;

            if (distance < bestDist)
            {
                bestDist = distance;
                best = candidate;
            }
        }

        return best;
    }

    private Transform GetGraspTarget(Transform candidate)
    {
        if (candidate == null || IsIgnoredGraspTransform(candidate)) return null;

        Transform graspRoot = null;
        Transform current = candidate;
        while (current != null)
        {
            if (IsInLayerMask(current.gameObject.layer, suctionGraspMask))
                graspRoot = current;

            current = current.parent;
        }

        return IsIgnoredGraspTransform(graspRoot) ? null : graspRoot;
    }

    private bool IsIgnoredGraspTransform(Transform candidate)
    {
        if (candidate == null || endEffectorTransform == null) return true;

        Transform robotRoot = GetRobotHierarchyRoot();
        if (candidate == endEffectorTransform) return true;
        if (candidate.IsChildOf(endEffectorTransform)) return true;
        if (robotRoot != null && candidate.IsChildOf(robotRoot)) return true;
        return false;
    }

    private static bool IsInLayerMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private Transform GetRobotHierarchyRoot()
    {
        if (endEffectorTransform == null) return null;

        Transform current = endEffectorTransform;
        while (current != null)
        {
            if (string.Equals(current.name, "robot", System.StringComparison.OrdinalIgnoreCase))
                return current;

            current = current.parent;
        }

        return endEffectorTransform.root;
    }

    private void ReleaseObject()
    {
        if (!_suctionActive || _grabbedObject == null) return;

        var rb = _grabbedObject.GetComponent<Rigidbody>();
        if (rb != null) rb.isKinematic = false;

        _grabbedObject.SetParent(_grabbedOriginalParent, worldPositionStays: true);
        Debug.Log($"[PathManager] Released '{_grabbedObject.name}'.");

        _grabbedObject = null;
        _grabbedOriginalParent = null;
        _suctionActive = false;
    }

    private void SetStabilizerColor(Color color)
    {
        if (stabilizerRenderer == null) return;

        var block = new MaterialPropertyBlock();
        stabilizerRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        stabilizerRenderer.SetPropertyBlock(block);
    }

    private void ResetDwellState()
    {
        _dwellTimer = -1f;
        _dwellActionFired = false;
        _postSuctionDelay = 0f;
        _pendingSuctionPin = -1;
    }
}