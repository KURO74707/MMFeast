using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class NpcOptimizationValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static readonly List<string> checks=new();
    static long separationBytes,barrierBytes,hudBytes,kitchenBytes,uncachedLabelBytes;
    static NpcOptimizationValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run(){EditorSceneManager.OpenScene(CookingPrototypeBuilder.ScenePath);SessionState.SetBool("NpcOptimizationPending",true);EditorApplication.EnterPlaymode();}
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("NpcOptimizationPending",false))return;
        SessionState.SetBool("NpcOptimizationPending",false);new GameObject("NPC optimization validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while(true){bool next;object current;try{next=test.MoveNext();current=next?test.Current:null;}catch(Exception error){Fail(error);yield break;}if(!next)break;yield return current;}
        File.WriteAllText("NPC_OPTIMIZATION_RESULTS.txt","PASS: "+checks.Count+" checks\n"+string.Join("\n",checks)+$"\n1000 warmed calls: Separation={separationBytes} bytes; empty waiting barrier={barrierBytes} bytes; unchanged HUD={hudBytes} bytes; kitchen status={kitchenBytes} bytes.\n100 uncached InputAction.GetBindingDisplayString calls={uncachedLabelBytes} bytes.\n");Time.timeScale=1;EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        var shared=Object.FindAnyObjectByType<GlobalData>().Settings;Check(!shared.GameplayDebugLogs,"Normal gameplay logs default off");
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var npc in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(npc.gameObject);yield return null;
        var config=Object.Instantiate(shared);var player=Object.FindAnyObjectByType<PlayerController>();var hud=Object.FindAnyObjectByType<PlayerStatusUI>();
        player.Configure(config);var prefab=AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab");
        var leader=Object.Instantiate(prefab,new Vector3(50,50),Quaternion.identity);Set(leader,"settings",config);Set(leader.GetComponent<EnemyHealth>(),"settings",config);leader.SetTarget(player.transform);
        Check(Field<PlayerController>(leader,"targetPlayer")==player,"SetTarget caches the target PlayerController");leader.SetTarget(null);Check(Field<PlayerController>(leader,"targetPlayer")==null,"Clearing target clears cached PlayerController");leader.SetTarget(player.transform);
        var actors=new List<EnemySimpleAI>{leader};leader.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;
        for(int i=0;i<200;i++)
        {
            var npc=Object.Instantiate(prefab,new Vector3(50+.25f+(i%20)*.025f,50+(i/20)*.025f),Quaternion.identity);
            npc.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Kinematic;actors.Add(npc);
        }
        Physics2D.SyncTransforms();
        var separation=(Func<Vector2>)Delegate.CreateDelegate(typeof(Func<Vector2>),leader,typeof(EnemySimpleAI).GetMethod("Separation",Private));
        Vector2 actual=separation();var hits=Field<List<Collider2D>>(leader,"separationHits");
        Check(hits.Count>128&&hits.Count==Physics2D.OverlapCircleAll(leader.transform.position,config.ZombieSeparationRadius).Length,"Growable query collects more than 128 colliders without clipping");
        Vector2 expected=Vector2.zero;foreach(var npc in actors){if(npc==leader)continue;Vector2 away=(Vector2)leader.transform.position-(Vector2)npc.transform.position;float distance=away.magnitude;if(distance<config.ZombieSeparationRadius&&distance>.0001f)expected+=away/distance*(1-distance/config.ZombieSeparationRadius);}
        expected=Vector2.ClampMagnitude(expected,1);Check(Vector2.Distance(actual,expected)<.0001f,"Dense separation matches full-neighbor reference calculation");
        for(int i=0;i<100;i++)separation();long before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)separation();separationBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(separationBytes==0,"Warmed separation calls allocate no managed bytes");
        Physics2D.queriesHitTriggers=false;separation();Check(!Field<List<Collider2D>>(leader,"separationHits").Exists(c=>c.isTrigger),"Query preserves global trigger filtering");Physics2D.queriesHitTriggers=true;
        int logs=0;Application.LogCallback capture=(message,trace,type)=>{if(type==LogType.Log)logs++;};Application.logMessageReceived+=capture;
        leader.GetComponent<EnemyHealth>().TakeDamage(1);Check(logs==0,"Disabled logging produces no damage log");Set(config,"gameplayDebugLogs",true);leader.GetComponent<EnemyHealth>().TakeDamage(1);Check(logs==1,"Designer can enable ordinary damage logging live");Application.logMessageReceived-=capture;Set(config,"gameplayDebugLogs",false);
        var world=leader.GetComponentInChildren<WorldHealthBar>();var hpSlider=world.GetComponentInChildren<Slider>();
        Check(Mathf.Approximately(hpSlider.value,leader.GetComponent<EnemyHealth>().CurrentHP/leader.GetComponent<EnemyHealth>().MaxHP),"World HP bar updates immediately through health event");
        var reassigned=actors[1].GetComponent<EnemyHealth>();world.Configure(reassigned,null,Field<Canvas>(world,"canvas"),hpSlider);reassigned.TakeDamage(10);
        Check(Mathf.Approximately(hpSlider.value,reassigned.CurrentHP/reassigned.MaxHP),"Reconfiguring a live world HP bar transfers its event subscription");
        world.Configure(leader.GetComponent<EnemyHealth>(),null,Field<Canvas>(world,"canvas"),hpSlider);
        leader.GetComponent<EnemyHealth>().KillByFoodProjectile();Check(!Field<Canvas>(world,"canvas").enabled,"Death event immediately hides world HP bar");
        actors.Remove(leader);yield return null;
        var mount=Object.FindAnyObjectByType<CustomerWaitingAreaMount>();var area=mount.CreatePreview(AssetDatabase.LoadAssetAtPath<CustomerWaitingArea>(CustomerWaitingAreaPrototypeSetup.PrefabPath),config);mount.PositionPreview(area,Vector2.down);Check(mount.TryInstallPreview(area),"Waiting area installs using existing side-placement path");
        foreach(var actor in actors){actor.enabled=false;actor.transform.position=area.transform.position;actor.GetComponent<Rigidbody2D>().position=area.transform.position;}
        Physics2D.SyncTransforms();var keep=(Action)Delegate.CreateDelegate(typeof(Action),area,typeof(CustomerWaitingArea).GetMethod("KeepUnseatedActorsOut",Private));keep();
        Check(Field<List<Collider2D>>(area,"barrierHits").Count>=200,"Waiting-area query handles 200 NPC colliders without clipping");
        Physics2D.SyncTransforms();foreach(var actor in actors)Check(!Field<BoxCollider2D>(area,"enemyBarrier").Distance(actor.GetComponent<Collider2D>()).isOverlapped,"Displaced NPC remains outside waiting area");
        foreach(var actor in actors)Object.Destroy(actor.gameObject);yield return null;
        for(int i=0;i<100;i++)keep();before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)keep();barrierBytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(barrierBytes==0,"Warmed waiting-area protection allocates no managed bytes");
        var refresh=(Action)Delegate.CreateDelegate(typeof(Action),hud,typeof(PlayerStatusUI).GetMethod("Refresh",Private));refresh();
        before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)refresh();hudBytes=GC.GetAllocatedBytesForCurrentThread()-before;Check(hudBytes==0,"Unchanged HUD refresh allocates no formatted strings");
        player.SetCurrentHP(60);Check(Field<Text>(hud,"hpText").text.StartsWith("HP 60"),"HUD still follows runtime HP edits");Set(config,"playerMaxHP",150f);config.Validate();refresh();Check(Field<Text>(hud,"hpText").text=="HP 60 / 150","HUD still reflects live maximum edits without refilling HP");
        var wallet=player.GetComponent<TruckShop>();wallet.SetCurrentBalance(23);refresh();Check(Field<Text>(hud,"coinsText").text=="COINS  23","HUD polls live coin edits");
        var layout=player.GetComponent<PlayerKitchenLayout>();Set(layout,"settings",config);var grid=Object.FindAnyObjectByType<TruckGridLayout>();
        player.transform.position=Vector3.zero;player.GetComponent<Rigidbody2D>().position=Vector2.zero;Physics2D.SyncTransforms();
        typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
        var status=(Action)Delegate.CreateDelegate(typeof(Action),layout,typeof(PlayerKitchenLayout).GetMethod("RefreshStatus",Private));for(int i=0;i<20;i++)status();
        before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<1000;i++)status();kitchenBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(kitchenBytes==0,"Warmed unchanged kitchen status allocates zero bytes, including binding labels");
        var statusText=Field<Text>(layout,"statusText");layout.ShowInteractionMessage("temporary notice");status();Check(statusText.text.Contains("temporary notice"),"Kitchen notification appears immediately");
        Set(layout,"messageUntil",Time.time-1);status();Check(!statusText.text.Contains("temporary notice"),"Kitchen notification expiry invalidates cached text");
        config.Interact.ChangeBinding(0).WithPath("<Keyboard>/f");config.Shop.ChangeBinding(0).WithPath("<Keyboard>/p");config.ItemShop.ChangeBinding(0).WithPath("<Keyboard>/i");config.Validate();status();
        Check(statusText.text.Contains("Hold F:")&&config.UpgradeKeyLabel.Contains("P")&&config.ItemShopKeyLabel.Contains("I"),"Cached labels track all three live key edits");
        config.Interact.ChangeBinding(0).WithPath("<Keyboard>/e");config.Shop.ChangeBinding(0).WithPath("<Keyboard>/r");config.ItemShop.ChangeBinding(0).WithPath("<Keyboard>/tab");config.Validate();
        for(int i=0;i<20;i++)config.Interact.GetBindingDisplayString();before=GC.GetAllocatedBytesForCurrentThread();for(int i=0;i<100;i++)config.Interact.GetBindingDisplayString();uncachedLabelBytes=GC.GetAllocatedBytesForCurrentThread()-before;
        Check(config.InteractKeyLabel==config.Interact.GetBindingDisplayString(),"Cached label matches the Input System display string");
        var human=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),new Vector3(30,30),Quaternion.identity);Set(human,"settings",config);human.ConfigureCoinWallet(wallet);int coins=wallet.CurrentBalance;human.Purify();human.Purify();Check(wallet.CurrentBalance==coins+config.CustomerPurificationCoins,"Injected local wallet receives exactly one reward");
        var foreignScene=UnityEngine.SceneManagement.SceneManager.CreateScene("Foreign wallet test");var foreign=new GameObject("Foreign wallet");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(foreign,foreignScene);
        var other=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),new Vector3(31,30),Quaternion.identity);Set(other,"settings",config);other.ConfigureCoinWallet(wallet);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(other.gameObject,foreignScene);
        // A moved customer must not pay its former scene's cached wallet. No wallet in this scene is an explicit error.
        coins=wallet.CurrentBalance;other.Purify();Check(wallet.CurrentBalance==coins,"Cached wallet never pays across a scene change");
        var fps=Field<Text>(hud,"fpsText");Time.timeScale=0;float start=Time.realtimeSinceStartup;while(Time.realtimeSinceStartup-start<.3f)yield return null;Check(fps.text.StartsWith("FPS ")&&fps.text!="FPS --","FPS counter continues updating while paused");Time.timeScale=1;
    }
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,Private).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add("PASS "+message);}
    static void Fail(Exception error){Time.timeScale=1;Debug.LogException(error);File.WriteAllText("NPC_OPTIMIZATION_RESULTS.txt","FAIL\n"+error+"\n"+string.Join("\n",checks));EditorApplication.Exit(1);}
}
