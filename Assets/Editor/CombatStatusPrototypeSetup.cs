using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Shared by the existing prototype builders and the migration menu.
public static class CombatStatusPrototypeSetup
{
    public const string CustomerMeatPath = "Assets/Prototype/Prefabs/CustomerMeat.prefab";

    [MenuItem("MMFeast/Set Up Combat HP SAN And HUD")]
    public static void Upgrade() => GlobalDataPrototypeSetup.Upgrade();

    public static void EnsureIngredients()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(CustomerMeatPath) != null) return;
        var meat = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Prefabs/Meat.prefab");
        if (meat == null) throw new InvalidOperationException("Create the existing Meat prefab before setting up Customer Meat.");
        var root = PrefabUtility.LoadPrefabContents("Assets/Prototype/Prefabs/Meat.prefab");
        try
        {
            root.name = "CustomerMeat";
            // Save an independent prefab with the same carry/cooking component and physics wiring.
            root.GetComponent<IngredientPickup>().ConfigureSource(IngredientPickup.MeatSourceType.Customer,
                new Color(1f, 0.72f, 0.72f));
            PrefabUtility.SaveAsPrefabAsset(root, CustomerMeatPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void ApplyToScene(Scene scene)
    {
        PlayerController player = null;
        PlayerStatusUI hud = null;
        Canvas canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<PlayerController>(true))
            {
                if (player != null) throw new InvalidOperationException("Prototype HUD expects one Player in " + scene.path);
                player = candidate;
            }
            foreach (var candidate in root.GetComponentsInChildren<PlayerStatusUI>(true))
            {
                if (hud != null) throw new InvalidOperationException("Multiple PlayerStatusUI panels in " + scene.path);
                hud = candidate;
            }
            if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
        }
        if (player == null) throw new InvalidOperationException("No Player found for prototype HUD in " + scene.path);
        if (hud != null)
        {
            EnsureStaminaBar(hud);
            EnsureFPSLabel(hud);
            return;
        }
        if (canvas == null)
        {
            var obj = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            SceneManager.MoveGameObjectToScene(obj, scene);
            canvas = obj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = obj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
        }
        var panel = Rect("PlayerStatusUI", canvas.transform, new Vector2(20f, -20f), new Vector2(300f, 94f));
        var hp = Bar("HPGroup", panel, 0f, new Color(0.85f, 0.2f, 0.22f), out Text hpText);
        var san = Bar("SANGroup", panel, -46f, new Color(0.25f, 0.6f, 0.95f), out Text sanText);
        hud = panel.gameObject.AddComponent<PlayerStatusUI>();
        hud.Configure(player, hp, san, hpText, sanText);
        EnsureStaminaBar(hud);
        EnsureFPSLabel(hud);
        EditorUtility.SetDirty(hud);
    }

    private static void EnsureFPSLabel(PlayerStatusUI hud)
    {
        var canvas = hud.GetComponentInParent<Canvas>();
        var existing = canvas.transform.Find("FPSHUD");
        var label = existing != null ? existing.GetComponent<Text>() :
            Rect("FPSHUD", canvas.transform, Vector2.zero, new Vector2(180f, 30f)).gameObject.AddComponent<Text>();
        var rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.anchoredPosition = new Vector2(-20f, 20f);
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleRight;
        label.raycastTarget = false;
        label.text = "FPS --";
        hud.ConfigureFPS(label);
        EditorUtility.SetDirty(label);
        EditorUtility.SetDirty(rect);
        EditorUtility.SetDirty(hud);
    }

    private static void EnsureStaminaBar(PlayerStatusUI hud)
    {
        var panel = (RectTransform)hud.transform;
        panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, 140f));
        var existing = panel.Find("StaminaGroup");
        Slider bar;
        Text label;
        if (existing != null)
        {
            bar = existing.GetComponentInChildren<Slider>(true);
            label = existing.GetComponentInChildren<Text>(true);
        }
        else bar = Bar("StaminaGroup", panel, -92f, new Color(0.25f, 0.85f, 0.4f), out label);
        hud.ConfigureStamina(bar, label);
        EditorUtility.SetDirty(panel);
        EditorUtility.SetDirty(hud);
    }

    private static Slider Bar(string name, Transform parent, float y, Color color, out Text label)
    {
        var group = Rect(name, parent, new Vector2(0f, y), new Vector2(300f, 42f));
        label = Rect("Label", group, Vector2.zero, new Vector2(300f, 22f)).gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleLeft;
        label.raycastTarget = false;
        var bar = Rect("Progress Bar", group, new Vector2(0f, -25f), new Vector2(300f, 14f));
        var background = bar.gameObject.AddComponent<Image>();
        background.color = new Color(0.08f, 0.1f, 0.14f, 0.95f);
        background.raycastTarget = false;
        var fill = Rect("Fill", bar, Vector2.zero, Vector2.zero);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one;
        fill.offsetMin = new Vector2(2f, 2f); fill.offsetMax = new Vector2(-2f, -2f);
        var image = fill.gameObject.AddComponent<Image>();
        image.color = color; image.raycastTarget = false;
        var slider = bar.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.interactable = false;
        slider.fillRect = fill;
        slider.minValue = 0f; slider.maxValue = 1f; slider.value = 1f;
        return slider;
    }
    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }
}
