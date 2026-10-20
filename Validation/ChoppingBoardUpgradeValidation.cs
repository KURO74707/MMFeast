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
public static class ChoppingBoardUpgradeValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new();
    static PlayerController player;static PlayerKitchenLayout layout;static TruckShop shop;static TruckShopUI ui;static TruckGridLayout grid;
    static GlobalGameSettings config;static ShopProductDefinition product;static Keyboard keyboard;static Mouse mouse;
    static ChoppingBoardUpgradeValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            var definition=Object.Instantiate(AssetDatabase.LoadAssetAtPath<ShopProductDefinition>(TruckShopPrototypeSetup.ChoppingBoardProductPath));
            var plain=new GameObject("Placement-only regression tool",typeof(PlaceableKitchenTool));var tool=plain.GetComponent<PlaceableKitchenTool>();Set(definition,"toolPrefab",tool);
            Check(definition.IsValid(out _),"Placement-only upgrade remains valid without a ChoppingBoard marker component");
            tool.enabled=false;Check(!definition.IsValid(out string disabled)&&disabled.Contains("Enable"),"Disabled placement component has explicit failure reason");tool.enabled=true;
            plain.SetActive(false);Check(!definition.IsValid(out _),"Inactive prefab root is still rejected");plain.SetActive(true);
            Set(definition,"toolPrefab",null);Check(!definition.IsValid(out string missing)&&missing.Contains("reference is missing"),"Missing tool reference has distinct failure reason");Object.DestroyImmediate(plain);Object.DestroyImmediate(definition);
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);GlobalDataPrototypeSetup.ApplyToScene(scene);GlobalDataPrototypeSetup.ApplyToScene(scene);EditorSceneManager.SaveScene(scene);
                var settings=Object.FindAnyObjectByType<GlobalData>().Settings;int count=0;
                foreach(var p in settings.ShopCatalog.Products)if(p.ToolPrefab!=null&&p.ToolPrefab.GetComponent<ChoppingBoard>()!=null){count++;Check(p.IsValid(out _)&&p.ToolPrefab.Footprint==Vector2Int.one,path+" valid 1x1 Chopping Board upgrade");}
                Check(count==1&&settings.ShopCatalog.Products.Count==4,path+" one board entry after repeated setup");Check(Object.FindObjectsByType<ChoppingBoard>().Length==0,path+" no board before adding");
            }
            AssetDatabase.SaveAssets();SessionState.SetString("BoardChecks",string.Join("\n",checks));SessionState.SetBool("BoardPending",true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("BoardPending",false))return;SessionState.SetBool("BoardPending",false);checks.Clear();checks.AddRange(SessionState.GetString("BoardChecks","").Split('\n'));
        new GameObject("Chopping Board validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        File.WriteAllText("CHOPPING_BOARD_RESULTS.txt","PASS: "+checks.Count+" checks\n"+string.Join("\n",checks));Time.timeScale=1;EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawn in Object.FindObjectsByType<EnemySpawner>())spawn.enabled=false;foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        player=Object.FindAnyObjectByType<PlayerController>();layout=player.GetComponent<PlayerKitchenLayout>();shop=player.GetComponent<TruckShop>();ui=Object.FindAnyObjectByType<TruckShopUI>();grid=Object.FindAnyObjectByType<TruckGridLayout>();
        var station=Object.FindAnyObjectByType<CookingStation>().GetComponent<PlaceableKitchenTool>();var cannon=Object.FindAnyObjectByType<FoodCannon>().GetComponent<PlaceableKitchenTool>();
        config=Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);var catalog=Object.Instantiate(config.ShopCatalog);var entries=new List<ShopProductDefinition>();
        foreach(var p in catalog.Products){if(p.ToolPrefab!=null&&p.ToolPrefab.GetComponent<ChoppingBoard>()!=null){product=Object.Instantiate(p);entries.Add(product);}else entries.Add(p);}
        Set(catalog,"products",entries);Set(config,"shopCatalog",catalog);player.Configure(config);Set(shop,"settings",config);Set(layout,"settings",config);
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Frame();Frame();Move(new Vector3(0,-.2f));shop.SetCurrentBalance(0);Frame(Key.R);Frame();yield return null;
        ui.Refresh();var content=Field<Transform>(ui,"content");content.parent.GetComponent<ScrollRect>().verticalNormalizedPosition=0;Canvas.ForceUpdateCanvases();yield return null;
        Button button=null;foreach(Transform row in content)if(row.gameObject.activeSelf&&row.name=="Product "+product.ProductId)button=row.Find("Buy").GetComponent<Button>();
        Check(button!=null&&button.interactable&&button.GetComponentInChildren<Text>().text=="Add & place"&&!button.transform.parent.Find("Price").gameObject.activeSelf,"Board has a free Add & place button with no price at zero coins");Capture("CHOPPING_BOARD_MENU_PREVIEW.png");
        var rect=(RectTransform)button.transform;Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(button.transform),"Scrolled board button receives real UI raycast");Pointer(0,point);Pointer(1,point);Pointer(0,point);
        var first=layout.HeldTool;Check(first!=null&&first.GetComponent<ChoppingBoard>()!=null&&first.IsCarried&&first.Footprint==Vector2Int.one&&grid.Tools.Count==2&&shop.CurrentBalance==0,"Real click creates unregistered 1x1 board preview for free");
        Check(first.GetComponent<CookingStation>()==null&&first.GetComponent<FoodCannon>()==null,"Board is an independent placement-only tool");
        AimAt(station.Cell,first);Check(!layout.TryInteract()&&first.IsCarried&&grid.Tools.Count==2,"New board cannot overwrite occupied station");AimAt(new Vector2Int(-1,0),first);Check(!layout.TryInteract()&&first.IsCarried,"Board cannot be installed outside grid");
        AimAt(Vector2Int.zero,first);Frame();Frame(Key.E);Frame();Check(!layout.IsOrganizing&&!first.IsCarried&&first.Cell==Vector2Int.zero&&grid.Tools.Count==3&&shop.CurrentBalance==0&&shop.PurchaseCount(product)==1,"E release confirms board once without resources");
        Check(!shop.TryConfirmPlacement(first,first.Cell,out _),"Duplicate board confirmation rejected");Frame();shop.OpenShop();Check(shop.TryBeginPurchase(product),"Can freely add a second board");var second=layout.HeldTool;AimAt(new Vector2Int(1,0),second);
        Check(layout.TryInteract()&&Object.FindObjectsByType<ChoppingBoard>().Length==2&&grid.Tools.Count==4&&shop.CurrentBalance==0,"Two separate board instances occupy separate cells");Frame();
        Near(first);LongE();Frame(Key.E);Frame();Check(layout.HeldTool==first&&first.IsCarried,"Organization short E lifts installed board");AimAt(new Vector2Int(0,1),first);Frame(Key.E);Frame();Check(first.Cell==new Vector2Int(0,1)&&!first.IsCarried,"Board moves to a free grid cell");
        Near(first);Frame(Key.E);Frame();AimAt(station.Cell,first);Frame(Key.E);Frame();Check(first.Cell==new Vector2Int(2,0)&&station.Cell==new Vector2Int(0,1)&&grid.ValidateLayout(out _),"1x1 board exchanges with 2x1 Cooking Station when both fit");
        Near(first);Frame(Key.E);Frame();AimAt(cannon.Cell,first);Frame(Key.E);Frame();Check(first.Cell==new Vector2Int(2,2)&&cannon.Cell==new Vector2Int(2,0)&&grid.ValidateLayout(out _),"1x1 board exchanges with 2x2 cannon when both fit");
        Near(first);Frame(Key.E);Frame();AimAt(second.Cell,first);Frame(Key.E);Frame();Check(first.Cell==new Vector2Int(1,0)&&second.Cell==new Vector2Int(2,2)&&grid.ValidateLayout(out _),"Boards exchange with each other without duplicate occupancy");
        Near(first);Frame(Key.E);Frame();var original=first.Cell;AimAt(new Vector2Int(0,3),first);layout.ExitMode();Check(first.Cell==original&&!first.IsCarried&&grid.ValidateLayout(out _),"Cancel relocation restores board original cell");Frame();
        Set(product,"purchaseLimit",2);shop.OpenShop();Check(!shop.CanPurchase(product,out _),"Designer board add limit enforced");Set(product,"purchaseLimit",0);var reservations=Field<List<RectInt>>(grid,"reservedAreas");reservations.Add(new RectInt(0,0,6,4));Check(!shop.TryBeginPurchase(product),"Full grid prevents another board");reservations.RemoveAt(reservations.Count-1);
        Set(product,"listed",false);Check(!shop.TryBeginPurchase(product),"Unlisted board upgrade cannot be added");Set(product,"listed",true);Check(shop.TryBeginPurchase(product),"Board addition can restart after rejection");var cancelled=layout.HeldTool;layout.ExitMode();yield return null;
        Check(cancelled==null&&Object.FindObjectsByType<ChoppingBoard>().Length==2&&grid.Tools.Count==4&&shop.CurrentBalance==0,"Cancel addition removes preview and keeps installed boards and coins");Frame();
        shop.OpenShop();shop.TryBeginPurchase(product);var disabled=layout.HeldTool;player.enabled=false;player.enabled=true;Frame();yield return null;Check(disabled==null&&!shop.HasPendingPurchase&&Object.FindObjectsByType<ChoppingBoard>().Length==2,"Disabling player cleans up uninstalled board");
        Near(first);layout.TryEnterMode();Tick(layout,"Update");Capture("CHOPPING_BOARD_GRID_PREVIEW.png");layout.ExitMode();InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
    }
    static void Near(PlaceableKitchenTool tool)=>Move(tool.transform.position+Vector3.right*.1f);
    static void AimAt(Vector2Int cell,PlaceableKitchenTool tool){Set(player,"<Facing>k__BackingField",Vector2.left);Move(grid.CellCenter(cell,tool.Footprint)+Vector3.right*config.KitchenPlacementDistance);}
    static void LongE(){Frame();Frame(Key.E);Set(player,"interactStartedAt",Time.time-1);Frame(Key.E);Frame();}
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(shop,"Update");Tick(layout,"Update");Tick(ui,"Update");}
    static void Pointer(ushort buttons,Vector2 p){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=p});InputSystem.Update();Tick(player,"Update");Object.FindAnyObjectByType<InputSystemUIInputModule>().Process();}
    static void Move(Vector3 p){player.transform.position=p;player.GetComponent<Rigidbody2D>().position=p;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});}
    static void Capture(string name)
    {
        var camera=Camera.main;camera.orthographicSize=6;camera.GetComponent<SimpleCameraShake>().enabled=false;var canvas=ui.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes(name,texture.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;target.Release();Object.Destroy(texture);Object.Destroy(target);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("CHOPPING_BOARD_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",checks));EditorApplication.Exit(1);}
}
