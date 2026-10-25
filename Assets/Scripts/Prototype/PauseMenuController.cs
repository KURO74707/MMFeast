using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Process the modal toggle before PlayerController reads this frame's gameplay input.
[DefaultExecutionOrder(-500)]
public class PauseMenuController : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private SessionSceneEntry sceneEntry;
    [SerializeField] private PlayerController[] players;
    [SerializeField] private CanvasGroup overlay;
    [SerializeField] private Button resumeButton, restartButton, returnButton;
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Text volumeText, message;
    public bool IsOpen { get; private set; }
    public bool IsReturning => returning && !restarting;
    public bool IsRestarting => returning && restarting;
    private GameSession session;
    private InputAction pauseInput;
    private GlobalGameSettings inputSettings;
    private int inputRevision;
    private bool waitingForRelease, returning, restarting;
    private float previousTimeScale;

    private void Awake()
    {
        session = sceneEntry != null ? sceneEntry.Session : null;
        if (!GlobalGameSettings.Require(this, settings) || session == null || overlay == null ||
            resumeButton == null || restartButton == null || returnButton == null || volumeSlider == null || volumeText == null || message == null)
        { Debug.LogError("Pause Menu requires its scene session, settings and UI references.", this); enabled = false; return; }
        foreach (var text in overlay.GetComponentsInChildren<Text>(true)) text.font = session.UIFont;
        resumeButton.onClick.AddListener(Resume);
        restartButton.onClick.AddListener(RestartRun);
        returnButton.onClick.AddListener(ReturnToMainMenu);
        volumeSlider.onValueChanged.AddListener(SetVolume);
        session.LoadFailed += OnLoadFailed;
        overlay.gameObject.SetActive(false);
    }
    private void OnEnable() { RebuildInput(); }
    private void RebuildInput()
    {
        pauseInput?.Dispose(); pauseInput = null;
        inputSettings = settings; inputRevision = settings != null ? settings.InputRevision : -1;
        waitingForRelease = true;
        if (settings == null || settings.Pause == null || settings.Pause.type != InputActionType.Button)
        { Debug.LogError("Global Data needs a Button action for Pause Menu.", this); return; }
        pauseInput = GlobalGameSettings.CloneInput(settings.Pause); pauseInput.Enable();
    }
    private void Update()
    {
        if (inputSettings != settings || (settings != null && inputRevision != settings.InputRevision)) RebuildInput();
        if (returning || session == null || session.IsLoading || pauseInput == null) return;
        if (waitingForRelease) { if (!pauseInput.IsPressed()) waitingForRelease = false; return; }
        if (pauseInput.WasPressedThisFrame()) { if (IsOpen) Resume(); else Open(); }
    }
    public void Open()
    {
        if (!isActiveAndEnabled || session == null || session.IsLoading || IsOpen) return;
        if (players != null) foreach (var player in players) if (player != null) player.SetPaused(true);
        previousTimeScale = Time.timeScale; Time.timeScale = 0f; IsOpen = true;
        message.text = "";
        transform.SetAsLastSibling(); overlay.gameObject.SetActive(true);
        overlay.alpha = 1f; overlay.interactable = overlay.blocksRaycasts = true;
        volumeSlider.SetValueWithoutNotify(session.MasterVolume); RefreshVolume();
        SetControlsInteractable(true);
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(resumeButton.gameObject);
    }
    public void Resume()
    {
        if (!IsOpen || returning) return;
        Close();
    }
    private void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        if (overlay != null) overlay.gameObject.SetActive(false);
        if (players != null) foreach (var player in players) if (player != null) player.SetPaused(false);
        Time.timeScale = returning ? 1f : previousTimeScale;
        session?.SaveVolume();
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    private void SetVolume(float value)
    { if (!IsOpen || returning || session == null) return; session.SetMasterVolume(value); RefreshVolume(); }
    private void RefreshVolume() => volumeText.text = Mathf.RoundToInt(session.MasterVolume * 100f) + "%";
    private void SetControlsInteractable(bool value)
    { resumeButton.interactable = restartButton.interactable = returnButton.interactable = volumeSlider.interactable = value; }
    public void RestartRun()
    {
        if (!IsOpen || returning || session == null) return;
        returning = restarting = true;
        PlayerLoadout loadout = null;
        if (players != null) foreach (var player in players)
            if (player != null && player.gameObject.scene == gameObject.scene) { loadout = player.Loadout; break; }
        if (loadout == null) { OnLoadFailed("未找到本局玩家，无法重新开始。"); return; }
        if (!session.RestartRun(gameObject.scene, loadout.Character, loadout.Weapon)) { OnLoadFailed(session.LastError); return; }
        message.text = "正在重新开始…"; SetControlsInteractable(false);
    }
    public void ReturnToMainMenu()
    {
        if (!IsOpen || returning || session == null) return;
        returning = true; restarting = false;
        if (!session.ReturnToMainMenu(settings)) { OnLoadFailed(session.LastError); return; }
        message.text = "正在返回开始界面…";
        SetControlsInteractable(false);
    }
    private void OnLoadFailed(string reason)
    {
        if (!returning) return;
        returning = restarting = false; Time.timeScale = 0f;
        message.text = reason;
        SetControlsInteractable(true);
    }
    private void OnDisable() { pauseInput?.Dispose(); pauseInput = null; Close(); }
    private void OnDestroy()
    {
        if (session != null) session.LoadFailed -= OnLoadFailed;
        if (resumeButton != null) resumeButton.onClick.RemoveListener(Resume);
        if (restartButton != null) restartButton.onClick.RemoveListener(RestartRun);
        if (returnButton != null) returnButton.onClick.RemoveListener(ReturnToMainMenu);
        if (volumeSlider != null) volumeSlider.onValueChanged.RemoveListener(SetVolume);
    }
}
