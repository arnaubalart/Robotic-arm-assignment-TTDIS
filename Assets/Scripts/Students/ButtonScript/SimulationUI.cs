using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimulationUI : MonoBehaviour
{
    [Header("Robot Settings")]
    public GameObject robotMovement;
    public TextMeshProUGUI buttonStart;

    private IK_Solver ikSolver;
    private PathManager pathManager;

    private Quaternion joint1InitialRotation;
    private Quaternion joint2InitialRotation;
    private Quaternion joint3InitialRotation;
    private Quaternion joint5InitialRotation;
    private Quaternion joint6InitialRotation;

    private bool initialPoseCached = false;

    private void Start()
    {
        CacheReferences();
        CacheInitialPose();
        SetStartButtonVisual(false);
    }

    private void CacheReferences()
    {
        if (robotMovement != null && ikSolver == null)
            ikSolver = robotMovement.GetComponent<IK_Solver>();

        if (pathManager == null)
            pathManager = FindFirstObjectByType<PathManager>();
    }

    private void CacheInitialPose()
    {
        if (initialPoseCached) return;
        if (ikSolver == null) return;

        if (ikSolver.joint1Pivot != null)
            joint1InitialRotation = ikSolver.joint1Pivot.transform.localRotation;

        if (ikSolver.joint2Pivot != null)
            joint2InitialRotation = ikSolver.joint2Pivot.transform.localRotation;

        if (ikSolver.joint3Pivot != null)
            joint3InitialRotation = ikSolver.joint3Pivot.transform.localRotation;

        if (ikSolver.joint5Pivot != null)
            joint5InitialRotation = ikSolver.joint5Pivot.transform.localRotation;

        if (ikSolver.joint6Pivot != null)
            joint6InitialRotation = ikSolver.joint6Pivot.transform.localRotation;

        initialPoseCached = true;
    }


    public void ResetSimulation()
    {
        CacheReferences();
        CacheInitialPose();

        if (ikSolver != null)
            ikSolver.enabled = false;

        if (pathManager != null)
            pathManager.ResetPath();

        RestoreInitialPose();

        SetStartButtonVisual(false);
    }

    public void ExitSimulation()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void StartRobot()
    {
        CacheReferences();

        if (ikSolver == null) return;

        if (!ikSolver.enabled)
            ActivateRobot();
        else
            DeactivateRobot();
    }

    private void ActivateRobot()
    {
        if (ikSolver == null) return;

        ikSolver.enabled = true;
        SetStartButtonVisual(true);
    }

    private void DeactivateRobot()
    {
        if (ikSolver != null)
            ikSolver.enabled = false;

        if (pathManager != null)
            pathManager.EmergencyStop();

        SetStartButtonVisual(false);
    }

    private void RestoreInitialPose()
    {
        if (!initialPoseCached || ikSolver == null) return;

        if (ikSolver.joint1Pivot != null)
            ikSolver.joint1Pivot.transform.localRotation = joint1InitialRotation;

        if (ikSolver.joint2Pivot != null)
            ikSolver.joint2Pivot.transform.localRotation = joint2InitialRotation;

        if (ikSolver.joint3Pivot != null)
            ikSolver.joint3Pivot.transform.localRotation = joint3InitialRotation;

        if (ikSolver.joint5Pivot != null)
            ikSolver.joint5Pivot.transform.localRotation = joint5InitialRotation;

        if (ikSolver.joint6Pivot != null)
            ikSolver.joint6Pivot.transform.localRotation = joint6InitialRotation;
    }

    private void SetStartButtonVisual(bool running)
    {
        if (buttonStart == null) return;

        if (running)
        {
            buttonStart.text = "Stop";
            if (ColorUtility.TryParseHtmlString("#E74C3C", out Color red))
                buttonStart.color = red;
        }
        else
        {
            buttonStart.text = "Start";
            if (ColorUtility.TryParseHtmlString("#2ECC71", out Color green))
                buttonStart.color = green;
        }
    }
}