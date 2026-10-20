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
public static class TruckShopValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new();
    static PlayerController player; static TruckShop shop; static PlayerKitchenLayout layout; static PlayerCarry carry;
    static TruckGridLayout grid; static TruckShopUI ui; static Keyboard keyboard; static Mouse mouse;
    static ShopProductDefinition product; static GlobalGameSettings config;
    static TruckShopValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            var shared = GlobalDataPrototypeSetup.GetOrCreateSettings();
            Check(new SerializedObject(shared).FindProperty("initialCoinsMigrated").boolValue && shared.InitialCoins==shared.ShopCatalog.StartingBalance,"Existing initial coins migrated from legacy catalog");
            var migrationConfig=Object.Instantiate(shared);var migrationCatalog=Object.Instantiate(shared.ShopCatalog);
            Set(migrationConfig,"shopCatalog",migrationCatalog);Set(migrationConfig,"initialCoinsMigrated",false);Set(migrationCatalog,"startingBalance",321);
            var migrate=typeof(TruckShopPrototypeSetup).GetMethod("MigrateCoins",BindingFlags.Static|BindingFlags.NonPublic);migrate.Invoke(null,new object[]{migrationConfig});
            Check(migrationConfig.InitialCoins==321,"Migration preserves custom legacy initial coins");Set(migrationCatalog,"startingBalance",999);migrate.Invoke(null,new object[]{migrationConfig});
            Check(migrationConfig.InitialCoins==321,"Repeated migration never overwrites configured initial coins");Object.DestroyImmediate(migrationConfig);Object.DestroyImmediate(migrationCatalog);
            Check(typeof(ShopProductDefinition).GetField("price",Private).IsDefined(typeof(HideInInspector),true) && typeof(TruckShopCatalog).GetField("startingBalance",Private).IsDefined(typeof(HideInInspector),true),"Legacy price and initial balance remain serialized but hidden");
            foreach (var path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                var scene=EditorSceneManager.OpenScene(path);GlobalDataPrototypeSetup.ApplyToScene(scene);GlobalDataPrototypeSetup.ApplyToScene(scene);EditorSceneManager.SaveScene(scene);
                Check(Object.FindObjectsByType<TruckShop>().Length==1 && Object.FindObjectsByType<TruckShopUI>().Length==1,path+" one shop and modal UI");
                Check(Object.FindObjectsByType<EventSystem>().Length==1 && Object.FindAnyObjectByType<InputSystemUIInputModule>().leftClick!=null,path+" existing Input System UI bindings wired");
                Check(Field<GlobalGameSettings>(Object.FindAnyObjectByType<TruckShop>(),"settings")==shared && shared.ShopCatalog.Products.Count==4,path+" shared configurable cannon, waiting-area and cooking-station catalog");
                Check(shared.Shop.bindings[0].path=="<Keyboard>/r" && shared.ShopCatalog.Products[0].IsValid(out _),path+" R and valid cannon product");
                Check(!Field<GameObject>(Object.FindAnyObjectByType<TruckShopUI>(),"overlay").activeSelf,path+" shop initially closed");
                var window=Object.FindAnyObjectByType<TruckShopUI>().transform.Find("Overlay/Window");
                Check(window.Find("Title").GetComponent<Text>().text=="TRUCK UPGRADES" && !window.Find("Balance").gameObject.activeSelf && !window.Find("Viewport/Content/ProductTemplate/Price").gameObject.activeSelf,path+" upgrade title, hidden balance and hidden prices saved in scene");
            }
            AssetDatabase.SaveAssets();SessionState.SetString("ShopChecks",string.Join("\n",results));SessionState.SetBool("ShopPending",true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("ShopPending",false))return;
        SessionState.SetBool("ShopPending",false);results.Clear();results.AddRange(SessionState.GetString("ShopChecks","").Split('\n'));
        new GameObject("Truck shop tests").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true)
        {
            bool next;object current;
            try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}
            if(!next)break;yield return current;
        }
        File.WriteAllText("TRUCK_SHOP_RESULTS.txt","PASS: "+results.Count+" checks\n"+string.Join("\n",results));Time.timeScale=1;EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);
        yield return null;
        player=Object.FindAnyObjectByType<PlayerController>();shop=player.GetComponent<TruckShop>();layout=player.GetComponent<PlayerKitchenLayout>();carry=player.GetComponent<PlayerCarry>();
        grid=Object.FindAnyObjectByType<TruckGridLayout>();ui=Object.FindAnyObjectByType<TruckShopUI>();
        config=Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);
        var catalog=Object.Instantiate(config.ShopCatalog);product=Object.Instantiate(catalog.Products[0]);Set(catalog,"products",new List<ShopProductDefinition>{product});Set(config,"shopCatalog",catalog);
        player.Configure(config);Set(shop,"settings",config);Set(layout,"settings",config);Set(carry,"settings",config);
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();InputSystem.QueueStateEvent(mouse,new MouseState());Frame();Frame();
        Check(shop.CurrentBalance==500 && product.Price==100,"Default initial balance and product price");
        Frame(Key.R);Check(shop.IsOpen && Time.timeScale==0,"Real R action opens shop and pauses game");Frame(Key.R);Check(shop.IsOpen,"Held R does not repeatedly toggle shop");Frame();
        Check(!shop.CanPurchase(product,out _) && !shop.TryBeginPurchase(product) && shop.CurrentBalance==500,"Outside truck purchase rejected without spending");
        Frame(Key.R);Check(!shop.IsOpen && Time.timeScale==1,"R closes shop and restores game speed");Frame();
        Move(new Vector3(1,-.2f));yield return new WaitForFixedUpdate();yield return null;
        Check(grid.Interior.IsPlayerInside(player.transform),"Player enters truck through real physics");
        Frame(Key.R);Frame();Check(shop.IsOpen && BuyButton().interactable,"In-truck empty-handed player can buy");
        Frame(Key.D,Key.Space,Key.E);Tick(player,"FixedUpdate");var combat=player.GetComponent<PlayerCombat>();float lastAttack=Field<float>(combat,"lastAttackTime");combat.TryAttack();
        Check(player.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero && !player.DashPressed && !player.InteractPressed && Field<float>(combat,"lastAttackTime")==lastAttack,"Shop blocks movement, dash, interaction and attacks");Frame();
        Set(product,"price",125);ui.Refresh();Check(!FindRowText("Price").gameObject.activeSelf && !Field<Text>(ui,"balanceText").gameObject.activeSelf,"Upgrade list hides legacy prices and coin balance");Set(product,"price",100);ui.Refresh();
        shop.SetCurrentBalance(0);ui.Refresh();Check(BuyButton().interactable,"Zero coins allow actual upgrade UI button");yield return null;Capture("TRUCK_SHOP_PREVIEW.png");
        var button=BuyButton();Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
        Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var raycasts=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},raycasts);
        Check(raycasts.Count>0 && raycasts[0].gameObject.transform.IsChildOf(button.transform),"Buy button receives actual canvas raycast");
        Pointer(0,screen);Pointer(1,screen);Pointer(0,screen);Tick(ui,"Update");
        Check(!shop.IsOpen && layout.IsPurchasePlacement && shop.HasPendingPurchase && Time.timeScale==1,"Actual UI pointer click starts grid placement and resumes game");
        var preview=layout.HeldTool;
        Check(preview!=null && preview.IsCarried && grid.Tools.Count==2 && shop.CurrentBalance==0,"Pending preview reserves no cells and spends no credits");
        Check(!preview.GetComponent<FoodCannon>().CanInteract(player.transform),"Pending cannon cannot be used as installed cannon");
        Check(!shop.TryBeginPurchase(product) && Object.FindObjectsByType<FoodCannon>().Length==2,"Repeated purchase request during placement cannot duplicate preview");
        AimAt(new Vector2Int(2,2),preview);Check(!layout.TryInteract() && shop.CurrentBalance==0 && grid.Tools.Count==2,"Purchase cannot swap into occupied cells and spends nothing");
        AimAt(new Vector2Int(-1,0),preview);Check(!layout.TryInteract() && preview.IsCarried && shop.CurrentBalance==0,"Invalid placement remains pending without spending");
        Set(product,"price",175);AimAt(new Vector2Int(0,0),preview);
        Check(layout.TryInteract() && !shop.HasPendingPurchase && !layout.IsOrganizing && grid.Tools.Count==3,"Valid placement installs and registers exactly one additional cannon");
        var first=preview.GetComponent<FoodCannon>();
        Check(shop.CurrentBalance==0 && shop.PurchaseCount(product)==1,"Successful free placement records one addition without spending coins");
        Check(Field<GlobalGameSettings>(first,"settings")==Object.FindAnyObjectByType<GlobalData>().Settings && Field<FoodTruckInteriorTrigger>(first,"truckInterior")==grid.Interior && Field<Camera>(first,"aimCamera")==Camera.main,"Runtime cannon retains shared config and receives local scene references");
        Check(!shop.TryConfirmPlacement(preview,new Vector2Int(0,0),out _) && shop.CurrentBalance==0,"Repeated confirmation cannot charge twice");Frame();
        shop.OpenShop();shop.SetCurrentBalance(99);Set(product,"price",100);ui.Refresh();
        Check(BuyButton().interactable && shop.CanPurchase(product,out _) && shop.CurrentBalance==99,"Legacy price never gates upgrade availability");shop.SetCurrentBalance(0);
        Set(product,"purchaseLimit",1);ui.Refresh();Check(!BuyButton().interactable && !shop.TryBeginPurchase(product),"Designer purchase limit enforced");Set(product,"purchaseLimit",0);
        var reserved=Field<List<RectInt>>(grid,"reservedAreas");reserved.Add(new RectInt(0,0,6,4));
        Check(!shop.TryBeginPurchase(product) && shop.CurrentBalance==0,"Full grid rejects purchase without charge");reserved.RemoveAt(reserved.Count-1);
        Set(product,"listed",false);ui.Refresh();Check(!shop.TryBeginPurchase(product) && ActiveRows()==0,"Unlisted product disappears and cannot be bought");Set(product,"listed",true);ui.Refresh();
        var variant=Object.Instantiate(product);Set(variant,"productId","variant_cannon");Set(variant,"displayName","Designer Cannon Variant");Set(variant,"price",150);
        Set(catalog,"products",new List<ShopProductDefinition>{product,variant});ui.Refresh();Check(ActiveRows()==2,"Designer can add another listed cannon product without code");
        Set(variant,"productId",product.ProductId);Check(!shop.TryBeginPurchase(product),"Duplicate product IDs reject ambiguous purchases");Set(catalog,"products",new List<ShopProductDefinition>{product});ui.Refresh();
        Check(shop.TryBeginPurchase(product),"Can begin a second purchase");Frame();Frame(Key.E);Set(player,"interactStartedAt",Time.time-.6f);Frame(Key.E);
        Check(!shop.HasPendingPurchase && !layout.IsOrganizing && shop.CurrentBalance==0 && grid.Tools.Count==3,"Long Interact cancels preview with no charge or occupied cells");Frame();
        shop.OpenShop();Check(shop.TryBeginPurchase(product),"Purchase can restart after cancellation");Frame();Frame(Key.R);
        Check(shop.IsOpen && !shop.HasPendingPurchase && shop.CurrentBalance==0,"Tab cancels placement and reopens shop without spending");Frame();shop.CloseShop();Frame();
        Time.timeScale=.5f;shop.OpenShop();shop.CloseShop();Check(Time.timeScale==.5f,"Closing shop restores prior game speed rather than forcing normal speed");Time.timeScale=1;
        shop.OpenShop();player.enabled=false;player.enabled=true;Check(!shop.IsOpen && Time.timeScale==1,"Disabling player while shop open cannot leave game paused");Frame();
        shop.OpenShop();Check(shop.TryBeginPurchase(product),"Purchase restart after enable cycle");var cancelled=layout.HeldTool;player.enabled=false;player.enabled=true;
        Check(!shop.HasPendingPurchase && !layout.IsPurchasePlacement && !cancelled.gameObject.activeSelf && shop.CurrentBalance==0,"Player disable cancels uncommitted purchase without refund bookkeeping");Frame();
        shop.OpenShop();Check(shop.TryBeginPurchase(product),"Begin purchase for leaving-truck validation");var insidePosition=player.transform.position;
        Move(new Vector3(10,10));Tick(layout,"Update");Check(!shop.HasPendingPurchase && !layout.IsOrganizing && shop.CurrentBalance==0,"Leaving truck cancels pending purchase without spending");Move(insidePosition);SeedInterior();Frame();
        shop.OpenShop();Check(shop.TryBeginPurchase(product),"Begin purchase for input-rebuild validation");config.Shop.ChangeBinding(0).WithPath("<Keyboard>/p");config.Validate();Frame();
        Check(!shop.HasPendingPurchase && !layout.IsOrganizing && shop.CurrentBalance==0,"Rebinding input cancels uncommitted purchase and clears preview");config.Shop.ChangeBinding(0).WithPath("<Keyboard>/r");config.Validate();Frame();Frame();
        shop.OpenShop();Check(shop.TryBeginPurchase(product),"Begin purchase for balance-edit validation");preview=layout.HeldTool;shop.SetCurrentBalance(0);AimAt(new Vector2Int(4,0),preview);
        Check(preview.IsCarried && grid.Tools.Count==3 && layout.CanStartPurchase,"Changing coins during placement preserves valid upgrade preview");shop.SetCurrentBalance(0);
        Check(layout.TryInteract() && shop.CurrentBalance==0 && shop.PurchaseCount(product)==2 && grid.Tools.Count==4,"Zero-coin confirmation installs the second cannon");var second=preview.GetComponent<FoodCannon>();Frame();
        Set(config,"initialCoins",999);Check(shop.CurrentBalance==0,"Editing initial coins does not reset current coins");
        // Verify nearest-cannon loading and aiming through the real existing PlayerCarry path.
        Move(first.transform.position+Vector3.up*.9f);SeedInterior();Frame();var meatPrefab=AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab");
        var food=Object.Instantiate(meatPrefab,player.transform.position,Quaternion.identity);food.Cook();Physics2D.SyncTransforms();carry.TryPickup();carry.TryInteract();
        Check(first.IsLoaded && !second.IsLoaded && carry.CurrentHeldItem==null,"Existing PlayerCarry loads nearest purchased cannon independently");yield return null;
        Move(second.transform.position+Vector3.up*.9f);SeedInterior();food=Object.Instantiate(meatPrefab,player.transform.position,Quaternion.identity);food.Cook();Physics2D.SyncTransforms();carry.TryPickup();carry.TryInteract();
        Check(first.IsLoaded && second.IsLoaded && carry.CurrentHeldItem==null,"Second cannon loads without replacing first cannon ammunition");yield return null;
        Move(first.transform.position+Vector3.up*.9f);SeedInterior();Set(config,"flightTime",.2f);Set(first,"settings",config);
        screen=Camera.main.WorldToScreenPoint(first.transform.position+Vector3.right*6);AimMouse(0,screen);AimMouse(2,screen);
        Check(first.IsAiming && !second.IsAiming,"Aim selects nearest purchased cannon instead of first object in scene");Vector3 target=first.Target;AimMouse(0,screen);
        Check(!first.IsLoaded && second.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length==1,"Releasing aim fires only selected cannon and retains other load");
        yield return new WaitForSeconds(.3f);var splash=GameObject.Find("FoodSplash").GetComponent<LineRenderer>();Vector3 center=Vector3.zero;for(int i=0;i<splash.positionCount;i++)center+=splash.GetPosition(i);center/=splash.positionCount;
        Check(Vector2.Distance(center,target)<.001f,"Purchased cannon projectile impacts exact selected target");
        Move(second.transform.position+Vector3.up*.9f);SeedInterior();screen=Camera.main.WorldToScreenPoint(second.transform.position+Vector3.right*6);AimMouse(2,screen);
        Check(second.IsAiming && !first.IsAiming,"Can independently select second purchased cannon");shop.OpenShop();
        Check(!second.IsAiming && second.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length==0,"Opening shop while aiming cancels without firing or consuming load");shop.CloseShop();AimMouse(0,screen);Frame();
        layout.TryEnterMode();Capture("TRUCK_SHOP_GRID_PREVIEW.png");layout.ExitMode();
        shop.OpenShop();config.Shop.ChangeBinding(0).WithPath("<Keyboard>/p");config.Validate();Frame();
        Check(!shop.IsOpen && Time.timeScale==1,"Runtime shop-key rebinding closes modal and restores game");Frame();Frame(Key.P);Check(shop.IsOpen,"Rebound shop key works after release");Frame();shop.CloseShop();
        InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
    }
    static Button BuyButton(){foreach(var child in Field<Transform>(ui,"content"))if(child is Transform t && t.gameObject.activeSelf)return t.Find("Buy").GetComponent<Button>();throw new Exception("No product row");}
    static Text FindRowText(string name)=>BuyButton().transform.parent.Find(name).GetComponent<Text>();
    static int ActiveRows(){int n=0;foreach(Transform child in Field<Transform>(ui,"content"))if(child.gameObject.activeSelf)n++;return n;}
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(shop,"Update");Tick(layout,"Update");Tick(ui,"Update");}
    static void Pointer(ushort buttons,Vector2 screen){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=screen});InputSystem.Update();Tick(player,"Update");Object.FindAnyObjectByType<InputSystemUIInputModule>().Process();}
    static void AimMouse(ushort buttons,Vector2 screen){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=screen});InputSystem.Update();Tick(player,"Update");Tick(carry,"LateUpdate");}
    static void Move(Vector3 pos){player.transform.position=pos;player.GetComponent<Rigidbody2D>().position=pos;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static void SeedInterior()=>typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
    static void AimAt(Vector2Int cell,PlaceableKitchenTool tool){Set(player,"<Facing>k__BackingField",Vector2.left);Move(grid.CellCenter(cell,tool.Footprint)+Vector3.right*config.KitchenPlacementDistance);}
    static void Capture(string file)
    {
        var camera=Camera.main;var canvas=ui.GetComponentInParent<Canvas>();var previous=canvas.renderMode;var previousSorting=canvas.sortingOrder;var previousOverride=canvas.overrideSorting;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;camera.GetComponent<SimpleCameraShake>().enabled=false;camera.orthographicSize=5;
        var render=new RenderTexture(1280,720,24);camera.targetTexture=render;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(file,pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);canvas.renderMode=previous;canvas.sortingOrder=previousSorting;canvas.overrideSorting=previousOverride;Canvas.ForceUpdateCanvases();
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("TRUCK_SHOP_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}
