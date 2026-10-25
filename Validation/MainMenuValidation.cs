using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MainMenuValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static double deadline;
    static bool hadVolume; static float previousVolume;
    static MainMenuValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run() { EditorApplication.delayCall += Prepare; }
    static void Prepare()
    {
        try
        {
            hadVolume = PlayerPrefs.HasKey(GameSession.VolumePreferenceKey); previousVolume = PlayerPrefs.GetFloat(GameSession.VolumePreferenceKey, 1);
            SessionState.SetBool("MenuHadVolume", hadVolume); SessionState.SetFloat("MenuPreviousVolume", previousVolume);
            Application.runInBackground = true;
            MainMenuPrototypeSetup.Upgrade();
            Check(EditorBuildSettings.scenes[0].path == MainMenuPrototypeSetup.ScenePath && EditorBuildSettings.scenes[0].enabled, "MainMenu is the first enabled build scene");
            var settings = GlobalDataPrototypeSetup.GetOrCreateSettings();
            Check(settings.Characters[0].displayColor != settings.Characters[1].displayColor, "Cat and bird have distinct configurable colors");
            Check(settings.Weapons[0].displayColor != settings.Weapons[1].displayColor, "Spatula and mallet have distinct configurable colors");
            string baseline = EditorJsonUtility.ToJson(settings); MainMenuPrototypeSetup.Upgrade();
            Check(EditorJsonUtility.ToJson(settings) == baseline, "Repeated setup preserves designer configuration");
            foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                EditorSceneManager.OpenScene(path);
                Check(Object.FindObjectsByType<SessionSceneEntry>().Length == 1 && Object.FindFirstObjectByType<PlayerAppearance>() != null, path + " has session entry and appearance binding");
            }
            EditorSceneManager.OpenScene(MainMenuPrototypeSetup.ScenePath);
            Check(Object.FindObjectsByType<EventSystem>().Length == 1, "Menu has exactly one EventSystem");
            SessionState.SetBool("MenuHadVolume", PlayerPrefs.HasKey(GameSession.VolumePreferenceKey));
            SessionState.SetFloat("MenuPreviousVolume", PlayerPrefs.GetFloat(GameSession.VolumePreferenceKey, 1));
            PlayerPrefs.DeleteKey(GameSession.VolumePreferenceKey); PlayerPrefs.Save();
            SessionState.SetString("MenuChecks", string.Join("\n", checks)); SessionState.SetBool("MenuPending", true);
            EditorSettings.enterPlayModeOptionsEnabled = false; EditorSceneManager.playModeStartScene = null;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MenuPending", false)) return;
        SessionState.SetBool("MenuPending", false);
        hadVolume = SessionState.GetBool("MenuHadVolume", false); previousVolume = SessionState.GetFloat("MenuPreviousVolume", 1);
        checks.Clear(); checks.AddRange(SessionState.GetString("MenuChecks", "").Split('\n'));
        deadline = EditorApplication.timeSinceStartup + 150; EditorApplication.update += Pump;
        EditorApplication.delayCall += () => new GameObject("Menu validation").AddComponent<MenuValidationRunner>().Begin(Guard(Tests()));
    }
    static void Pump()
    {
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (deadline > 0 && EditorApplication.timeSinceStartup > deadline) Fail(new Exception("Menu validation timed out"));
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
        RestoreVolume(); File.WriteAllText("MAIN_MENU_RESULTS.txt", "PASS: " + checks.Count + " checks\n" + string.Join("\n", checks));
        Time.timeScale = 1; EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        Time.timeScale = 1;
        var menu = Object.FindFirstObjectByType<MainMenuController>(); var session = Object.FindFirstObjectByType<GameSession>();
        var original = Object.FindFirstObjectByType<GlobalData>().Settings;
        Check(menu.CurrentPage == MainMenuController.Page.Home && !menu.IsBusy, "Home is the initial page");
        Check(Field<CanvasGroup>(menu, "home").gameObject.activeSelf && !Field<CanvasGroup>(menu, "selection").gameObject.activeSelf, "Only home is initially visible");
        Check(menu.transform.Find("Home/Title").GetComponent<Text>().text == "MMFeast", "Home title is exactly MMFeast");
        Check(ButtonLabel(menu, "homeStart") == "开始游戏" && ButtonLabel(menu, "homeSettings") == "设置" && ButtonLabel(menu, "homeCredits") == "制作团队", "Three home buttons use requested labels");
        session.UIFont.RequestCharactersInTexture("开始游戏设置制作团队", 28);
        Check(session.UIFont.HasCharacter('开') && session.UIFont.HasCharacter('设'), "Runtime font supports Chinese menu labels");
        Check(Mathf.Approximately(session.MasterVolume, original.DefaultMasterVolume), "Missing saved preference uses Global Data volume default");
        yield return null;
        var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(null, Field<Button>(menu, "homeStart").transform.position) };
        var raycasts = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, raycasts);
        Check(raycasts.Count > 0 && raycasts[0].gameObject.GetComponentInParent<Button>() == Field<Button>(menu, "homeStart"), "Home start button receives pointer raycasts through overlay");
        Capture("MENU_HOME_PREVIEW.png");
        var config = Object.Instantiate(original); Set(config, "menuFadeDuration", .02f);
        var bird = Object.Instantiate(original.Characters[1]); bird.maxHP = 137; bird.maxSAN = 83; bird.maxStamina = 80; bird.initialStamina = 37; bird.staminaRecoveryPerSecond = 0;
        var mallet = Object.Instantiate(original.Weapons[1]);
        Set(config, "characters", new[] { original.Characters[0], bird }); Set(config, "weapons", new[] { original.Weapons[0], mallet });
        Set(menu, "settings", config); yield return null;
        Field<Button>(menu, "homeStart").onClick.Invoke(); Field<Button>(menu, "homeStart").onClick.Invoke();
        Check(menu.IsBusy, "Repeated clicks are locked during page transition");
        yield return new WaitForSecondsRealtime(.1f);
        Check(menu.CurrentPage == MainMenuController.Page.Selection && !menu.IsBusy, "Start opens character and weapon selection");
        var characterCards = Field<List<MenuChoiceCard>>(menu, "characterCards"); var weaponCards = Field<List<MenuChoiceCard>>(menu, "weaponCards");
        Check(characterCards.Count == 2 && weaponCards.Count == 2, "Cards come from Global Data catalogs");
        Field<Button>(characterCards[1], "button").onClick.Invoke(); Field<Button>(weaponCards[1], "button").onClick.Invoke();
        Check(menu.SelectedCharacter == bird && menu.SelectedWeapon == mallet, "Card buttons select bird and mallet independently");
        Check(Field<Image>(menu, "characterPreview").color == bird.displayColor && Field<Image>(menu, "weaponPreview").color == mallet.displayColor, "Preview uses selected definition colors");
        var characterRect = (RectTransform)Field<Image>(menu, "characterPreview").transform; var weaponRect = (RectTransform)Field<Image>(menu, "weaponPreview").transform;
        Check(characterRect.rect.height > characterRect.rect.width && Mathf.Approximately(weaponRect.rect.width, weaponRect.rect.height), "Character preview is a rectangle; weapon preview is a square");
        yield return null; Capture("MENU_SELECTION_PREVIEW.png");
        bird.displayColor = new Color(.32f, .8f, .55f); mallet.displayColor = new Color(.95f, .38f, .21f); yield return null;
        Check(Field<Image>(menu, "characterPreview").color == bird.displayColor && Field<Image>(weaponCards[1], "preview").color == mallet.displayColor, "Live color edits update previews and cards");
        Check(original.DefaultCharacter.id == "cat" && original.DefaultWeapon.id == "spatula", "Player selection does not overwrite Designer defaults");
        Set(config, "characters", new CharacterDefinition[] { original.Characters[0], bird, bird, null }); yield return null;
        Check(Field<List<MenuChoiceCard>>(menu, "characterCards").Count == 2, "Null and duplicate catalog entries do not create extra cards");
        var attack = mallet.attack; mallet.attack = null; yield return null;
        Check(!Field<Button>(menu, "beginGame").interactable, "Invalid selected weapon disables start"); mallet.attack = attack;
        Set(config, "gameplayScenePath", "Assets/MissingScene.unity"); menu.BeginGame();
        Check(!menu.IsBusy && !session.IsLoading && !string.IsNullOrEmpty(Field<Text>(menu, "selectionMessage").text), "Missing game scene reports an error without starting a load");
        Set(config, "gameplayScenePath", original.GameplayScenePath);
        Field<Button>(menu, "selectionBack").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
        Time.timeScale = 0; Field<Button>(menu, "homeSettings").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
        Check(menu.CurrentPage == MainMenuController.Page.Settings && !menu.IsBusy, "Settings transition works with timeScale zero"); Time.timeScale = 1;
        var slider = Field<Slider>(menu, "volumeSlider"); slider.value = 0;
        Check(AudioListener.volume == 0 && Field<Text>(menu, "volumeText").text == "0%", "Zero volume mutes and updates percent text");
        slider.value = 1; Check(AudioListener.volume == 1, "Maximum slider value restores full volume");
        slider.value = .35f; Check(Mathf.Approximately(session.MasterVolume, .35f) && Mathf.Approximately(AudioListener.volume, .35f), "Volume change applies immediately");
        yield return null; Capture("MENU_SETTINGS_PREVIEW.png");
        Field<Button>(menu, "settingsBack").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
        Check(Mathf.Approximately(PlayerPrefs.GetFloat(GameSession.VolumePreferenceKey), .35f), "Leaving settings saves local volume preference");
        var restoredSession = new GameObject("Preference restore probe").AddComponent<GameSession>(); restoredSession.Configure(original);
        Check(Mathf.Approximately(restoredSession.MasterVolume, .35f), "A new session restores saved volume"); Object.Destroy(restoredSession.gameObject); yield return null;
        Field<Button>(menu, "homeCredits").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
        Check(menu.CurrentPage == MainMenuController.Page.Credits, "Credits button opens its own panel");
        Check(menu.transform.Find("Credits/Team").GetComponent<Text>().text == "Game design: Sangni Lin, Emily Cai\nGame art: Sangni Lin\nUI/UX: Keller Wang\nEngineering: Kenny Luo", "Credits exactly match supplied names and roles");
        yield return null; Capture("MENU_CREDITS_PREVIEW.png");
        Field<Button>(menu, "creditsBack").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
        Field<Button>(menu, "homeStart").onClick.Invoke(); yield return new WaitForSecondsRealtime(.1f);
        Check(menu.SelectedCharacter == bird && menu.SelectedWeapon == mallet, "Back navigation preserves choices");
        Field<Button>(menu, "beginGame").onClick.Invoke(); Field<Button>(menu, "beginGame").onClick.Invoke();
        Check(menu.IsBusy, "Starting a run locks repeated start clicks");
        while (SceneManager.GetActiveScene().path == MainMenuPrototypeSetup.ScenePath) yield return null;
        yield return null;
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
        var player = Object.FindFirstObjectByType<PlayerController>();
        Check(SceneManager.GetActiveScene().path == original.GameplayScenePath, "Selected run loads configured cooking gameplay scene");
        Check(player.Loadout.Character == bird && player.Loadout.Weapon == mallet, "Gameplay receives the chosen definitions");
        Check(player.HP == 137 && player.MaxHP == 137 && player.SAN == 83 && player.CurrentStamina == 37, "Chosen character initializes HP/SAN/stamina before player Awake");
        Check(Object.FindObjectsByType<GameSession>().Length == 1 && Mathf.Approximately(AudioListener.volume, .35f), "One session and selected volume survive scene loading");
        var bodyRenderer = player.transform.Find("Visual").GetComponent<SpriteRenderer>(); var weaponRenderer = player.transform.Find("FacingIndicator").GetComponent<SpriteRenderer>();
        var hud = Object.FindFirstObjectByType<PlayerStatusUI>();
        Check(bodyRenderer.color == bird.displayColor && weaponRenderer.color == mallet.displayColor && Field<Image>(hud, "characterSwatch").color == bird.displayColor && Field<Image>(hud, "weaponSwatch").color == mallet.displayColor, "Game and HUD squares keep selection colors");
        Check(player.GetComponentsInChildren<Text>(true).Length == 0 && Field<Text>(Object.FindFirstObjectByType<PlayerStatusUI>(), "loadoutText").text.Contains("Character: " + bird.displayName) && Field<Text>(Object.FindFirstObjectByType<PlayerStatusUI>(), "loadoutText").text.Contains("Weapon: " + mallet.displayName), "Player has no world text; HUD shows chosen character and weapon");
        Check(Mathf.Approximately(player.GetComponent<BoxCollider2D>().size.x, .7f) && Mathf.Approximately(bodyRenderer.transform.localScale.x, bodyRenderer.transform.localScale.y), "Player visual is square and retains its collider size");
        player.TakeDamage(1); Color flashColor = bodyRenderer.color;
        bird.displayColor = new Color(.2f, .6f, .95f); yield return null;
        Check(bodyRenderer.color == flashColor && bodyRenderer.color != bird.displayColor && Field<Image>(hud, "characterSwatch").color == bird.displayColor, "HUD follows live base color while player preserves active hit flash");
        yield return new WaitForSeconds(original.PlayerHitFlashDuration + .05f);
        Check(bodyRenderer.color == bird.displayColor, "Hit flash restores the new character color");
        mallet.displayColor = new Color(.5f, .9f, .3f); yield return null;
        Check(weaponRenderer.color == mallet.displayColor && Field<Image>(hud, "weaponSwatch").color == mallet.displayColor, "Weapon and HUD color continue to update live in gameplay");
        Check(!session.TryGetPendingSelection(player.gameObject.scene, out _, out _), "Pending selection is consumed exactly once");
        yield return null; Capture("MENU_GAMEPLAY_PREVIEW.png");
        yield return SceneManager.LoadSceneAsync(SceneUtility.GetBuildIndexByScenePath(CombatPrototypeBuilder.ScenePath)); yield return null;
        var directPlayer = Object.FindFirstObjectByType<PlayerController>();
        Check(directPlayer.Loadout.Character == original.DefaultCharacter && directPlayer.HP == original.DefaultCharacter.maxHP, "Loading another prototype directly does not reuse stale run selection");
        Check(Mathf.Approximately(AudioListener.volume, .35f), "Volume also persists into the other prototype scene");
        Check(original.DefaultCharacter.id == "cat" && original.DefaultWeapon.id == "spatula", "Full menu flow leaves shared default selection unchanged");
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
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add("PASS " + message); File.WriteAllText("MAIN_MENU_PROGRESS.txt", string.Join("\n", checks)); }
    static void RestoreVolume()
    { if (hadVolume) PlayerPrefs.SetFloat(GameSession.VolumePreferenceKey, previousVolume); else PlayerPrefs.DeleteKey(GameSession.VolumePreferenceKey); PlayerPrefs.Save(); }
    static void Fail(Exception error) { RestoreVolume(); Debug.LogException(error); File.WriteAllText("MAIN_MENU_RESULTS.txt", "FAIL after " + checks.Count + "\n" + string.Join("\n", checks) + "\n" + error); EditorApplication.Exit(1); }
}
