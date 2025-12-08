using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Scenes")]
    [Tooltip("Name of the gameplay scene to load when 'Play' is pressed.")]
    [SerializeField] private string gameSceneName = "GameScene";

    [Header("Credits UI")]
    [Tooltip("Root GameObject for the credits UI (panel, canvas, etc.).")]
    [SerializeField] private GameObject creditsRoot;

    [Tooltip("Objects to disable when credits are shown (buttons, panels, etc.).")]
    [SerializeField] private List<GameObject> objectsToDisableDuringCredits = new();

    [Header("Optional")]
    [Tooltip("Should the cursor be unlocked/visible in this menu?")]
    [SerializeField] private bool manageCursorForMenu = true;

    private readonly List<GameObject> _disabledForCredits = new();
    private bool _creditsVisible = false;

    private void Awake()
    {
        // Ensure credits UI starts hidden
        if (creditsRoot != null)
            creditsRoot.SetActive(false);

        if (manageCursorForMenu)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    // ===================== PUBLIC BUTTON HOOKS =====================

    /// <summary>
    /// Called by the Play button. Loads the game scene.
    /// </summary>
    public void OnPlayButton()
    {
        if (string.IsNullOrEmpty(gameSceneName))
        {
            Debug.LogError("[MainMenuController] Game scene name is empty. Set it in the inspector.");
            return;
        }

        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>
    /// Called by the Quit button. Exits the application (or stops play mode in Editor).
    /// </summary>
    public void OnQuitButton()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    /// <summary>
    /// Called by the Credits button. Shows the credits screen.
    /// </summary>
    public void OnShowCreditsButton()
    {
        if (_creditsVisible)
            return;

        _creditsVisible = true;

        // Show credits root
        if (creditsRoot != null)
            creditsRoot.SetActive(true);

        // Disable configured objects and remember which ones we actually turned off
        _disabledForCredits.Clear();
        foreach (var go in objectsToDisableDuringCredits)
        {
            if (go == null) continue;
            if (!go.activeSelf) continue; // don't touch already inactive objects

            go.SetActive(false);
            _disabledForCredits.Add(go);
        }
    }

    /// <summary>
    /// Called by the Back/Close button on the credits screen.
    /// </summary>
    public void OnHideCreditsButton()
    {
        if (!_creditsVisible)
            return;

        _creditsVisible = false;

        // Hide credits UI
        if (creditsRoot != null)
            creditsRoot.SetActive(false);

        // Re-enable only the things we actually disabled
        foreach (var go in _disabledForCredits)
        {
            if (go != null)
                go.SetActive(true);
        }

        _disabledForCredits.Clear();
    }
}