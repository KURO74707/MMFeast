using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CookingPrototypeBuilder
{
    public const string ScenePath = "Assets/Prototype/CookingCollection.unity";
    private const string PrefabPath = "Assets/Prototype/Prefabs/CookingStation.prefab";

    [MenuItem("MMFeast/Create Cooking Prototype")]
    public static void Create()
    {
        if (File.Exists(ScenePath)) { Debug.Log("Cooking prototype already exists: " + ScenePath); return; }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
            { Debug.LogWarning("Save your untitled scene first, then use MMFeast > Create Cooking Prototype."); return; }
        }
        if (!File.Exists(CombatPrototypeBuilder.ScenePath)) CombatPrototypeBuilder.Create();
        if (!AssetDatabase.CopyAsset(CombatPrototypeBuilder.ScenePath, ScenePath))
            throw new System.InvalidOperationException("Could not copy the combat scene.");

        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        try
        {
            // Explicitly expose the new Meat settings on the existing prefab.
            GameObject meat = PrefabUtility.LoadPrefabContents("Assets/Prototype/Prefabs/Meat.prefab");
            try
            {
                SerializedObject serialized = new SerializedObject(meat.GetComponent<IngredientPickup>());
                serialized.FindProperty("itemRenderer").objectReferenceValue = meat.GetComponent<SpriteRenderer>();
                serialized.ApplyModifiedPropertiesWithoutUndo();
                GlobalDataPrototypeSetup.BindTree(meat);
                PrefabUtility.SaveAsPrefabAsset(meat, "Assets/Prototype/Prefabs/Meat.prefab");
            }
            finally { PrefabUtility.UnloadPrefabContents(meat); }

            GameObject station;
            if (File.Exists(PrefabPath)) station = (GameObject)PrefabUtility.InstantiatePrefab(
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            else
            {
                station = new GameObject("CookingStation");
                SceneManager.MoveGameObjectToScene(station, scene);
                station.AddComponent<BoxCollider2D>().size = new Vector2(1.8f, 0.9f);
                GameObject visual = new GameObject("StationVisual");
                visual.transform.SetParent(station.transform, false);
                visual.transform.localScale = new Vector3(1.8f, 0.9f, 1f);
                SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
                renderer.color = new Color(0.3f, 0.55f, 0.55f);
                renderer.sortingOrder = 0;
                Transform point = new GameObject("CookingPoint").transform;
                point.SetParent(station.transform, false);
                point.localPosition = new Vector3(0f, 0.25f, 0f);
                station.AddComponent<CookingStation>().Configure(point);
                GlobalDataPrototypeSetup.BindTree(station);
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(station, PrefabPath);
                Object.DestroyImmediate(station);
                station = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            }
            station.transform.position = new Vector3(3f, 2f, 0f);
            FoodTruckPrototypeBuilder.AddToScene(scene);
            GlobalDataPrototypeSetup.BindPrefabs();
            GlobalDataPrototypeSetup.ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("Cooking prototype ready: " + ScenePath);
        }
        finally
        {
            EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }
}

