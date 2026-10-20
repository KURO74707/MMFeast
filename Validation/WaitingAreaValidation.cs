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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WaitingAreaValidation
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly List<string> results=new();
    static PlayerController player;static PlayerCarry carry;static PlayerKitchenLayout layout;static TruckShop shop;static TruckShopUI ui;static TruckGridLayout grid;
    static GlobalGameSettings config;static ShopProductDefinition product;static Keyboard keyboard;static Mouse mouse;static CustomerWaitingArea area;
    static WaitingAreaValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);GlobalDataPrototypeSetup.ApplyToScene(scene);GlobalDataPrototypeSetup.ApplyToScene(scene);EditorSceneManager.SaveScene(scene);
                var settings=Object.FindAnyObjectByType<GlobalData>().Settings;int count=0;
                foreach(var entry in settings.ShopCatalog.Products)if(entry!=null&&entry.Placement==ShopProductDefinition.PlacementKind.RearWaitingArea){count++;Check(entry.IsValid(out _),path+" valid waiting-area product");}
                Check(count==1 && Object.FindObjectsByType<CustomerWaitingAreaMount>().Length==1,path+" one product and one rear mount after repeated setup");
                Check(Object.FindObjectsByType<CustomerWaitingArea>().Length==0,path+" waiting area exists only after purchase");
                Check(Field<CustomerWaitingAreaMount>(Object.FindAnyObjectByType<TruckShop>(),"rearMount")==Object.FindAnyObjectByType<CustomerWaitingAreaMount>(),path+" shop references local rear mount");
            }
            AssetDatabase.SaveAssets();SessionState.SetString("WaitingChecks",string.Join("\n",results));SessionState.SetBool("WaitingPending",true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("WaitingPending",false))return;
        SessionState.SetBool("WaitingPending",false);results.Clear();results.AddRange(SessionState.GetString("WaitingChecks","").Split('\n'));
        new GameObject("Waiting area tests").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true)
        {
            bool next;object current;
            try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}
            if(!next)break;yield return current;
        }
        Time.timeScale=1;File.WriteAllText("WAITING_AREA_RESULTS.txt","PASS: "+results.Count+" checks\n"+string.Join("\n",results));EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        player=Object.FindAnyObjectByType<PlayerController>();carry=player.GetComponent<PlayerCarry>();layout=player.GetComponent<PlayerKitchenLayout>();shop=player.GetComponent<TruckShop>();ui=Object.FindAnyObjectByType<TruckShopUI>();grid=Object.FindAnyObjectByType<TruckGridLayout>();
        config=Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);var catalog=Object.Instantiate(config.ShopCatalog);var list=new List<ShopProductDefinition>();
        foreach(var entry in catalog.Products){var clone=Object.Instantiate(entry);list.Add(clone);if(clone.Placement==ShopProductDefinition.PlacementKind.RearWaitingArea)product=clone;}
        Set(catalog,"products",list);Set(config,"shopCatalog",catalog);player.Configure(config);Set(carry,"settings",config);Set(layout,"settings",config);Set(shop,"settings",config);
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Frame();Frame();
        Move(new Vector3(1,-.2f));yield return new WaitForFixedUpdate();yield return null;
        Check(config.WaitingAreaCapacity==3 && product.Price==150,"Default capacity 3 and waiting-area price 150");
        Frame(Key.R);Frame();shop.SetCurrentBalance(0);Check(shop.TryBeginPurchase(product)&&shop.HasPendingPurchase&&shop.CurrentBalance==0,"Zero coins can start waiting-area upgrade");layout.ExitMode();shop.SetCurrentBalance(500);shop.CloseShop();shop.OpenShop();
        var reservations=Field<List<RectInt>>(grid,"reservedAreas");reservations.Add(new RectInt(0,0,6,4));Check(shop.CanPurchase(product,out _),"Rear upgrade can be purchased even when kitchen grid is full");reservations.RemoveAt(reservations.Count-1);
        var content=Field<Transform>(ui,"content");var scroll=content.parent.GetComponent<ScrollRect>();scroll.verticalNormalizedPosition=.7f;Canvas.ForceUpdateCanvases();Tick(ui,"Update");yield return null;
        Button buy=null;foreach(Transform row in content)if(row.gameObject.activeSelf&&row.name=="Product "+product.ProductId)buy=row.Find("Buy").GetComponent<Button>();
        Check(buy!=null&&buy.GetComponentInChildren<Text>().text=="Add & place","Waiting-area product has placement purchase button");
        Capture("WAITING_AREA_SHOP_PREVIEW.png");
        var rect=(RectTransform)buy.transform;Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
        Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(buy.transform),"Scrolled waiting-area buy button receives actual UI raycast");
        Pointer(0,point);Pointer(1,point);Pointer(0,point);Tick(ui,"Update");area=Object.FindAnyObjectByType<CustomerWaitingArea>();
        Check(layout.WaitingAreaPreview==area && shop.CurrentBalance==500 && shop.HasPendingPurchase,"Selecting waiting area starts unpaid preview");
        Set(player,"<Facing>k__BackingField",Vector2.down);Tick(layout,"Update");Check(layout.TryInteract(),"Confirm selected bottom side installs waiting area");
        Check(area!=null&&!shop.IsOpen&&shop.CurrentBalance==500&&shop.PurchaseCount(product)==1,"Confirmed purchase installs waiting area exactly once");
        Check(area.transform.parent==Object.FindAnyObjectByType<CustomerWaitingAreaMount>().transform && area.transform.position.y < grid.Interior.transform.position.y-2,"Waiting area installed behind truck");
        Check(grid.Tools.Count==2&&!shop.HasPendingPurchase&&!layout.IsPurchasePlacement,"Rear installation consumes no kitchen grid cells or pending tool preview");
        Check(area.Capacity==3&&Field<GlobalGameSettings>(area,"settings")==config,"Installed area uses shared designer capacity configuration");
        Frame();shop.OpenShop();Check(!shop.TryBeginPurchase(product)&&shop.CurrentBalance==500&&Object.FindObjectsByType<CustomerWaitingArea>().Length==1,"Repeated rear purchase is rejected without extra charge");shop.CloseShop();Frame();
        // A real short E press must capture, then seat, a live Customer.
        Move(new Vector3(3.8f,-3.1f));var first=Spawn(new Vector3(4.6f,-3.1f));Physics2D.SyncTransforms();
        Frame(Key.E);Check(carry.CurrentHeldCustomer==null,"Customer is not grabbed on initial E down");Frame();Tick(carry,"LateUpdate");
        Check(carry.CurrentHeldCustomer==first&&first.IsCarried&&first.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Kinematic,"Short E release grabs live customer");
        Tick(first.GetComponent<EnemySimpleAI>(),"FixedUpdate");Check(first.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero,"Carried Customer stops autonomous movement");
        var ingredient=Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab"),player.transform.position,Quaternion.identity);Physics2D.SyncTransforms();carry.TryPickup();
        Check(carry.CurrentHeldItem==null&&carry.CurrentHeldCustomer==first,"Customer carry is mutually exclusive with ingredients");Object.DestroyImmediate(ingredient.gameObject);
        Move(new Vector3(0,-1.5f));SeedInterior();Check(!layout.TryEnterMode(),"Holding Customer prevents kitchen organization");shop.OpenShop();Check(!shop.CanPurchase(product,out _),"Holding Customer prevents purchase placement");shop.CloseShop();Frame();
        Move(area.transform.position+Vector3.right*2.1f);Frame(Key.E);Frame();Tick(carry,"LateUpdate");
        Check(carry.CurrentHeldCustomer==null&&first.IsWaiting&&first.WaitingArea==area&&area.OccupiedCount==1,"Second E seats Customer in nearby waiting area");
        Check(first.GetComponent<Collider2D>().isTrigger&&first.GetComponent<Rigidbody2D>().simulated,"Waiting Customer remains detectable to food projectiles");
        Vector3 seat=first.transform.position;first.transform.position+=Vector3.right*8;Tick(area,"LateUpdate");Tick(first.GetComponent<EnemySimpleAI>(),"FixedUpdate");
        Check(Vector3.Distance(first.transform.position,seat)<.001f&&first.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero,"Waiting Customer is confined to a fixed seat even after external displacement");
        Move(first.transform.position+Vector3.right*.1f);Check(!carry.TryGrabCustomer()&&carry.CurrentHeldCustomer==null,"Waiting Customer cannot independently leave or be grabbed as a free Customer");
        var second=GrabAndSeat();var third=GrabAndSeat();Check(area.OccupiedCount==3,"Area accepts up to configured capacity");
        var fourth=Grab();Move(area.transform.position+Vector3.right*2.1f);carry.TryInteract();
        Check(area.OccupiedCount==3&&carry.CurrentHeldCustomer==fourth&&fourth.IsCarried,"Full area retains Customer in player's hands");
        Set(config,"waitingAreaCapacity",1);config.Validate();Tick(area,"LateUpdate");Check(area.OccupiedCount==3&&first.IsWaiting&&second.IsWaiting&&third.IsWaiting,"Lowering capacity does not remove existing Customers");
        carry.TryInteract();Check(carry.CurrentHeldCustomer==fourth,"Lowered capacity refuses additional Customer");
        Set(config,"waitingAreaCapacity",4);config.Validate();carry.TryInteract();Check(area.OccupiedCount==4&&carry.CurrentHeldCustomer==null,"Increasing capacity immediately allows another Customer");
        float san=player.SAN;first.Purify();Check(area.OccupiedCount==3&&first.IsPurified&&!first.IsWaiting&&player.SAN==san,"Purification immediately frees seat without SAN penalty");
        var replacement=GrabAndSeat();Check(area.OccupiedCount==4&&replacement.IsWaiting,"Freed seat can be reused before purified object removal delay");
        int meatBefore=Object.FindObjectsByType<IngredientPickup>().Length;second.TakeDamage(1000,player);
        Check(area.OccupiedCount==3&&second.IsDead&&player.SAN==san-config.SANPenaltyForKillingCustomer&&Object.FindObjectsByType<IngredientPickup>().Length==meatBefore+1,"Combat death frees seat and preserves existing drop/SAN behavior");
        foreach(var meat in Object.FindObjectsByType<IngredientPickup>())Object.Destroy(meat.gameObject);
        var dropped=Grab();Move(new Vector3(0,0));SeedInterior();carry.TryInteract();
        Check(!dropped.IsCarried&&carry.CurrentHeldCustomer==null&&dropped.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Dynamic&&!dropped.GetComponent<Collider2D>().isTrigger,"E away from area releases Customer and restores normal physics");
        Check(!grid.Interior.GetComponent<BoxCollider2D>().OverlapPoint(dropped.transform.position),"Ground release does not leave a free Customer inside player-only truck");Object.Destroy(dropped.gameObject);
        var disabled=Grab();player.enabled=false;Tick(carry,"LateUpdate");player.enabled=true;
        Check(!disabled.IsCarried&&carry.CurrentHeldCustomer==null,"Disabling player safely releases carried Customer");Object.Destroy(disabled.gameObject);Frame();
        var carriedPurified=Grab();carriedPurified.Purify();Check(carry.CurrentHeldCustomer==null&&carriedPurified.IsPurified,"Purifying a carried Customer immediately frees player's hands");
        var deactivated=Grab();deactivated.gameObject.SetActive(false);Check(carry.CurrentHeldCustomer==null&&!deactivated.IsCarried,"Deactivating Customer cleans up carrier ownership");Object.Destroy(deactivated.gameObject);
        // Fixed waiting seats must still participate in the actual food projectile AoE.
        var shot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<FoodProjectile>("Assets/Prototype/Prefabs/FoodProjectile.prefab"),area.transform.position+new Vector3(-4,0,-.2f),Quaternion.identity);
        Vector3 target=area.transform.position;target.z=-shot.GroundClearance;shot.Launch(FoodBallistics.LaunchVelocity(shot.transform.position,target,shot.Gravity,.2f),target,.2f);
        yield return new WaitForSeconds(.3f);
        Check(shot==null&&area.OccupiedCount==0&&third.IsPurified&&fourth.IsPurified&&replacement.IsPurified,"Actual food AoE purifies waiting Customers and releases every affected seat");
        var released=GrabAndSeat();area.gameObject.SetActive(false);Check(!released.IsWaiting&&released.gameObject.activeInHierarchy&&released.GetComponent<Rigidbody2D>().bodyType==RigidbodyType2D.Dynamic,"Disabling waiting area releases its Customers instead of trapping them");area.gameObject.SetActive(true);Object.Destroy(released.gameObject);
        yield return new WaitForSeconds(1.2f);Set(config,"waitingAreaCapacity",3);config.Validate();player.SetCurrentHP(100);player.SetCurrentSAN(100);
        var visibleA=GrabAndSeat();var visibleB=GrabAndSeat();Tick(area,"LateUpdate");
        Check(Field<Text>(area,"countLabel").text=="WAITING  2 / 3","Waiting area displays live occupancy and capacity");
        Move(new Vector3(2.5f,-3.1f));Tick(layout,"Update");yield return new WaitForSeconds(.2f);Capture("WAITING_AREA_PREVIEW.png");
        area.ReleaseAll();Object.Destroy(visibleA.gameObject);Object.Destroy(visibleB.gameObject);InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
    }
    static Customer Spawn(Vector3 pos){var c=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),pos,Quaternion.identity);c.GetComponent<EnemySimpleAI>().SetTarget(player.transform);return c;}
    static Customer Grab(){Move(new Vector3(4,-3.1f));var c=Spawn(new Vector3(4.8f,-3.1f));Physics2D.SyncTransforms();if(!carry.TryGrabCustomer()||carry.CurrentHeldCustomer!=c)throw new Exception("Expected Customer pickup");return c;}
    static Customer GrabAndSeat(){var c=Grab();Move(area.transform.position+Vector3.right*2.1f);carry.TryInteract();if(!c.IsWaiting)throw new Exception("Expected Customer seating");return c;}
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(shop,"Update");Tick(layout,"Update");Tick(ui,"Update");}
    static void Pointer(ushort buttons,Vector2 point){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=point});InputSystem.Update();Tick(player,"Update");Object.FindAnyObjectByType<InputSystemUIInputModule>().Process();}
    static void Move(Vector3 pos){player.transform.position=pos;player.GetComponent<Rigidbody2D>().position=pos;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static void SeedInterior()=>typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
    static void Capture(string file)
    {
        var camera=Camera.main;camera.orthographicSize=6;camera.GetComponent<SimpleCameraShake>().enabled=false;var canvas=ui.GetComponentInParent<Canvas>();var previous=canvas.renderMode;var sorting=canvas.sortingOrder;var order=canvas.overrideSorting;
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;
        var render=new RenderTexture(1280,720,24);camera.targetTexture=render;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(file,pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);canvas.renderMode=previous;canvas.sortingOrder=sorting;canvas.overrideSorting=order;Canvas.ForceUpdateCanvases();
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("WAITING_AREA_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}
