using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ShopItemDefinition))]
public class ShopItemDefinitionEditor : Editor
{
    public override void OnInspectorGUI() => ItemShopAssetEditor.Draw(this);
}
[CustomEditor(typeof(ItemShopCatalog))]
public class ItemShopCatalogEditor : Editor
{
    public override void OnInspectorGUI() => ItemShopAssetEditor.Draw(this);
}
public static class ItemShopAssetEditor
{
    public static void Draw(Editor editor)
    {
        EditorGUILayout.HelpBox("共享资源：Play Mode 修改也会保存。回血量即时影响地上未使用的血包；下架只阻止新购买。可通过 Create > MMFeast > Item Shop 新建商品。", MessageType.Info);
        editor.serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        editor.DrawDefaultInspector();
        if (EditorGUI.EndChangeCheck() || editor.serializedObject.ApplyModifiedProperties())
        { EditorUtility.SetDirty(editor.target); AssetDatabase.SaveAssetIfDirty(editor.target); }
        if (editor.target is ShopItemDefinition item && !item.IsValid(out string reason)) EditorGUILayout.HelpBox(reason, MessageType.Warning);
    }
}
