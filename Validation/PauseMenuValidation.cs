using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PauseMenuValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static double deadline;
    static bool hadVolume; static float previousVolume;
    static PauseMenuValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run() { EditorApplication.delayCall += Prepare; }
    static void Prepare()
    {
        try
        {
            hadVolume = PlayerPrefs.HasKey(GameSession.VolumePreferenceKey); previousVolume = PlayerPrefs.GetFloat(GameSession.VolumePreferenceKey, 1);
            SessionState.SetBool("PauseHadVolume", hadVolume); SessionState.SetFloat("PausePreviousVolume", previousVolume);
            Application.runInBackground = true;
            PauseMenuPrototypeSetup.Upgrade();
            foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                Check(Object.FindObjectsByType<PauseMenuController>().Length == 1, path + " has one pause controller");
                var pause = Object.FindFirstObjectByType<PauseMenuController>();
                var data = GlobalDataPrototypeSetup.GetOrCreateSettings();
                Check(Field<GlobalGameSettings>(pause, "settings") == data && Field<SessionSceneEntry>(pause, "sceneEntry").gameObject.scene == scene, path + " has shared data and local session entry");
                Check(Object.FindObjectsByType<EventSystem>().Length == 1 && pause.GetComponentInParent<Canvas>() == Object.FindFirstObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>(), path + " reuses HUD Canvas and EventSystem");
                string before = EditorJsonUtility.ToJson(data); MainMenuPrototypeSetup.ApplyGameplayScene(scene);
                Check(before == EditorJsonUtility.ToJson(data) && Object.FindObjectsByType<PauseMenuController>().Length == 1, path + " setup preserves data and avoids duplicate menus");
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene(CookingPrototypeBuilder.ScenePath);
            PlayerPrefs.DeleteKey(GameSession.VolumePreferenceKey); PlayerPrefs.Save();
            SessionState.SetString("PauseChecks", string.Join("\n", checks)); SessionState.SetBool("PausePending", true);
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("PausePending", false)) return;
        SessionState.SetBool("PausePending", false);
        hadVolume = SessionState.GetBool("PauseHadVolume", false); previousVolume = SessionState.GetFloat("PausePreviousVolume", 1);
        checks.Clear(); checks.AddRange(SessionState.GetString("PauseChecks", "").Split('\n'));
        deadline = EditorApplication.timeSinceStartup + 150; EditorApplication.update += Pump;
        EditorApplication.delayCall += () => new GameObject("Pause validation").AddComponent<MenuValidationRunner>().Begin(Guard(Tests()));
    }
    static void Pump()
    {
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (deadline > 0 && EditorApplication.timeSinceStartup > deadline) Fail(new Exception("Pause validation timed out"));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while (true)
        {
            bool next; object current;
            try { next = test.MoveNext(); current = next ? test.Current : null; }
            catch (Exception e) { Fail(e); yield break; }
            if (!next) break; yield return current;
        }
        RestoreVolume(); File.WriteAllText("PAUSE_MENU_RESULTS.txt", "PASS: " + checks.Count + " checks\n" + string.Join("\n", checks));
        Time.timeScale = 1; EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach (var spawn in Object.FindObjectsByType<EnemySpawner>()) spawn.enabled = false;
        foreach (var ai in Object.FindObjectsByType<EnemySimpleAI>()) Object.Destroy(ai.gameObject);
        yield return null;
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        var player = Object.FindFirstObjectByType<PlayerController>();
        var session = Object.FindFirstObjectByType<GameSession>();
        var original = Object.FindFirstObjectByType<GlobalData>().Settings;
        var config = Object.Instantiate(original); Set(pause, "settings", config); player.Configure(config);
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
        Send(keyboard, mouse); Tick(player, "Update"); Tick(pause, "Update"); Tick(pause, "Update");
        Check(!pause.IsOpen && Time.timeScale == 1 && !Field<CanvasGroup>(pause, "overlay").gameObject.activeSelf, "Gameplay begins unpaused with hidden overlay");
        Check(config.Pause.bindings[0].path == "<Keyboard>/escape" && Field<InputAction>(pause, "pauseInput") != config.Pause, "Esc default uses an independent input copy");
        Check(Field<Button>(pause, "resumeButton").GetComponentInChildren<Text>().text == "继续游戏" && Field<Button>(pause, "returnButton").GetComponentInChildren<Text>().text == "返回开始界面", "Pause buttons have requested labels");
        Send(keyboard, mouse, Key.Escape); Tick(pause, "Update"); Tick(player, "Update");
        Check(pause.IsOpen && player.IsPauseOpen && player.GameplayInputsBlocked && Time.timeScale == 0, "Actual Esc opens pause before gameplay input");
        Send(keyboard, mouse, Key.Escape); Tick(pause, "Update");
        Check(pause.IsOpen, "Holding Esc does not repeatedly toggle"); Send(keyboard, mouse);
        yield return null;
        var hits = new List<RaycastResult>();
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, Field<Button>(pause, "resumeButton").transform.position) };
        EventSystem.current.RaycastAll(pointer, hits);
        Check(hits.Count > 0 && hits[0].gameObject.GetComponentInParent<Button>() == Field<Button>(pause, "resumeButton"), "Overlay buttons receive pointer input above HUD");
        Field<Slider>(pause, "volumeSlider").value = .37f;
        Check(Mathf.Approximately(session.MasterVolume, .37f) && Mathf.Approximately(AudioListener.volume, .37f) && Field<Text>(pause, "volumeText").text == "37%", "Paused slider adjusts shared volume and percentage");
        Check(Mathf.Approximately(PlayerPrefs.GetFloat(GameSession.VolumePreferenceKey), .37f), "Volume writes the shared preference");
        yield return null; Capture("PAUSE_MENU_PREVIEW.png");
        float time = Time.time; yield return new WaitForSecondsRealtime(.1f);
        Check(Time.time == time, "Scaled gameplay time is frozen while UI runs");
        Send(keyboard, mouse, Key.W, Key.Space, Key.E, Key.R, Key.Tab); Tick(player, "Update"); Tick(player, "FixedUpdate");
        Check(!player.DashPressed && !player.InteractPressed && !player.ShopPressed && !player.ItemShopPressed && !player.HasMovementInput && !player.IsDashing, "Pause suppresses movement, dash, interaction and shops");
        var truckShop = player.GetComponent<TruckShop>(); var itemShop = player.GetComponent<ItemShop>();
        Check(!truckShop.OpenShop() && !itemShop.OpenShop(), "Both shops reject opening under pause");
        Check(!player.GetComponent<PlayerKitchenLayout>().TryEnterMode(), "Kitchen mode cannot start under pause");
        Field<Button>(pause, "resumeButton").onClick.Invoke();
        Check(!pause.IsOpen && !player.IsPauseOpen && Time.timeScale == 1, "Continue button restores time and gameplay");
        Tick(player, "Update"); Tick(player, "FixedUpdate");
        Check(!player.DashPressed && !player.InteractPressed && !player.ShopPressed && !player.ItemShopPressed && !player.IsDashing, "Held actions across resume do not trigger");
        Send(keyboard, mouse); Tick(player, "Update"); Tick(pause, "Update");
        Send(keyboard, mouse, Key.Escape); Tick(pause, "Update"); Check(pause.IsOpen, "Esc reopens after release");
        Send(keyboard, mouse); Tick(pause, "Update"); Send(keyboard, mouse, Key.Escape); Tick(pause, "Update");
        Check(!pause.IsOpen && Time.timeScale == 1, "Esc also closes pause"); Send(keyboard, mouse);
        Time.timeScale = .5f; pause.Open(); pause.Resume(); Check(Time.timeScale == .5f, "Resume restores a non-default time scale");
        Check(itemShop.OpenShop(), "Fixture opens paid shop"); pause.Open();
        Check(pause.IsOpen && !itemShop.IsOpen && Time.timeScale == 0, "Pause closes paid shop before taking time ownership");
        pause.Resume(); Check(Time.timeScale == .5f, "Resume after paid shop restores original scale");
        Check(truckShop.OpenShop(), "Fixture opens free upgrades"); pause.Open();
        Check(!truckShop.IsOpen && pause.IsOpen, "Pause closes free upgrades"); pause.Resume();
        Check(Time.timeScale == .5f, "Resume after upgrades restores original scale"); Time.timeScale = 1;
        var cannon = Object.FindFirstObjectByType<FoodCannon>(); var layout = player.GetComponent<PlayerKitchenLayout>();
        Move(player, cannon.transform.position + Vector3.down * .9f); yield return new WaitForFixedUpdate(); yield return null;
        Check(layout.TryEnterMode(), "Fixture enters kitchen mode"); pause.Open();
        Check(!layout.IsOrganizing && pause.IsOpen, "Pause exits kitchen mode"); pause.Resume();
        Send(keyboard, mouse); Tick(player, "Update"); Set(cannon, "isLoaded", true);
        InputSystem.QueueStateEvent(mouse, new MouseState { buttons = 2, position = Camera.main.WorldToScreenPoint(cannon.transform.position + Vector3.right * 6) });
        InputSystem.Update(); Tick(player, "Update");
        Check(cannon.HandleAimInput(player, true) && cannon.IsAiming, "Fixture aims a loaded cannon"); pause.Open();
        Check(!cannon.IsAiming && !player.IsCannonAiming && cannon.IsLoaded, "Pause cancels aim and retains ammunition");
        Send(keyboard, mouse); Tick(player, "Update"); pause.Resume(); Tick(player, "Update"); cannon.HandleAimInput(player, true);
        Check(cannon.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length == 0 && !player.RightMouseReleased, "Releasing aim across pause does not fire");
        pause.Open(); pause.enabled = false;
        Check(!pause.IsOpen && !player.IsPauseOpen && Time.timeScale == 1, "Disabling controller restores time and player input");
        pause.enabled = true; Send(keyboard, mouse); Tick(pause, "Update"); pause.Open();
        config.Pause.ChangeBinding(0).WithPath("<Keyboard>/p"); config.Validate(); Tick(pause, "Update"); Tick(player, "Update");
        Check(pause.IsOpen && player.IsPauseOpen && Time.timeScale == 0, "Live rebinding preserves pause ownership");
        Send(keyboard, mouse); Tick(pause, "Update"); Send(keyboard, mouse, Key.P); Tick(pause, "Update");
        Check(!pause.IsOpen && Time.timeScale == 1, "Rebound key resumes gameplay");
        Send(keyboard, mouse); Tick(pause, "Update"); Send(keyboard, mouse, Key.Escape); Tick(pause, "Update");
        Check(!pause.IsOpen, "Old binding no longer toggles pause"); Send(keyboard, mouse);
        pause.Open(); Set(config, "mainMenuScenePath", "Assets/MissingMenu.unity"); pause.ReturnToMainMenu();
        Check(pause.IsOpen && !pause.IsReturning && !session.IsLoading && Time.timeScale == 0 && !string.IsNullOrEmpty(Field<Text>(pause, "message").text), "Missing menu scene displays error and keeps game paused");
        Check(Field<Button>(pause, "resumeButton").interactable, "Failed return leaves Continue available");
        Set(config, "mainMenuScenePath", original.MainMenuScenePath); Field<Button>(pause, "returnButton").onClick.Invoke();
        Check(pause.IsReturning && session.IsLoading && !Field<Button>(pause, "resumeButton").interactable, "Return asynchronously loads once and locks menu"); pause.ReturnToMainMenu();
        while (session.IsLoading) yield return null; yield return null;
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        Check(menu != null && menu.CurrentPage == MainMenuController.Page.Home && Time.timeScale == 1, "Return reaches Home with time restored");
        Check(Object.FindObjectsByType<GameSession>().Length == 1 && Object.FindFirstObjectByType<GameSession>() == session, "Return reuses exactly one session");
        Check(Object.FindObjectsByType<PauseMenuController>().Length == 0 && Object.FindObjectsByType<PlayerController>().Length == 0, "Return unloads gameplay and pause objects");
        Check(Mathf.Approximately(Field<Slider>(menu, "volumeSlider").value, .37f) && Mathf.Approximately(AudioListener.volume, .37f), "Main menu retains paused volume");
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
        menu.ShowPage(MainMenuController.Page.Selection); yield return new WaitForSecondsRealtime(.5f); menu.BeginGame();
        yield return new WaitForSecondsRealtime(.5f); // BeginGame first fades the current UI before loading.
        while (session.IsLoading) yield return null; yield return null;
        Check(Object.FindFirstObjectByType<PlayerController>() != null && Object.FindFirstObjectByType<PauseMenuController>() != null && Time.timeScale == 1, "New game starts normally after returning");
        Check(Mathf.Approximately(AudioListener.volume, .37f) && !Object.FindFirstObjectByType<PauseMenuController>().IsOpen, "New run retains volume and starts unpaused");
        InputSystem.RemoveDevice(keyboard); InputSystem.RemoveDevice(mouse); Object.Destroy(config);
    }
    static void Send(Keyboard keyboard, Mouse mouse, params Key[] keys)
    { InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys)); InputSystem.QueueStateEvent(mouse, new MouseState()); InputSystem.Update(); }
    static void Tick(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static void Move(PlayerController player, Vector3 position)
    { player.transform.position = position; player.GetComponent<Rigidbody2D>().position = position; Physics2D.SyncTransforms(); }
    static void Capture(string filename)
    {
        var camera = Camera.main; if (camera == null) return;
        var changed = new List<Canvas>(); var scales = new List<float>();
        var texture = new RenderTexture(1280, 720, 24); texture.Create();
        var previousTarget = camera.targetTexture; var previousActive = RenderTexture.active;
        try
        {
            camera.targetTexture = texture;
            foreach (var canvas in Object.FindObjectsByType<Canvas>()) if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                changed.Add(canvas); scales.Add(canvas.scaleFactor);
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1; canvas.scaleFactor = 1;
            }
            Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = texture });
            RenderTexture.active = texture; var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply(); File.WriteAllBytes(filename, pixels.EncodeToPNG()); Object.Destroy(pixels);
        }
        finally
        {
            for (int i = 0; i < changed.Count; i++) { changed[i].renderMode = RenderMode.ScreenSpaceOverlay; changed[i].worldCamera = null; changed[i].scaleFactor = scales[i]; }
            camera.targetTexture = previousTarget; RenderTexture.active = previousActive; texture.Release(); Object.Destroy(texture);
        }
    }
    static string ButtonLabel(MainMenuController menu, string field) => Field<Button>(menu, field).GetComponentInChildren<Text>().text;
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Private).GetValue(target);
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add("PASS " + message); File.WriteAllText("PAUSE_MENU_PROGRESS.txt", string.Join("\n", checks)); }
    static void RestoreVolume()
    { if (hadVolume) PlayerPrefs.SetFloat(GameSession.VolumePreferenceKey, previousVolume); else PlayerPrefs.DeleteKey(GameSession.VolumePreferenceKey); PlayerPrefs.Save(); }
    static void Fail(Exception error) { RestoreVolume(); Debug.LogException(error); File.WriteAllText("PAUSE_MENU_RESULTS.txt", "FAIL after " + checks.Count + "\n" + string.Join("\n", checks) + "\n" + error); EditorApplication.Exit(1); }
}

