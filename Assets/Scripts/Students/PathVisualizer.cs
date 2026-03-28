using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PathManipulation))]
public class PathVisualizer : MonoBehaviour
{
    public float lineWidth = 0.005f;
    public float arrowWidth = 0.0012f;
    public float arrowStemLength = 0.09f;
    public float arrowHeadLength = 0.045f;
    public float arrowPositionT = 0.65f;

    public Color lineColorStart = Color.cyan;
    public Color lineColorEnd = Color.blue;
    public Color pathArrowColor = Color.yellow;

    private PathManipulation pathManip;
    private LineRenderer mainLine;
    private readonly List<GameObject> pathArrowObjects = new List<GameObject>();

    private void Start()
    {
        pathManip = GetComponent<PathManipulation>();
        SetupMainLine();
    }

    private void Update()
    {
        if (pathManip == null)
            pathManip = GetComponent<PathManipulation>();

        if (pathManip == null || pathManip.nodes == null)
            return;

        UpdateMainLine();
        UpdatePathArrows();
    }

    private void SetupMainLine()
    {
        if (mainLine == null)
        {
            mainLine = GetComponent<LineRenderer>();
            if (mainLine == null)
                mainLine = gameObject.AddComponent<LineRenderer>();
        }

        mainLine.useWorldSpace = true;
        mainLine.loop = false;
        mainLine.startWidth = lineWidth;
        mainLine.endWidth = lineWidth;
        mainLine.positionCount = 0;
        mainLine.startColor = lineColorStart;
        mainLine.endColor = lineColorEnd;
        mainLine.numCapVertices = 0;
        mainLine.numCornerVertices = 0;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            mainLine.material = new Material(shader);

        gameObject.layer = 2;
    }

    private void UpdateMainLine()
    {
        if (mainLine == null) return;

        if (pathManip.nodes == null || pathManip.nodes.Count == 0)
        {
            mainLine.positionCount = 0;
            return;
        }

        List<Vector3> validPositions = new List<Vector3>();

        for (int i = 0; i < pathManip.nodes.Count; i++)
        {
            if (pathManip.nodes[i] != null)
                validPositions.Add(pathManip.nodes[i].transform.position);
        }

        mainLine.positionCount = validPositions.Count;

        for (int i = 0; i < validPositions.Count; i++)
            mainLine.SetPosition(i, validPositions[i]);
    }

    private void UpdatePathArrows()
    {
        int needed = 0;

        if (pathManip.nodes != null)
        {
            for (int i = 0; i < pathManip.nodes.Count - 1; i++)
            {
                if (pathManip.nodes[i] != null && pathManip.nodes[i + 1] != null)
                    needed++;
            }
        }

        EnsureArrowPool(needed);

        int arrowIndex = 0;

        if (pathManip.nodes == null) return;

        for (int i = 0; i < pathManip.nodes.Count - 1; i++)
        {
            PathNode a = pathManip.nodes[i];
            PathNode b = pathManip.nodes[i + 1];

            if (a == null || b == null) continue;

            Vector3 from = a.transform.position;
            Vector3 to = b.transform.position;
            Vector3 segment = to - from;
            float segmentLength = segment.magnitude;

            if (segmentLength < 0.0001f) continue;

            Vector3 dir = segment / segmentLength;
            Vector3 tip = Vector3.Lerp(from, to, Mathf.Clamp01(arrowPositionT));

            float stemLength = Mathf.Min(arrowStemLength, segmentLength * 0.35f);
            float headLength = Mathf.Min(arrowHeadLength, segmentLength * 0.18f);

            GameObject arrow = pathArrowObjects[arrowIndex];
            arrow.SetActive(true);

            DrawArrow(arrow, tip, dir, stemLength, headLength);

            arrowIndex++;
        }

        for (int i = arrowIndex; i < pathArrowObjects.Count; i++)
            pathArrowObjects[i].SetActive(false);
    }

    private void EnsureArrowPool(int needed)
    {
        while (pathArrowObjects.Count < needed)
        {
            GameObject arrowRoot = new GameObject("PathArrow_" + pathArrowObjects.Count);
            arrowRoot.transform.SetParent(transform, false);
            arrowRoot.layer = 2;

            CreatePart(arrowRoot);
            CreatePart(arrowRoot);
            CreatePart(arrowRoot);

            pathArrowObjects.Add(arrowRoot);
        }
    }

    private void CreatePart(GameObject parent)
    {
        GameObject go = new GameObject("Part");
        go.transform.SetParent(parent.transform, false);
        go.layer = 2;

        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.startWidth = arrowWidth;
        lr.endWidth = arrowWidth;
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
            lr.material = new Material(shader);
    }

    private void DrawArrow(GameObject root, Vector3 tip, Vector3 dir, float stemLength, float headLength)
    {
        LineRenderer stem = root.transform.GetChild(0).GetComponent<LineRenderer>();
        LineRenderer leftPart = root.transform.GetChild(1).GetComponent<LineRenderer>();
        LineRenderer rightPart = root.transform.GetChild(2).GetComponent<LineRenderer>();

        Vector3 tail = tip - dir * stemLength;

        Quaternion look = Quaternion.LookRotation(dir);
        Vector3 left = look * Quaternion.Euler(0f, 180f - 22f, 0f) * Vector3.forward;
        Vector3 right = look * Quaternion.Euler(0f, 180f + 22f, 0f) * Vector3.forward;

        stem.startColor = pathArrowColor;
        stem.endColor = pathArrowColor;
        stem.SetPosition(0, tail);
        stem.SetPosition(1, tip);

        leftPart.startColor = pathArrowColor;
        leftPart.endColor = pathArrowColor;
        leftPart.SetPosition(0, tip);
        leftPart.SetPosition(1, tip + left * headLength);

        rightPart.startColor = pathArrowColor;
        rightPart.endColor = pathArrowColor;
        rightPart.SetPosition(0, tip);
        rightPart.SetPosition(1, tip + right * headLength);
    }
}