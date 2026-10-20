using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class CookingStationUpgradeValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new();
    static PlayerController player;static PlayerCarry carry;static PlayerKitchenLayout layout;static TruckShop shop;static TruckShopUI ui;static TruckGridLayout grid;
    static GlobalGameSettings config;static ShopProductDefinition product;static Keyboard keyboard;static Mouse mouse;
    static CookingStationUpgradeValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);GlobalDataPrototypeSetup.ApplyToScene(scene);GlobalDataPrototypeSetup.ApplyToScene(scene);EditorSceneManager.SaveScene(scene);
                var settings=Object.FindAnyObjectByType<GlobalData>().Settings;int count=0;
                foreach(var p in settings.ShopCatalog.Products)if(p.ToolPrefab!=null&&p.ToolPrefab.GetComponent<CookingStation>()!=null){count++;Check(p.IsValid(out _)&&p.ToolPrefab.Footprint==new Vector2Int(2,1),path+" valid 2x1 Cooking Station upgrade");}
                Check(count==1&&settings.ShopCatalog.Products.Count==4,path+" one Cooking Station entry after repeated setup");
                Check(Object.FindObjectsByType<CookingStation>().Length==1,path+" no extra station exists before adding");
            }
            AssetDatabase.SaveAssets();SessionState.SetString("CookUpgradeChecks",string.Join("\n",checks));SessionState.SetBool("CookUpgradePending",true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("CookUpgradePending",false))return;
        SessionState.SetBool("CookUpgradePending",false);checks.Clear();checks.AddRange(SessionState.GetString("CookUpgradeChecks","").Split('\n'));
        new GameObject("Cooking station upgrade checks").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        File.WriteAllText("COOKING_UPGRADE_RESULTS.txt","PASS: "+checks.Count+" checks\n"+string.Join("\n",checks));Time.timeScale=1;EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawn in Object.FindObjectsByType<EnemySpawner>())spawn.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        player=Object.FindAnyObjectByType<PlayerController>();carry=player.GetComponent<PlayerCarry>();layout=player.GetComponent<PlayerKitchenLayout>();shop=player.GetComponent<TruckShop>();ui=Object.FindAnyObjectByType<TruckShopUI>();grid=Object.FindAnyObjectByType<TruckGridLayout>();
        var original=Object.FindAnyObjectByType<CookingStation>();var shared=Object.FindAnyObjectByType<GlobalData>().Settings;
        config=Object.Instantiate(shared);var catalog=Object.Instantiate(config.ShopCatalog);var entries=new List<ShopProductDefinition>();
        foreach(var p in catalog.Products){if(p.ToolPrefab!=null&&p.ToolPrefab.GetComponent<CookingStation>()!=null){product=Object.Instantiate(p);entries.Add(product);}else entries.Add(p);}
        Set(catalog,"products",entries);Set(config,"shopCatalog",catalog);player.Configure(config);Set(shop,"settings",config);Set(layout,"settings",config);Set(carry,"settings",config);
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Frame();Frame();Move(new Vector3(0,-.2f));SeedInside();shop.SetCurrentBalance(0);
        Frame(Key.R);Frame();yield return null;ui.Refresh();var content=Field<Transform>(ui,"content");var scroll=content.parent.GetComponent<ScrollRect>();scroll.verticalNormalizedPosition=.35f;Canvas.ForceUpdateCanvases();yield return null;
        Button button=null;foreach(Transform row in content)if(row.gameObject.activeSelf&&row.name=="Product "+product.ProductId)button=row.Find("Buy").GetComponent<Button>();
        Check(button!=null&&button.interactable&&button.GetComponentInChildren<Text>().text=="Add & place","Cooking Station has a free Add & place button at zero coins");
        Check(!button.transform.parent.Find("Price").gameObject.activeSelf,"Cooking Station upgrade shows no price");Capture("COOKING_UPGRADE_MENU_PREVIEW.png");
        var rect=(RectTransform)button.transform;Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(button.transform),"Scrolled Cooking Station button receives actual UI raycast");Pointer(0,point);Pointer(1,point);Pointer(0,point);
        var firstTool=layout.HeldTool;var first=firstTool.GetComponent<CookingStation>();
        Check(!shop.IsOpen&&layout.IsPurchasePlacement&&first!=null&&firstTool.IsCarried&&grid.Tools.Count==2&&shop.CurrentBalance==0,"Actual UI click begins unregistered station grid preview for free");
        Check(Field<FoodTruckInteriorTrigger>(first,"truckInterior")==grid.Interior&&Field<GlobalGameSettings>(first,"settings")==shared,"Runtime station gets local truck and shared settings");
        Check(!first.CanUse(player.transform)&&!first.TryCook(player.transform),"Unplaced station cannot be used or cooked on");
        AimAt(new Vector2Int(2,0),firstTool);Check(!layout.TryInteract()&&firstTool.IsCarried&&grid.Tools.Count==2,"Adding cannot replace or swap occupied existing Cooking Station");
        AimAt(new Vector2Int(-1,0),firstTool);Check(!layout.TryInteract()&&firstTool.IsCarried,"Out-of-bounds station preview stays uninstalled");
        AimAt(new Vector2Int(0,0),firstTool);Frame();Frame(Key.E);Frame();
        Check(!layout.IsOrganizing&&!firstTool.IsCarried&&firstTool.Cell==new Vector2Int(0,0)&&grid.Tools.Count==3&&shop.PurchaseCount(product)==1&&shop.CurrentBalance==0,"E confirms first 2x1 station exactly once with no resource cost");
        Check(!shop.TryConfirmPlacement(firstTool,firstTool.Cell,out _)&&grid.Tools.Count==3,"Duplicate station confirmation cannot register twice");Frame();
        Check(shop.OpenShop()&&shop.TryBeginPurchase(product),"Can add another independent Cooking Station");var secondTool=layout.HeldTool;var second=secondTool.GetComponent<CookingStation>();AimAt(new Vector2Int(4,0),secondTool);
        Check(layout.TryInteract()&&Object.FindObjectsByType<CookingStation>().Length==3&&shop.PurchaseCount(product)==2,"Multiple extra stations coexist with original");Frame();
        Check(Field<Transform>(first,"cookingPoint")!=Field<Transform>(second,"cookingPoint")&&Field<Transform>(first,"cookingPoint").IsChildOf(first.transform)&&Field<Transform>(second,"cookingPoint").IsChildOf(second.transform),"Each station owns an independent CookingPoint");
        MoveNear(firstTool);LongE();Frame(Key.E);Frame();Check(layout.HeldTool==firstTool&&firstTool.IsCarried,"Organization E lifts the newly added Cooking Station");
        AimAt(new Vector2Int(0,1),firstTool);Frame(Key.E);Frame();Check(firstTool.Cell==new Vector2Int(0,1)&&!firstTool.IsCarried,"Added station moves to another free grid cell");
        MoveNear(firstTool);Frame(Key.E);Frame();AimAt(original.GetComponent<PlaceableKitchenTool>().Cell,firstTool);Frame(Key.E);Frame();
        Check(firstTool.Cell==new Vector2Int(2,0)&&original.GetComponent<PlaceableKitchenTool>().Cell==new Vector2Int(0,1)&&grid.ValidateLayout(out _),"Added and original Cooking Stations atomically exchange grid cells");layout.ExitMode();Frame();
        MoveNear(firstTool);SeedInside();Check(first.CanUse(player.transform),"Installed station is usable inside its local truck");var foodA=PlaceRaw(first);
        MoveNear(secondTool);SeedInside();var foodB=PlaceRaw(second);
        Check(first.CurrentMeat==foodA&&second.CurrentMeat==foodB&&original.CurrentMeat==null,"Separate stations retain separate food states");
        MoveNear(firstTool);SeedInside();Check(first.TryCook(player.transform)&&foodA.IsCooked&&!foodB.IsCooked,"Cooking on one added station does not cook another station's food");
        LongE();Frame(Key.E);Frame();Check(layout.HeldTool==null&&!firstTool.CanMove&&first.CurrentMeat==foodA,"Occupied station remains immovable until food is removed");
        layout.ExitMode();Frame();MoveNear(firstTool);SeedInside();carry.TryInteract();Check(carry.CurrentHeldItem==foodA&&foodA.IsCooked&&first.CurrentMeat==null,"Existing E carry interaction retrieves cooked food from added station");Object.DestroyImmediate(foodA.gameObject);
        MoveNear(secondTool);SeedInside();carry.TryInteract();Check(carry.CurrentHeldItem==foodB&&!foodB.IsCooked&&second.CurrentMeat==null,"Retrieving another station keeps its raw food unchanged");Object.DestroyImmediate(foodB.gameObject);
        MoveNear(firstTool);LongE();Frame(Key.E);Frame();var before=firstTool.Cell;AimAt(new Vector2Int(0,0),firstTool);layout.ExitMode();
        Check(firstTool.Cell==before&&!firstTool.IsCarried&&grid.ValidateLayout(out _),"Canceling movement restores added station and leaves grid valid");Frame();
        Set(product,"purchaseLimit",2);shop.OpenShop();Check(!shop.CanPurchase(product,out _),"Designer add limit applies to Cooking Stations");Set(product,"purchaseLimit",0);
        var reservations=Field<List<RectInt>>(grid,"reservedAreas");reservations.Add(new RectInt(0,0,6,4));Check(!shop.CanPurchase(product,out _)&&!shop.TryBeginPurchase(product),"Full kitchen grid rejects another station without a preview");reservations.RemoveAt(reservations.Count-1);
        Set(product,"listed",false);Check(!shop.TryBeginPurchase(product),"Unavailable station upgrade cannot be added");Set(product,"listed",true);
        Check(shop.TryBeginPurchase(product),"Station upgrade can restart after rejected conditions");var cancelled=layout.HeldTool;layout.ExitMode();yield return null;
        Check(cancelled==null&&Object.FindObjectsByType<CookingStation>().Length==3&&grid.Tools.Count==4&&shop.CurrentBalance==0,"Canceling new station removes only preview without coins or occupied cells");
        Frame();shop.OpenShop();shop.TryBeginPurchase(product);var disabled=layout.HeldTool;player.enabled=false;player.enabled=true;Frame();yield return null;
        Check(disabled==null&&!shop.HasPendingPurchase&&Object.FindObjectsByType<CookingStation>().Length==3,"Disabling player cleans up uninstalled Cooking Station");
        MoveNear(firstTool);SeedInside();layout.TryEnterMode();Tick(layout,"Update");Capture("COOKING_UPGRADE_GRID_PREVIEW.png");layout.ExitMode();
        InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
    }
    static IngredientPickup PlaceRaw(CookingStation station){var meat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab"),player.transform.position,Quaternion.identity);Physics2D.SyncTransforms();carry.TryPickup();carry.TryInteract();if(station.CurrentMeat!=meat)throw new Exception("Expected food at nearest station");return meat;}
    static void MoveNear(PlaceableKitchenTool tool){Move(tool.transform.position+Vector3.down*.6f);SeedInside();}
    static void AimAt(Vector2Int cell,PlaceableKitchenTool tool){Set(player,"<Facing>k__BackingField",Vector2.left);Move(grid.CellCenter(cell,tool.Footprint)+Vector3.right*config.KitchenPlacementDistance);SeedInside();}
    static void LongE(){Frame();Frame(Key.E);Set(player,"interactStartedAt",Time.time-1);Frame(Key.E);Frame();}
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(shop,"Update");Tick(layout,"Update");Tick(ui,"Update");}
    static void Pointer(ushort buttons,Vector2 p){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=p});InputSystem.Update();Tick(player,"Update");Object.FindAnyObjectByType<InputSystemUIInputModule>().Process();}
    static void Move(Vector3 p){player.transform.position=p;player.GetComponent<Rigidbody2D>().position=p;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static void SeedInside()=>typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
    static void Capture(string name)
    {
        var camera=Camera.main;camera.orthographicSize=6;camera.GetComponent<SimpleCameraShake>().enabled=false;var canvas=ui.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(name,texture.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;target.Release();Object.Destroy(texture);Object.Destroy(target);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("COOKING_UPGRADE_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",checks));EditorApplication.Exit(1);}
}
