using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    private VisualElement _hud;
    private VisualElement _overlayBackground;
    private VisualElement _pauseLabel;
    private VisualElement _menuButtons;
    private VisualElement _confirmDialog;
    private VisualElement _movementIcon;
    private VisualElement _sprintIcon;
    private VisualElement _inspectionVignette;

    private bool _isPaused = false;
    private bool _isMenuOpen = false;
    private string _pendingAction;

    [SerializeField] private InputActionProperty _pauseAction;

    [Header("HUD Icons")]
    [SerializeField] private Sprite _fastPushIcon;
    [SerializeField] private Sprite _precisePushIcon;
    [SerializeField] private Sprite _walkIcon;
    [SerializeField] private Sprite _runIcon;

    void OnEnable()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;

        _hud = root.Q<VisualElement>("HUD");
        _overlayBackground = root.Q<VisualElement>("OverlayBackground");
        _pauseLabel = root.Q<VisualElement>("PauseLabelContainer");
        _menuButtons = root.Q<VisualElement>("MenuButtonsContainer");
        _confirmDialog = root.Q<VisualElement>("ConfirmDialog");
        _movementIcon = root.Q<VisualElement>("MovementIcon");
        _sprintIcon = root.Q<VisualElement>("SprintIcon");
        _inspectionVignette = root.Q<VisualElement>("InspectionVignette");

        SetupHudIconElement(_movementIcon);
        SetupHudIconElement(_sprintIcon);

        root.Q<Button>("RestartButton").clicked += RestartLevel;
        root.Q<Button>("SettingsButton").clicked += OpenSettings;
        root.Q<Button>("ExitMainButton").clicked += () => ShowConfirmation("MainMenu");
        root.Q<Button>("QuitButton").clicked += () => ShowConfirmation("Quit");
        
        root.Q<Button>("ConfirmYes").clicked += ExecuteConfirmedAction;
        root.Q<Button>("ConfirmNo").clicked += () => _confirmDialog.style.display = DisplayStyle.None;

        // Initialize Vignette sprite if available
        var leya = Object.FindAnyObjectByType<LeyasCamera>(FindObjectsInactive.Include);
        // Vignette is now handled by Post Processing, dependency removed.


        // Initialize HUD icon state
        var interaction = Object.FindAnyObjectByType<TimCubeController>();
        if (interaction != null)
        {
            // Direct field access check since we are in the same project
            // Using true/false to force initial class assignment
            SetPreciseMode(false); 
        }

        SetSprintToggle(false);

        _pauseAction.action?.Enable();
    }

    void OnDisable() => _pauseAction.action?.Disable();

    void Update()
    {
        if (_pauseAction.action != null && _pauseAction.action.WasPressedThisFrame())
        {
            HandleInput();
        }
    }

    private void HandleInput()
    {
        if (_confirmDialog.style.display == DisplayStyle.Flex)
        {
            _confirmDialog.style.display = DisplayStyle.None;
            return;
        }

        var control = _pauseAction.action.activeControl;
        bool pressedEscape = control != null && control.name == "escape";
        bool pressedP = control != null && control.name == "p";

        if (pressedP)
        {
            if (_isMenuOpen) return;
            TogglePauseMode(true);
        }
        else if (pressedEscape)
        {
            TogglePauseMode(false);
        }
    }

    private void TogglePauseMode(bool simplePauseOnly)
    {
        _isPaused = !_isPaused;

        if (!_isPaused)
        {
            _overlayBackground.style.display = DisplayStyle.None;
            _hud.style.display = DisplayStyle.Flex;
            _isMenuOpen = false;
            ResumeGame();
        }
        else
        {
            _overlayBackground.style.display = DisplayStyle.Flex;
            _hud.style.display = DisplayStyle.None;
            _isMenuOpen = !simplePauseOnly;

            _pauseLabel.style.display = simplePauseOnly ? DisplayStyle.Flex : DisplayStyle.None;
            _menuButtons.style.display = simplePauseOnly ? DisplayStyle.None : DisplayStyle.Flex;
            
            PauseGame();
        }
    }

    private void PauseGame()
    {
        Time.timeScale = 0;
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
    }

    private void ResumeGame()
    {
        Time.timeScale = 1;
        
        // Keep cursor visible and unlocked at all times
        UnityEngine.Cursor.visible = true;
        UnityEngine.Cursor.lockState = CursorLockMode.None;
    }

    private void RestartLevel()
    {
        ResumeGame();
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    private void OpenSettings() => Debug.Log("Settings menu requested.");

    private void ShowConfirmation(string action)
    {
        _pendingAction = action;
        _confirmDialog.style.display = DisplayStyle.Flex;
    }

    private void ExecuteConfirmedAction()
    {
        ResumeGame();
        if (_pendingAction == "MainMenu") SceneManager.LoadScene("MainMenu");
        else if (_pendingAction == "Quit")
        {
            #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
            #else
            Application.Quit();
            #endif
        }
    }

    public void SetPreciseMode(bool isPrecise)
    {
        if (_movementIcon == null) return;
        ApplyHudIcon(_movementIcon, isPrecise ? _precisePushIcon : _fastPushIcon);
    }

    // Updates sprint toggle icon (walk vs run latched mode).
    public void SetSprintToggle(bool isRunToggled)
    {
        if (_sprintIcon == null) return;
        ApplyHudIcon(_sprintIcon, isRunToggled ? _runIcon : _walkIcon);
    }

    private void SetupHudIconElement(VisualElement iconElement)
    {
        if (iconElement == null) return;
        iconElement.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Contain);
    }

    private void ApplyHudIcon(VisualElement iconElement, Sprite sprite)
    {
        if (iconElement == null || sprite == null) return;
        iconElement.style.backgroundImage = new StyleBackground(sprite);
    }

    public void SetInspectionModeUI(bool active)
    {
        if (_inspectionVignette == null) return;
        _inspectionVignette.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
    }
}
