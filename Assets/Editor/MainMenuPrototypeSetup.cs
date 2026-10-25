using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public static class MainMenuPrototypeSetup
{
    public const string ScenePath = "Assets/Prototype/MainMenu.unity";
    private static readonly Color Ink = new Color(.055f, .08f, .105f);
    private static readonly Color Cream = new Color(.96f, .94f, .86f);
    private static readonly Color Gold = new Color(1f, .72f, .23f);

    public static void EnsureColors(GlobalGameSettings settings)
    {
        var data = new SerializedObject(settings);
        if (data.FindProperty("loadoutColorsMigrated").boolValue) return;
        if (settings.Characters != null) foreach (var character in settings.Characters)
        {
            if (character == null || HasSavedColor(character)) continue;
            character.displayColor = character.id == "bird" ? new Color(.72f, .46f, .94f) : new Color(.15f, .7f, 1f);
            EditorUtility.SetDirty(character); AssetDatabase.SaveAssetIfDirty(character);
        }
        if (settings.Weapons != null) foreach (var weapon in settings.Weapons)
        {
            if (weapon == null || HasSavedColor(weapon)) continue;
            weapon.displayColor = weapon.id == "meat_mallet" ? new Color(1f, .35f, .35f) : Gold;
            EditorUtility.SetDirty(weapon); AssetDatabase.SaveAssetIfDirty(weapon);
        }
        data.FindProperty("loadoutColorsMigrated").boolValue = true;
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
    }
    private static bool HasSavedColor(Object asset)
    { string path = AssetDatabase.GetAssetPath(asset); return !string.IsNullOrEmpty(path) && File.Exists(path) && File.ReadAllText(path).Contains("displayColor:"); }

    [MenuItem("MMFeast/Set Up Main Menu")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before menu setup.");
        var active = SceneManager.GetActiveScene();
        if (SceneManager.sceneCount == 1 && string.IsNullOrEmpty(active.path))
        {
            if (active.isDirty) throw new InvalidOperationException("Save your untitled scene before menu setup.");
            EditorSceneManager.OpenScene(CookingPrototypeBuilder.ScenePath, OpenSceneMode.Single);
        }
        foreach (string path in new[] { ScenePath, CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty) throw new InvalidOperationException("Save " + path + " before menu setup.");
        }
        var settings = GlobalDataPrototypeSetup.GetOrCreateSettings(); EnsureColors(settings);
        EditorUtility.SetDirty(settings); AssetDatabase.SaveAssetIfDirty(settings);
        var previous = SceneManager.GetActiveScene();
        if (!File.Exists(ScenePath)) CreateMenu(settings);
        foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
        {
            if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { ApplyGameplayScene(scene); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        var scenes = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(ScenePath, true) };
        foreach (var scene in EditorBuildSettings.scenes) if (scene.path != ScenePath) scenes.Add(scene);
        if (!scenes.Exists(s => s.path == settings.GameplayScenePath)) scenes.Add(new EditorBuildSettingsScene(settings.GameplayScenePath, true));
        else scenes.Find(s => s.path == settings.GameplayScenePath).enabled = true;
        EditorBuildSettings.scenes = scenes.ToArray();
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();
    }
    public static void ApplyGameplayScene(Scene scene)
    {
        SessionSceneEntry entry = null;
        foreach (var root in scene.GetRootGameObjects()) if (entry == null) entry = root.GetComponentInChildren<SessionSceneEntry>(true);
        if (entry == null)
        {
            var go = new GameObject("Session Scene Entry"); SceneManager.MoveGameObjectToScene(go, scene);
            entry = go.AddComponent<SessionSceneEntry>();
        }
        var settings = GlobalDataPrototypeSetup.GetOrCreateSettings();
        entry.Configure(settings, true); EditorUtility.SetDirty(entry);
        foreach (var root in scene.GetRootGameObjects()) foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
        {
            var body = player.transform.Find("Visual")?.GetComponent<SpriteRenderer>();
            var weapon = player.transform.Find("FacingIndicator")?.GetComponent<SpriteRenderer>();
            if (body == null || weapon == null) throw new InvalidOperationException("Player appearance needs existing Visual and FacingIndicator.");
            body.transform.localScale = new Vector3(.7f, .7f, 1f);
            weapon.transform.localScale = Vector3.one * .22f;
            var loadout = player.GetComponent<PlayerLoadout>();
            if (loadout == null) { CharacterWeaponPrototypeSetup.BindPlayer(player.gameObject); loadout = player.GetComponent<PlayerLoadout>(); }
            var binding = new SerializedObject(loadout); binding.FindProperty("settings").objectReferenceValue = settings; binding.ApplyModifiedPropertiesWithoutUndo();
            var characterLabel = player.transform.Find("CharacterName");
            var weaponLabel = weapon.transform.Find("WeaponName");
            if (characterLabel != null) Object.DestroyImmediate(characterLabel.gameObject);
            if (weaponLabel != null) Object.DestroyImmediate(weaponLabel.gameObject);
            var appearance = player.GetComponent<PlayerAppearance>(); if (appearance == null) appearance = player.gameObject.AddComponent<PlayerAppearance>();
            appearance.Configure(body, weapon, player.GetComponent<PlayerHitFeedback>());
            if (loadout.Character != null) body.color = loadout.Character.displayColor;
            if (loadout.Weapon != null) weapon.color = loadout.Weapon.displayColor;
            foreach (var item in new Object[] { appearance, body, weapon, body.transform, weapon.transform }) EditorUtility.SetDirty(item);
        }
        foreach (var root in scene.GetRootGameObjects()) foreach (var hud in root.GetComponentsInChildren<PlayerStatusUI>(true))
            CharacterWeaponPrototypeSetup.ConfigureHUD(hud);
        PauseMenuPrototypeSetup.ApplyToScene(scene, entry, settings);
    }
    private static void CreateMenu(GlobalGameSettings settings)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive); SceneManager.SetActiveScene(scene);
        try
        {
            var camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -10); camera.GetComponent<Camera>().clearFlags = CameraClearFlags.SolidColor;
            camera.GetComponent<Camera>().backgroundColor = Ink; camera.GetComponent<Camera>().orthographic = true;
            var entry = new GameObject("Session Scene Entry").AddComponent<SessionSceneEntry>(); entry.Configure(settings, false);
            var global = new GameObject("Global Data").AddComponent<GlobalData>(); Set(global, "settings", settings);
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var root = new GameObject("MainMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup)); root.layer = 5;
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var background = Rect("Background", root.transform, 0, 0, 1280, 720); Stretch(background); background.gameObject.AddComponent<Image>().color = Ink;
            var home = Page("Home", root.transform); var selection = Page("Selection", root.transform); var options = Page("Settings", root.transform); var credits = Page("Credits", root.transform);
            Label("Title", home.transform, "MMFeast", 0, 150, 900, 120, 88).color = Gold;
            var homeStart = Button("Start", home.transform, "开始游戏", 0, -10, 320);
            var homeSettings = Button("Settings", home.transform, "设置", 0, -90, 320);
            var homeCredits = Button("Credits", home.transform, "制作团队", 0, -170, 320);
            Label("Title", selection.transform, "选择角色与武器", 0, 285, 1000, 60, 36);
            Label("CharactersTitle", selection.transform, "角色", -280, 220, 420, 42, 24);
            Label("WeaponsTitle", selection.transform, "武器", 280, 220, 420, 42, 24);
            var characterContent = ChoiceList("CharacterList", selection.transform, -280);
            var weaponContent = ChoiceList("WeaponList", selection.transform, 280);
            var characterPreview = Block("CharacterPreview", selection.transform, -100, -150, 46, 78);
            var weaponPreview = Block("WeaponPreview", selection.transform, 100, -150, 42, 42);
            var characterName = Label("CharacterName", selection.transform, "Cat", -100, -215, 320, 32, 22);
            var weaponName = Label("WeaponName", selection.transform, "Spatula", 160, -215, 320, 32, 22);
            var message = Label("Message", selection.transform, "", 0, -75, 1000, 40, 18); message.color = new Color(1f, .55f, .4f);
            var selectionBack = Button("Back", selection.transform, "返回", -190, -290, 240);
            var beginGame = Button("BeginGame", selection.transform, "开始游戏", 190, -290, 300);
            Label("Title", options.transform, "设置", 0, 200, 900, 70, 40);
            Label("VolumeLabel", options.transform, "主音量", 0, 65, 450, 50, 26);
            var slider = VolumeSlider(options.transform); var percent = Label("VolumePercent", options.transform, "100%", 0, -55, 450, 50, 24);
            var settingsBack = Button("Back", options.transform, "返回", 0, -220, 260);
            Label("Title", credits.transform, "制作团队", 0, 220, 900, 70, 40);
            var names = Label("Team", credits.transform, "Game design: Sangni Lin, Emily Cai\nGame art: Sangni Lin\nUI/UX: Keller Wang\nEngineering: Kenny Luo", 0, 0, 950, 240, 28); names.lineSpacing = 1.5f;
            var creditsBack = Button("Back", credits.transform, "返回", 0, -240, 260);
            var templates = Rect("Templates", root.transform, 0, 0, 1, 1); templates.gameObject.SetActive(false);
            var characterTemplate = Card("CharacterCard", templates, true); var weaponTemplate = Card("WeaponCard", templates, false);
            var overlay = Page("LoadingOverlay", root.transform); overlay.gameObject.AddComponent<Image>().color = new Color(.02f, .03f, .04f, .98f);
            Label("Loading", overlay.transform, "正在进入游戏…", 0, 0, 900, 100, 34);
            var controller = root.AddComponent<MainMenuController>();
            var refs = new Dictionary<string, Object> {
                {"settings",settings},{"sceneEntry",entry},{"navigation",root.GetComponent<CanvasGroup>()},
                {"home",home},{"selection",selection},{"options",options},{"credits",credits},{"loadingOverlay",overlay},
                {"homeStart",homeStart},{"homeSettings",homeSettings},{"homeCredits",homeCredits},
                {"selectionBack",selectionBack},{"settingsBack",settingsBack},{"creditsBack",creditsBack},{"beginGame",beginGame},
                {"characterContent",characterContent},{"weaponContent",weaponContent},{"characterTemplate",characterTemplate},{"weaponTemplate",weaponTemplate},
                {"characterPreview",characterPreview},{"weaponPreview",weaponPreview},{"characterName",characterName},{"weaponName",weaponName},
                {"selectionMessage",message},{"volumeText",percent},{"volumeSlider",slider}
            };
            foreach (var pair in refs) Set(controller, pair.Key, pair.Value);
            selection.gameObject.SetActive(false); options.gameObject.SetActive(false); credits.gameObject.SetActive(false);
            overlay.alpha = 0f; overlay.blocksRaycasts = false;
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }
    private static CanvasGroup Page(string name, Transform parent)
    { var rect = Rect(name, parent, 0, 0, 1280, 720); Stretch(rect); return rect.gameObject.AddComponent<CanvasGroup>(); }
    internal static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
    {
        var go = new GameObject(name, typeof(RectTransform)); go.layer = 5; var rect = (RectTransform)go.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f); rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height); return rect;
    }
    internal static void Stretch(RectTransform rect)
    { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
    internal static Text Label(string name, Transform parent, string value, float x, float y, float width, float height, int size)
    {
        var text = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Text>(); text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = size; text.color = Cream;
        text.alignment = TextAnchor.MiddleCenter; text.raycastTarget = false; return text;
    }
    private static Image Block(string name, Transform parent, float x, float y, float width, float height)
    { var image = Rect(name, parent, x, y, width, height).gameObject.AddComponent<Image>(); image.color = Gold; image.raycastTarget = false; return image; }
    internal static Button Button(string name, Transform parent, string label, float x, float y, float width)
    {
        var rect = Rect(name, parent, x, y, width, 60); var image = rect.gameObject.AddComponent<Image>(); image.color = new Color(.17f, .23f, .28f);
        var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
        var colors = button.colors; colors.normalColor = image.color; colors.highlightedColor = colors.selectedColor = new Color(.28f, .36f, .43f); colors.pressedColor = new Color(.65f, .4f, .12f); button.colors = colors;
        image.color = Color.white; // ColorBlock supplies the tint; do not multiply the dark color twice.
        Label("Label", rect, label, 0, 0, width - 24, 50, 25); return button;
    }
    private static RectTransform ChoiceList(string name, Transform parent, float x)
    {
        var root = Rect(name, parent, x, 75, 430, 230); root.gameObject.AddComponent<Image>().color = new Color(.08f, .12f, .16f);
        var scroll = root.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Rect("Viewport", root, 0, 0, 430, 230); Stretch(viewport); viewport.gameObject.AddComponent<RectMask2D>();
        var content = Rect("Content", viewport, 0, 0, 430, 0); content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1); content.pivot = new Vector2(.5f, 1); content.sizeDelta = Vector2.zero;
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.padding = new RectOffset(8, 8, 8, 8); layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandHeight = false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.viewport = viewport; scroll.content = content; return content;
    }
    private static MenuChoiceCard Card(string name, Transform parent, bool character)
    {
        var root = Rect(name, parent, 0, 0, 410, 96); root.gameObject.AddComponent<LayoutElement>().preferredHeight = 96;
        var border = root.gameObject.AddComponent<Image>(); border.color = new Color(.17f, .23f, .28f);
        var inner = Rect("Inner", root, 0, 0, 404, 90); Stretch(inner); inner.offsetMin = Vector2.one * 3; inner.offsetMax = Vector2.one * -3; inner.gameObject.AddComponent<Image>().color = new Color(.105f, .15f, .20f);
        var preview = Block("ColorPreview", root, -155, 0, character ? 34 : 38, character ? 62 : 38);
        var title = Label("Name", root, character ? "Character" : "Weapon", 40, 0, 270, 70, 24); title.alignment = TextAnchor.MiddleLeft;
        var button = root.gameObject.AddComponent<Button>(); button.targetGraphic = border; button.transition = Selectable.Transition.None;
        var card = root.gameObject.AddComponent<MenuChoiceCard>(); card.Configure(button, preview, border, title); return card;
    }
    internal static Slider VolumeSlider(Transform parent)
    {
        var root = Rect("VolumeSlider", parent, 0, 0, 480, 32); root.gameObject.AddComponent<Image>().color = Color.clear;
        Block("Track", root, 0, 0, 480, 10).color = new Color(.25f, .32f, .38f);
        var fillArea = Rect("FillArea", root, 0, 0, 480, 10);
        var fill = Block("Fill", fillArea, 0, 0, 480, 10); Stretch((RectTransform)fill.transform);
        var handleArea = Rect("HandleArea", root, 0, 0, 480, 30);
        var handle = Block("Handle", handleArea, 0, 0, 22, 30); handle.raycastTarget = true;
        ((RectTransform)handle.transform).sizeDelta = new Vector2(22, 0);
        var slider = root.gameObject.AddComponent<Slider>(); slider.fillRect = (RectTransform)fill.transform; slider.handleRect = (RectTransform)handle.transform;
        slider.targetGraphic = handle; slider.minValue = 0; slider.maxValue = 1; slider.value = 1; return slider;
    }
    private static void Set(Object target, string field, Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
}
