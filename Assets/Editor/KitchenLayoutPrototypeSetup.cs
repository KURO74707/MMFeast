using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class KitchenLayoutPrototypeSetup
{
    [MenuItem("MMFeast/Set Up Kitchen Grid Layout")]
    public static void Upgrade() => GlobalDataPrototypeSetup.Upgrade();

    // Used by prefab builders as well as scene migration; existing designer settings are retained.
    public static bool BindComponents(GameObject root)
    {
        bool changed = false;
        foreach (var station in root.GetComponentsInChildren<CookingStation>(true))
            if (station.GetComponent<PlaceableKitchenTool>() == null)
            { station.gameObject.AddComponent<PlaceableKitchenTool>(); changed = true; }
        foreach (var cannon in root.GetComponentsInChildren<FoodCannon>(true))
            if (cannon.GetComponent<PlaceableKitchenTool>() == null)
            {
                var tool = cannon.gameObject.AddComponent<PlaceableKitchenTool>();
                var fields = new SerializedObject(tool);
                fields.FindProperty("footprint").vector2IntValue = new Vector2Int(2, 2);
                fields.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
        foreach (var interior in root.GetComponentsInChildren<FoodTruckInteriorTrigger>(true))
        {
            var truck = interior.transform.parent != null ? interior.transform.parent : interior.transform;
            var grid = truck.GetComponent<TruckGridLayout>();
            if (grid == null)
            {
                grid = truck.gameObject.AddComponent<TruckGridLayout>();
                grid.Configure(interior, AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Prefabs/FoodTrajectory.mat"));
                changed = true;
            }
            // One-time conversion of the old fixed cannon space. Retain other designer reservations.
            var gridFields = new SerializedObject(grid);
            var initialized = gridFields.FindProperty("movableCannonLayoutInitialized");
            if (!initialized.boolValue)
            {
                var areas = gridFields.FindProperty("reservedAreas");
                for (int i = areas.arraySize - 1; i >= 0; i--)
                    if (areas.GetArrayElementAtIndex(i).rectIntValue == new RectInt(2, 2, 2, 2))
                        areas.DeleteArrayElementAtIndex(i);
                initialized.boolValue = true;
                gridFields.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(grid);
                PrefabUtility.RecordPrefabInstancePropertyModifications(grid);
                changed = true;
            }
        }
        return changed;
    }

    public static void ApplyToScene(Scene scene)
    {
        TruckGridLayout grid = null; PlayerController player = null; Canvas canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (grid == null) grid = root.GetComponentInChildren<TruckGridLayout>(true);
            if (player == null) player = root.GetComponentInChildren<PlayerController>(true);
            if (canvas == null) canvas = root.GetComponentInChildren<Canvas>(true);
        }
        if (grid == null) return; // The original combat-only builder can run before a truck exists.
        if (player == null || canvas == null)
            throw new System.InvalidOperationException("Kitchen layout needs existing Food Truck, Player and HUD Canvas in " + scene.path);
        // An old scene may override its prefab's reservations. Migrate those overrides once as well.
        BindComponents(grid.gameObject);
        foreach (var root in scene.GetRootGameObjects())
        foreach (var tool in root.GetComponentsInChildren<PlaceableKitchenTool>(true))
        {
            if (tool.Grid != null) { grid.Register(tool); continue; }
            var initial = grid.CellForCenter(tool.transform.position, tool.Footprint);
            tool.transform.SetParent(grid.transform, true);
            tool.Configure(grid, initial);
            tool.SnapToGrid();
            EditorUtility.SetDirty(tool);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tool);
            PrefabUtility.RecordPrefabInstancePropertyModifications(tool.transform);
        }
        var anchor = player.transform.Find("ToolCarryPoint");
        if (anchor == null)
        {
            anchor = new GameObject("ToolCarryPoint").transform;
            anchor.SetParent(player.transform, false); anchor.localPosition = Vector3.up;
        }
        var panel = canvas.transform.Find("KitchenLayoutStatus");
        Text label;
        if (panel == null)
        {
            var rect = (RectTransform)new GameObject("KitchenLayoutStatus", typeof(RectTransform), typeof(Image)).transform;
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0f); rect.pivot = new Vector2(.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 75f); rect.sizeDelta = new Vector2(900f, 62f);
            var background = rect.GetComponent<Image>(); background.color = new Color(.04f, .06f, .09f, .9f); background.raycastTarget = false;
            var textRect = (RectTransform)new GameObject("Label", typeof(RectTransform), typeof(Text)).transform;
            textRect.SetParent(rect, false); textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f); textRect.offsetMax = new Vector2(-10f, -4f);
            label = textRect.GetComponent<Text>(); label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontSize = 17; label.color = Color.white; label.alignment = TextAnchor.MiddleCenter; label.raycastTarget = false;
            label.text = "Hold E: organize kitchen (empty hands) | Tap E: interact";
            panel = rect;
        }
        else label = panel.GetComponentInChildren<Text>(true);
        var layout = player.GetComponent<PlayerKitchenLayout>();
        if (layout == null) layout = player.gameObject.AddComponent<PlayerKitchenLayout>();
        layout.Configure(grid, anchor, label);
        EditorUtility.SetDirty(layout); EditorUtility.SetDirty(grid);
        PrefabUtility.RecordPrefabInstancePropertyModifications(grid);
    }
}

[CustomEditor(typeof(TruckGridLayout))]
public class TruckGridLayoutEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        EditorGUILayout.HelpBox("Cell 是占地左下角；Reserved Areas 给不可移动设施留位，可移动大炮由工具占位管理。运行时移动只保留到本局结束。修改初始格子后点击下方按钮对齐并保存场景。", MessageType.Info);
        using (new EditorGUI.DisabledScope(Application.isPlaying))
        if (GUILayout.Button("Apply Initial Grid Layout"))
        {
            var grid = (TruckGridLayout)target;
            if (!grid.ValidateLayout(out var reason)) { Debug.LogError(reason, grid); return; }
            foreach (var tool in grid.Tools)
            {
                if (tool == null) continue;
                Undo.RecordObject(tool.transform, "Snap kitchen tool to grid");
                tool.SnapToGrid(); PrefabUtility.RecordPrefabInstancePropertyModifications(tool.transform);
            }
        }
    }
}
