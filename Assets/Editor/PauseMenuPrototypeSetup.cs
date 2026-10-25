using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static MainMenuPrototypeSetup;

public static class PauseMenuPrototypeSetup
{
    [MenuItem("MMFeast/Set Up Pause Menu")]
    public static void Upgrade() => MainMenuPrototypeSetup.Upgrade();

    public static void ApplyToScene(Scene scene, SessionSceneEntry entry, GlobalGameSettings settings)
    {
        Canvas canvas = null; PauseMenuController controller = null;
        var players = new List<PlayerController>();
        foreach (var root in scene.GetRootGameObjects())
        {
            players.AddRange(root.GetComponentsInChildren<PlayerController>(true));
            if (canvas == null) canvas = root.GetComponentInChildren<PlayerStatusUI>(true)?.GetComponentInParent<Canvas>();
            if (controller == null) controller = root.GetComponentInChildren<PauseMenuController>(true);
        }
        if (canvas == null || players.Count == 0) throw new System.InvalidOperationException("Pause Menu requires the existing player and HUD Canvas.");
        canvas.sortingOrder = Mathf.Max(canvas.sortingOrder, 100); // Keep modal UI above world-space name labels.
        EditorUtility.SetDirty(canvas);
        if (controller == null)
        {
            var root = Rect("Pause Menu", canvas.transform, 0, 0, 1280, 720); Stretch(root);
            controller = root.gameObject.AddComponent<PauseMenuController>();
            var overlay = Rect("Overlay", root, 0, 0, 1280, 720); Stretch(overlay);
            overlay.gameObject.AddComponent<Image>().color = new Color(.02f, .03f, .05f, .82f);
            var group = overlay.gameObject.AddComponent<CanvasGroup>();
            var panel = Rect("Panel", overlay, 0, 0, 640, 570);
            panel.gameObject.AddComponent<Image>().color = new Color(.055f, .08f, .105f);
            Label("Title", panel, "游戏暂停", 0, 215, 560, 70, 40).color = new Color(1f, .72f, .23f);
            var resume = Button("Resume", panel, "继续游戏", 0, 115, 420);
            Label("VolumeLabel", panel, "主音量", 0, 35, 420, 36, 25);
            var slider = VolumeSlider(panel); ((RectTransform)slider.transform).anchoredPosition = new Vector2(0, -12);
            var volume = Label("VolumePercent", panel, "100%", 0, -62, 420, 36, 23);
            var back = Button("ReturnToMainMenu", panel, "返回开始界面", 0, -145, 420);
            Label("ReturnHint", panel, "返回后将结束本局游戏", 0, -200, 560, 30, 18).color = new Color(.7f, .76f, .8f);
            var message = Label("Message", panel, "", 0, -243, 600, 52, 18); message.color = new Color(1f, .55f, .4f);
            Set(controller, "overlay", group); Set(controller, "resumeButton", resume); Set(controller, "returnButton", back);
            Set(controller, "volumeSlider", slider); Set(controller, "volumeText", volume); Set(controller, "message", message);
            overlay.gameObject.SetActive(false);
        }
        EnsureRestartButton(controller);
        Set(controller, "settings", settings); Set(controller, "sceneEntry", entry);
        var data = new SerializedObject(controller); var list = data.FindProperty("players"); list.arraySize = players.Count;
        for (int i = 0; i < players.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = players[i];
        data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(controller);
    }
    private static void EnsureRestartButton(PauseMenuController controller)
    {
        var data = new SerializedObject(controller);
        if (data.FindProperty("restartButton").objectReferenceValue != null) return;
        var panel = controller.transform.Find("Overlay/Panel") as RectTransform;
        if (panel == null) throw new System.InvalidOperationException("Pause Menu needs its existing Overlay/Panel to add Restart.");
        var button = panel.Find("Restart")?.GetComponent<Button>();
        if (button == null)
        {
            panel.sizeDelta = new Vector2(panel.sizeDelta.x, Mathf.Max(panel.sizeDelta.y, 650));
            // Migrate the original layout once; later setup calls preserve Designer edits.
            MoveDefaultY(panel.Find("Title") as RectTransform, 215, 275);
            MoveDefaultY(panel.Find("Resume") as RectTransform, 115, 185);
            button = Button("Restart", panel, "重新开始", 0, 105, 420);
            button.transform.SetSiblingIndex(panel.Find("Resume").GetSiblingIndex() + 1);
            EditorUtility.SetDirty(panel);
        }
        Set(controller, "restartButton", button);
    }
    private static void MoveDefaultY(RectTransform rect, float original, float updated)
    {
        if (rect == null || !Mathf.Approximately(rect.anchoredPosition.y, original)) return;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, updated); EditorUtility.SetDirty(rect);
    }
    private static void Set(Object target, string field, Object value)
    { var data = new SerializedObject(target); data.FindProperty(field).objectReferenceValue = value; data.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(target); }
}
