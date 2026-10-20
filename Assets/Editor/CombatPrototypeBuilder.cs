using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CombatPrototypeBuilder
{
    private const string Root = "Assets/Prototype";
    public const string ScenePath = Root + "/CombatCollection.unity";
    private static Scene buildScene;

    static CombatPrototypeBuilder()
    {
        EditorApplication.delayCall += CreateOnImport;
    }

    private static void CreateOnImport()
    {
        if (Application.isBatchMode || File.Exists(ScenePath)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += CreateOnImport;
            return;
        }
        Create();
    }

    [MenuItem("MMFeast/Create Combat Collection Prototype")]
    public static void Create()
    {
        if (File.Exists(ScenePath))
        {
            Debug.Log("Prototype already exists: " + ScenePath);
            return;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
            {
                Debug.LogWarning("Save your untitled scene first, then use MMFeast > Create Combat Collection Prototype.");
                return;
            }
        }
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(
            "Assets/Settings/InputSystem_Actions.inputactions");
        if (actions == null) throw new System.InvalidOperationException("Existing input asset was not found.");
        Directory.CreateDirectory(Root + "/Prefabs");
        AssetDatabase.Refresh();

        // Build in an additive scene so any currently open scene is left intact.
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        buildScene = scene;
        SceneManager.SetActiveScene(scene);
        Sprite square = CreateSquareSprite();

        GameObject meat = CreateBody("Meat", square, new Color(0.9f, 0.35f, 0.5f), 0.35f);
        meat.GetComponent<Collider2D>().isTrigger = true;
        meat.AddComponent<IngredientPickup>();
        GlobalDataPrototypeSetup.BindTree(meat);
        GameObject meatPrefab = PrefabUtility.SaveAsPrefabAsset(meat, Root + "/Prefabs/Meat.prefab");
        Object.DestroyImmediate(meat);

        GameObject enemy = CreateBody("Enemy", square, new Color(0.9f, 0.2f, 0.2f), 0.7f);
        enemy.AddComponent<EnemyHealth>().Configure(meatPrefab.GetComponent<IngredientPickup>());
        enemy.AddComponent<EnemySimpleAI>();
        GlobalDataPrototypeSetup.BindTree(enemy);
        GameObject enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemy, Root + "/Prefabs/Enemy.prefab");
        Object.DestroyImmediate(enemy);

        GameObject player = CreateBody("Player", square, new Color(0.15f, 0.7f, 1f), 0.7f);
        // Keep player scale at one so pickup range and hold offsets are in world units.
        player.transform.localScale = Vector3.one;
        // Use a scaled visual child, keeping the physics root at unit scale.
        Object.DestroyImmediate(player.GetComponent<SpriteRenderer>());
        GameObject visual = NewObject("Visual");
        visual.transform.SetParent(player.transform, false);
        visual.transform.localScale = Vector3.one * 0.7f;
        SpriteRenderer playerSprite = visual.AddComponent<SpriteRenderer>();
        playerSprite.sprite = square;
        playerSprite.color = new Color(0.15f, 0.7f, 1f);
        playerSprite.sortingOrder = 1;
        player.GetComponent<BoxCollider2D>().size = Vector2.one * 0.7f;
        PlayerController controller = player.AddComponent<PlayerController>();
        controller.Configure(GlobalDataPrototypeSetup.GetOrCreateSettings());
        player.AddComponent<PlayerCombat>();
        Transform hold = NewObject("CarryPoint").transform;
        hold.SetParent(player.transform, false);
        hold.localPosition = new Vector3(0f, 0.8f, 0f);
        player.AddComponent<PlayerCarry>().Configure(hold);
        GameObject direction = NewObject("FacingIndicator");
        direction.transform.SetParent(player.transform, false);
        direction.transform.localPosition = Vector3.right * 0.55f;
        direction.transform.localScale = Vector3.one * 0.15f;
        SpriteRenderer indicator = direction.AddComponent<SpriteRenderer>();
        indicator.sprite = square;
        indicator.color = Color.yellow;
        indicator.sortingOrder = 2;
        controller.SetFacingIndicator(direction.transform);

        GameObject cameraObject = NewObject("Main Camera", typeof(Camera));
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.GetComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 8f;
        camera.backgroundColor = new Color(0.06f, 0.08f, 0.12f);
        camera.clearFlags = CameraClearFlags.SolidColor;

        Transform[] points = new Transform[4];
        Vector3[] positions = { new Vector3(-9, 0), new Vector3(9, 0), new Vector3(0, 6), new Vector3(0, -6) };
        GameObject spawner = NewObject("EnemySpawner");
        for (int i = 0; i < points.Length; i++)
        {
            points[i] = NewObject("SpawnPoint_" + (i + 1)).transform;
            points[i].SetParent(spawner.transform);
            points[i].position = positions[i];
        }
        spawner.AddComponent<EnemySpawner>().Configure(enemyPrefab.GetComponent<EnemySimpleAI>(), player.transform, points);
        GlobalDataPrototypeSetup.BindPrefabs();
        GlobalDataPrototypeSetup.ApplyToScene(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        AssetDatabase.SaveAssets();
        Debug.Log("Combat prototype created. Open " + ScenePath + " and press Play.");
    }

    private static GameObject CreateBody(string name, Sprite sprite, Color color, float size)
    {
        GameObject obj = NewObject(name);
        obj.transform.localScale = Vector3.one * size;
        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = name == "Meat" ? 3 : 1;
        obj.AddComponent<BoxCollider2D>();
        Rigidbody2D body = obj.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeRotation;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        return obj;
    }

    private static GameObject NewObject(string name, params System.Type[] components)
    {
        GameObject obj = new GameObject(name, components);
        SceneManager.MoveGameObjectToScene(obj, buildScene);
        return obj;
    }

    private static Sprite CreateSquareSprite()
    {
        string path = Root + "/PrototypeSquare.png";
        Texture2D texture = new Texture2D(16, 16);
        Color[] pixels = new Color[256];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        texture.SetPixels(pixels);
        texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spritePixelsPerUnit = 16;
        importer.filterMode = FilterMode.Point;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}

