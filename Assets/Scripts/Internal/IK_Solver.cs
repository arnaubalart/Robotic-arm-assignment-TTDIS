using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IK_Solver : MonoBehaviour
{
    public enum Axis { X, Y, Z }

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector — joints
    // ──────────────────────────────────────────────────────────────────────────

    [Header("Joint Pivots")]
    public GameObject joint1Pivot;
    public GameObject joint2Pivot;
    public GameObject joint3Pivot;
    public GameObject joint4Pivot;
    public GameObject joint5Pivot;
    public GameObject joint6Pivot;

    [Header("End-Effector")]
    [Tooltip("The tooltip transform — the point on the tool that should touch the target.")]
    public GameObject tooltip;

    // ──────────────────────────────────────────────────────────────────────────
    // Arm lengths — deduced automatically from pivot positions at Start()
    // ──────────────────────────────────────────────────────────────────────────
    //
    //   L1  Y-distance joint1 → joint2  (shoulder height above base, Y-up)
    //   L2  distance   joint2 → joint3  (upper arm)
    //   L3  distance   joint3 → joint5  (lower arm; joint4 is wrist-roll, no length)
    //   L4  distance   joint5 → tooltip  (tool tip offset from wrist)
    //
    // Values are logged to the Console at startup so you can verify them.

    private float L1, L2, L3, L4;

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector — motion
    // ──────────────────────────────────────────────────────────────────────────

    [Space(10)]
    [Header("Control Settings")]
    [Tooltip("Maximum joint speed in degrees per second.")]
    public float Speed = 20f;

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector — path
    // ──────────────────────────────────────────────────────────────────────────

    [Space(10)]
    [Header("Path Settings")]
    [Tooltip("Assign a path manager component here.")]
    public MonoBehaviour pathManagerObject;

    // Resolved at Start() — works with either manager type.
    private IPathManager pathManager;

    [Tooltip("Angle threshold (degrees) at which the arm is considered to have reached a node.")]
    public float nodeSettleThreshold = 1.0f;

    // ──────────────────────────────────────────────────────────────────────────
    // Inspector — physical robot
    // ──────────────────────────────────────────────────────────────────────────

    [Space(10)]
    [Header("Connection Settings")]
    [Tooltip("Enable to stream joint angles to the physical robot over TCP.")]
    public bool PhysicalConnection = false;
    public string RobotIP   = "192.168.3.11";
    public int    RobotPort = 3920;
    public bool   ResetAndEnableRobot = false;

    [Space(10)]
    [Header("Vacuum Pump Test")]
    [Tooltip("Digital output pin connected to the vacuum pump.")]
    public int VacuumPumpPin = 0;
    [Tooltip("Toggle ON to activate the vacuum pump, OFF to deactivate. Use this to test the connection.")]
    public bool VacuumPumpOn = false;

    // ──────────────────────────────────────────────────────────────────────────
    // Private state
    // ──────────────────────────────────────────────────────────────────────────

    private readonly float[] currentAngles = new float[6];
    private readonly float[] currentSpeeds = new float[6];
    private          float[] targetAngles  = new float[6];
    private          float[] syncSpeeds    = new float[6]; // per-joint speed limits for synchronized arrival

    private readonly float maxSpeed     = 15f;    // deg/s hard ceiling
    private readonly float acceleration =  2f;    // deg/s²

    private IK_Calculator    ikCalculator;
    private RobotController  robotController;

    private bool isSingularityWarning = false;
    private Dictionary<int, float> lastWarningAngles = new Dictionary<int, float>();
    private bool    _lastVacuumPumpState = false;
    private int     _lastSentNodeIndex = -1;
    private bool    _wasRunning = false;
    private float[] _lastSentAngles = new float[6]; // last angles sent to physical robot
    private const float RESEND_THRESHOLD = 0.5f;    // degrees — resend if any joint drifts more than this

    // ──────────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (PhysicalConnection)
            StartCoroutine(StartRobotConnection());

        if (!ValidatePivotObjects()) Quit();

        if (Speed <= 0f)
        {
            Debug.LogError("[IK_Solver] Speed must be greater than 0.");
            Quit();
        }

        pathManager = pathManagerObject as IPathManager;
        if (pathManager == null)
        {
            Debug.LogError("[IK_Solver] pathManagerObject must implement IPathManager.");
            Quit();
        }

        ConfigurePathManagerRuntimeReferences();

        for (int i = 0; i < currentAngles.Length; i++)
        {
            currentAngles[i] = 0f;
            currentSpeeds[i] = 0f;
        }

        DeduceArmLengths();
        ikCalculator = new IK_Calculator(L1, L2, L3);
    }

    private void OnValidate()
    {
        ConfigurePathManagerRuntimeReferences();
    }

    private void OnApplicationQuit()
    {
        if (robotController != null)
            robotController.Disconnect();
    }

    private IEnumerator StartRobotConnection()
    {
        robotController = new RobotController(robotIp: RobotIP, port: RobotPort);
        var task = robotController.ConnectToRobot();
        while (!task.IsCompleted) yield return null;

        // Share the controller with PathManager so it can send suction commands.
        if (robotController.IsConnected() && pathManager != null)
        {
            pathManager.SetRobotController(robotController);

            // Send the start node position immediately after connection.
            _lastSentNodeIndex = -1;
        }
    }

    private void ConfigurePathManagerRuntimeReferences()
    {
        if (!(pathManagerObject is InternalPathManager internalPathManager))
            return;

        if (tooltip != null)
            internalPathManager.endEffectorTransform = tooltip.transform;

        internalPathManager.suctionOutputPin = VacuumPumpPin;

        Renderer stabilizer = FindStabilizerRenderer();
        if (stabilizer != null)
            internalPathManager.stabilizerRenderer = stabilizer;
    }

    private Renderer FindStabilizerRenderer()
    {
        if (tooltip == null)
            return null;

        Renderer[] renderers = tooltip.GetComponentsInChildren<Renderer>(includeInactive: true);
        foreach (Renderer renderer in renderers)
        {
            if (renderer == null)
                continue;

            Transform current = renderer.transform;
            while (current != null)
            {
                if (string.Equals(current.name, "stabilizer", System.StringComparison.OrdinalIgnoreCase))
                    return renderer;

                if (current == tooltip.transform)
                    break;

                current = current.parent;
            }
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Arm-length deduction
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Measures each arm segment from the world positions of the pivot GameObjects
    /// at startup (all joints are at 0° so world distances equal segment lengths).
    ///
    ///   L1  world-Y delta joint1 → joint2   shoulder height (Y-up)
    ///   L2  distance      joint2 → joint3   upper arm
    ///   L3  distance      joint3 → joint5   lower arm  (joint4 = wrist roll, no offset)
    ///   L4  distance      joint5 → tooltip
    /// </summary>
    private void DeduceArmLengths()
    {
        // Shoulder height: world-Y distance from base pivot to shoulder pivot.
        // World Y = up in Unity, regardless of the model's parent rotation.
        L1 = joint2Pivot.transform.position.y - joint1Pivot.transform.position.y;

        // Upper arm: shoulder pivot → elbow pivot.
        L2 = Vector3.Distance(joint2Pivot.transform.position,
                              joint3Pivot.transform.position);

        // Lower arm: elbow pivot → wrist-pitch pivot.
        // Joint 4 is a pure wrist roll (no translational offset in the IK model).
        L3 = Vector3.Distance(joint3Pivot.transform.position,
                              joint5Pivot.transform.position);

        // Tool offset: wrist-pitch pivot → tooltip (the point that must touch the target).
        L4 = tooltip != null
            ? Vector3.Distance(joint5Pivot.transform.position, tooltip.transform.position)
            : Vector3.Distance(joint5Pivot.transform.position, joint6Pivot.transform.position);

        Debug.Log($"[IK_Solver] Deduced arm lengths — " +
                  $"L1 (shoulder height): {L1:F4}  " +
                  $"L2 (upper arm): {L2:F4}  " +
                  $"L3 (lower arm): {L3:F4}  " +
                  $"L4 (tool offset): {L4:F4}");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Update loop
    // ──────────────────────────────────────────────────────────────────────────

    private void Update()
    {
        // 1 ─ Remote-reset the physical robot if requested.
        if (robotController != null && ResetAndEnableRobot)
        {
            robotController.ResetAndEnable();
            ResetAndEnableRobot = false;
        }

        // 1b ─ Vacuum pump test toggle.
        if (robotController != null && VacuumPumpOn != _lastVacuumPumpState)
        {
            robotController.SetDigitalOutput(VacuumPumpPin, VacuumPumpOn);
            Debug.Log($"[IK_Solver] Vacuum pump test: pin {VacuumPumpPin} → {(VacuumPumpOn ? "ON" : "OFF")}");
            _lastVacuumPumpState = VacuumPumpOn;
        }

        if (pathManager == null) return;

        // 2 ─ Wait until tracking is initialized before moving the arm.
        if (!pathManager.IsReadyToMove) return;

        // 3 ─ Fetch the current target node from the path.
        PathNode currentNode = pathManager.GetCurrentTargetNode();
        if (currentNode == null) return;

        // 4 ─ Always drive the arm toward the current node so dragging a node
        //     gives live visual feedback. Dwell and auto-advance are gated
        //     separately below and only fire when IsRunning or IsStepping.
        bool activelyMoving = pathManager.IsRunning || pathManager.IsStepping;

        float attackAngle = currentNode.angleOfAttack;

        // 4 ─ Compute target position relative to the base pivot, in WORLD coordinates.
        //     We subtract the position but do NOT apply the inverse rotation, because
        //     the IK math works in Unity's world frame (Y-up, Z-forward).
        //     The −90° X rotation on the robot parent is handled by using Axis.Z
        //     for joint 1 instead of Axis.Y (model local Z = world Y = up axis).
        Vector3 relativeTarget = currentNode.position - joint1Pivot.transform.position;

        // 4 ─ Compute the wrist position (still world-relative),
        //     accounting for the approach angle and tool offset.
        Vector3 wristTarget = Calculate3DofLocation(relativeTarget, attackAngle);

        // 5 ─ Compute target joint angles for joints 1–3 via IK.
        Vector3 rawAngles = ikCalculator.Calculate(wristTarget);

        // Read current joint angles for singularity check.
        // Joint 1 rotates around local Z (= world Y up, due to −90° X parent).
        // Joints 2+3 rotate around local X (= world X, unaffected by the parent rotation).
        Vector3 currentJointAngles = new Vector3(
            joint1Pivot.transform.localEulerAngles.z,
            joint2Pivot.transform.localEulerAngles.x,
            joint3Pivot.transform.localEulerAngles.x);

        Vector3 safeAngles = CheckOutOfBoundsSingularity(currentJointAngles, rawAngles);

        // 6 ─ Compute per-joint synchronized speed limits so all joints arrive at the
        //     same time. The slowest joint (largest distance) runs at maxSpeed; all
        //     others are scaled down proportionally.
        float[] pendingTargets = new float[]
        {
            safeAngles.x,
            90f - safeAngles.y,
           -safeAngles.z,
            0f,
            attackAngle + safeAngles.y + safeAngles.z,
            0f
        };
        float maxDist = 0f;
        for (int i = 0; i < pendingTargets.Length; i++)
            maxDist = Mathf.Max(maxDist, Mathf.Abs(pendingTargets[i] - currentAngles[i]));
        for (int i = 0; i < syncSpeeds.Length; i++)
        {
            float dist = Mathf.Abs(pendingTargets[i] - currentAngles[i]);
            syncSpeeds[i] = maxDist > 0.001f ? maxSpeed * (dist / maxDist) : maxSpeed;
            syncSpeeds[i] = Mathf.Max(syncSpeeds[i], 0.5f); // never fully stall
        }

        // Drive joints 1–3.
        MoveJoint(joint1Pivot, 0, -179f, 179f,   safeAngles.x,          Axis.Z, syncSpeeds[0]);
        MoveJoint(joint2Pivot, 1,  -80f, 140f,   90f - safeAngles.y,    Axis.X, syncSpeeds[1]);
        MoveJoint(joint3Pivot, 2,  -10f, 140f,  -safeAngles.z,          Axis.X, syncSpeeds[2]);

        // 7 ─ Joint 5 maintains the angle-of-attack (compensates for joints 2+3).
        //     AoA = approach angle measured from horizontal, pointing DOWN.
        //     Total tool pitch from vertical = θ2 + θ3 + θ5 = 90 + AoA.
        //     → θ5 = (90 + AoA) − (90 − gamma) − (−alpha) = AoA + gamma + alpha
        float joint5Angle = attackAngle + safeAngles.y + safeAngles.z;
        MoveJoint(joint5Pivot, 4, -95f, 95f, joint5Angle, Axis.X, syncSpeeds[4]);

        // 8 ─ Handle dwell and auto-advance once the active path has settled at the node.
        //     Gate on activelyMoving so idle dragging never triggers dwell or advance.
        if (activelyMoving && IsArmSettled())
        {
            if (pathManager.HandleNodeDwell())
            {
                if (pathManager.IsRunning && !pathManager.IsPathComplete())
                    pathManager.AdvanceToNextNode();
                else if (pathManager.IsStepping)
                    pathManager.NotifyArmSettled();
            }
        }

        // 9 ─ Send target joint angles to the physical robot whenever the target has
        //     changed meaningfully — covers node advances, tracking reference movement,
        //     and manual node drags in the Scene view.
        int  currentNodeIndex   = pathManager.GetCurrentNodeIndex();
        bool justStartedRunning = pathManager.IsRunning && !_wasRunning;
        _wasRunning = pathManager.IsRunning;
        if (robotController != null)
        {
            bool nodeChanged    = currentNodeIndex != _lastSentNodeIndex;
            bool anglesDrifted  = TargetAnglesDrifted();
            if (nodeChanged || justStartedRunning || anglesDrifted)
            {
                robotController.SendJointCommand(targetAngles, Speed);
                _lastSentNodeIndex = currentNodeIndex;
                System.Array.Copy(targetAngles, _lastSentAngles, 6);
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Path-following helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if any joint's target angle has drifted more than
    /// <see cref="RESEND_THRESHOLD"/> degrees since the last command was sent.
    /// Triggers a resend when the tracking reference moves the target position.
    /// </summary>
    private bool TargetAnglesDrifted()
    {
        for (int i = 0; i < 6; i++)
        {
            if (Mathf.Abs(targetAngles[i] - _lastSentAngles[i]) > RESEND_THRESHOLD)
                return true;
        }
        return false;
    }

    /// <summary>
    /// Returns true when joints 1–3 are within <see cref="nodeSettleThreshold"/>
    /// degrees of their target angles, indicating the arm has settled at the current node.
    /// </summary>
    private bool IsArmSettled()
    {
        return Mathf.Abs(currentAngles[0] - targetAngles[0]) < nodeSettleThreshold
            && Mathf.Abs(currentAngles[1] - targetAngles[1]) < nodeSettleThreshold
            && Mathf.Abs(currentAngles[2] - targetAngles[2]) < nodeSettleThreshold;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // IK geometry helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Computes the wrist position (joint 5) that places the tool tip at
    /// <paramref name="relTarget"/> while approaching at <paramref name="angleOfAttack"/>
    /// degrees above the horizontal.
    ///
    /// All coordinates are world-relative to the base pivot (Y-up, Z-forward).
    ///
    /// The tool offset L4 is backed off along the <b>radial</b> direction in XZ
    /// (not just Z) so the arm works correctly for targets at any yaw angle.
    ///
    ///   rTarget = sqrt(tx² + tz²)                        ← horizontal distance to tip
    ///   backoff = L4 · cos(AoA)                           ← radial retreat in XZ
    ///   lift    = L4 · sin(AoA)                           ← vertical retreat in Y
    ///
    ///   wrist_xz = target_xz · (rTarget − backoff) / rTarget   ← scale XZ inward
    ///   wrist_y  = ty + lift
    /// </summary>
    private Vector3 Calculate3DofLocation(Vector3 relTarget, float angleOfAttack)
    {
        float aoa     = angleOfAttack * Mathf.Deg2Rad;
        float rTarget = Mathf.Sqrt(relTarget.x * relTarget.x +
                                   relTarget.z * relTarget.z);

        float lift    = L4 * Mathf.Sin(aoa);
        float backoff = L4 * Mathf.Cos(aoa);

        // Degenerate case: target is directly above/below the base axis.
        if (rTarget < 0.0001f)
            return new Vector3(0f, relTarget.y + lift, 0f);

        // Scale the XZ vector inward by the radial backoff.
        float scale = (rTarget - backoff) / rTarget;

        return new Vector3(
            relTarget.x * scale,             // XZ scaled inward along radial direction
            relTarget.y + lift,              // wrist lifted above the target tip
            relTarget.z * scale              // XZ scaled inward along radial direction
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Joint motion
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Moves a single joint toward <paramref name="targetAngle"/> using a
    /// smooth acceleration / deceleration profile.
    /// </summary>
    private void MoveJoint(GameObject joint, int index,
                           float minAngle, float maxAngle,
                           float targetAngle, Axis axis, float jointMaxSpeed = -1f)
    {
        targetAngle = ClampAngle(index, targetAngle, minAngle, maxAngle);
        targetAngles[index] = targetAngle;

        float current   = currentAngles[index];
        float speed     = currentSpeeds[index];
        float distance  = Mathf.Abs(targetAngle - current);
        bool  forward   = current < targetAngle;
        float speedCap  = jointMaxSpeed > 0f ? Mathf.Min(jointMaxSpeed, maxSpeed) : maxSpeed;

        float decelDist = CalculateDecelerationDistance(speed, acceleration);

        if (distance < decelDist)
            speed = Mathf.Max(speed - acceleration * Time.deltaTime, 0f);
        else
            speed = Mathf.Min(speed + acceleration * Time.deltaTime, speedCap);

        speed = Mathf.Clamp(speed, 0f, speedCap);

        float step = Mathf.Min(speed * Time.deltaTime, distance);
        current += forward ? step : -step;

        UpdateJointRotation(joint, current, axis);
        currentAngles[index] = current;
        currentSpeeds[index] = speed;
    }

    private void UpdateJointRotation(GameObject joint, float angle, Axis axis)
    {
        Vector3 euler = joint.transform.localRotation.eulerAngles;
        switch (axis)
        {
            case Axis.X: euler = new Vector3(angle, 0f, 0f); break;
            case Axis.Y: euler = new Vector3(0f, angle, 0f); break;
            case Axis.Z: euler = new Vector3(0f, 0f, angle); break;
        }
        joint.transform.localRotation = Quaternion.Euler(euler);
    }

    private float CalculateDecelerationDistance(float speed, float decel)
    {
        if (decel <= 0f) return 0f;
        return (speed * speed) / (2f * decel);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Angle clamping & singularity detection
    // ──────────────────────────────────────────────────────────────────────────

    private float ClampAngle(int index, float angle, float min, float max)
    {
        float clamped = Mathf.Clamp(angle, min, max);
        if (angle < min)
        {
            if (!lastWarningAngles.TryGetValue(index, out float prev) || prev != min)
            {
                Debug.LogWarning($"[Joint {index}] {angle:F1}° below min ({min}°). Clamping.");
                lastWarningAngles[index] = min;
            }
        }
        else if (angle > max)
        {
            if (!lastWarningAngles.TryGetValue(index, out float prev) || prev != max)
            {
                Debug.LogWarning($"[Joint {index}] {angle:F1}° above max ({max}°). Clamping.");
                lastWarningAngles[index] = max;
            }
        }
        else
        {
            lastWarningAngles.Remove(index);
        }
        return clamped;
    }

    private Vector3 CheckOutOfBoundsSingularity(Vector3 current, Vector3 target)
    {
        if (float.IsNaN(target.x) || float.IsNaN(target.y) || float.IsNaN(target.z))
        {
            if (!isSingularityWarning)
                Debug.LogWarning("[IK_Solver] NaN in target angles — retaining current pose.");
            isSingularityWarning = true;
            return current;
        }
        isSingularityWarning = false;
        return target;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Validation helpers
    // ──────────────────────────────────────────────────────────────────────────

    private bool ValidatePivotObjects()
    {
        bool ok = true;
        ok &= validatePivot(joint1Pivot, "joint-1-pivot");
        ok &= validatePivot(joint2Pivot, "joint-2-pivot");
        ok &= validatePivot(joint3Pivot, "joint-3-pivot");
        ok &= validatePivot(joint4Pivot, "joint-4-pivot");
        ok &= validatePivot(joint5Pivot, "joint-5-pivot");
        ok &= validatePivot(joint6Pivot, "joint-6-pivot");
        ok &= validatePivot(tooltip,    "tooltip");
        return ok;
    }

    private bool validatePivot(GameObject obj, string expected)
    {
        if (obj == null)
        {
            Debug.LogError($"[IK_Solver] '{expected}' is not assigned in the Inspector.");
            return false;
        }
        if (obj.name != expected)
        {
            Debug.LogError($"[IK_Solver] Expected '{expected}' but got '{obj.name}'.");
            return false;
        }
        Debug.Log($"[IK_Solver] '{obj.name}' validated.");
        return true;
    }

    private void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}