// Shared path-manager contract used by IK_Solver.
public interface IPathManager
{
    bool IsRunning     { get; }
    bool IsStepping    { get; }   // true while the arm is moving to a manually stepped node
    bool IsReadyToMove { get; }

    PathNode GetCurrentTargetNode();
    int      GetCurrentNodeIndex();
    bool     IsPathComplete();
    void     AdvanceToNextNode();

    // Returns true when the node's dwell and suction actions are complete.
    bool HandleNodeDwell();

    void SetRobotController(RobotController controller);

    // Called by IK_Solver once the arm has settled at the current node.
    void NotifyArmSettled();
}
