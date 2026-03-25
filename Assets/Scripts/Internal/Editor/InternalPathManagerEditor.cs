using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(InternalPathManager), true)]
public class InternalPathManagerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        InternalPathManager pathManager = (InternalPathManager)target;

        DrawScriptField(pathManager);
        DrawConfiguration(pathManager);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        DrawTraversalControls(pathManager);
        EditorGUILayout.Space();
        DrawTraversalStatus(pathManager);
        EditorGUILayout.Space();
        DrawNodeOverview(pathManager);

        if (Application.isPlaying)
            Repaint();
    }

    private void DrawScriptField(MonoBehaviour behaviour)
    {
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Script", MonoScript.FromMonoBehaviour(behaviour), typeof(MonoScript), false);
        }
    }

    private void DrawConfiguration(InternalPathManager pathManager)
    {
        if (pathManager is PathManager runtimePathManager)
        {
            SerializedProperty pathManipulationProperty = serializedObject.FindProperty("pathManipulation");
            if (pathManipulationProperty != null)
            {
                EditorGUILayout.LabelField("Path Manipulation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(pathManipulationProperty);
            }

            if (runtimePathManager.pathManipulation == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a PathManipulation component to edit nodes and add feedforward visuals.",
                    MessageType.Warning);
            }
            else
            {
                int nodeCount = runtimePathManager.pathManipulation.nodes != null
                    ? runtimePathManager.pathManipulation.nodes.Count
                    : 0;

                EditorGUILayout.LabelField("Node Count", nodeCount.ToString());
                EditorGUILayout.HelpBox(
                    "Edit nodes and feedforward visualizations on the PathManipulation component.",
                    MessageType.Info);
            }

            return;
        }

        SerializedProperty nodesProperty = serializedObject.FindProperty("nodes");
        if (nodesProperty != null)
        {
            EditorGUILayout.LabelField("Path Nodes", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(nodesProperty, includeChildren: true);
        }
    }

    private void DrawTraversalControls(InternalPathManager pathManager)
    {
        EditorGUILayout.LabelField("Traversal", EditorStyles.boldLabel);

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to use traversal controls.", MessageType.Info);
        }

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto"))
                InvokeTraversalAction(pathManager.Play);

            if (GUILayout.Button("Emergency Stop"))
                InvokeTraversalAction(pathManager.EmergencyStop);
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous Node"))
                InvokeTraversalAction(pathManager.GoToPreviousNode);

            if (GUILayout.Button("Next Node"))
                InvokeTraversalAction(pathManager.AdvanceToNextNode);
            EditorGUILayout.EndHorizontal();
        }
    }

    private void DrawTraversalStatus(InternalPathManager pathManager)
    {
        EditorGUILayout.LabelField("Traversal Status", EditorStyles.boldLabel);

        string mode = pathManager.IsRunning ? "Auto" : "Manual / stopped";
        EditorGUILayout.LabelField("Mode", mode);

        var pathNodes = pathManager.GetPathNodes();
        if (pathNodes == null || pathNodes.Count == 0)
        {
            EditorGUILayout.LabelField("Current Node", "None");
            EditorGUILayout.LabelField("Path Complete", "Yes");
            return;
        }

        int currentIndex = Mathf.Clamp(pathManager.GetCurrentNodeIndex(), 0, pathNodes.Count - 1);
        EditorGUILayout.LabelField("Current Node", $"[{currentIndex}] {GetNodeTitle(currentIndex, pathNodes.Count)}");
        EditorGUILayout.LabelField("Path Complete", pathManager.IsPathComplete() ? "Yes" : "No");
    }

    private void DrawNodeOverview(InternalPathManager pathManager)
    {
        EditorGUILayout.LabelField("Node Overview", EditorStyles.boldLabel);

        var pathNodes = pathManager.GetPathNodes();
        if (pathNodes == null || pathNodes.Count == 0)
        {
            EditorGUILayout.HelpBox("No nodes configured.", MessageType.Info);
            return;
        }

        int currentIndex = Mathf.Clamp(pathManager.GetCurrentNodeIndex(), 0, pathNodes.Count - 1);

        for (int i = 0; i < pathNodes.Count; i++)
        {
            EditorGUILayout.LabelField(BuildNodeOverviewLine(pathNodes[i], i, pathNodes.Count, currentIndex));
        }
    }

    private string BuildNodeOverviewLine(PathNode node, int index, int nodeCount, int currentIndex)
    {
        string prefix = index == currentIndex ? "-> " : "   ";
        string line = $"{prefix}[{index}] {GetNodeTitle(index, nodeCount)}";

        if (node == null)
            return $"{line} (missing)";

        if (node.activateSuction)
            line += " [suction ON]";

        if (node.deactivateSuction)
            line += " [suction OFF]";

        if (node.waitSeconds > 0f)
            line += $" wait {node.waitSeconds:0.0}s";

        if (node.suctionDelay > 0f)
            line += $" suction delay {node.suctionDelay:0.0}s";

        if (node.isReached)
            line += " [reached]";

        return line;
    }

    private string GetNodeTitle(int index, int nodeCount)
    {
        if (index == 0)
            return "Start";

        if (index == nodeCount - 1)
            return "Target";

        return $"Node {index}";
    }

    private void InvokeTraversalAction(System.Action action)
    {
        action?.Invoke();
        SceneView.RepaintAll();
        Repaint();
    }
}
