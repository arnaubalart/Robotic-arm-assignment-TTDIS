using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(PathNode))]
public class InteractiveNode : MonoBehaviour
{
    private Renderer rendGlobal;
    private Color originalColor;
    private Transform originalParent;

    private Collider[] nodeColliders;
    private SphereCollider sphereCol;
    private GameObject visualSphere;

    private MonoBehaviour pathManagerBehaviour;
    private PropertyInfo isRunningProperty;
    private PropertyInfo isSteppingProperty;

    private IK_Solver ikSolver;
    private bool interactionEnabled = true;

    void Start()
    {
        originalParent = transform.parent;

        CachePathManager();
        CacheIKSolver();

        nodeColliders = GetComponents<Collider>();
        foreach (Collider c in nodeColliders)
        {
            if (c != null) c.isTrigger = true;
        }

        sphereCol = GetComponent<SphereCollider>();
        if (sphereCol == null) sphereCol = gameObject.AddComponent<SphereCollider>();

        sphereCol.radius = 0.15f;
        sphereCol.isTrigger = true;

        nodeColliders = GetComponents<Collider>();

        Transform existingVisual = transform.Find("VisualSphere");
        if (existingVisual != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(existingVisual.gameObject);
            else Destroy(existingVisual.gameObject);
#else
            Destroy(existingVisual.gameObject);
#endif
        }

        visualSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        visualSphere.name = "VisualSphere";
        visualSphere.transform.SetParent(transform);
        visualSphere.transform.localPosition = Vector3.zero;
        visualSphere.transform.localRotation = Quaternion.identity;
        visualSphere.transform.localScale = Vector3.one * 0.08f;

        Collider visualCollider = visualSphere.GetComponent<Collider>();
        if (visualCollider != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(visualCollider);
            else Destroy(visualCollider);
#else
            Destroy(visualCollider);
#endif
        }

        rendGlobal = visualSphere.GetComponent<Renderer>();

        PathManipulation pathManip = FindFirstObjectByType<PathManipulation>();
        if (pathManip != null && pathManip.nodes != null)
        {
            int myIndex = pathManip.nodes.IndexOf(GetComponent<PathNode>());
            if (myIndex == 0) rendGlobal.material.color = Color.green;
            else if (myIndex == pathManip.nodes.Count - 1) rendGlobal.material.color = Color.red;
            else rendGlobal.material.color = Color.cyan;
        }
        else
        {
            rendGlobal.material.color = Color.cyan;
        }

        originalColor = rendGlobal.material.color;

        gameObject.layer = 4;
        visualSphere.layer = 4;

        ApplyInteractionState(true);
    }

    void Update()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);

        bool shouldEnableInteraction = !IsRobotBusy();

        if (shouldEnableInteraction != interactionEnabled)
        {
            ApplyInteractionState(shouldEnableInteraction);
        }
    }

    void LateUpdate()
    {
        if (transform.parent != originalParent)
        {
            transform.SetParent(originalParent);
        }
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (rendGlobal != null && interactionEnabled)
        {
            rendGlobal.material.color = isHighlighted ? Color.yellow : originalColor;
        }
    }

    private void ApplyInteractionState(bool enable)
    {
        interactionEnabled = enable;

        if (nodeColliders == null) nodeColliders = GetComponents<Collider>();

        foreach (Collider c in nodeColliders)
        {
            if (c != null) c.enabled = enable;
        }

        if (rendGlobal != null)
        {
            rendGlobal.enabled = enable;
            if (enable)
                rendGlobal.material.color = originalColor;
        }
    }

    private bool IsRobotBusy()
    {
        if (!Application.isPlaying) return false;

        CacheIKSolver();
        if (ikSolver != null && ikSolver.enabled)
            return true;

        if (pathManagerBehaviour == null || isRunningProperty == null)
            CachePathManager();

        if (pathManagerBehaviour == null || isRunningProperty == null)
            return false;

        bool isRunning = false;
        bool isStepping = false;

        object runningValue = isRunningProperty.GetValue(pathManagerBehaviour);
        if (runningValue is bool runningBool) isRunning = runningBool;

        if (isSteppingProperty != null)
        {
            object steppingValue = isSteppingProperty.GetValue(pathManagerBehaviour);
            if (steppingValue is bool steppingBool) isStepping = steppingBool;
        }

        return isRunning || isStepping;
    }

    private void CachePathManager()
    {
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);

        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour == null) continue;

            System.Type type = behaviour.GetType();
            PropertyInfo runningProp = type.GetProperty("IsRunning");
            if (runningProp == null || runningProp.PropertyType != typeof(bool)) continue;

            pathManagerBehaviour = behaviour;
            isRunningProperty = runningProp;
            isSteppingProperty = type.GetProperty("IsStepping");
            return;
        }

        pathManagerBehaviour = null;
        isRunningProperty = null;
        isSteppingProperty = null;
    }

    private void CacheIKSolver()
    {
        if (ikSolver == null)
            ikSolver = FindFirstObjectByType<IK_Solver>();
    }
}