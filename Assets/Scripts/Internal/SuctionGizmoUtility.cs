using UnityEngine;

public static class SuctionGizmoUtility
{
    private static readonly Color DetectionRadiusColor = new Color(1f, 0.85f, 0.2f, 0.9f);
    private static readonly Color DetectionFillColor   = new Color(1f, 0.85f, 0.2f, 0.08f);

    public static void DrawDetectionRadius(Transform tooltipTransform, float radius)
    {
        if (tooltipTransform == null || radius <= 0f) return;

        Vector3 position = tooltipTransform.position;
        float dotRadius = Mathf.Max(radius * 0.08f, 0.004f);

        Gizmos.color = DetectionFillColor;
        Gizmos.DrawSphere(position, radius);

        Gizmos.color = DetectionRadiusColor;
        Gizmos.DrawWireSphere(position, radius);
        Gizmos.DrawSphere(position, dotRadius);
    }
}
