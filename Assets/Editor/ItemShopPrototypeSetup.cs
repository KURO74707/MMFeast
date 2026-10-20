using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static TruckShopPrototypeSetup;

public static class ItemShopPrototypeSetup
{
    public const string CatalogPath = "Assets/Prototype/ItemShopCatalog.asset";
    public const string ItemPath = "Assets/Prototype/MedkitItem.asset";
    public const string PrefabPath = "Assets/Prototype/Prefabs/Medkit.prefab";
    [MenuItem("MMFeast/Set Up Item Shop")]
    public static void Upgrade() => GlobalDataPrototypeSetup.Upgrade();
    public static void EnsureCatalog()
    {
        var settings = GlobalDataPrototypeSetup.GetOrCreateSettings();
        var fields = new SerializedObject(settings);
        if (!fields.FindProperty("itemShopInputsMigrated").boolValue)
        {
            // Only migrate once. Subsequent builders preserve Designer's bindings.
            var old = settings.Shop;
            old.ChangeBinding(0).WithPath("<Keyboard>/r");
            fields.Update();
            fields.FindProperty("itemShopInputsMigrated").boolValue = true;
            fields.ApplyModifiedPropertiesWithoutUndo();
            settings.Validate(); EditorUtility.SetDirty(settings);
        }
        if (settings.ItemShopCatalog != null) return;
        var catalog = AssetDatabase.LoadAssetAtPath<ItemShopCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<ItemShopCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath);
            var item = AssetDatabase.LoadAssetAtPath<ShopItemDefinition>(ItemPath);
            if (item == null)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<MedkitPickup>(PrefabPath);
                if (prefab == null)
                {
                    var root = new GameObject("Medkit", typeof(MedkitPickup));
                    root.GetComponent<CircleCollider2D>().radius = .35f;
                    root.GetComponent<CircleCollider2D>().isTrigger = true;
                    var body = root.GetComponent<Rigidbody2D>(); body.bodyType = RigidbodyType2D.Kinematic; body.gravityScale = 0;
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
                    Part("Pack", root.transform, sprite, new Vector2(.6f,.5f), Color.white, 5);
                    Part("CrossVertical", root.transform, sprite, new Vector2(.12f,.34f), new Color(.9f,.12f,.18f), 6);
                    Part("CrossHorizontal", root.transform, sprite, new Vector2(.34f,.12f), new Color(.9f,.12f,.18f), 6);
                    prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath).GetComponent<MedkitPickup>();
                    Object.DestroyImmediate(root);
                }
                item = ScriptableObject.CreateInstance<ShopItemDefinition>();
                var data = new SerializedObject(item);
                data.FindProperty("prefab").objectReferenceValue = prefab;
                data.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
                data.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(item, ItemPath);
            }
            var entries = new SerializedObject(catalog); var items = entries.FindProperty("items"); items.arraySize = 1;
            items.GetArrayElementAtIndex(0).objectReferenceValue = item; entries.ApplyModifiedPropertiesWithoutUndo();
        }
        fields.Update(); fields.FindProperty("itemShopCatalog").objectReferenceValue = catalog;
        fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings);
    }
    static void Part(string name, Transform parent, Sprite sprite, Vector2 size, Color color, int order)
    {
        var part = new GameObject(name, typeof(SpriteRenderer)); part.transform.SetParent(parent, false);
        part.transform.localScale = new Vector3(size.x,size.y,1);
        var renderer = part.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order;
    }
    public static void ApplyToScene(Scene scene)
    {
        PlayerController player = null; TruckGridLayout grid = null; Canvas canvas = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (player == null) player = root.GetComponentInChildren<PlayerController>(true);
            if (grid == null) grid = root.GetComponentInChildren<TruckGridLayout>(true);
            if (canvas == null) canvas = root.GetComponentInChildren<PlayerStatusUI>(true)?.GetComponentInParent<Canvas>();
        }
        if (grid == null) return;
        if (player == null || canvas == null) throw new System.InvalidOperationException("Item Shop requires the existing player and HUD Canvas.");
        EnsureCatalog();
        var shop = player.GetComponent<ItemShop>(); if (shop == null) shop = player.gameObject.AddComponent<ItemShop>();
        shop.Configure(grid.Interior.transform); EditorUtility.SetDirty(shop);
        if (canvas.transform.Find("ItemShopUI") != null) return;
        var uiRoot = Rect("ItemShopUI", canvas.transform, Vector2.zero, Vector2.zero); Stretch(uiRoot);
        var ui = uiRoot.gameObject.AddComponent<ItemShopUI>();
        var overlay = Rect("Overlay", uiRoot, Vector2.zero, Vector2.zero); Stretch(overlay);
        Background(overlay, new Color(.015f,.025f,.045f,.88f));
        var window = Rect("Window", overlay, Vector2.zero, new Vector2(760,540));
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f,.5f); Background(window, new Color(.1f,.15f,.21f));
        Label("Title",window,new Vector2(26,-18),new Vector2(440,36),"ITEM SHOP",27);
        var balance = Label("Balance",window,new Vector2(26,-64),new Vector2(550,28),"COINS",21); balance.color = new Color(1,.82f,.28f);
        var close = Button("Close",window,new Vector2(620,-18),new Vector2(114,36),"Close");
        Label("Rules",window,new Vector2(26,-105),new Vector2(708,42),"Buy a medkit and find it on the field. Touch it to recover HP.",17);
        var viewport = Rect("Viewport",window,new Vector2(26,-160),new Vector2(708,265));
        viewport.gameObject.AddComponent<RectMask2D>(); Background(viewport,new Color(.06f,.09f,.14f));
        var content = Rect("Content",viewport,Vector2.zero,Vector2.zero);
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        var group = content.gameObject.AddComponent<VerticalLayoutGroup>(); group.spacing=10; group.padding=new RectOffset(8,8,8,8);
        group.childControlWidth=group.childControlHeight=group.childForceExpandWidth=true; group.childForceExpandHeight=false;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit=ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;
        scroll.movementType=ScrollRect.MovementType.Clamped;scroll.scrollSensitivity=30;
        var template = Rect("ItemTemplate",content,Vector2.zero,new Vector2(692,184)); template.gameObject.AddComponent<LayoutElement>().preferredHeight=184;
        Background(template,new Color(.15f,.22f,.30f));
        var icon = Rect("Icon",template,new Vector2(14,-16),new Vector2(42,42)).gameObject.AddComponent<Image>(); icon.raycastTarget=false;
        Label("Title",template,new Vector2(70,-13),new Vector2(430,30),"Medkit",21);
        Label("Description",template,new Vector2(16,-58),new Vector2(462,53),"",16);
        Label("Price",template,new Vector2(500,-20),new Vector2(178,28),"25 coins",19);
        Button("Buy",template,new Vector2(500,-62),new Vector2(174,40),"Buy & deliver");
        Label("Status",template,new Vector2(16,-132),new Vector2(660,40),"",15); template.gameObject.SetActive(false);
        var message = Label("Message",window,new Vector2(26,-446),new Vector2(708,66),"",16);
        ui.Configure(shop,overlay.gameObject,content,template.gameObject,balance,message,close);
        overlay.gameObject.SetActive(false); EditorUtility.SetDirty(ui);
    }
}
