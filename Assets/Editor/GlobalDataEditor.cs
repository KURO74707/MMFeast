using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GlobalGameSettings))]
public class GlobalGameSettingsEditor : Editor
{
    private readonly System.Collections.Generic.Dictionary<Object, Editor> loadoutEditors = new System.Collections.Generic.Dictionary<Object, Editor>();
    private bool showLoadouts = true;
    public override bool RequiresConstantRepaint() => Application.isPlaying;
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox("共享配置：修改会保存，包括 Play Mode 调参。玩家开局 HP / SAN 等于上限；修改上限只钳制已有玩家，不补满。怪物、食客最大 HP 用于新实例；初始耐力用于下次开局。", MessageType.Info);
        serializedObject.Update();
        EditorGUI.BeginChangeCheck();
        DrawPropertiesExcluding(serializedObject, "m_Script", "initialCoins", "customerPurificationCoins");
        EditorGUILayout.Space();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("initialCoins"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("customerPurificationCoins"));
        bool changed = EditorGUI.EndChangeCheck();
        if (serializedObject.ApplyModifiedProperties() || changed)
        {
            var settings = (GlobalGameSettings)target;
            settings.Validate();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }
        showLoadouts = EditorGUILayout.Foldout(showLoadouts, "Character and Weapon Details / 角色与武器参数", true);
        if (showLoadouts)
        {
            var settings = (GlobalGameSettings)target;
            EditorGUILayout.HelpBox("默认猫不启用尚未定义的升级代价。鸟的唱歌参数在 BirdSong；武器角度在 Attack，击退在 Hit Effect。Play Mode 配置修改会保存。切换不补满状态、不重置攻击冷却，会取消当前 Dash / 瞄准。只编辑数值时，当前 Dash 保留开始时的快照。", MessageType.Info);
            if (settings.Characters != null) foreach (var character in settings.Characters)
            {
                DrawDefinition(character);
                if (character != null) DrawDefinition(character.talent);
            }
            if (settings.Weapons != null) foreach (var weapon in settings.Weapons)
            {
                DrawDefinition(weapon);
                if (weapon != null) { DrawDefinition(weapon.attack); DrawDefinition(weapon.hitEffect); }
            }
            if (settings.DefaultCharacter == null || settings.DefaultWeapon == null || settings.DefaultWeapon.attack == null)
                EditorGUILayout.HelpBox("指定默认角色及有效初始武器。可通过 MMFeast > Set Up Characters and Weapons 完成首次迁移。", MessageType.Error);
        }
        if (Application.isPlaying)
            foreach (var wallet in FindObjectsByType<TruckShop>())
            {
                if (wallet.Settings != target) continue;
                EditorGUI.BeginChangeCheck();
                int coins = EditorGUILayout.IntField("Current Coins / 当前金币 (" + wallet.gameObject.scene.name + ")", wallet.CurrentBalance);
                if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(wallet, "Adjust runtime coins"); wallet.SetCurrentBalance(coins); }
            }
    }
    private void DrawDefinition(Object asset)
    {
        if (asset == null) return;
        EditorGUILayout.Space(); EditorGUILayout.LabelField(asset.name, EditorStyles.boldLabel);
        loadoutEditors.TryGetValue(asset, out var editor);
        CreateCachedEditor(asset, null, ref editor); loadoutEditors[asset] = editor; editor.OnInspectorGUI();
    }
    private void OnDisable()
    { foreach (var editor in loadoutEditors.Values) if (editor != null) DestroyImmediate(editor); loadoutEditors.Clear(); }
}

[CustomEditor(typeof(GlobalData))]
public class GlobalDataEditor : Editor
{
    private Editor settingsEditor;
    private Editor gridEditor;
    private Editor catalogEditor;
    private readonly System.Collections.Generic.Dictionary<ShopProductDefinition, Editor> productEditors = new System.Collections.Generic.Dictionary<ShopProductDefinition, Editor>();
    private bool showShop;
    private bool showItems;
    private Editor itemCatalogEditor;
    private readonly System.Collections.Generic.Dictionary<ShopItemDefinition, Editor> itemEditors = new System.Collections.Generic.Dictionary<ShopItemDefinition, Editor>();
    private bool showGrid;
    private bool showRuntime;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("settings"));
        serializedObject.ApplyModifiedProperties();
        var panel = (GlobalData)target;
        if (panel.Settings == null)
        {
            EditorGUILayout.HelpBox("请指定共享 GlobalGameSettings 资源。", MessageType.Error);
            return;
        }
        CreateCachedEditor(panel.Settings, typeof(GlobalGameSettingsEditor), ref settingsEditor);
        settingsEditor.OnInspectorGUI();
        showShop = EditorGUILayout.Foldout(showShop, "Truck Upgrades / 餐车升级", true);
        if (showShop && panel.Settings.ShopCatalog != null)
        {
            CreateCachedEditor(panel.Settings.ShopCatalog, null, ref catalogEditor);
            catalogEditor.OnInspectorGUI();
            var drawn = new System.Collections.Generic.HashSet<ShopProductDefinition>();
            foreach (var product in panel.Settings.ShopCatalog.Products)
            {
                if (product == null || !drawn.Add(product)) continue;
                EditorGUILayout.Space(); EditorGUILayout.LabelField(product.name, EditorStyles.boldLabel);
                productEditors.TryGetValue(product, out var productEditor);
                CreateCachedEditor(product, null, ref productEditor); productEditors[product] = productEditor;
                productEditor.OnInspectorGUI();
            }
        }
        showItems = EditorGUILayout.Foldout(showItems, "Item Shop / 道具商店", true);
        if (showItems && panel.Settings.ItemShopCatalog != null)
        {
            CreateCachedEditor(panel.Settings.ItemShopCatalog, null, ref itemCatalogEditor);
            itemCatalogEditor.OnInspectorGUI();
            var drawn = new System.Collections.Generic.HashSet<ShopItemDefinition>();
            foreach (var item in panel.Settings.ItemShopCatalog.Items)
            {
                if (item == null || !drawn.Add(item)) continue;
                EditorGUILayout.Space(); EditorGUILayout.LabelField(item.name, EditorStyles.boldLabel);
                itemEditors.TryGetValue(item, out var itemEditor);
                CreateCachedEditor(item, null, ref itemEditor); itemEditors[item] = itemEditor;
                itemEditor.OnInspectorGUI();
            }
        }
        showGrid = EditorGUILayout.Foldout(showGrid, "Kitchen Grid / 餐车厨房网格", true);
        if (showGrid)
        {
            foreach (var grid in FindObjectsByType<TruckGridLayout>())
            {
                if (grid.gameObject.scene != panel.gameObject.scene) continue;
                CreateCachedEditor(grid, typeof(TruckGridLayoutEditor), ref gridEditor);
                gridEditor.OnInspectorGUI();
                break;
            }
        }
        EditorGUILayout.Space();
        showRuntime = EditorGUILayout.Foldout(showRuntime, "Runtime Player State / 运行时玩家状态", true);
        if (!showRuntime) return;
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("进入 Play Mode 后可查看和修改当前 HP、SAN、耐力。", MessageType.Info);
            return;
        }
        EditorGUILayout.HelpBox("这些是角色当前状态，修改不会写回共享初始配置。", MessageType.Info);
        foreach (var player in FindObjectsByType<PlayerController>())
        {
            if (player.gameObject.scene != panel.gameObject.scene) continue;
            EditorGUILayout.ObjectField("Player", player, typeof(PlayerController), true);
            var loadout = player.Loadout;
            if (loadout != null)
            {
                EditorGUI.BeginChangeCheck();
                var character = (CharacterDefinition)EditorGUILayout.ObjectField("Current Character / 当前角色", loadout.Character, typeof(CharacterDefinition), false);
                if (EditorGUI.EndChangeCheck() && character != null) loadout.SelectCharacter(character);
                EditorGUI.BeginChangeCheck();
                var weapon = (WeaponDefinition)EditorGUILayout.ObjectField("Current Weapon / 当前武器", loadout.Weapon, typeof(WeaponDefinition), false);
                if (EditorGUI.EndChangeCheck() && weapon != null) loadout.EquipWeapon(weapon);
                if (GUILayout.Button("Use Global Selection / 跟随全局选择")) loadout.UseGlobalSelection();
                var combat = player.GetComponent<PlayerCombat>();
                if (combat != null) EditorGUILayout.LabelField("Effective Damage / Cooldown", combat.Damage.ToString("0.##") + " / " + combat.Cooldown.ToString("0.###") + " s");
                EditorGUILayout.Toggle("Singing / 唱歌中", loadout.IsSinging);
            }
            EditorGUI.BeginChangeCheck();
            float hp = EditorGUILayout.FloatField("Current HP", player.HP);
            float san = EditorGUILayout.FloatField("Current SAN", player.SAN);
            float stamina = EditorGUILayout.FloatField("Current Stamina", player.CurrentStamina);
            if (!EditorGUI.EndChangeCheck()) continue;
            Undo.RecordObject(player, "Adjust runtime player stats");
            player.SetCurrentHP(hp);
            player.SetCurrentSAN(san);
            player.SetCurrentStamina(stamina);
        }
    }

    public override bool RequiresConstantRepaint() => Application.isPlaying;
    private void OnDisable()
    {
        if (settingsEditor != null) DestroyImmediate(settingsEditor);
        if (gridEditor != null) DestroyImmediate(gridEditor);
        if (catalogEditor != null) DestroyImmediate(catalogEditor);
        if (itemCatalogEditor != null) DestroyImmediate(itemCatalogEditor);
        foreach (var editor in itemEditors.Values) if (editor != null) DestroyImmediate(editor);
        itemEditors.Clear();
        foreach (var editor in productEditors.Values) if (editor != null) DestroyImmediate(editor);
        productEditors.Clear();
    }
}
