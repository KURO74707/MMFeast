using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CustomerWaitingAreaPrototypeSetup
{
    public const string PrefabPath = "Assets/Prototype/Prefabs/CustomerWaitingArea.prefab";
    public const string ProductPath = "Assets/Prototype/WaitingAreaProduct.asset";
    public static bool BindAreas(GameObject root)
    {
        bool changed = false;
        foreach (var area in root.GetComponentsInChildren<CustomerWaitingArea>(true))
        {
            var fields = new SerializedObject(area);
            if (fields.FindProperty("enemyBarrier").objectReferenceValue != null) continue;
            var child = new GameObject("EnemyBarrier", typeof(BoxCollider2D));
            child.transform.SetParent(area.transform, false);
            var barrier = child.GetComponent<BoxCollider2D>();
            var footprint = area.GetComponent<BoxCollider2D>();
            barrier.size = footprint.size; barrier.offset = footprint.offset;
            barrier.isTrigger = false; barrier.includeLayers = 1 << 8; barrier.excludeLayers = ~(1 << 8); barrier.layerOverridePriority = 100;
            area.ConfigureBarrier(barrier); EditorUtility.SetDirty(area); changed = true;
        }
        return changed;
    }
    public static bool BindMounts(GameObject root)
    {
        bool changed = false;
        foreach (var interior in root.GetComponentsInChildren<FoodTruckInteriorTrigger>(true))
        {
            var truck = interior.transform.parent;
            var existing = truck.GetComponentInChildren<CustomerWaitingAreaMount>(true);
            if (existing != null) { existing.Configure(interior); EditorUtility.SetDirty(existing); changed = true; continue; }
            var mount = new GameObject("CustomerWaitingAreaMount", typeof(CustomerWaitingAreaMount));
            mount.transform.SetParent(truck, false); mount.transform.localPosition = new Vector3(0,-3.1f,0);
            mount.GetComponent<CustomerWaitingAreaMount>().Configure(interior);
            changed = true;
        }
        return changed;
    }
    public static void EnsureCatalogProduct(TruckShopCatalog catalog)
    {
        var fields = new SerializedObject(catalog);
        if (fields.FindProperty("waitingAreaProductInitialized").boolValue) return;
        var prefab = AssetDatabase.LoadAssetAtPath<CustomerWaitingArea>(PrefabPath);
        if (prefab == null)
        {
            var obj = new GameObject("CustomerWaitingArea", typeof(BoxCollider2D));
            var collider = obj.GetComponent<BoxCollider2D>(); collider.isTrigger = true; collider.size = new Vector2(3.6f,1.6f);
            var visual = new GameObject("Floor", typeof(SpriteRenderer)); visual.transform.SetParent(obj.transform,false); visual.transform.localScale = new Vector3(3.6f,1.6f,1);
            var renderer = visual.GetComponent<SpriteRenderer>(); renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png"); renderer.color = new Color(.22f,.32f,.44f); renderer.sortingOrder = -4;
            var border = obj.AddComponent<LineRenderer>(); border.useWorldSpace = false; border.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Prototype/Prefabs/FoodTrajectory.mat");
            border.widthMultiplier=.05f; border.loop=true;border.positionCount=4;border.sortingOrder=-2;border.startColor=border.endColor=new Color(.65f,.8f,1f);
            border.SetPositions(new[]{new Vector3(-1.8f,-.8f),new Vector3(1.8f,-.8f),new Vector3(1.8f,.8f),new Vector3(-1.8f,.8f)});
            var labelRoot = new GameObject("Occupancy", typeof(RectTransform), typeof(Canvas)); labelRoot.transform.SetParent(obj.transform,false);
            labelRoot.transform.localPosition=new Vector3(0,1.02f,-.25f);labelRoot.transform.localScale=Vector3.one*.01f;
            var canvas=labelRoot.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.sortingOrder=12;
            var rect=(RectTransform)labelRoot.transform;rect.sizeDelta=new Vector2(380,45);
            var label = new GameObject("Label",typeof(RectTransform),typeof(Text));label.transform.SetParent(labelRoot.transform,false);
            var labelRect=(RectTransform)label.transform;labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            var text=label.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=30;text.color=Color.white;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.text="WAITING 0 / 3";
            obj.AddComponent<CustomerWaitingArea>().ConfigureLabel(text);
            GlobalDataPrototypeSetup.BindTree(obj);
            prefab=PrefabUtility.SaveAsPrefabAsset(obj,PrefabPath).GetComponent<CustomerWaitingArea>(); Object.DestroyImmediate(obj);
        }
        var product = AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(ProductPath);
        if (product == null)
        {
            product=ScriptableObject.CreateInstance<ShopProductDefinition>();var productFields=new SerializedObject(product);
            productFields.FindProperty("placement").enumValueIndex=(int)ShopProductDefinition.PlacementKind.RearWaitingArea;
            productFields.FindProperty("waitingAreaPrefab").objectReferenceValue=prefab;
            productFields.FindProperty("productId").stringValue="customer_waiting_area";
            productFields.FindProperty("displayName").stringValue="Customer Waiting Area";
            productFields.FindProperty("description").stringValue="Choose the top, bottom, left or right side of the truck, then confirm placement. Tap E to carry and seat customers. Capacity is set in Global Data.";
            productFields.FindProperty("price").intValue=150;productFields.FindProperty("purchaseLimit").intValue=1;
            productFields.FindProperty("icon").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Prototype/PrototypeSquare.png");
            productFields.ApplyModifiedPropertiesWithoutUndo();AssetDatabase.CreateAsset(product,ProductPath);
        }
        bool hasRearProduct=false;
        foreach(var entry in catalog.Products) if(entry!=null && entry.Placement==ShopProductDefinition.PlacementKind.RearWaitingArea)hasRearProduct=true;
        fields.Update();var products=fields.FindProperty("products");
        if(!hasRearProduct){int next=products.arraySize;products.arraySize++;products.GetArrayElementAtIndex(next).objectReferenceValue=product;}
        fields.FindProperty("waitingAreaProductInitialized").boolValue=true;fields.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(catalog);
    }
}
