using System.Reflection;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class PathVisualizer : MonoBehaviour
{
    private PathManipulation pathManip;
    private LineRenderer line;

    private MonoBehaviour pathManagerBehaviour;
    private PropertyInfo isRunningProperty;
    private PropertyInfo isSteppingProperty;

    private IK_Solver ikSolver;

    void Start()
    {
        pathManip = GetComponent<PathManipulation>();
        line = GetComponent<LineRenderer>();

        line.useWorldSpace = true;
        line.startWidth = 0.008f;
        line.endWidth = 0.008f;

        Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended Premultiply");
        if (shader != null)
            line.material = new Material(shader);

        line.startColor = Color.cyan;
        line.endColor = Color.blue;

        CachePathManager();
        CacheIKSolver();
    }

    void Update()
    {
        if (line == null) return;

        bool robotBusy = IsRobotBusy();

        line.enabled = !robotBusy;

        if (robotBusy) return;

        if (pathManip != null && pathManip.nodes != null && pathManip.nodes.Count > 0)
        {
            line.positionCount = pathManip.nodes.Count;

            for (int i = 0; i < pathManip.nodes.Count; i++)
            {
                if (pathManip.nodes[i] != null)
                    line.SetPosition(i, pathManip.nodes[i].transform.position);
            }
        }
        else
        {
            line.positionCount = 0;
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
        if (runningValue is bool runningBool)
            isRunning = runningBool;

        if (isSteppingProperty != null)
        {
            object steppingValue = isSteppingProperty.GetValue(pathManagerBehaviour);
            if (steppingValue is bool steppingBool)
                isStepping = steppingBool;
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