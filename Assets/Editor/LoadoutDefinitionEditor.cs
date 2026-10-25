using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CharacterDefinition))]
[CanEditMultipleObjects]
public class CharacterDefinitionEditor : LoadoutDefinitionEditor { }
[CustomEditor(typeof(WeaponDefinition))]
[CanEditMultipleObjects]
public class WeaponDefinitionEditor : LoadoutDefinitionEditor { }
[CustomEditor(typeof(CharacterTalentDefinition), true)]
public class TalentDefinitionEditor : LoadoutDefinitionEditor { }
[CustomEditor(typeof(WeaponAttackDefinition), true)]
public class AttackDefinitionEditor : LoadoutDefinitionEditor { }
[CustomEditor(typeof(WeaponHitEffectDefinition), true)]
public class HitEffectDefinitionEditor : LoadoutDefinitionEditor { }

public class LoadoutDefinitionEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck(); DrawDefaultInspector();
        if (!EditorGUI.EndChangeCheck()) return;
        foreach (var asset in targets)
        {
            if (asset is CharacterDefinition character) character.Validate();
            if (asset is WeaponDefinition weapon) weapon.Validate();
            EditorUtility.SetDirty(asset); AssetDatabase.SaveAssetIfDirty(asset);
        }
    }
}
