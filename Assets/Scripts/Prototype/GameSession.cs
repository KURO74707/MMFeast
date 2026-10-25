using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// A session is passed through scene entries; there is no static Instance or mutable settings asset.
public class GameSession : MonoBehaviour
{
    public const string VolumePreferenceKey = "MMFeast.MasterVolume";
    public float MasterVolume { get; private set; }
    public Font UIFont { get; private set; }
    public bool IsLoading { get; private set; }
    public string LastError { get; private set; }
    public CharacterDefinition SelectedCharacter { get; private set; }
    public WeaponDefinition SelectedWeapon { get; private set; }
    public event Action<string> LoadFailed;
    private string targetScenePath;
    private bool pendingSelection, configured, volumeDirty;
    private float previousListenerVolume;
    private void Awake() => DontDestroyOnLoad(gameObject);
    public void Configure(GlobalGameSettings settings)
    {
        if (configured) return;
        configured = true; previousListenerVolume = AudioListener.volume;
        float saved = PlayerPrefs.GetFloat(VolumePreferenceKey, settings.DefaultMasterVolume);
        MasterVolume = float.IsNaN(saved) || float.IsInfinity(saved) ? settings.DefaultMasterVolume : Mathf.Clamp01(saved);
        AudioListener.volume = MasterVolume; UIFont = MenuFont.Create();
    }
    public void SetMasterVolume(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value)) return;
        MasterVolume = Mathf.Clamp01(value); AudioListener.volume = MasterVolume;
        PlayerPrefs.SetFloat(VolumePreferenceKey, MasterVolume); volumeDirty = true;
    }
    public void SaveVolume() { if (volumeDirty) { PlayerPrefs.Save(); volumeDirty = false; } }
    public bool CanStart(GlobalGameSettings settings, CharacterDefinition character, WeaponDefinition weapon, out string reason)
    {
        reason = null;
        if (IsLoading) reason = "正在加载游戏…";
        else if (settings == null || character == null || weapon == null || weapon.attack == null ||
            settings.Characters == null || Array.IndexOf(settings.Characters, character) < 0 ||
            settings.Weapons == null || Array.IndexOf(settings.Weapons, weapon) < 0) reason = "请选择有效的角色和武器。";
        else if (SceneUtility.GetBuildIndexByScenePath(settings.GameplayScenePath) < 0) reason = "游戏场景未启用，请检查 Global Data 和 Build Settings。";
        return reason == null;
    }
    public bool BeginRun(GlobalGameSettings settings, CharacterDefinition character, WeaponDefinition weapon)
    {
        if (!CanStart(settings, character, weapon, out string reason)) { LastError = reason; return false; }
        SaveVolume(); LastError = null;
        SelectedCharacter = character; SelectedWeapon = weapon; targetScenePath = settings.GameplayScenePath;
        pendingSelection = true; IsLoading = true; Time.timeScale = 1f;
        StartCoroutine(LoadGame(SceneUtility.GetBuildIndexByScenePath(targetScenePath))); return true;
    }
    public bool ReturnToMainMenu(GlobalGameSettings settings)
    {
        if (IsLoading) { LastError = "正在加载场景…"; return false; }
        int index = settings == null ? -1 : SceneUtility.GetBuildIndexByScenePath(settings.MainMenuScenePath);
        if (index < 0) { LastError = "开始场景未启用，请检查 Global Data 和 Build Settings。"; return false; }
        SaveVolume(); LastError = null; pendingSelection = false; targetScenePath = null;
        IsLoading = true;
        StartCoroutine(LoadGame(index)); return IsLoading;
    }
    public bool RestartRun(Scene scene, CharacterDefinition character, WeaponDefinition weapon)
    {
        if (IsLoading) { LastError = "正在加载场景…"; return false; }
        if (!scene.IsValid() || !scene.isLoaded || scene.buildIndex < 0)
        { LastError = "本局场景未启用，无法重新开始。请检查 Build Settings。"; return false; }
        if (character == null || weapon == null || weapon.attack == null)
        { LastError = "当前角色或武器配置无效，无法重新开始。"; return false; }
        SaveVolume(); LastError = null;
        // Capture the player's actual loadout, including direct scene starts and equipment changes.
        SelectedCharacter = character; SelectedWeapon = weapon; targetScenePath = scene.path;
        pendingSelection = true; IsLoading = true;
        StartCoroutine(LoadGame(scene.buildIndex)); return IsLoading;
    }
    private IEnumerator LoadGame(int buildIndex)
    {
        AsyncOperation loading = null;
        try { loading = SceneManager.LoadSceneAsync(buildIndex, LoadSceneMode.Single); }
        catch (Exception error) { LastError = "无法加载场景：" + error.Message; }
        if (loading == null)
        {
            IsLoading = false; pendingSelection = false;
            LastError = LastError ?? "无法加载场景。"; LoadFailed?.Invoke(LastError); yield break;
        }
        while (!loading.isDone) yield return null;
        IsLoading = false;
    }
    public bool TryGetPendingSelection(Scene scene, out CharacterDefinition character, out WeaponDefinition weapon)
    {
        character = null; weapon = null;
        if (!pendingSelection || scene.path != targetScenePath) return false;
        character = SelectedCharacter; weapon = SelectedWeapon; return true;
    }
    public void MarkSelectionApplied() => pendingSelection = false;
    private void OnApplicationPause(bool paused) { if (paused) SaveVolume(); }
    private void OnApplicationQuit() => SaveVolume();
    private void OnDestroy() { SaveVolume(); if (configured) AudioListener.volume = previousListenerVolume; }
}
