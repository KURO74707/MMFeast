using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class FoodTruckHealthPrototypeSetup
{
    [MenuItem("MMFeast/Set Up Food Truck Health")]
    public static void Upgrade() => GlobalDataPrototypeSetup.Upgrade();
    public static bool BindTruck(GameObject root)
    {
        bool changed = false;
        foreach (var interior in root.GetComponentsInChildren<FoodTruckInteriorTrigger>(true))
        {
            var truckRoot = interior.transform.parent;
            var health = truckRoot.GetComponent<FoodTruckHealth>();
            if (health == null) { health = truckRoot.gameObject.AddComponent<FoodTruckHealth>(); changed = true; }
            var barrier = truckRoot.Find("EnemyBarrier");
            if (barrier == null) continue; // Existing truck builder creates its barrier before final binding.
            var collider = barrier.GetComponent<Collider2D>();
            if (collider == null) throw new InvalidOperationException("FoodTruck EnemyBarrier needs its existing Collider2D.");
            var fields = new SerializedObject(health);
            if (fields.FindProperty("damageCollider").objectReferenceValue == collider) continue;
            health.ConfigureCollider(collider);
            EditorUtility.SetDirty(health); PrefabUtility.RecordPrefabInstancePropertyModifications(health);
            changed = true;
        }
        return changed;
    }
    public static void ApplyToScene(Scene scene)
    {
        FoodTruckHealth truck = null; FoodTruckStatusUI panel = null; Canvas canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var candidate in root.GetComponentsInChildren<FoodTruckHealth>(true))
            { if (truck != null) throw new InvalidOperationException("Prototype expects one Food Truck in " + scene.path); truck = candidate; }
            foreach (var candidate in root.GetComponentsInChildren<FoodTruckStatusUI>(true))
            { if (panel != null) throw new InvalidOperationException("Multiple truck status panels in " + scene.path); panel = candidate; }
            foreach (var candidate in root.GetComponentsInChildren<Canvas>(true))
                if (canvas == null && candidate.renderMode == RenderMode.ScreenSpaceOverlay) canvas = candidate;
        }
        if (truck == null) return; // Initial combat-only builder can run before the truck is added.
        foreach (var root in scene.GetRootGameObjects())
        {
            foreach (var spawner in root.GetComponentsInChildren<EnemySpawner>(true))
            { spawner.ConfigureFoodTruck(truck); EditorUtility.SetDirty(spawner); PrefabUtility.RecordPrefabInstancePropertyModifications(spawner); }
            foreach (var enemy in root.GetComponentsInChildren<EnemySimpleAI>(true))
                if (enemy.GetComponent<EnemyHealth>() != null)
                { enemy.SetFoodTruckTarget(truck); EditorUtility.SetDirty(enemy); PrefabUtility.RecordPrefabInstancePropertyModifications(enemy); }
        }
        if (panel != null) return;
        if (canvas == null) throw new InvalidOperationException("Use the existing Player HUD Canvas for truck status.");
        var rect = Rect("FoodTruckStatusUI",canvas.transform,new Vector2(20,20),new Vector2(300,42));
        var label = Rect("TruckHPLabel",rect,new Vector2(0,20),new Vector2(300,22)).gameObject.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 18;
        label.color = Color.white; label.raycastTarget = false; label.alignment = TextAnchor.MiddleLeft;
        var barRect = Rect("TruckHPBar",rect,Vector2.zero,new Vector2(300,14));
        var background = barRect.gameObject.AddComponent<Image>(); background.color = new Color(.08f,.1f,.14f,.95f); background.raycastTarget = false;
        var fill = Rect("Fill",barRect,Vector2.zero,Vector2.zero);
        fill.anchorMin = Vector2.zero; fill.anchorMax = Vector2.one; fill.offsetMin = new Vector2(2,2); fill.offsetMax = new Vector2(-2,-2);
        var image = fill.gameObject.AddComponent<Image>(); image.color = new Color(.95f,.65f,.15f); image.raycastTarget = false;
        var slider = barRect.gameObject.AddComponent<Slider>(); slider.interactable = false;
        slider.transition = Selectable.Transition.None; slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.fillRect = fill; slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
        panel = rect.gameObject.AddComponent<FoodTruckStatusUI>(); panel.Configure(truck,slider,label); EditorUtility.SetDirty(panel);
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 position,Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform;
        rect.SetParent(parent,false); rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
}
