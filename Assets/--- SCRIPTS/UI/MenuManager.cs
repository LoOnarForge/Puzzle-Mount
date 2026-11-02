using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    private VisualElement _uiRoot;
    private VisualElement _hud;
    private VisualElement _overlayBackground;
    private VisualElement _pauseLabel;
    private VisualElement _menuButtons;
    private VisualElement _confirmDialog;
    private VisualElement _movementIcon;
    private VisualElement _sprintIcon;
    private VisualElement _inspectionVignette;
    private VisualElement _settingsPanel;
    private Toggle[] _resolutionPresetToggles;
    private bool _suppressResolutionToggleCallback;

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
        _uiRoot = GetComponent<UIDocument>().rootVisualElement;

        _hud = _uiRoot.Q<VisualElement>("HUD");
        _overlayBackground = _uiRoot.Q<VisualElement>("OverlayBackground");
        _pauseLabel = _uiRoot.Q<VisualElement>("PauseLabelContainer");
        _menuButtons = _uiRoot.Q<VisualElement>("MenuButtonsContainer");
        _confirmDialog = _uiRoot.Q<VisualElement>("ConfirmDialog");
        _movementIcon = _uiRoot.Q<VisualElement>("MovementIcon");
        _sprintIcon = _uiRoot.Q<VisualElement>("SprintIcon");
        _inspectionVignette = _uiRoot.Q<VisualElement>("InspectionVignette");
        _settingsPanel = _uiRoot.Q<VisualElement>("SettingsPanel");

        SetupHudIconElement(_movementIcon);
        SetupGraphicsSettings(_uiRoot);
        SetupHudIconElement(_sprintIcon);

        _uiRoot.Q<Button>("RestartButton").clicked += RestartLevel;
        _uiRoot.Q<Button>("SettingsButton").clicked += OpenSettings;
        _uiRoot.Q<Button>("ExitMainButton").clicked += () => ShowConfirmation("MainMenu");
        _uiRoot.Q<Button>("QuitButton").clicked += () => ShowConfirmation("Quit");
        
        _uiRoot.Q<Button>("ConfirmYes").clicked += ExecuteConfirmedAction;
        _uiRoot.Q<Button>("ConfirmNo").clicked += () => _confirmDialog.style.display = DisplayStyle.None;

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
            if (_settingsPanel != null && _settingsPanel.style.display == DisplayStyle.Flex)
            {
                CloseSettings();
                return;
            }

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
            if (_settingsPanel != null)
                _settingsPanel.style.display = DisplayStyle.None;
            ResumeGame();
        }
        else
        {
            _overlayBackground.style.display = DisplayStyle.Flex;
            _hud.style.display = DisplayStyle.None;
            _isMenuOpen = !simplePauseOnly;

            _pauseLabel.style.display = simplePauseOnly ? DisplayStyle.Flex : DisplayStyle.None;
            _menuButtons.style.display = simplePauseOnly ? DisplayStyle.None : DisplayStyle.Flex;
            if (_settingsPanel != null)
                _settingsPanel.style.display = DisplayStyle.None;
            
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

    private void OpenSettings()
    {
        if (_settingsPanel == null || _menuButtons == null)
            return;

        _menuButtons.style.display = DisplayStyle.None;
        _settingsPanel.style.display = DisplayStyle.Flex;
        SyncGraphicsSettingsUi();
    }

    private void CloseSettings()
    {
        if (_settingsPanel == null || _menuButtons == null)
            return;

        _settingsPanel.style.display = DisplayStyle.None;
        _menuButtons.style.display = DisplayStyle.Flex;
    }

    private void SetupGraphicsSettings(VisualElement root)
    {
        RuntimeGraphicsSettings.EnsureInitialized();

        root.Q<Toggle>("ShadowsToggle")?.RegisterValueChangedCallback(evt =>
            RuntimeGraphicsSettings.SetShadowsEnabled(evt.newValue));

        Label shadowDistanceLabel = root.Q<Label>("ShadowDistanceLabel");
        Slider shadowDistanceSlider = root.Q<Slider>("ShadowDistanceSlider");
        if (shadowDistanceSlider != null)
        {
            shadowDistanceSlider.lowValue = RuntimeGraphicsSettings.ShadowDistanceMin;
            shadowDistanceSlider.highValue = RuntimeGraphicsSettings.ShadowDistanceMax;
            shadowDistanceSlider.RegisterValueChangedCallback(evt =>
            {
                RuntimeGraphicsSettings.SetShadowDistance(evt.newValue);
                if (shadowDistanceLabel != null)
                    shadowDistanceLabel.text = $"Shadow distance: {evt.newValue:0}";
            });
        }

        Label renderScaleLabel = root.Q<Label>("RenderScaleLabel");
        Slider renderScaleSlider = root.Q<Slider>("RenderScaleSlider");
        if (renderScaleSlider != null)
        {
            renderScaleSlider.RegisterValueChangedCallback(evt =>
            {
                float scale = evt.newValue / 100f;
                RuntimeGraphicsSettings.SetRenderScale(scale);
                if (renderScaleLabel != null)
                    renderScaleLabel.text = $"Render scale: {evt.newValue:0}%";
            });
        }

        _resolutionPresetToggles = new Toggle[RuntimeGraphicsSettings.ResolutionPresetCount];
        for (int i = 0; i < _resolutionPresetToggles.Length; i++)
        {
            Toggle toggle = root.Q<Toggle>($"ResolutionPreset{i}");
            _resolutionPresetToggles[i] = toggle;
            if (toggle == null)
                continue;

            toggle.text = RuntimeGraphicsSettings.GetResolutionPresetLabel(i);
            int presetIndex = i;
            toggle.RegisterValueChangedCallback(evt => OnResolutionPresetToggleChanged(presetIndex, evt.newValue));
        }

        root.Q<Button>("SettingsBackButton")?.RegisterCallback<ClickEvent>(_ => CloseSettings());
        SyncGraphicsSettingsUi();
    }

    private void SyncGraphicsSettingsUi()
    {
        if (_uiRoot == null)
            return;

        SetToggleIfFound(_uiRoot, "ShadowsToggle", RuntimeGraphicsSettings.ShadowsEnabled);

        Slider shadowDistanceSlider = _uiRoot.Q<Slider>("ShadowDistanceSlider");
        if (shadowDistanceSlider != null)
            shadowDistanceSlider.SetValueWithoutNotify(RuntimeGraphicsSettings.ShadowDistance);

        Label shadowDistanceLabel = _uiRoot.Q<Label>("ShadowDistanceLabel");
        if (shadowDistanceLabel != null)
            shadowDistanceLabel.text = $"Shadow distance: {RuntimeGraphicsSettings.ShadowDistance:0}";

        Slider renderScaleSlider = _uiRoot.Q<Slider>("RenderScaleSlider");
        if (renderScaleSlider != null)
            renderScaleSlider.SetValueWithoutNotify(RuntimeGraphicsSettings.RenderScale * 100f);

        Label renderScaleLabel = _uiRoot.Q<Label>("RenderScaleLabel");
        if (renderScaleLabel != null)
            renderScaleLabel.text = $"Render scale: {RuntimeGraphicsSettings.RenderScale * 100f:0}%";

        _suppressResolutionToggleCallback = true;
        if (_resolutionPresetToggles != null)
        {
            for (int i = 0; i < _resolutionPresetToggles.Length; i++)
            {
                Toggle toggle = _resolutionPresetToggles[i];
                if (toggle == null)
                    continue;

                toggle.text = RuntimeGraphicsSettings.GetResolutionPresetLabel(i);
                toggle.SetValueWithoutNotify(i == RuntimeGraphicsSettings.ActiveResolutionPreset);
            }
        }
        _suppressResolutionToggleCallback = false;
    }

    private static void SetToggleIfFound(VisualElement searchRoot, string toggleName, bool value)
    {
        Toggle toggle = searchRoot.Q<Toggle>(toggleName);
        if (toggle != null)
            toggle.SetValueWithoutNotify(value);
    }

    private void OnResolutionPresetToggleChanged(int presetIndex, bool isOn)
    {
        if (_suppressResolutionToggleCallback)
            return;

        if (!isOn)
        {
            if (_resolutionPresetToggles[presetIndex] != null)
                _resolutionPresetToggles[presetIndex].SetValueWithoutNotify(true);
            return;
        }

        _suppressResolutionToggleCallback = true;
        for (int i = 0; i < _resolutionPresetToggles.Length; i++)
        {
            Toggle toggle = _resolutionPresetToggles[i];
            if (toggle == null)
                continue;

            toggle.SetValueWithoutNotify(i == presetIndex);
        }
        _suppressResolutionToggleCallback = false;

        RuntimeGraphicsSettings.SetResolutionPreset(presetIndex);
    }

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
