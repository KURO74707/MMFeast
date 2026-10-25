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
public static class RestartRunValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static double deadline;
    static bool hadVolume; static float previousVolume;
    static RestartRunValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run() { EditorApplication.delayCall += Prepare; }
    static void Prepare()
    {
        try
        {
            hadVolume = PlayerPrefs.HasKey(GameSession.VolumePreferenceKey); previousVolume = PlayerPrefs.GetFloat(GameSession.VolumePreferenceKey, 1);
            SessionState.SetBool("RestartHadVolume", hadVolume); SessionState.SetFloat("RestartPreviousVolume", previousVolume);
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
                var restart = Field<Button>(pause, "restartButton");
                Check(restart != null && restart.GetComponentInChildren<Text>().text == "重新开始", path + " has a bound Restart button");
                var rect = (RectTransform)restart.transform; var position = rect.anchoredPosition;
                rect.anchoredPosition += Vector2.right * 13; MainMenuPrototypeSetup.ApplyGameplayScene(scene);
                Check(Object.FindObjectsByType<PauseMenuController>().Length == 1 && rect.anchoredPosition == position + Vector2.right * 13, path + " repeated migration preserves Restart layout");
                rect.anchoredPosition = position;
                EditorSceneManager.SaveScene(scene);
            }
            EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            PlayerPrefs.DeleteKey(GameSession.VolumePreferenceKey); PlayerPrefs.Save();
            SessionState.SetString("RestartChecks", string.Join("\n", checks)); SessionState.SetBool("RestartPending", true);
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("RestartPending", false)) return;
        SessionState.SetBool("RestartPending", false);
        hadVolume = SessionState.GetBool("RestartHadVolume", false); previousVolume = SessionState.GetFloat("RestartPreviousVolume", 1);
        checks.Clear(); checks.AddRange(SessionState.GetString("RestartChecks", "").Split('\n'));
        deadline = EditorApplication.timeSinceStartup + 150; EditorApplication.update += Pump;
        EditorApplication.delayCall += () => new GameObject("Restart validation").AddComponent<MenuValidationRunner>().Begin(Guard(Tests()));
    }
    static void Pump()
    {
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (deadline > 0 && EditorApplication.timeSinceStartup > deadline) Fail(new Exception("Restart validation timed out"));
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
        RestoreVolume(); File.WriteAllText("RESTART_RUN_RESULTS.txt", "PASS: " + checks.Count + " checks\n" + string.Join("\n", checks));
        Time.timeScale = 1; EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        StopNPCs(); yield return null;
        var player = Object.FindFirstObjectByType<PlayerController>();
        var pause = Object.FindFirstObjectByType<PauseMenuController>();
        var session = Object.FindFirstObjectByType<GameSession>();
        var original = Object.FindFirstObjectByType<GlobalData>().Settings;
        var character = player.Loadout.Character; var weapon = player.Loadout.Weapon;
        Check(session.SelectedCharacter == null && session.SelectedWeapon == null, "Direct scene start has no cached menu selection");
        Check(player.gameObject.scene.path == CombatPrototypeBuilder.ScenePath && original.GameplayScenePath == CookingPrototypeBuilder.ScenePath, "Fixture uses Combat while configured menu destination is Cooking");
        pause.RestartRun(); Check(!session.IsLoading, "Restart cannot run while pause is closed");
        pause.Open(); session.SetMasterVolume(.43f);
        Check(!session.RestartRun(default(Scene), character, weapon) && !session.IsLoading && Time.timeScale == 0, "Unsaved or invalid scene rejects restart without leaving pause");
        var wallet = player.GetComponent<TruckShop>(); wallet.SetCurrentBalance(777);
        player.SetCurrentHP(12); player.SetCurrentSAN(9); player.SetCurrentStamina(7);
        player.transform.position = new Vector3(8, 5, 0);
        var pickup = Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab"));
        pickup.transform.position = new Vector3(12, 6, 0);
        var marker = new GameObject("Restart transient marker");
        var cannon = Object.FindFirstObjectByType<FoodCannon>(); Set(cannon, "isLoaded", true);
        var truck = Object.FindFirstObjectByType<FoodTruckHealth>(); truck.TakeDamage(truck.MaxHP - 12);
        var oldPlayer = player; var oldPause = pause;
        Field<Button>(pause, "restartButton").onClick.Invoke();
        Check(pause.IsRestarting && !pause.IsReturning && session.IsLoading, "Restart button begins asynchronous reload");
        Check(!Field<Button>(pause, "resumeButton").interactable && !Field<Button>(pause, "restartButton").interactable && !Field<Button>(pause, "returnButton").interactable && !Field<Slider>(pause, "volumeSlider").interactable, "All pause controls lock during restart");
        pause.RestartRun(); pause.ReturnToMainMenu(); pause.Resume();
        Check(pause.IsRestarting && pause.IsOpen && session.SelectedCharacter == character && session.SelectedWeapon == weapon, "Repeated clicks cannot change an in-flight restart");
        while (session.IsLoading) yield return null; yield return null;
        StopNPCs(); player = Object.FindFirstObjectByType<PlayerController>(); pause = Object.FindFirstObjectByType<PauseMenuController>();
        Check(player.gameObject.scene.path == CombatPrototypeBuilder.ScenePath, "Restart reloads this run's scene rather than configured menu destination");
        Check(oldPlayer == null && oldPause == null && player != null && pause != null, "Restart replaces gameplay and pause objects");
        Check(player.Loadout.Character == character && player.Loadout.Weapon == weapon, "Direct scene restart preserves default character and weapon");
        Check(player.HP == character.maxHP && player.SAN == character.maxSAN && player.CurrentStamina == character.initialStamina, "Restart restores initial HP, SAN and stamina");
        Check(player.GetComponent<TruckShop>().CurrentBalance == original.InitialCoins, "Restart resets run coins to configured initial balance");
        Check(pickup == null && marker == null && !Object.FindFirstObjectByType<FoodCannon>().IsLoaded, "Restart clears dropped items, transient objects and loaded ammunition");
        Check(Object.FindFirstObjectByType<FoodTruckHealth>().CurrentHP == original.FoodTruckMaxHP, "Restart restores food truck health");
        Check(Vector2.Distance(player.transform.position, new Vector2(8, 5)) > 1, "Restart restores scene spawn position");
        Check(Object.FindObjectsByType<GameSession>().Length == 1 && Object.FindFirstObjectByType<GameSession>() == session, "Restart keeps one existing session");
        Check(Time.timeScale == 1 && !pause.IsOpen && !player.IsPauseOpen && !player.GameplayInputsBlocked, "Restart resumes normal time with menu closed and gameplay unblocked");
        Check(Mathf.Approximately(session.MasterVolume, .43f) && Mathf.Approximately(AudioListener.volume, .43f), "Restart preserves volume");

        // Change the actual equipment while leaving the session's previous selection stale.
        var bird = Object.Instantiate(original.Characters[1]); bird.maxHP = 137; bird.maxSAN = 83;
        bird.initialStamina = 37; bird.staminaRecoveryPerSecond = 0;
        var mallet = Object.Instantiate(original.Weapons[1]);
        player.Loadout.SelectCharacter(bird); player.Loadout.EquipWeapon(mallet);
        player.SetCurrentHP(0); player.SetCurrentSAN(3); player.SetCurrentStamina(1);
        Check(session.SelectedCharacter == character && session.SelectedWeapon == weapon, "Fixture leaves cached session choice different from actual equipment");
        pause.Open(); Field<Button>(pause, "restartButton").onClick.Invoke();
        while (session.IsLoading) yield return null; yield return null;
        StopNPCs(); player = Object.FindFirstObjectByType<PlayerController>(); pause = Object.FindFirstObjectByType<PauseMenuController>();
        Check(player.Loadout.Character == bird && player.Loadout.Weapon == mallet && session.SelectedCharacter == bird && session.SelectedWeapon == mallet, "Restart captures current bird and mallet instead of stale session choices");
        Check(player.HP == 137 && player.SAN == 83 && player.CurrentStamina == 37, "Selected character applies before player initialization, including restart after death");
        Check(player.transform.Find("Visual").GetComponent<SpriteRenderer>().color == bird.displayColor && player.transform.Find("FacingIndicator").GetComponent<SpriteRenderer>().color == mallet.displayColor, "Restart preserves character and weapon colors");
        Check(original.DefaultCharacter == character && original.DefaultWeapon == weapon, "Restart does not overwrite Designer defaults or catalog");
        pause.Open(); var attack = mallet.attack; mallet.attack = null;
        Field<Button>(pause, "restartButton").onClick.Invoke();
        Check(!session.IsLoading && !pause.IsRestarting && pause.IsOpen && Time.timeScale == 0 && !string.IsNullOrEmpty(Field<Text>(pause, "message").text), "Invalid equipment reports failure and retains safe pause");
        Check(Field<Button>(pause, "resumeButton").interactable && Field<Button>(pause, "restartButton").interactable && Field<Button>(pause, "returnButton").interactable, "Failed restart unlocks all actions");
        mallet.attack = attack; Field<Button>(pause, "resumeButton").onClick.Invoke();
        Check(Time.timeScale == 1 && !pause.IsOpen, "Continue works after rejected restart");

        Check(session.BeginRun(original, original.Characters[1], original.Weapons[1]), "Fixture starts menu-selected bird and mallet in Cooking");
        while (session.IsLoading) yield return null; yield return null;
        StopNPCs(); player = Object.FindFirstObjectByType<PlayerController>(); pause = Object.FindFirstObjectByType<PauseMenuController>();
        var chosenCharacter = player.Loadout.Character; var chosenWeapon = player.Loadout.Weapon;
        pause.Open(); yield return null; Capture("RESTART_MENU_PREVIEW.png");
        Field<Button>(pause, "restartButton").onClick.Invoke();
        while (session.IsLoading) yield return null; yield return null;
        StopNPCs(); player = Object.FindFirstObjectByType<PlayerController>(); pause = Object.FindFirstObjectByType<PauseMenuController>();
        Check(player.gameObject.scene.path == CookingPrototypeBuilder.ScenePath && player.Loadout.Character == chosenCharacter && player.Loadout.Weapon == chosenWeapon, "Cooking run restart preserves menu-selected bird and mallet");
        Check(Time.timeScale == 1 && !pause.IsOpen && Mathf.Approximately(AudioListener.volume, .43f), "Repeated scene restarts preserve time, pause and volume invariants");
        pause.Open(); Field<Button>(pause, "returnButton").onClick.Invoke();
        while (session.IsLoading) yield return null; yield return null;
        var menu = Object.FindFirstObjectByType<MainMenuController>();
        Check(menu != null && menu.CurrentPage == MainMenuController.Page.Home && Time.timeScale == 1, "Return to Home still works after restarting");
        Check(menu.SelectedCharacter == chosenCharacter && menu.SelectedWeapon == chosenWeapon && Mathf.Approximately(session.MasterVolume, .43f), "Home retains selection and volume after restart and return");
        Object.Destroy(bird); Object.Destroy(mallet);
    }
    static void StopNPCs()
    {
        foreach (var spawn in Object.FindObjectsByType<EnemySpawner>()) spawn.enabled = false;
        foreach (var ai in Object.FindObjectsByType<EnemySimpleAI>()) ai.enabled = false;
    }
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
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add("PASS " + message); File.WriteAllText("RESTART_RUN_PROGRESS.txt", string.Join("\n", checks)); }
    static void RestoreVolume()
    { if (hadVolume) PlayerPrefs.SetFloat(GameSession.VolumePreferenceKey, previousVolume); else PlayerPrefs.DeleteKey(GameSession.VolumePreferenceKey); PlayerPrefs.Save(); }
    static void Fail(Exception error) { RestoreVolume(); Debug.LogException(error); File.WriteAllText("RESTART_RUN_RESULTS.txt", "FAIL after " + checks.Count + "\n" + string.Join("\n", checks) + "\n" + error); EditorApplication.Exit(1); }
}


