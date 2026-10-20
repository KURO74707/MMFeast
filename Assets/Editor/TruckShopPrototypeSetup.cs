using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TruckShopPrototypeSetup
{
    public const string CatalogPath = "Assets/Prototype/TruckShopCatalog.asset";
    public const string ProductPath = "Assets/Prototype/ExtraCannonProduct.asset";
    public const string CookingStationProductPath = "Assets/Prototype/ExtraCookingStationProduct.asset";
    public const string ChoppingBoardProductPath = "Assets/Prototype/ChoppingBoardProduct.asset";
    public const string ChoppingBoardPrefabPath = "Assets/Prototype/Prefabs/ChoppingBoard.prefab";
    const string InputsPath = "Assets/Settings/InputSystem_Actions.inputactions";
    [MenuItem("MMFeast/Set Up Truck Upgrades")]
    public static void Upgrade() => GlobalDataPrototypeSetup.Upgrade();
    public static void EnsureCatalog()
    {
        var settings = GlobalDataPrototypeSetup.GetOrCreateSettings();
        if (settings.ShopCatalog != null)
        { CustomerWaitingAreaPrototypeSetup.EnsureCatalogProduct(settings.ShopCatalog); EnsureCookingStationUpgrade(settings.ShopCatalog); EnsureChoppingBoardUpgrade(settings.ShopCatalog); MigrateCoins(settings); return; }
        var catalog = AssetDatabase.LoadAssetAtPath<TruckShopCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<TruckShopCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath);
            var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(ProductPath);
            if (product == null)
            {
                product = ScriptableObject.CreateInstance<ShopProductDefinition>();
                var productFields = new SerializedObject(product);
                productFields.FindProperty("toolPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<PlaceableKitchenTool>("Assets/Prototype/Prefabs/FoodCannon.prefab");
                productFields.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
                productFields.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.CreateAsset(product, ProductPath);
            }
            var fields = new SerializedObject(catalog); var products = fields.FindProperty("products");
            products.arraySize = 1; products.GetArrayElementAtIndex(0).objectReferenceValue = product; fields.ApplyModifiedPropertiesWithoutUndo();
        }
        var settingsFields = new SerializedObject(settings);
        CustomerWaitingAreaPrototypeSetup.EnsureCatalogProduct(catalog);
        EnsureCookingStationUpgrade(catalog);
        EnsureChoppingBoardUpgrade(catalog);
        settingsFields.FindProperty("shopCatalog").objectReferenceValue = catalog; settingsFields.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(settings); EditorUtility.SetDirty(catalog);
        MigrateCoins(settings);
    }
    static void EnsureCookingStationUpgrade(TruckShopCatalog catalog)
    {
        var fields = new SerializedObject(catalog);
        if (fields.FindProperty("cookingStationUpgradeInitialized").boolValue) return;
        var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(CookingStationProductPath);
        if (product == null)
        {
            var tool = AssetDatabase.LoadAssetAtPath<PlaceableKitchenTool>("Assets/Prototype/Prefabs/CookingStation.prefab");
            if (tool == null) throw new System.InvalidOperationException("Create the existing Cooking Station prefab before setting up upgrades.");
            product = ScriptableObject.CreateInstance<ShopProductDefinition>();
            var entry = new SerializedObject(product);
            entry.FindProperty("productId").stringValue = "extra_cooking_station";
            entry.FindProperty("displayName").stringValue = "Extra Cooking Station";
            entry.FindProperty("description").stringValue = "Add an independently usable Cooking Station. Place it on an empty 2 x 1 grid area. Empty it before moving or swapping.";
            entry.FindProperty("toolPrefab").objectReferenceValue = tool;
            entry.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
            entry.FindProperty("price").intValue = 0;
            entry.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(product, CookingStationProductPath);
        }
        bool found = false;
        foreach (var existing in catalog.Products) if (existing != null && existing.ToolPrefab != null && existing.ToolPrefab.GetComponent<CookingStation>() != null) found = true;
        var products = fields.FindProperty("products");
        if (!found) { int next = products.arraySize; products.arraySize++; products.GetArrayElementAtIndex(next).objectReferenceValue = product; }
        fields.FindProperty("cookingStationUpgradeInitialized").boolValue = true;
        fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
    }
    static void EnsureChoppingBoardUpgrade(TruckShopCatalog catalog)
    {
        var fields = new SerializedObject(catalog);
        if (fields.FindProperty("choppingBoardUpgradeInitialized").boolValue) return;
        var tool = AssetDatabase.LoadAssetAtPath<PlaceableKitchenTool>(ChoppingBoardPrefabPath);
        if (tool == null)
        {
            var root = new GameObject("ChoppingBoard", typeof(ChoppingBoard), typeof(BoxCollider2D));
            root.GetComponent<BoxCollider2D>().size = new Vector2(.8f,.66f);
            tool = root.GetComponent<PlaceableKitchenTool>();
            var toolFields = new SerializedObject(tool);
            toolFields.FindProperty("footprint").vector2IntValue = Vector2Int.one;
            toolFields.ApplyModifiedPropertiesWithoutUndo();
            var square = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
            BoardSprite("BoardEdge", root.transform, square, new Vector2(.8f,.66f), Vector3.zero, new Color(.36f,.22f,.12f), 0);
            BoardSprite("BoardSurface", root.transform, square, new Vector2(.7f,.56f), Vector3.zero, new Color(.76f,.55f,.31f), 1);
            var knife = BoardSprite("Knife", root.transform, square, new Vector2(.34f,.07f), new Vector3(.08f,.04f,0), new Color(.82f,.87f,.9f), 2);
            knife.transform.localRotation = Quaternion.Euler(0,0,35);
            GlobalDataPrototypeSetup.BindTree(root);
            tool = PrefabUtility.SaveAsPrefabAsset(root, ChoppingBoardPrefabPath).GetComponent<PlaceableKitchenTool>();
            Object.DestroyImmediate(root);
        }
        var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(ChoppingBoardProductPath);
        if (product == null)
        {
            product = ScriptableObject.CreateInstance<ShopProductDefinition>();
            var entry = new SerializedObject(product);
            entry.FindProperty("productId").stringValue = "chopping_board";
            entry.FindProperty("displayName").stringValue = "Chopping Board";
            entry.FindProperty("description").stringValue = "Place a chopping board on an empty 1 x 1 grid cell. Move or swap it in kitchen layout mode.";
            entry.FindProperty("toolPrefab").objectReferenceValue = tool;
            entry.FindProperty("icon").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
            entry.FindProperty("price").intValue = 0;
            entry.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.CreateAsset(product, ChoppingBoardProductPath);
        }
        bool found = false;
        foreach (var existing in catalog.Products) if (existing != null && existing.ToolPrefab != null && existing.ToolPrefab.GetComponent<ChoppingBoard>() != null) found = true;
        var products = fields.FindProperty("products");
        if (!found) { int next = products.arraySize; products.arraySize++; products.GetArrayElementAtIndex(next).objectReferenceValue = product; }
        fields.FindProperty("choppingBoardUpgradeInitialized").boolValue = true;
        fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
    }
    static SpriteRenderer BoardSprite(string name, Transform parent, Sprite sprite, Vector2 size, Vector3 position, Color color, int order)
    {
        var part = new GameObject(name, typeof(SpriteRenderer)); part.transform.SetParent(parent, false);
        part.transform.localPosition = position; part.transform.localScale = new Vector3(size.x,size.y,1);
        var renderer = part.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.color = color; renderer.sortingOrder = order; return renderer;
    }
    static void MigrateCoins(GlobalGameSettings settings)
    {
        var fields = new SerializedObject(settings);
        if (fields.FindProperty("initialCoinsMigrated").boolValue) return;
        fields.FindProperty("initialCoins").intValue = settings.ShopCatalog.StartingBalance;
        fields.FindProperty("initialCoinsMigrated").boolValue = true;
        fields.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(settings);
    }
    public static void ApplyToScene(Scene scene)
    {
        PlayerController player = null; TruckGridLayout grid = null; Canvas canvas = null; Camera camera = null; EventSystem events = null;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (player == null) player = root.GetComponentInChildren<PlayerController>(true);
            if (grid == null) grid = root.GetComponentInChildren<TruckGridLayout>(true);
            if (canvas == null) canvas = root.GetComponentInChildren<PlayerStatusUI>(true)?.GetComponentInParent<Canvas>();
            if (camera == null) camera = root.GetComponentInChildren<Camera>(true);
            if (events == null) events = root.GetComponentInChildren<EventSystem>(true);
        }
        if (grid == null) return;
        if (player == null || canvas == null || camera == null) throw new System.InvalidOperationException("Truck Upgrades needs existing player, grid, canvas and camera.");
        EnsureCatalog();
        var shop = player.GetComponent<TruckShop>(); if (shop == null) shop = player.gameObject.AddComponent<TruckShop>();
        shop.Configure(grid, camera, grid.GetComponentInChildren<CustomerWaitingAreaMount>(true)); EditorUtility.SetDirty(shop);
        EnsureCoinsHUD(canvas, shop);
        if (canvas.GetComponent<GraphicRaycaster>() == null) canvas.gameObject.AddComponent<GraphicRaycaster>();
        if (events == null)
        {
            var obj = new GameObject("EventSystem", typeof(EventSystem)); SceneManager.MoveGameObjectToScene(obj, scene); events = obj.GetComponent<EventSystem>();
        }
        var module = events.GetComponent<InputSystemUIInputModule>();
        if (module == null) module = events.gameObject.AddComponent<InputSystemUIInputModule>();
        module.actionsAsset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputsPath);
        module.point = ActionReference("Point"); module.leftClick = ActionReference("Click");
        module.rightClick = ActionReference("RightClick"); module.middleClick = ActionReference("MiddleClick");
        module.move = ActionReference("Navigate"); module.submit = ActionReference("Submit"); module.cancel = ActionReference("Cancel");
        module.scrollWheel = ActionReference("ScrollWheel"); EditorUtility.SetDirty(module);
        var existing = canvas.transform.Find("TruckShopUI");
        if (existing != null)
        {
            var existingContent = existing.Find("Overlay/Window/Viewport/Content") as RectTransform;
            if (existingContent != null) { existingContent.sizeDelta = new Vector2(0, existingContent.sizeDelta.y); EditorUtility.SetDirty(existingContent); }
            var rules = existing.Find("Overlay/Window/Rules")?.GetComponent<Text>();
            if (rules != null) { rules.text = "Add truck upgrades without resources. Grid tools: choose a free cell. Waiting area: choose a truck side."; EditorUtility.SetDirty(rules); }
            var existingScroll = existing.Find("Overlay/Window/Viewport")?.GetComponent<ScrollRect>();
            if (existingScroll != null) { existingScroll.scrollSensitivity = 30f; EditorUtility.SetDirty(existingScroll); }
            ConfigureUpgradeWindow(existing);
            return;
        }
        var uiRoot = Rect("TruckShopUI", canvas.transform, Vector2.zero, Vector2.zero); Stretch(uiRoot);
        var ui = uiRoot.gameObject.AddComponent<TruckShopUI>();
        var overlay = Rect("Overlay", uiRoot, Vector2.zero, Vector2.zero); Stretch(overlay);
        Background(overlay, new Color(.015f, .025f, .045f, .88f));
        var window = Rect("Window", overlay, Vector2.zero, new Vector2(760, 540));
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(.5f,.5f);
        Background(window, new Color(.10f,.15f,.21f,1));
        Label("Title", window, new Vector2(26,-18), new Vector2(440,36), "TRUCK UPGRADES", 27);
        var balance = Label("Balance", window, new Vector2(26,-64), new Vector2(550,28), "", 21);
        var close = Button("Close", window, new Vector2(620,-18), new Vector2(114,36), "Close");
        Label("Rules", window, new Vector2(26,-105), new Vector2(708,42), "Add truck upgrades without resources. Grid tools: choose a free cell. Waiting area: choose a truck side.", 17);
        var viewport = Rect("Viewport", window, new Vector2(26,-160), new Vector2(708,265));
        viewport.gameObject.AddComponent<RectMask2D>(); Background(viewport, new Color(.06f,.09f,.14f,1));
        var content = Rect("Content", viewport, Vector2.zero, new Vector2(708,0));
        content.anchorMin = new Vector2(0,1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f,1);
        content.sizeDelta = Vector2.zero;
        var group = content.gameObject.AddComponent<VerticalLayoutGroup>(); group.spacing = 10; group.padding = new RectOffset(8,8,8,8);
        group.childControlWidth = true; group.childControlHeight = true; group.childForceExpandHeight = false; group.childForceExpandWidth = true;
        content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        var template = Rect("ProductTemplate", content, Vector2.zero, new Vector2(692,184));
        template.gameObject.AddComponent<LayoutElement>().preferredHeight = 184;
        Background(template, new Color(.15f,.22f,.30f,1));
        var icon = Rect("Icon", template, new Vector2(14,-16), new Vector2(42,42)).gameObject.AddComponent<Image>(); icon.raycastTarget = false; icon.color = new Color(.55f,.72f,1);
        Label("Title", template, new Vector2(70,-13), new Vector2(430,30), "Extra Food Cannon", 21);
        Label("Description", template, new Vector2(16,-58), new Vector2(462,53), "Upgrade description", 16);
        Label("Price", template, new Vector2(500,-20), new Vector2(178,28), "", 19);
        Button("Buy", template, new Vector2(500,-62), new Vector2(174,40), "Add & place");
        Label("Status", template, new Vector2(16,-132), new Vector2(660,40), "", 15);
        template.gameObject.SetActive(false);
        var message = Label("Message", window, new Vector2(26,-446), new Vector2(708,66), "", 16);
        ui.Configure(shop, overlay.gameObject, content, template.gameObject, balance, message, close);
        EditorUtility.SetDirty(ui); overlay.gameObject.SetActive(false);
        ConfigureUpgradeWindow(uiRoot);
    }
    static void ConfigureUpgradeWindow(Transform uiRoot)
    {
        var window = uiRoot.Find("Overlay/Window");
        var title = window.Find("Title").GetComponent<Text>(); title.text = "TRUCK UPGRADES"; EditorUtility.SetDirty(title);
        var rules = (RectTransform)window.Find("Rules"); rules.anchoredPosition = new Vector2(26,-65); EditorUtility.SetDirty(rules);
        var viewport = (RectTransform)window.Find("Viewport"); viewport.anchoredPosition = new Vector2(26,-114); viewport.sizeDelta = new Vector2(708,310); EditorUtility.SetDirty(viewport);
        var balance = window.Find("Balance").GetComponent<Text>(); balance.text = ""; balance.gameObject.SetActive(false); EditorUtility.SetDirty(balance);
        var price = window.Find("Viewport/Content/ProductTemplate/Price").GetComponent<Text>(); price.text = ""; price.gameObject.SetActive(false); EditorUtility.SetDirty(price);
        var button = window.Find("Viewport/Content/ProductTemplate/Buy").GetComponentInChildren<Text>(); button.text = "Add & place"; EditorUtility.SetDirty(button);
    }
    static void EnsureCoinsHUD(Canvas canvas, TruckShop shop)
    {
        var existing = canvas.transform.Find("CoinsHUD");
        Text label;
        if (existing != null) label = existing.GetComponent<Text>();
        else
        {
            label = Label("CoinsHUD", canvas.transform, new Vector2(-20,-20), new Vector2(300,36), "COINS  " + GlobalDataPrototypeSetup.GetOrCreateSettings().InitialCoins, 24);
            var rect = (RectTransform)label.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            label.alignment = TextAnchor.MiddleRight;
            label.color = new Color(1f,.82f,.28f);
        }
        canvas.GetComponentInChildren<PlayerStatusUI>(true).ConfigureCoins(shop, label);
        EditorUtility.SetDirty(label);
        EditorUtility.SetDirty(canvas.GetComponentInChildren<PlayerStatusUI>(true));
    }
    static InputActionReference ActionReference(string name)
    {
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(InputsPath))
            if (asset is InputActionReference reference && reference.action != null && reference.action.actionMap.name == "UI" && reference.action.name == name) return reference;
        throw new System.InvalidOperationException("Missing existing UI action reference: " + name);
    }
    internal static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var rect = (RectTransform)new GameObject(name,typeof(RectTransform)).transform; rect.SetParent(parent,false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0,1); rect.anchoredPosition = position; rect.sizeDelta = size; return rect;
    }
    internal static void Stretch(RectTransform rect) { rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero; }
    internal static void Background(RectTransform rect, Color color) { var image=rect.gameObject.AddComponent<Image>();image.color=color; }
    internal static Text Label(string name,Transform parent,Vector2 pos,Vector2 size,string value,int fontSize)
    {
        var text=Rect(name,parent,pos,size).gameObject.AddComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=fontSize;text.color=Color.white;text.text=value;text.raycastTarget=false;text.alignment=TextAnchor.MiddleLeft;return text;
    }
    internal static Button Button(string name,Transform parent,Vector2 pos,Vector2 size,string value)
    {
        var rect=Rect(name,parent,pos,size);Background(rect,new Color(.18f,.47f,.64f,1));var button=rect.gameObject.AddComponent<Button>();button.targetGraphic=rect.GetComponent<Image>();
        var colors=button.colors;colors.disabledColor=new Color(.35f,.4f,.45f,.65f);button.colors=colors;
        var label=Label("Label",rect,Vector2.zero,size,value,17);label.alignment=TextAnchor.MiddleCenter;return button;
    }
}
