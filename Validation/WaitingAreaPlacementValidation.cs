using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WaitingAreaPlacementValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> results=new();
    static PlayerController player;static PlayerKitchenLayout layout;static TruckShop shop;static TruckGridLayout grid;
    static ShopProductDefinition product;static GlobalGameSettings config;static Keyboard keyboard;static CustomerWaitingAreaMount mount;
    static WaitingAreaPlacementValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);GlobalDataPrototypeSetup.ApplyToScene(scene);GlobalDataPrototypeSetup.ApplyToScene(scene);EditorSceneManager.SaveScene(scene);
                var mounts=Object.FindObjectsByType<CustomerWaitingAreaMount>();
                Check(mounts.Length==1 && Field<FoodTruckInteriorTrigger>(mounts[0],"interior")==Object.FindAnyObjectByType<FoodTruckInteriorTrigger>(),path+" one mount with local truck reference after repeated setup");
                Check(Object.FindObjectsByType<CustomerWaitingArea>().Length==0,path+" no waiting area before purchase");
            }
            AssetDatabase.SaveAssets();SessionState.SetString("SideChecks",string.Join("\n",results));SessionState.SetBool("SidePending",true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("SidePending",false))return;
        SessionState.SetBool("SidePending",false);results.Clear();results.AddRange(SessionState.GetString("SideChecks","").Split('\n'));
        new GameObject("Side placement checks").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        Time.timeScale=1;File.WriteAllText("WAITING_SIDE_RESULTS.txt","PASS: "+results.Count+" checks\n"+string.Join("\n",results));EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        player=Object.FindAnyObjectByType<PlayerController>();layout=player.GetComponent<PlayerKitchenLayout>();shop=player.GetComponent<TruckShop>();grid=Object.FindAnyObjectByType<TruckGridLayout>();mount=Object.FindAnyObjectByType<CustomerWaitingAreaMount>();
        config=Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);var catalog=Object.Instantiate(config.ShopCatalog);var entries=new List<ShopProductDefinition>();
        foreach(var entry in catalog.Products){var copy=Object.Instantiate(entry);entries.Add(copy);if(copy.Placement==ShopProductDefinition.PlacementKind.RearWaitingArea)product=copy;}
        Set(product,"purchaseLimit",0);Set(catalog,"products",entries);Set(config,"shopCatalog",catalog);player.Configure(config);Set(shop,"settings",config);Set(layout,"settings",config);Set(player.GetComponent<PlayerCarry>(),"settings",config);
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();Frame();Frame();Move(new Vector3(0,-.2f));SeedInside();
        var directions=new[]{Vector2.up,Vector2.down,Vector2.left,Vector2.right};var keys=new[]{Key.W,Key.S,Key.A,Key.D};
        CustomerWaitingArea last=null;
        for(int i=0;i<4;i++)
        {
            shop.SetCurrentBalance(500);Check(shop.OpenShop()&&shop.TryBeginPurchase(product),"Begin unpaid side selection "+directions[i]);
            var preview=layout.WaitingAreaPreview;Frame();Frame(keys[i]);Frame();
            Check(preview!=null&&preview.IsPlacementPreview&&!preview.GetComponent<BoxCollider2D>().enabled&&!mount.IsOccupied&&shop.CurrentBalance==500&&shop.HasPendingPurchase&&layout.IsOrganizing,"Preview owns no installed area and spends nothing "+i);
            var offset=preview.transform.position-grid.Interior.transform.position;
            Check(Vector2.Dot(((Vector2)offset).normalized,directions[i])>.999f,"Movement input selects correct truck side "+i);
            Check(!preview.IsInRange(player.transform)&&preview.GetComponentInChildren<SpriteRenderer>().color.a<.5f,"Preview is translucent and cannot receive customers "+i);
            if(i==3)Capture("WAITING_SIDE_PLACEMENT_PREVIEW.png");
            Frame(Key.E);Check(shop.CurrentBalance==500,"E down does not confirm or charge "+i);Frame();
            Check(!layout.IsOrganizing&&!shop.HasPendingPurchase&&mount.InstalledArea==preview&&!preview.IsPlacementPreview&&shop.CurrentBalance==500,"E release installs chosen side for free "+i);
            Physics2D.SyncTransforms();var bounds=preview.GetComponent<BoxCollider2D>().bounds;var truck=grid.Interior.GetComponent<BoxCollider2D>().bounds;
            Check(!bounds.Intersects(truck)&&grid.Tools.Count==2,"Installed side area stays outside kitchen and uses no grid cells "+i);
            Check(preview.GetComponent<BoxCollider2D>().enabled&&preview.GetComponentInChildren<SpriteRenderer>().color.a>.99f&&Vector3.Dot(preview.GetComponentInChildren<Text>().transform.right,Vector3.right)>.999f,"Installed collider/color restored and count label remains upright "+i);
            Check(!shop.TryConfirmWaitingArea(preview,out _)&&shop.CurrentBalance==500,"Duplicate confirmation cannot charge twice "+i);
            shop.OpenShop();Check(!shop.CanPurchase(product,out _),"Only one waiting area allowed across all sides "+i);shop.CloseShop();Frame();
            if(i<3)Object.DestroyImmediate(preview.gameObject);else last=preview;
        }
        // The right-side installation must retain all existing Customer behavior.
        var carry=player.GetComponent<PlayerCarry>();Move(last.transform.position+Vector3.right*1.5f);
        var customer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),player.transform.position+Vector3.up*.7f,Quaternion.identity);
        customer.GetComponent<EnemySimpleAI>().enabled=false;Physics2D.SyncTransforms();
        Check(carry.TryGrabCustomer(),"Customer can be picked up beside side-mounted area");carry.TryInteract();
        Check(last.OccupiedCount==1&&customer.IsWaiting&&carry.CurrentHeldCustomer==null,"E interaction seats customer in side-mounted area");
        Tick(last,"LateUpdate");var seat=customer.transform.position;customer.transform.position+=Vector3.up*5;Tick(last,"LateUpdate");
        Check(Vector3.Distance(customer.transform.position,seat)<.001f,"Customer cannot leave rotated side-mounted seat");
        int before=shop.CurrentBalance;customer.Purify();Check(last.OccupiedCount==0&&shop.CurrentBalance==before+config.CustomerPurificationCoins,"Side-mounted purification frees seat and pays coins");
        Move(new Vector3(0,-.2f));SeedInside();Tick(layout,"Update");Capture("WAITING_SIDE_INSTALLED_PREVIEW.png");Object.DestroyImmediate(last.gameObject);
        // All normal cancellation paths must destroy the preview without spending.
        shop.SetCurrentBalance(500);shop.OpenShop();shop.TryBeginPurchase(product);var cancelled=layout.WaitingAreaPreview;Frame();Frame(Key.R);
        Check(shop.IsOpen&&!shop.HasPendingPurchase&&!layout.IsOrganizing&&!cancelled.gameObject.activeSelf&&shop.CurrentBalance==500&&!mount.IsOccupied,"Tab cancels preview and reopens shop without payment");shop.CloseShop();Frame();yield return null;
        shop.OpenShop();shop.TryBeginPurchase(product);Frame();Frame(Key.E);Set(player,"interactStartedAt",Time.time-1);Frame(Key.E);Frame();
        Check(!layout.IsOrganizing&&!shop.HasPendingPurchase&&shop.CurrentBalance==500&&!mount.IsOccupied,"Long E cancels without installing or spending");yield return null;
        shop.OpenShop();shop.TryBeginPurchase(product);Move(new Vector3(9,9));Frame();
        Check(!layout.IsOrganizing&&!shop.HasPendingPurchase&&shop.CurrentBalance==500,"Leaving truck cancels unpaid preview");yield return null;Move(new Vector3(0,-.2f));SeedInside();
        shop.OpenShop();shop.TryBeginPurchase(product);player.enabled=false;
        Check(!layout.IsOrganizing&&!shop.HasPendingPurchase&&shop.CurrentBalance==500,"Disabling player cancels unpaid preview");player.enabled=true;Frame();yield return null;
        shop.OpenShop();shop.TryBeginPurchase(product);Set(config,"customerPurificationCoins",26);config.Validate();config.Interact.ChangeBinding(0).WithPath("<Keyboard>/f");config.Validate();Frame();
        Check(!layout.IsOrganizing&&!shop.HasPendingPurchase&&shop.CurrentBalance==500,"Rebinding input cancels unpaid side placement");Frame();yield return null;
        shop.OpenShop();shop.TryBeginPurchase(product);Set(product,"price",200);Check(layout.TryInteract()&&shop.CurrentBalance==500,"Legacy waiting-area price changes do not affect free installation");Object.DestroyImmediate(mount.InstalledArea.gameObject);
        Frame();shop.OpenShop();shop.TryBeginPurchase(product);shop.SetCurrentBalance(0);Check(layout.TryInteract()&&!shop.HasPendingPurchase&&mount.IsOccupied&&shop.CurrentBalance==0,"Zero coins at confirmation allow free waiting-area installation");layout.ExitMode();Object.DestroyImmediate(mount.InstalledArea.gameObject);yield return null;
        Check(Object.FindObjectsByType<CustomerWaitingArea>().Length==0,"No cancelled waiting-area previews leaked");
        InputSystem.RemoveDevice(keyboard);
    }
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(shop,"Update");Tick(layout,"Update");}
    static void Move(Vector3 p){player.transform.position=p;player.GetComponent<Rigidbody2D>().position=p;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static void SeedInside()=>typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
    static void Capture(string file)
    {
        var camera=Camera.main;camera.orthographicSize=6;camera.GetComponent<SimpleCameraShake>().enabled=false;var canvas=Object.FindAnyObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;Tick(Object.FindAnyObjectByType<PlayerStatusUI>(),"LateUpdate");
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes(file,pixels.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;target.Release();Object.Destroy(pixels);Object.Destroy(target);canvas.renderMode=RenderMode.ScreenSpaceOverlay;
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool value,string message){if(!value)throw new Exception(message);results.Add("PASS "+message);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("WAITING_SIDE_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}
