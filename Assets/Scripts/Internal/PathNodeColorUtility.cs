using UnityEngine;

public static class PathNodeColorUtility
{
    private static readonly Color StartNodeColor   = Color.green;
    private static readonly Color EndNodeColor     = Color.red;
    private static readonly Color SuctionNodeColor = Color.yellow;
    private static readonly Color NeutralNodeColor = new Color(0.2f, 0.6f, 1f, 1f);

    public static Color GetColor(PathNode node, int nodeIndex, int nodeCount, float alpha = 1f)
    {
        Color color;

        if (nodeIndex == 0)
            color = StartNodeColor;
        else if (nodeIndex == nodeCount - 1)
            color = EndNodeColor;
        else if (HasSuctionAction(node))
            color = SuctionNodeColor;
        else
            color = NeutralNodeColor;

        color.a = alpha;
        return color;
    }

    public static bool HasSuctionAction(PathNode node)
    {
        return node != null && (node.activateSuction || node.deactivateSuction);
    }
}
