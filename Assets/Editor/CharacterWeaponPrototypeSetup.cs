using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CharacterWeaponPrototypeSetup
{
    public const string Folder = "Assets/Prototype/Loadouts";
    public static void EnsureDefinitions(GlobalGameSettings settings)
    {
        var serialized = new SerializedObject(settings);
        if (serialized.FindProperty("loadoutsMigrated").boolValue) { MainMenuPrototypeSetup.EnsureColors(settings); return; }
        if (!Directory.Exists(Folder)) { Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); }
        var fan = GetOrCreate<FanAttackDefinition>("SpatulaFan", out _);
        var hammerFan = GetOrCreate<FanAttackDefinition>("MalletFan", out bool newHammerFan);
        if (newHammerFan) hammerFan.angle = 80f;
        var knockback = GetOrCreate<KnockbackEffectDefinition>("MalletKnockback", out _);
        var song = GetOrCreate<BirdSongDefinition>("BirdSong", out _);
        var spatula = GetOrCreate<WeaponDefinition>("Spatula", out bool newSpatula);
        var mallet = GetOrCreate<WeaponDefinition>("MeatMallet", out bool newMallet);
        if (newSpatula) ConfigureWeapon(spatula, "spatula", "Spatula", serialized, fan, null);
        if (newMallet) ConfigureWeapon(mallet, "meat_mallet", "Meat Mallet", serialized, hammerFan, knockback);
        var cat = GetOrCreate<CharacterDefinition>("Cat", out bool newCat);
        var bird = GetOrCreate<CharacterDefinition>("Bird", out bool newBird);
        if (newCat) ConfigureCharacter(cat, "cat", "Cat", serialized, spatula, null);
        if (newBird) ConfigureCharacter(bird, "bird", "Bird", serialized, spatula, song);
        var characters = serialized.FindProperty("characters"); characters.arraySize = 2;
        characters.GetArrayElementAtIndex(0).objectReferenceValue = cat; characters.GetArrayElementAtIndex(1).objectReferenceValue = bird;
        var weapons = serialized.FindProperty("weapons"); weapons.arraySize = 2;
        weapons.GetArrayElementAtIndex(0).objectReferenceValue = spatula; weapons.GetArrayElementAtIndex(1).objectReferenceValue = mallet;
        serialized.FindProperty("defaultCharacter").objectReferenceValue = cat;
        serialized.FindProperty("loadoutsMigrated").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        MainMenuPrototypeSetup.EnsureColors(settings);
        foreach (var asset in new UnityEngine.Object[] { fan, hammerFan, knockback, song, spatula, mallet, cat, bird, settings })
        { EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset); }
    }
    private static T GetOrCreate<T>(string name, out bool created) where T : ScriptableObject
    {
        string path = Folder + "/" + name + ".asset";
        var asset = AssetDatabase.LoadAssetAtPath<T>(path); created = asset == null;
        if (created) { asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); }
        return asset;
    }
    private static void ConfigureCharacter(CharacterDefinition character, string id, string label, SerializedObject old, WeaponDefinition weapon, CharacterTalentDefinition talent)
    {
        character.id = id; character.displayName = label; character.startingWeapon = weapon; character.talent = talent;
        character.displayColor = id == "bird" ? new Color(.72f, .46f, .94f) : new Color(.15f, .7f, 1f);
        var data = new SerializedObject(character);
        string[] fields = { "playerMaxHP:maxHP", "playerMaxSAN:maxSAN", "playerMoveSpeed:moveSpeed", "maxStamina:maxStamina", "initialStamina:initialStamina", "dashDistance:dashDistance", "dashDuration:dashDuration", "dashInvulnerabilityDuration:dashInvulnerabilityDuration", "dashStaminaCost:dashStaminaCost", "staminaRecoveryDelay:staminaRecoveryDelay", "staminaRecoveryPerSecond:staminaRecoveryPerSecond" };
        foreach (string field in fields) { var names = field.Split(':'); data.FindProperty(names[1]).floatValue = old.FindProperty(names[0]).floatValue; }
        data.ApplyModifiedPropertiesWithoutUndo(); character.Validate();
    }
    private static void ConfigureWeapon(WeaponDefinition weapon, string id, string label, SerializedObject old, FanAttackDefinition attack, WeaponHitEffectDefinition effect)
    {
        weapon.id = id; weapon.displayName = label; weapon.attack = attack; weapon.hitEffect = effect;
        weapon.displayColor = id == "meat_mallet" ? new Color(1f, .35f, .35f) : new Color(1f, .72f, .23f);
        weapon.damage = old.FindProperty("playerAttackDamage").floatValue;
        weapon.range = old.FindProperty("playerAttackRange").floatValue;
        weapon.cooldown = old.FindProperty("playerAttackCooldown").floatValue;
        weapon.hitShakeStrength = old.FindProperty("playerAttackShakeStrength").floatValue;
        weapon.hitShakeDuration = old.FindProperty("playerAttackShakeDuration").floatValue;
        weapon.Validate();
    }
    public static bool BindPlayer(GameObject root)
    {
        bool changed = false;
        foreach (var player in root.GetComponentsInChildren<PlayerController>(true))
            if (player.GetComponent<PlayerLoadout>() == null) { player.gameObject.AddComponent<PlayerLoadout>(); changed = true; }
        return changed;
    }
    public static void ConfigureHUD(PlayerStatusUI hud)
    {
        var canvas = hud.GetComponentInParent<Canvas>();
        var label = canvas.transform.Find("LoadoutLabel") ?? hud.transform.Find("LoadoutLabel");
        if (label == null)
        {
            var go = new GameObject("LoadoutLabel", typeof(RectTransform), typeof(Text));
            label = go.transform;
        }
        label.SetParent(canvas.transform, false);
        var rect = (RectTransform)label;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-58f, 58f); rect.sizeDelta = new Vector2(420f, 64f);
        var text = label.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20; text.color = Color.white; text.raycastTarget = false;
        text.alignment = TextAnchor.MiddleRight; text.text = "Character: --\nWeapon: --";
        EditorUtility.SetDirty(rect); EditorUtility.SetDirty(text);
        var settings = GlobalDataPrototypeSetup.GetOrCreateSettings();
        var character = Swatch(canvas.transform, "CharacterColor", 102f, settings.DefaultCharacter != null ? settings.DefaultCharacter.displayColor : Color.clear);
        var weapon = Swatch(canvas.transform, "WeaponColor", 78f, settings.DefaultWeapon != null ? settings.DefaultWeapon.displayColor : Color.clear);
        hud.ConfigureLoadout(label.GetComponent<Text>(), character, weapon); EditorUtility.SetDirty(hud);
    }
    private static Image Swatch(Transform parent, string name, float y, Color color)
    {
        var existing = parent.Find(name);
        var image = existing != null ? existing.GetComponent<Image>() : new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        var rect = (RectTransform)image.transform; rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f); rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = new Vector2(-32f, y); rect.sizeDelta = new Vector2(22f, 22f);
        image.color = color; image.raycastTarget = false;
        EditorUtility.SetDirty(rect); EditorUtility.SetDirty(image); return image;
    }
    [MenuItem("MMFeast/Set Up Characters and Weapons")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before setup.");
        foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
        { var open = SceneManager.GetSceneByPath(path); if (open.IsValid() && open.isLoaded && open.isDirty) throw new InvalidOperationException("Save " + path + " before setup."); }
        var settings = GlobalDataPrototypeSetup.GetOrCreateSettings(); EnsureDefinitions(settings);
        var previous = SceneManager.GetActiveScene();
        foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
        {
            if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path); bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                foreach (var root in scene.GetRootGameObjects())
                {
                    GlobalDataPrototypeSetup.BindTree(root);
                    foreach (var hud in root.GetComponentsInChildren<PlayerStatusUI>(true)) ConfigureHUD(hud);
                }
                MainMenuPrototypeSetup.ApplyGameplayScene(scene);
                EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
            }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();
    }
}
