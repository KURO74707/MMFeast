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
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class WaitingAreaRelocationValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new();
    static PlayerController player;static PlayerCarry carry;static PlayerKitchenLayout layout;static TruckShop shop;static TruckGridLayout grid;
    static CustomerWaitingArea area;static Keyboard keyboard;
    static WaitingAreaRelocationValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            var prefab=AssetDatabase.LoadAssetAtPath<CustomerWaitingArea>(CustomerWaitingAreaPrototypeSetup.PrefabPath);
            var barrier=Field<BoxCollider2D>(prefab,"enemyBarrier");
            Check(barrier!=null&&!barrier.isTrigger&&barrier.includeLayers.value==(1<<8)&&barrier.excludeLayers.value==~(1<<8),"Waiting-area prefab has solid enemy-only barrier");
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                var scene=EditorSceneManager.OpenScene(path);GlobalDataPrototypeSetup.ApplyToScene(scene);GlobalDataPrototypeSetup.ApplyToScene(scene);EditorSceneManager.SaveScene(scene);
                Check(Object.FindObjectsByType<CustomerWaitingAreaMount>().Length==1&&Object.FindObjectsByType<CustomerWaitingArea>().Length==0,path+" repeated setup retains single local mount and no preinstalled area");
            }
            AssetDatabase.SaveAssets();SessionState.SetString("MoveAreaChecks",string.Join("\n",checks));SessionState.SetBool("MoveAreaPending",true);EditorApplication.EnterPlaymode();
        }catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("MoveAreaPending",false))return;
        SessionState.SetBool("MoveAreaPending",false);checks.Clear();checks.AddRange(SessionState.GetString("MoveAreaChecks","").Split('\n'));
        new GameObject("Waiting area relocation validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current;try{next=routine.MoveNext();current=next?routine.Current:null;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        File.WriteAllText("WAITING_RELOCATION_RESULTS.txt","PASS: "+checks.Count+" checks\n"+string.Join("\n",checks));Time.timeScale=1;EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawn in Object.FindObjectsByType<EnemySpawner>())spawn.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        player=Object.FindAnyObjectByType<PlayerController>();carry=player.GetComponent<PlayerCarry>();layout=player.GetComponent<PlayerKitchenLayout>();shop=player.GetComponent<TruckShop>();grid=Object.FindAnyObjectByType<TruckGridLayout>();
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard=InputSystem.AddDevice<Keyboard>();Frame();Frame();Move(new Vector3(0,-1.8f));SeedInside();ShopProductDefinition product=null;
        foreach(var p in shop.Catalog.Products)if(p.Placement==ShopProductDefinition.PlacementKind.RearWaitingArea)product=p;
        shop.OpenShop();Check(shop.TryBeginPurchase(product),"Waiting area starts purchase preview");Frame();Frame(Key.S);Frame();Frame(Key.E);Frame();area=Object.FindAnyObjectByType<CustomerWaitingArea>();
        Check(area!=null&&!area.IsPlacementPreview&&shop.CurrentBalance==500,"Purchased bottom waiting area installed once");
        var a=Seat();var b=Seat();Check(area.OccupiedCount==2,"Two customers seated before moving");
        Move(new Vector3(0,-1.85f));SeedInside();LongE();
        Check(layout.IsOrganizing&&!layout.IsPurchasePlacement,"Actual long E opens normal organization mode");
        Frame(Key.E);Frame();Tick(area,"LateUpdate");var origin=new Vector3(0,-3.1f);
        Check(layout.WaitingAreaPreview==area&&area.IsRelocating&&area.OccupiedCount==2&&shop.CurrentBalance==500,"Short E lifts the installed area with occupants and no payment");
        Check(Field<BoxCollider2D>(area,"enemyBarrier").enabled&&!area.GetComponent<BoxCollider2D>().enabled&&!area.IsInRange(player.transform),"Relocation retains enemy barrier but suspends customer seating");
        Frame(Key.W);Frame();Tick(area,"LateUpdate");Check(area.transform.position.y>2&&a.IsWaiting&&b.IsWaiting,"Changing direction moves the same occupied area preview");
        layout.ExitMode();Check(Vector3.Distance(area.transform.position,origin)<.001f&&!area.IsRelocating&&area.OccupiedCount==2&&shop.CurrentBalance==500,"Cancel restores original side and occupants without charging");
        Frame();LongE();
        var directions=new[]{Key.W,Key.A,Key.D,Key.S};var playerEdges=new[]{new Vector3(0,1.85f),new Vector3(-2.85f,0),new Vector3(2.85f,0),new Vector3(0,-1.85f)};
        for(int i=0;i<4;i++)
        {
            Frame(Key.E);Frame();Check(area.IsRelocating,"Lift installed area for direction "+i);
            Frame(directions[i]);Frame();Tick(area,"LateUpdate");Frame(Key.E);Frame();Physics2D.SyncTransforms();
            Check(layout.IsOrganizing&&layout.WaitingAreaPreview==null&&!area.IsRelocating&&!area.IsPlacementPreview&&area.Mount.InstalledArea==area,"Confirm relocation retains installed instance and organization mode "+i);
            var collider=area.GetComponent<BoxCollider2D>();Check(collider.OverlapPoint(a.transform.position)&&collider.OverlapPoint(b.transform.position)&&area.OccupiedCount==2,"Seated customers stay within relocated area "+i);
            Check(shop.CurrentBalance==500&&shop.PurchaseCount(product)==1&&grid.Tools.Count==2,"Relocation never charges or changes purchase count / grid occupancy "+i);
            Move(playerEdges[i]);SeedInside();
        }
        Frame(Key.E);Frame();Frame(Key.W);Frame();Frame(Key.R);
        Check(shop.IsOpen&&!layout.IsOrganizing&&!area.IsRelocating&&Vector3.Distance(area.transform.position,origin)<.001f&&area.OccupiedCount==2,"Tab cancels relocation before opening shop");shop.CloseShop();Frame();
        LongE();Frame(Key.E);Frame();Frame(Key.W);Frame();layout.enabled=false;
        Check(!area.IsRelocating&&Vector3.Distance(area.transform.position,origin)<.001f&&a.IsWaiting&&b.IsWaiting,"Disabling layout restores occupied area");layout.enabled=true;Frame();
        LongE();Frame(Key.E);Frame();Frame(Key.W);Frame();Move(new Vector3(9,9));Frame();
        Check(!layout.IsOrganizing&&!area.IsRelocating&&Vector3.Distance(area.transform.position,origin)<.001f&&area.OccupiedCount==2,"Leaving truck restores occupied area");
        // Drive actual dynamic bodies against the installed barrier from the exterior.
        var zombie=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyHealth>("Assets/Prototype/Prefabs/Enemy.prefab"),new Vector3(-.8f,-5.2f),Quaternion.identity);zombie.GetComponent<EnemySimpleAI>().enabled=false;
        var free=Spawn(new Vector3(.8f,-5.2f));var zb=zombie.GetComponent<Rigidbody2D>();var cb=free.GetComponent<Rigidbody2D>();
        for(int i=0;i<35;i++){zb.linearVelocity=cb.linearVelocity=Vector2.up*5;yield return new WaitForFixedUpdate();}
        zb.linearVelocity=cb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
        Check(zombie.transform.position.y < -3.9f && free.transform.position.y < -3.9f,"Real physics blocks walking zombies and free customers at the waiting-area boundary");
        Check(!free.IsWaiting&&area.OccupiedCount==2,"Walking customers cannot self-seat");
        // A new installation or relocation may cover an existing NPC; it must be expelled safely.
        zb.position=area.transform.position;zombie.transform.position=area.transform.position;cb.position=area.transform.position;free.transform.position=area.transform.position;Tick(area,"FixedUpdate");Physics2D.SyncTransforms();
        Check(!area.GetComponent<BoxCollider2D>().OverlapPoint(zombie.transform.position)&&!area.GetComponent<BoxCollider2D>().OverlapPoint(free.transform.position),"Unseated NPCs already inside the footprint are moved out");
        Check(!grid.Interior.GetComponent<BoxCollider2D>().OverlapPoint(zombie.transform.position)&&!grid.Interior.GetComponent<BoxCollider2D>().OverlapPoint(free.transform.position),"Expulsion does not place NPCs inside the truck");
        Object.Destroy(zombie.gameObject);Object.Destroy(free.gameObject);yield return null;
        player.enabled=false;Move(new Vector3(-3,-3.1f));var pb=player.GetComponent<Rigidbody2D>();
        for(int i=0;i<30;i++){pb.linearVelocity=Vector2.right*5;yield return new WaitForFixedUpdate();}
        pb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();Check(area.GetComponent<BoxCollider2D>().OverlapPoint(player.transform.position),"Player walks through enemy barrier into waiting area");player.enabled=true;Frame();
        var carried=Spawn(player.transform.position+Vector3.down*1.3f);Physics2D.SyncTransforms();Check(carry.TryGrabCustomer(),"Player can grab a free customer outside waiting area");
        Move(area.transform.position);Tick(carried,"LateUpdate");Tick(area,"FixedUpdate");Check(carried.IsCarried&&carry.CurrentHeldCustomer==carried,"Carried customer bypasses barrier while held by player");carry.TryInteract();
        Check(carried.IsWaiting&&area.OccupiedCount==3,"Manual E admission works through enemy-only barrier");
        int balance=shop.CurrentBalance;var shot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<FoodProjectile>("Assets/Prototype/Prefabs/FoodProjectile.prefab"),area.transform.position,Quaternion.identity);Physics2D.SyncTransforms();shot.Impact();
        Check(a.IsPurified&&b.IsPurified&&carried.IsPurified&&area.OccupiedCount==0&&shop.CurrentBalance==balance+3*Object.FindAnyObjectByType<GlobalData>().Settings.CustomerPurificationCoins,"Barrier does not block food AoE, seat cleanup or coin rewards");
        yield return new WaitForSeconds(1.2f);Move(new Vector3(0,-1.85f));SeedInside();LongE();Frame(Key.E);Frame();Frame(Key.D);Frame();Tick(area,"LateUpdate");Capture();layout.ExitMode();
        Check(Object.FindObjectsByType<CustomerWaitingArea>().Length==1&&!area.IsRelocating&&shop.CurrentBalance==balance+75,"Relocation cleanup retains exactly one installed waiting area");
        InputSystem.RemoveDevice(keyboard);
    }
    static Customer Spawn(Vector3 p){var c=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),p,Quaternion.identity);c.GetComponent<EnemySimpleAI>().enabled=false;return c;}
    static Customer Seat(){Move(new Vector3(4,-3.1f));var c=Spawn(new Vector3(4.7f,-3.1f));Physics2D.SyncTransforms();if(!carry.TryGrabCustomer())throw new Exception("Seat grab failed");Move(area.transform.position+Vector3.right*2.1f);carry.TryInteract();if(!c.IsWaiting)throw new Exception("Seat failed");return c;}
    static void LongE(){Frame();Frame(Key.E);Set(player,"interactStartedAt",Time.time-1);Frame(Key.E);Frame();}
    static void Frame(params Key[] keys){InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(shop,"Update");Tick(layout,"Update");}
    static void Move(Vector3 p){player.transform.position=p;player.GetComponent<Rigidbody2D>().position=p;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static void SeedInside()=>typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
    static void Capture()
    {
        var camera=Camera.main;camera.orthographicSize=6;camera.GetComponent<SimpleCameraShake>().enabled=false;var canvas=Object.FindAnyObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=target;var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1280,720),0,0);texture.Apply();File.WriteAllBytes("WAITING_RELOCATION_PREVIEW.png",texture.EncodeToPNG());RenderTexture.active=null;camera.targetTexture=null;target.Release();Object.Destroy(texture);Object.Destroy(target);
    }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool value,string name){if(!value)throw new Exception(name);checks.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("WAITING_RELOCATION_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",checks));EditorApplication.Exit(1);}
}
