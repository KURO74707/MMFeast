using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class CombatFeedbackPrototypeSetup
{
    public const string BarPath = "Assets/Prototype/Prefabs/WorldHealthBar.prefab";
    public const string TelegraphMaterialPath = "Assets/Prototype/Prefabs/ZombieTelegraph.mat";
    [MenuItem("MMFeast/Set Up Combat Feedback")]
    public static void Upgrade() => GlobalDataPrototypeSetup.Upgrade();

    public static bool BindEntityVisuals(GameObject root)
    {
        bool changed = false;
        foreach (var health in root.GetComponentsInChildren<EnemyHealth>(true))
        {
            changed |= EnsureBar(health.gameObject, health, null);
            var ai = health.GetComponent<EnemySimpleAI>();
            if (ai == null || health.GetComponent<Customer>() != null) continue;
            var indicator = health.GetComponentInChildren<ZombieAttackTelegraph>(true);
            if (indicator == null)
            {
                var obj = new GameObject("AttackTelegraph", typeof(MeshFilter), typeof(MeshRenderer));
                obj.transform.SetParent(health.transform, false);
                obj.transform.localPosition = Vector3.back * .1f;
                var renderer = obj.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = TelegraphMaterial();
                renderer.sortingOrder = 0;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.enabled = false;
                indicator = obj.AddComponent<ZombieAttackTelegraph>();
                indicator.Configure(obj.GetComponent<MeshFilter>(), renderer);
                changed = true;
            }
            var fields = new SerializedObject(ai);
            if (fields.FindProperty("attackTelegraph").objectReferenceValue != indicator)
            {
                ai.ConfigureTelegraph(indicator);
                EditorUtility.SetDirty(ai); PrefabUtility.RecordPrefabInstancePropertyModifications(ai);
                changed = true;
            }
        }
        foreach (var customer in root.GetComponentsInChildren<Customer>(true))
            changed |= EnsureBar(customer.gameObject, null, customer);
        return changed;
    }
    private static Material TelegraphMaterial()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(TelegraphMaterialPath);
        if (material != null) return material;
        material = new Material(Shader.Find("Sprites/Default")) { name = "ZombieTelegraph" };
        AssetDatabase.CreateAsset(material, TelegraphMaterialPath);
        return material;
    }
    private static bool EnsureBar(GameObject entity, EnemyHealth zombie, Customer customer)
    {
        if (entity.GetComponentInChildren<WorldHealthBar>(true) != null) return false;
        var prefab = BarPrefab();
        var anchor = entity.transform.Find("HealthBarAnchor");
        if (anchor == null)
        {
            anchor = new GameObject("HealthBarAnchor").transform;
            anchor.SetParent(entity.transform, false);
            anchor.localPosition = new Vector3(0f, 1f, -.25f);
        }
        var obj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, anchor);
        obj.name = "WorldHealthBar";
        var bar = obj.GetComponent<WorldHealthBar>();
        bar.Configure(zombie, customer, obj.GetComponent<Canvas>(), obj.GetComponentInChildren<Slider>());
        EditorUtility.SetDirty(bar); PrefabUtility.RecordPrefabInstancePropertyModifications(bar);
        return true;
    }
    private static GameObject BarPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarPath);
        if (prefab != null) return prefab;
        var obj = new GameObject("WorldHealthBar", typeof(RectTransform), typeof(Canvas));
        try
        {
            var rect = (RectTransform)obj.transform;
            rect.sizeDelta = new Vector2(100f, 12f);
            rect.localScale = Vector3.one * .012f;
            var canvas = obj.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true; canvas.sortingOrder = 25;
            var background = new GameObject("HP Progress Bar", typeof(RectTransform), typeof(Image), typeof(Slider));
            background.transform.SetParent(rect, false);
            var backgroundRect = (RectTransform)background.transform;
            backgroundRect.anchorMin = Vector2.zero; backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = backgroundRect.offsetMax = Vector2.zero;
            var backgroundImage = background.GetComponent<Image>();
            backgroundImage.color = new Color(.08f,.08f,.1f,.95f); backgroundImage.raycastTarget = false;
            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(background.transform, false);
            var fillRect = (RectTransform)fill.transform;
            fillRect.anchorMin = Vector2.zero; fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(2,2); fillRect.offsetMax = new Vector2(-2,-2);
            var fillImage = fill.GetComponent<Image>(); fillImage.color = new Color(.9f,.2f,.25f); fillImage.raycastTarget = false;
            var slider = background.GetComponent<Slider>(); slider.transition = Selectable.Transition.None;
            slider.interactable = false; slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.fillRect = fillRect; slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
            obj.AddComponent<WorldHealthBar>().Configure(null,null,canvas,slider);
            return PrefabUtility.SaveAsPrefabAsset(obj,BarPath);
        }
        finally { Object.DestroyImmediate(obj); }
    }
    public static void ApplyToScene(Scene scene)
    {
        PlayerController player = null;
        Camera camera = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (player == null) player = root.GetComponentInChildren<PlayerController>(true);
            foreach (var candidate in root.GetComponentsInChildren<Camera>(true))
                if (candidate.CompareTag("MainCamera")) camera = candidate;
        }
        if (player == null || camera == null) throw new System.InvalidOperationException("Combat feedback needs the existing Player and Main Camera in " + scene.path);
        var shake = camera.GetComponent<SimpleCameraShake>();
        if (shake == null) shake = camera.gameObject.AddComponent<SimpleCameraShake>();
        var feedback = player.GetComponent<PlayerHitFeedback>();
        if (feedback == null) feedback = player.gameObject.AddComponent<PlayerHitFeedback>();
        var visual = player.transform.Find("Visual");
        if (visual == null || visual.GetComponent<SpriteRenderer>() == null)
            throw new System.InvalidOperationException("Player feedback needs the existing Visual SpriteRenderer.");
        feedback.Configure(visual.GetComponent<SpriteRenderer>(), shake);
        var combat = player.GetComponent<PlayerCombat>();
        if (combat != null) { combat.ConfigureShake(shake); EditorUtility.SetDirty(combat); }
        EditorUtility.SetDirty(feedback); EditorUtility.SetDirty(shake);
    }
}
