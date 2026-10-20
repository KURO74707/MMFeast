using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class FoodCannonPrototypeBuilder
{
    private const string Root = "Assets/Prototype/Prefabs/";
    [MenuItem("MMFeast/Add Food Cannon And Customers")]
    public static void Upgrade()
    {
        FoodTruckPrototypeBuilder.UpgradeCombatScene();
        Material material = AssetDatabase.LoadAssetAtPath<Material>(Root + "FoodTrajectory.mat");
        if (material == null)
        {
            material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, Root + "FoodTrajectory.mat");
        }
        Sprite square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
        GameObject projectile = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "FoodProjectile.prefab");
        if (projectile == null)
        {
            GameObject obj = new GameObject("FoodProjectile");
            Rigidbody body = obj.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = 0f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            obj.AddComponent<SphereCollider>().radius = 0.12f;
            Transform visual = CreateVisual("Visual", obj.transform, square, new Color(1f, 0.65f, 0.2f), new Vector3(0.3f, 0.3f, 1f), 15);
            obj.AddComponent<FoodProjectile>().Configure(visual, material);
            GlobalDataPrototypeSetup.BindTree(obj);
            projectile = PrefabUtility.SaveAsPrefabAsset(obj, Root + "FoodProjectile.prefab");
            Object.DestroyImmediate(obj);
        }
        GameObject customer = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "Customer.prefab");
        if (customer == null)
        {
            GameObject obj = new GameObject("Customer");
            obj.layer = LayerMask.NameToLayer("PrototypeEnemy");
            SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = square;
            renderer.color = new Color(1f, 0.75f, 0.15f);
            renderer.sortingOrder = 1;
            obj.transform.localScale = Vector3.one * 0.65f;
            obj.AddComponent<BoxCollider2D>();
            obj.AddComponent<Rigidbody2D>();
            obj.AddComponent<Customer>();
            GlobalDataPrototypeSetup.BindTree(obj);
            customer = PrefabUtility.SaveAsPrefabAsset(obj, Root + "Customer.prefab");
            Object.DestroyImmediate(obj);
        }
        UpgradeCustomerMovement();
        GameObject cannon = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "FoodCannon.prefab");
        if (cannon == null)
        {
            GameObject obj = new GameObject("FoodCannon");
            CreateVisual("CannonBase", obj.transform, square, new Color(0.5f, 0.6f, 0.8f), new Vector3(0.8f, 0.8f, 1f), 4);
            Transform barrel = new GameObject("CannonBarrel").transform;
            barrel.SetParent(obj.transform, false);
            Transform barrelVisual = CreateVisual("BarrelVisual", barrel, square, Color.gray, new Vector3(0.9f, 0.25f, 1f), 5);
            barrelVisual.localPosition = Vector3.right * 0.35f;
            Transform muzzle = new GameObject("MuzzlePoint").transform;
            muzzle.SetParent(barrel, false);
            muzzle.localPosition = new Vector3(0.8f, 0f, -0.2f);
            LineRenderer path = obj.AddComponent<LineRenderer>();
            ConfigureLine(path, material, Color.cyan, 0.04f);
            path.enabled = false;
            Transform target = new GameObject("TargetPoint").transform;
            target.SetParent(obj.transform, false);
            CreateVisual("LandingDot", target, square, Color.yellow, Vector3.one * 0.18f, 14);
            LineRenderer ring = target.gameObject.AddComponent<LineRenderer>();
            ConfigureLine(ring, material, Color.yellow, 0.04f);
            target.gameObject.SetActive(false);
            obj.AddComponent<FoodCannon>().Configure(barrel, muzzle, projectile.GetComponent<FoodProjectile>(), path, target, ring);
            GlobalDataPrototypeSetup.BindTree(obj);
            cannon = PrefabUtility.SaveAsPrefabAsset(obj, Root + "FoodCannon.prefab");
            Object.DestroyImmediate(obj);
        }
        UpgradeScene(CombatPrototypeBuilder.ScenePath, cannon, customer);
        UpgradeScene(CookingPrototypeBuilder.ScenePath, cannon, customer);
        AssetDatabase.SaveAssets();
        Debug.Log("Food Cannon and Customers ready in CombatCollection and CookingCollection.");
    }

    public static void UpgradeCustomerMovement()
    {
        const string path = Root + "Customer.prefab";
        GameObject customer = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Rigidbody2D body = customer.GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Dynamic;
            body.gravityScale = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            if (customer.GetComponent<EnemySimpleAI>() == null) customer.AddComponent<EnemySimpleAI>();
            GlobalDataPrototypeSetup.BindTree(customer);
            PrefabUtility.SaveAsPrefabAsset(customer, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(customer); }
    }

    private static void UpgradeScene(string path, GameObject cannonPrefab, GameObject customerPrefab)
    {
        Scene previous = SceneManager.GetActiveScene();
        Scene scene = SceneManager.GetSceneByPath(path);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;
        if (openedHere) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        if (scene.isDirty) throw new System.InvalidOperationException("Save " + path + " before upgrading.");
        SceneManager.SetActiveScene(scene);
        try
        {
            FoodTruckInteriorTrigger interior = null;
            FoodCannon cannon = null;
            Camera camera = null;
            GameObject ground = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (interior == null) interior = root.GetComponentInChildren<FoodTruckInteriorTrigger>(true);
                if (cannon == null) cannon = root.GetComponentInChildren<FoodCannon>(true);
                if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
                if (root.name == "FoodProjectileGround") ground = root;
                foreach (EnemySpawner spawner in root.GetComponentsInChildren<EnemySpawner>(true))
                {
                    spawner.ConfigureCustomers(customerPrefab.GetComponent<Customer>());
                    EditorUtility.SetDirty(spawner);
                }
            }
            if (interior == null) throw new System.InvalidOperationException("Food truck interior not found in " + path);
            if (cannon == null)
            {
                cannon = ((GameObject)PrefabUtility.InstantiatePrefab(cannonPrefab, scene)).GetComponent<FoodCannon>();
                cannon.transform.SetParent(interior.transform.parent, false);
                cannon.transform.localPosition = new Vector3(0f, 1.2f, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(cannon.transform);
            }
            cannon.SetSceneReferences(interior, camera);
            EditorUtility.SetDirty(cannon);
            PrefabUtility.RecordPrefabInstancePropertyModifications(cannon);
            if (ground == null)
            {
                ground = new GameObject("FoodProjectileGround");
                SceneManager.MoveGameObjectToScene(ground, scene);
                ground.transform.position = new Vector3(0f, 0f, 0.5f);
                ground.AddComponent<BoxCollider>().size = new Vector3(100f, 100f, 1f);
            }
            GlobalDataPrototypeSetup.BindPrefabs();
            GlobalDataPrototypeSetup.ApplyToScene(scene);
            EditorSceneManager.SaveScene(scene);
        }
        finally
        {
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        }
    }

    private static Transform CreateVisual(string name, Transform parent, Sprite sprite, Color color, Vector3 scale, int order)
    {
        GameObject visual = new GameObject(name);
        visual.transform.SetParent(parent, false);
        visual.transform.localScale = scale;
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        return visual.transform;
    }
    private static void ConfigureLine(LineRenderer line, Material material, Color color, float width)
    {
        line.sharedMaterial = material;
        line.useWorldSpace = true;
        line.startColor = line.endColor = color;
        line.startWidth = line.endWidth = width;
        line.sortingOrder = 13;
    }
}

