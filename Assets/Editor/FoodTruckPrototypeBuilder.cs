using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FoodTruckPrototypeBuilder
{
    private const string TruckPrefabPath = "Assets/Prototype/Prefabs/FoodTruck.prefab";

    [MenuItem("MMFeast/Add Food Truck Access To Cooking Prototype")]
    public static void Upgrade()
    {
        UpgradeScene(CookingPrototypeBuilder.ScenePath);
    }

    [MenuItem("MMFeast/Upgrade Combat Scene With Truck And Cooking")]
    public static void UpgradeCombatScene()
    {
        UpgradeScene(CombatPrototypeBuilder.ScenePath);
        var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        scenes.Add(new EditorBuildSettingsScene(CombatPrototypeBuilder.ScenePath, true));
        foreach (EditorBuildSettingsScene existing in EditorBuildSettings.scenes)
            if (existing.path != CombatPrototypeBuilder.ScenePath) scenes.Add(existing);
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void UpgradeScene(string path)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (scene.isDirty)
        {
            Debug.LogWarning("Save the cooking scene before adding food truck access.");
            return;
        }
        SceneManager.SetActiveScene(scene);
        try
        {
            bool hasStation = false;
            foreach (GameObject root in scene.GetRootGameObjects())
                if (root.GetComponentInChildren<CookingStation>(true) != null) hasStation = true;
            if (!hasStation)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Prefabs/CookingStation.prefab");
                if (prefab == null) throw new System.InvalidOperationException("CookingStation prefab was not found.");
                PrefabUtility.InstantiatePrefab(prefab, scene);
            }
            AddToScene(scene);
            GlobalDataPrototypeSetup.BindPrefabs();
            GlobalDataPrototypeSetup.ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Food truck cooking access ready: " + path);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    public static void AddToScene(Scene scene)
    {
        int enemyLayer = EnsureEnemyLayer();
        ConfigureEnemyPrefab(enemyLayer);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefabPath) != null) ConfigureTruckPrefab(enemyLayer);
        CookingStation station = null;
        FoodTruckInteriorTrigger interior = null;
        Camera camera = null;
        PlayerController player = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CookingStation candidate in root.GetComponentsInChildren<CookingStation>(true))
            {
                if (station != null) throw new System.InvalidOperationException("Prototype expects one CookingStation.");
                station = candidate;
            }
            if (interior == null) interior = root.GetComponentInChildren<FoodTruckInteriorTrigger>(true);
            if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
            if (player == null) player = root.GetComponentInChildren<PlayerController>(true);
            foreach (EnemySimpleAI enemy in root.GetComponentsInChildren<EnemySimpleAI>(true)) SetLayer(enemy.gameObject, enemyLayer);
        }
        if (station == null) throw new System.InvalidOperationException("CookingStation was not found.");
        if (interior == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefabPath);
            if (prefab == null)
            {
                GameObject truck = new GameObject("FoodTruck");
                SceneManager.MoveGameObjectToScene(truck, scene);
                GameObject floor = new GameObject("InteriorFloor");
                floor.transform.SetParent(truck.transform, false);
                floor.transform.localScale = new Vector3(6f, 4f, 1f);
                SpriteRenderer renderer = floor.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
                renderer.color = new Color(0.16f, 0.22f, 0.28f);
                renderer.sortingOrder = -5;
                GameObject trigger = new GameObject("FoodTruckInteriorTrigger");
                trigger.transform.SetParent(truck.transform, false);
                BoxCollider2D collider = trigger.AddComponent<BoxCollider2D>();
                collider.isTrigger = true;
                collider.size = new Vector2(6f, 4f);
                trigger.AddComponent<FoodTruckInteriorTrigger>();
                GlobalDataPrototypeSetup.BindTree(truck);
                prefab = PrefabUtility.SaveAsPrefabAsset(truck, TruckPrefabPath);
                Object.DestroyImmediate(truck);
                ConfigureTruckPrefab(enemyLayer);
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TruckPrefabPath);
            }
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            interior = instance.GetComponentInChildren<FoodTruckInteriorTrigger>();
        }
        Vector3 center = camera != null ? new Vector3(camera.transform.position.x, camera.transform.position.y, 0f) : Vector3.zero;
        Transform truckRoot = interior.transform.parent;
        truckRoot.position = center;
        PrefabUtility.RecordPrefabInstancePropertyModifications(truckRoot);
        station.transform.position = center + Vector3.down * 1.2f;
        PrefabUtility.RecordPrefabInstancePropertyModifications(station.transform);
        // Begin outside the truck so entry is observable immediately.
        if (player != null) player.transform.position = center + Vector3.down * 4f;
        station.SetTruckInterior(interior);
        EditorUtility.SetDirty(station);
        PrefabUtility.RecordPrefabInstancePropertyModifications(station);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static int EnsureEnemyLayer()
    {
        const string name = "PrototypeEnemy";
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;
        SerializedObject settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = settings.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;
            layer.stringValue = name;
            settings.ApplyModifiedPropertiesWithoutUndo();
            return i;
        }
        throw new System.InvalidOperationException("No free layer is available for PrototypeEnemy.");
    }

    private static void ConfigureEnemyPrefab(int layer)
    {
        const string path = "Assets/Prototype/Prefabs/Enemy.prefab";
        GameObject enemy = PrefabUtility.LoadPrefabContents(path);
        try
        {
            SetLayer(enemy, layer);
            enemy.GetComponent<Rigidbody2D>().collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            GlobalDataPrototypeSetup.BindTree(enemy);
            PrefabUtility.SaveAsPrefabAsset(enemy, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(enemy); }
    }

    private static void SetLayer(GameObject obj, int layer)
    {
        obj.layer = layer;
        foreach (Transform child in obj.transform) SetLayer(child.gameObject, layer);
    }

    private static void ConfigureTruckPrefab(int enemyLayer)
    {
        GameObject truck = PrefabUtility.LoadPrefabContents(TruckPrefabPath);
        try
        {
            FoodTruckInteriorTrigger interior = truck.GetComponentInChildren<FoodTruckInteriorTrigger>(true);
            BoxCollider2D volume = interior.GetComponent<BoxCollider2D>();
            Transform barrierTransform = truck.transform.Find("EnemyBarrier");
            if (barrierTransform == null)
            {
                barrierTransform = new GameObject("EnemyBarrier", typeof(BoxCollider2D)).transform;
                barrierTransform.SetParent(truck.transform, false);
            }
            barrierTransform.localPosition = interior.transform.localPosition;
            barrierTransform.localScale = interior.transform.localScale;
            BoxCollider2D barrier = barrierTransform.GetComponent<BoxCollider2D>();
            barrier.isTrigger = false;
            barrier.offset = volume.offset;
            barrier.size = volume.size + Vector2.one * 0.1f;
            int enemyMask = 1 << enemyLayer;
            barrier.includeLayers = enemyMask;
            barrier.excludeLayers = ~enemyMask;
            barrier.layerOverridePriority = 100;
            GlobalDataPrototypeSetup.BindTree(truck);
            PrefabUtility.SaveAsPrefabAsset(truck, TruckPrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(truck); }
    }
}

