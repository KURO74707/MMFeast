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
public static class ItemShopValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new();
    static PlayerController player; static TruckShop wallet; static ItemShop shop; static ItemShopUI ui;
    static GlobalGameSettings config; static ShopItemDefinition item; static Keyboard keyboard; static Mouse mouse;
    static ItemShopValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GlobalDataPrototypeSetup.ApplyToScene(scene); GlobalDataPrototypeSetup.ApplyToScene(scene); EditorSceneManager.SaveScene(scene);
                var data = Object.FindAnyObjectByType<GlobalData>().Settings;
                Check(data.Shop.bindings[0].path == "<Keyboard>/r" && data.ItemShop.bindings[0].path == "<Keyboard>/tab", path + " R upgrades / Tab item shop");
                Check(Object.FindObjectsByType<ItemShop>().Length == 1 && Object.FindObjectsByType<EventSystem>().Length == 1 && Object.FindObjectsByType<Canvas>().Length == 1, path + " reuse one player shop, Canvas and EventSystem");
                var owner = Object.FindAnyObjectByType<ItemShop>();
                Check(Field<GlobalGameSettings>(owner,"settings") == data && Field<Transform>(owner,"truckCenter").gameObject.scene == scene, path + " shared config and local truck reference");
                Check(data.ItemShopCatalog.Items.Count == 1 && data.ItemShopCatalog.Items[0].IsValid(out _) && data.ItemShopCatalog.Items[0].Price == 25 && data.ItemShopCatalog.Items[0].HealAmount == 25, path + " valid default 25 coin / 25 HP medkit");
                Check(data.ItemDropSize == new Vector2(16,10) && data.ItemDropAttempts == 30, path + " drop defaults");
            }
            var shared = GlobalDataPrototypeSetup.GetOrCreateSettings();
            shared.Shop.ChangeBinding(0).WithPath("<Keyboard>/u"); shared.ItemShop.ChangeBinding(0).WithPath("<Keyboard>/i");
            ItemShopPrototypeSetup.EnsureCatalog();
            Check(shared.Shop.bindings[0].path == "<Keyboard>/u" && shared.ItemShop.bindings[0].path == "<Keyboard>/i", "Repeated migration preserves Designer bindings");
            shared.Shop.ChangeBinding(0).WithPath("<Keyboard>/r"); shared.ItemShop.ChangeBinding(0).WithPath("<Keyboard>/tab"); shared.Validate(); EditorUtility.SetDirty(shared); AssetDatabase.SaveAssets();
            SessionState.SetString("ItemChecks",string.Join("\n",results)); SessionState.SetBool("ItemPending",true); EditorApplication.EnterPlaymode();
        }
        catch(Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("ItemPending",false))return;
        SessionState.SetBool("ItemPending",false); results.Clear(); results.AddRange(SessionState.GetString("ItemChecks","").Split('\n'));
        new GameObject("Item shop validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        File.WriteAllText("ITEM_SHOP_RESULTS.txt","PASS: "+results.Count+" checks\n"+string.Join("\n",results)); Time.timeScale=1; EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawn in Object.FindObjectsByType<EnemySpawner>())spawn.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject); yield return null;
        player=Object.FindAnyObjectByType<PlayerController>(); wallet=player.GetComponent<TruckShop>(); shop=player.GetComponent<ItemShop>();ui=Object.FindAnyObjectByType<ItemShopUI>();
        config=Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);var catalog=Object.Instantiate(config.ItemShopCatalog);item=Object.Instantiate(catalog.Items[0]);
        Set(catalog,"items",new List<ShopItemDefinition>{item});Set(config,"itemShopCatalog",catalog);player.Configure(config);Set(wallet,"settings",config);Set(shop,"settings",config);
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();Frame();Frame();Move(new Vector3(8,6));
        Frame(Key.Tab);Check(shop.IsOpen&&!wallet.IsOpen&&Time.timeScale==0,"Tab opens paid shop anywhere and pauses");Frame();Frame(Key.R);
        Check(wallet.IsOpen&&!shop.IsOpen&&Time.timeScale==0,"R switches paid shop to free upgrades without unpausing");Frame();Frame(Key.Tab);
        Check(shop.IsOpen&&!wallet.IsOpen&&Time.timeScale==0,"Tab switches back to paid shop");Frame();Frame(Key.Tab);Check(!player.IsShopOpen&&Time.timeScale==1,"Tab closes and restores time");Frame();
        var carry=player.GetComponent<PlayerCarry>();var meat=Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab"),player.transform.position,Quaternion.identity);Physics2D.SyncTransforms();carry.TryPickup();
        Check(carry.HasHeldObject&&shop.OpenShop()&&shop.CanPurchase(item,out _),"Hands full and full HP do not prevent shopping");
        wallet.SetCurrentBalance(24);Check(!shop.TryPurchase(item)&&wallet.CurrentBalance==24&&Packs().Length==0&&shop.Message.Contains("coins"),"Insufficient coins reject delivery without charging");
        wallet.SetCurrentBalance(100);shop.CloseShop();shop.OpenShop();ui.Refresh();Tick(ui,"Update");yield return null;Capture("ITEM_SHOP_PREVIEW.png");
        var content=Field<Transform>(ui,"content");Button buy=null;foreach(Transform child in content)if(child.gameObject.activeSelf)buy=child.Find("Buy").GetComponent<Button>();
        Check(buy!=null&&buy.interactable&&buy.transform.parent.Find("Price").GetComponent<Text>().text=="25 coins"&&Field<Text>(ui,"balanceText").text=="COINS  100","Paid UI displays live price and wallet");
        Canvas.ForceUpdateCanvases();var rect=(RectTransform)buy.transform;Vector2 screen=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=screen},hits);Check(hits.Count>0&&hits[0].gameObject.transform.IsChildOf(buy.transform),"Buy button receives actual HUD raycast");
        Pointer(0,screen);Pointer(1,screen);Pointer(0,screen);Check(Packs().Length==1&&wallet.CurrentBalance==75,"Actual UI purchase delivers one pack and charges once");
        var pack=Packs()[0];Check(pack.Item==item,"Ground pack retains shared item reference");
        for(int i=0;i<5;i++){wallet.AddCoins(25);Check(shop.TryPurchase(item),"Repeated random purchase "+i);}
        foreach(var p in Packs())
        {
            Vector2 delta=(Vector2)p.transform.position-((Vector2)Field<Transform>(shop,"truckCenter").position+config.ItemDropCenterOffset);
            Check(Mathf.Abs(delta.x)+p.DropRadius<=8.001f&&Mathf.Abs(delta.y)+p.DropRadius<=5.001f,"Entire medkit stays inside configured rectangle");
            var overlaps=Physics2D.OverlapCircleAll(p.transform.position,p.DropRadius+config.ItemDropSpacing-.001f);bool clear=true;foreach(var c in overlaps)if(c.gameObject!=p.gameObject)clear=false;
            Check(clear,"Random drop avoids truck, characters, obstacles and earlier packs");
        }
        int before=wallet.CurrentBalance;Set(config,"itemDropSize",new Vector2(.8f,.8f));Set(config,"itemDropCenterOffset",Vector2.zero);config.Validate();
        Check(!shop.TryPurchase(item)&&wallet.CurrentBalance==before&&shop.Message.Contains("No clear"),"Truck-blocked delivery area fails without deduction");
        Set(config,"itemDropCenterOffset",new Vector2(12,12));config.Validate();Check(shop.TryPurchase(item)&&wallet.CurrentBalance==before-25,"Live delivery area edit applies to next purchase");
        var prefab=item.Prefab;Set(item,"prefab",null);before=wallet.CurrentBalance;Check(!shop.TryPurchase(item)&&wallet.CurrentBalance==before,"Missing prefab never charges");Set(item,"prefab",prefab);
        var invalid=Object.Instantiate(prefab,new Vector3(100,100),Quaternion.identity);invalid.GetComponent<CircleCollider2D>().isTrigger=false;Set(item,"prefab",invalid);
        Check(!shop.TryPurchase(item)&&wallet.CurrentBalance==before,"Invalid pickup trigger never charges");Set(item,"prefab",prefab);Object.Destroy(invalid.gameObject);
        var source=Object.Instantiate(prefab,new Vector3(120,120),Quaternion.identity);source.gameObject.AddComponent<ItemShopSpawnFailureProbe>();Set(item,"prefab",source);
        Set(config,"itemDropSize",new Vector2(16,10));config.Validate();ItemShopSpawnFailureProbe.RejectNextSpawn=true;
        Check(!shop.TryPurchase(item)&&wallet.CurrentBalance==before&&shop.Message.Contains("Delivery failed"),"Spawn-time component failure cleans up delivery and never charges");Set(item,"prefab",prefab);Object.Destroy(source.gameObject);
        Set(config,"itemDropSize",new Vector2(.8f,.8f));Set(config,"itemDropCenterOffset",new Vector2(-12,-12));config.Validate();
        var waiting=Object.Instantiate(AssetDatabase.LoadAssetAtPath<CustomerWaitingArea>(CustomerWaitingAreaPrototypeSetup.PrefabPath),Field<Transform>(shop,"truckCenter").position+new Vector3(-12,-12),Quaternion.identity);
        bool queryTriggers=Physics2D.queriesHitTriggers;Physics2D.queriesHitTriggers=false;
        Check(!shop.TryPurchase(item)&&wallet.CurrentBalance==before,"Drop explicitly avoids waiting-area trigger even when global trigger queries are disabled");Physics2D.queriesHitTriggers=queryTriggers;Object.Destroy(waiting.gameObject);
        Set(config,"itemDropSize",new Vector2(16,10));config.Validate();Set(item,"price",7);wallet.SetCurrentBalance(7);Check(shop.TryPurchase(item)&&wallet.CurrentBalance==0,"Live price edit used exactly at purchase");
        Set(item,"listed",false);Check(!shop.TryPurchase(item),"Delisting blocks new purchases");
        Check(!wallet.TrySpendCoins(-1)&&!wallet.TrySpendCoins(1)&&wallet.TrySpendCoins(0)&&wallet.CurrentBalance==0,"Wallet rejects negative and unaffordable spends");
        shop.CloseShop();Frame();player.SetCurrentHP(player.MaxHP);Move(pack.transform.position);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        Check(pack!=null&&player.HP==player.MaxHP,"Full HP preserves pack through real trigger contact");
        Set(item,"healAmount",40f);player.TakeDamage(60);yield return new WaitForFixedUpdate();yield return new WaitForFixedUpdate();
        Check(pack==null&&player.HP==80,"Staying on pack after damage heals using live amount, even after delisting");
        var capped=Packs()[0];player.SetCurrentHP(95);int events=0;player.StatsChanged+=()=>events++;
        Check(capped.TryUse(player.GetComponent<Collider2D>())&&player.HP==100&&events==1,"Heal caps HP and raises existing status event once");
        Check(!capped.TryUse(player.GetComponent<Collider2D>())&&events==1,"Pack cannot apply twice before deferred destruction");
        var deadPack=Packs()[1];player.SetCurrentHP(0);Check(!deadPack.TryUse(player.GetComponent<Collider2D>())&&player.Heal(25)==0&&player.HP==0,"Dead player cannot consume packs or revive via Heal");player.SetCurrentHP(50);
        var enemy=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyHealth>("Assets/Prototype/Prefabs/Enemy.prefab"),new Vector3(20,20),Quaternion.identity);enemy.GetComponent<EnemySimpleAI>().enabled=false;
        var customer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),new Vector3(21,20),Quaternion.identity);
        Check(!deadPack.TryUse(enemy.GetComponent<Collider2D>())&&!deadPack.TryUse(customer.GetComponent<Collider2D>()),"Zombie and Customer cannot consume medkits");
        Check(carry.HasHeldObject&&deadPack.TryUse(player.GetComponent<Collider2D>())&&carry.HasHeldObject,"Hands remain occupied while medkit heals immediately");yield return null;
        Set(item,"listed",true);Time.timeScale=.5f;shop.OpenShop();shop.enabled=false;Check(Time.timeScale==.5f&&!shop.IsOpen,"Shop disable restores preexisting time scale");shop.enabled=true;Time.timeScale=1;Frame();
        shop.OpenShop();player.enabled=false;Check(!shop.IsOpen&&Time.timeScale==1,"Player disable closes paid shop and resumes");player.enabled=true;Frame();Frame();
        shop.OpenShop();config.ItemShop.ChangeBinding(0).WithPath("<Keyboard>/i");config.Validate();Frame();Check(!shop.IsOpen&&Time.timeScale==1,"Live rebinding closes modal and restores time");Frame();Frame(Key.Tab);Check(!shop.IsOpen,"Old paid-shop binding no longer opens");Frame();Frame(Key.I);Check(shop.IsOpen,"New paid-shop binding works after release");Frame();
        Frame(Key.D,Key.Space,Key.E);Tick(player,"FixedUpdate");Check(!player.AttackPressed&&!player.CookPressed&&!player.DashPressed&&player.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero,"Paid modal blocks gameplay movement and actions");Frame();shop.CloseShop();Frame();
        var variant=Object.Instantiate(item);Set(variant,"displayName","Large medkit");Set(variant,"price",50);Set(variant,"healAmount",75f);Set(catalog,"items",new List<ShopItemDefinition>{item,variant});shop.OpenShop();ui.Refresh();int rows=0;foreach(Transform child in content)if(child.gameObject.activeSelf)rows++;
        Check(rows==2,"Designer adds medkit variants through catalog without code");Set(catalog,"items",new List<ShopItemDefinition>{variant});ui.Refresh();rows=0;foreach(Transform child in content)if(child.gameObject.activeSelf)rows++;Check(rows==1&&!shop.CanPurchase(item,out _),"Removing catalog entry updates UI and prevents purchase");
        shop.CloseShop();Frame();
        var cannon=Object.FindAnyObjectByType<FoodCannon>();var grid=Object.FindAnyObjectByType<TruckGridLayout>();Move(cannon.transform.position+Vector3.up*.8f);
        typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
        carry.CurrentHeldItem.Cook();Check(cannon.TryLoad(carry.CurrentHeldItem,player.transform),"Existing cooked food loads cannon before modal input checks");yield return null;Frame();
        Vector2 aimPoint=Camera.main.WorldToScreenPoint(cannon.transform.position+Vector3.right*6);
        InputSystem.QueueStateEvent(mouse,new MouseState{buttons=2,position=aimPoint});Frame();cannon.HandleAimInput(player,true);
        Check(cannon.IsAiming,"Actual aim input starts loaded cannon aim");shop.OpenShop();Check(!cannon.IsAiming&&cannon.IsLoaded,"Opening paid shop cancels aim and retains ammunition");
        InputSystem.QueueStateEvent(mouse,new MouseState{buttons=0,position=aimPoint});Frame();shop.CloseShop();Frame();Check(cannon.IsLoaded&&Object.FindObjectsByType<FoodProjectile>().Length==0,"Closing shop and releasing aim cannot fire unexpectedly");
        InputSystem.QueueStateEvent(mouse,new MouseState{buttons=2,position=aimPoint});Frame();cannon.HandleAimInput(player,true);Check(cannon.IsAiming,"Cannon can aim again after modal close");
        config.ItemShop.ChangeBinding(0).WithPath("<Keyboard>/o");config.Validate();Frame();Check(!cannon.IsAiming&&cannon.IsLoaded,"Changing paid-shop key during aim cancels safely without spending ammunition");
        InputSystem.QueueStateEvent(mouse,new MouseState{buttons=0,position=aimPoint});Frame();Check(Object.FindObjectsByType<FoodProjectile>().Length==0,"Release after live rebinding produces no projectile");
        InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
    }
    static MedkitPickup[] Packs()=>Object.FindObjectsByType<MedkitPickup>();
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(wallet,"Update");Tick(shop,"Update");Tick(player.GetComponent<PlayerKitchenLayout>(),"Update");Tick(ui,"Update");}
    static void Pointer(ushort buttons,Vector2 p){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=p});InputSystem.Update();Tick(player,"Update");Object.FindAnyObjectByType<InputSystemUIInputModule>().Process();}
    static void Move(Vector3 p){player.transform.position=p;player.GetComponent<Rigidbody2D>().position=p;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static void Capture(string file)
    {
        var camera=Camera.main;var canvas=ui.GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;
        camera.GetComponent<SimpleCameraShake>().enabled=false;var render=new RenderTexture(1280,720,24);camera.targetTexture=render;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(file,pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=false;canvas.sortingOrder=0;Canvas.ForceUpdateCanvases();
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("ITEM_SHOP_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}
