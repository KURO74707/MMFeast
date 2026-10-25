using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class GlobalDataPrototypeSetup
{
    public const string SettingsPath = "Assets/Prototype/GlobalGameSettings.asset";

    public static GlobalGameSettings GetOrCreateSettings()
    {
        var settings = AssetDatabase.LoadAssetAtPath<GlobalGameSettings>(SettingsPath);
        if (settings != null) { CharacterWeaponPrototypeSetup.EnsureDefinitions(settings); return settings; }
        Directory.CreateDirectory("Assets/Prototype");
        AssetDatabase.Refresh();
        settings = ScriptableObject.CreateInstance<GlobalGameSettings>();
        var inputs = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Settings/InputSystem_Actions.inputactions");
        if (inputs == null) throw new InvalidOperationException("Existing movement input asset was not found.");
        settings.CopyMoveBindingsFrom(inputs);
        AssetDatabase.CreateAsset(settings, SettingsPath);
        CharacterWeaponPrototypeSetup.EnsureDefinitions(settings);
        return settings;
    }

    // Called by existing builders before saving prefabs or scenes.
    public static bool BindTree(GameObject root)
    {
        var settings = GetOrCreateSettings();
        bool changed = CharacterWeaponPrototypeSetup.BindPlayer(root);
        changed |= CustomerWaitingAreaPrototypeSetup.BindMounts(root);
        changed |= CustomerWaitingAreaPrototypeSetup.BindAreas(root);
        changed |= KitchenLayoutPrototypeSetup.BindComponents(root);
        changed |= CombatFeedbackPrototypeSetup.BindEntityVisuals(root);
        changed |= FoodTruckHealthPrototypeSetup.BindTruck(root);
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            if (component is Customer customer)
            {
                var customerFields = new SerializedObject(customer);
                var ingredient = customerFields.FindProperty("ingredientPrefab");
                if (ingredient.objectReferenceValue == null)
                {
                    var meat = AssetDatabase.LoadAssetAtPath<IngredientPickup>(CombatStatusPrototypeSetup.CustomerMeatPath);
                    if (meat != null)
                    {
                        ingredient.objectReferenceValue = meat;
                        customerFields.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(customer);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(customer);
                        changed = true;
                    }
                }
            }
            var serialized = new SerializedObject(component);
            var property = serialized.FindProperty("settings");
            if (property == null || property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (property.objectReferenceValue == settings) continue;
            property.objectReferenceValue = settings;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(component);
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            changed = true;
        }
        return changed;
    }

    public static void BindPrefabs()
    {
        CombatStatusPrototypeSetup.EnsureIngredients();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prototype/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var root = PrefabUtility.LoadPrefabContents(path);
            try { if (BindTree(root)) PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }

    public static void ApplyToScene(Scene scene)
    {
        GlobalData panel = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<GlobalData>(true))
            {
                if (panel != null) throw new InvalidOperationException("Scene contains multiple Global Data panels.");
                panel = candidate;
            }
            BindTree(root);
        }
        if (panel == null)
        {
            var root = new GameObject("Global Data");
            SceneManager.MoveGameObjectToScene(root, scene);
            root.AddComponent<GlobalData>();
            BindTree(root);
        }
        CombatStatusPrototypeSetup.ApplyToScene(scene);
        CombatFeedbackPrototypeSetup.ApplyToScene(scene);
        FoodTruckHealthPrototypeSetup.ApplyToScene(scene);
        KitchenLayoutPrototypeSetup.ApplyToScene(scene);
        TruckShopPrototypeSetup.ApplyToScene(scene);
        ItemShopPrototypeSetup.ApplyToScene(scene);
        foreach (var root in scene.GetRootGameObjects()) BindTree(root);
        MainMenuPrototypeSetup.ApplyGameplayScene(scene);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    [MenuItem("MMFeast/Set Up Global Data")]
    public static void Upgrade()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode before migrating Global Data.");
        // Validate all open prototype scenes before making any asset changes.
        string[] paths = { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath };
        foreach (string path in paths)
        {
            var scene = SceneManager.GetSceneByPath(path);
            if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save " + path + " before setting up Global Data.");
        }
        BindPrefabs();
        var settings = GetOrCreateSettings();
        settings.Validate();
        EditorUtility.SetDirty(settings);
        var previous = SceneManager.GetActiveScene();
        foreach (string path in paths)
        {
            if (!File.Exists(path)) continue;
            var scene = SceneManager.GetSceneByPath(path);
            bool openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try { ApplyToScene(scene); EditorSceneManager.SaveScene(scene); }
            finally { if (openedHere) EditorSceneManager.CloseScene(scene, true); }
        }
        if (previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();
        Debug.Log("Global Data is linked to one shared settings asset in both prototype scenes.");
    }
}
