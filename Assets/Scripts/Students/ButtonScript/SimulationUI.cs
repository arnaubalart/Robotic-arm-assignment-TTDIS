using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SimulationUI : MonoBehaviour
{
    [Header("Robot Settings")]
    public GameObject robotMovement;
    public TextMeshProUGUI buttonStart;

    public void ResetSimulation()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void ExitSimulation()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // Questa funzione viene chiamata dal Bottone UI
    public void StartRobot()
    {
        if (robotMovement == null) return;
        IK_Solver ik = robotMovement.GetComponent<IK_Solver>();
        if (ik == null) return;

        // Se lo script è spento, lo ACCENDIAMO. Se è acceso, lo SPEGNIAMO.
        if (!ik.enabled)
        {
            ActivateRobot(ik);
        }
        else
        {
            DeactivateRobot(ik);
        }
    }

    private void ActivateRobot(IK_Solver ik)
    {
        ik.enabled = true;
        PathManager pathScript = Object.FindFirstObjectByType<PathManager>();

        // UI Update
        buttonStart.text = "Stop";
        if (ColorUtility.TryParseHtmlString("#E74C3C", out Color red)) buttonStart.color = red;

        // Start Path Logic
        if (pathScript != null) pathScript.Play();

        // Start Monitor
        StopAllCoroutines();
        StartCoroutine(CheckWhenFinished());
    }

    private void DeactivateRobot(IK_Solver ik)
    {
        ik.enabled = false;
        PathManager pathScript = Object.FindFirstObjectByType<PathManager>();

        // UI Update
        buttonStart.text = "Start";
        if (ColorUtility.TryParseHtmlString("#2ECC71", out Color green)) buttonStart.color = green;

        // Stop Path Logic
        if (pathScript != null) pathScript.EmergencyStop();

        StopAllCoroutines();
    }

    private IEnumerator CheckWhenFinished()
    {
        PathManager pathScript = Object.FindFirstObjectByType<PathManager>();
        if (pathScript == null) yield break;

        // ASPETTA: Evita che legga "Complete" se il robot è ancora fermo al traguardo precedente
        yield return new WaitForSeconds(0.8f);

        while (true)
        {
            IK_Solver ik = robotMovement.GetComponent<IK_Solver>();
            if (ik == null || !ik.enabled) yield break;

            if (pathScript.IsPathComplete())
            {
                // Tempo per il rilascio ventosa
                yield return new WaitForSeconds(1.5f);

                // Spegniamo tutto usando la funzione dedicata
                DeactivateRobot(ik);
                yield break;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }
}