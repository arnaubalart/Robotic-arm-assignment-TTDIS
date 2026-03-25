using UnityEngine;

// Attach this component to any GameObject to make it a waypoint in the robot path.
// When the GameObject moves, the node position updates automatically.
[ExecuteAlways]
public class PathNode : MonoBehaviour
{
    [HideInInspector]
    public Vector3 position;

    [Tooltip("Vertical approach angle in degrees (0 = horizontal, 90 = straight down).")]
    [Range(0f, 90f)]
    public float angleOfAttack = 90f;

    [Header("Suction Cup")]
    [Tooltip("Activate the suction end-effector when the arm reaches this node.")]
    public bool activateSuction = false;

    [Tooltip("Deactivate the suction end-effector when the arm reaches this node.")]
    public bool deactivateSuction = false;

    [Tooltip("Minimum seconds to remain at this node before the arm can advance.")]
    [Min(0f)]
    public float waitSeconds = 0f;

    [Tooltip("Seconds after arriving before the suction action fires. Runs independently of waitSeconds.")]
    [Min(0f)]
    public float suctionDelay = 0f;

    [Header("Attachment")]
    [Tooltip("When set, this node follows the assigned GameObject's position instead of its own transform.")]
    public GameObject attachedTo;

    // Set by PathManager at runtime — true once the arm has settled at this node.
    [HideInInspector] public bool isReached = false;

    private void Update()
    {
        if (attachedTo != null)
            transform.position = attachedTo.transform.position;

        position = transform.position;
    }

    // -------------------------------------------------------------------------
    // Student-facing API helpers
    // -------------------------------------------------------------------------

    public Vector3 GetPosition() => position;

    public float GetAngleOfAttack() => angleOfAttack;

    public bool GetActivateSuction() => activateSuction;

    public bool GetDeactivateSuction() => deactivateSuction;

    public bool HasSuctionAction() => activateSuction || deactivateSuction;

    public float GetWaitTime() => waitSeconds;

    public float GetSuctionDelay() => suctionDelay;

    public GameObject GetAttachedObject() => attachedTo;

    public bool IsReached() => isReached;
}
