using UnityEditor;
using UnityEngine;

public class SharedShopAssetEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("餐车升级：添加设施暂不需要任何资源。修改配置会保存，包括 Play Mode；添加次数和摆放结果只保存在本局。", MessageType.Info);
        serializedObject.Update();
        EditorGUI.BeginChangeCheck(); DrawPropertiesExcluding(serializedObject, "m_Script", "price", "startingBalance"); bool changed = EditorGUI.EndChangeCheck();
        if (serializedObject.ApplyModifiedProperties() || changed)
        {
            if (target is ShopProductDefinition product) product.Validate();
            EditorUtility.SetDirty(target); AssetDatabase.SaveAssetIfDirty(target);
        }
    }
}
[CustomEditor(typeof(ShopProductDefinition))]
public class ShopProductDefinitionEditor : SharedShopAssetEditor { }
[CustomEditor(typeof(TruckShopCatalog))]
public class TruckShopCatalogEditor : SharedShopAssetEditor { }
