using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class FoodTruckHealthValidation
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static GlobalGameSettings config; private static string baseline;
    private static int checks; private static readonly System.Collections.Generic.List<string> results=new();
    private static int destroyedLogs;
    static FoodTruckHealthValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade(); config=GlobalDataPrototypeSetup.GetOrCreateSettings(); baseline=EditorJsonUtility.ToJson(config);
            Check(config.FoodTruckMaxHP==500 && config.FoodTruckAttackableRange==.8f,"Truck config defaults");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Prefabs/FoodTruck.prefab");
            Check(prefab.GetComponentsInChildren<Collider2D>(true).Length==2,"No new truck collision layers or colliders");
            Check(prefab.GetComponent<FoodTruckHealth>()!=null && Field<Collider2D>(prefab.GetComponent<FoodTruckHealth>(),"damageCollider").name=="EnemyBarrier","Truck prefab reuses EnemyBarrier collider");
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                EditorSceneManager.OpenScene(path);
                var truck=Object.FindFirstObjectByType<FoodTruckHealth>(); var ui=Object.FindFirstObjectByType<FoodTruckStatusUI>();
                Check(truck!=null && Object.FindObjectsByType<FoodTruckHealth>().Length==1,path+" one truck health");
                Check(ui!=null && Object.FindObjectsByType<FoodTruckStatusUI>().Length==1,path+" one truck HUD");
                Check(Field<FoodTruckHealth>(ui,"truck")==truck,path+" local HUD reference");
                Check(Field<FoodTruckHealth>(Object.FindFirstObjectByType<EnemySpawner>(),"foodTruck")==truck,path+" spawner truck reference");
                Check(Object.FindObjectsByType<PlayerStatusUI>().Length==1,path+" player HUD preserved");
                var rect=(RectTransform)ui.transform;
                Check(rect.anchorMin==Vector2.zero && rect.anchorMax==Vector2.zero && rect.anchoredPosition==new Vector2(20,20),path+" bottom-left anchoring");
                Check(new SerializedObject(truck).FindProperty("settings").objectReferenceValue==config,path+" shared config reference");
            }
            GlobalDataPrototypeSetup.Upgrade(); Check(Object.FindObjectsByType<FoodTruckStatusUI>().Length==1,"Repeated setup is idempotent");
            EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            SessionState.SetString("TruckBaseline",baseline);SessionState.SetInt("TruckChecks",checks);SessionState.SetString("TruckResults",string.Join("\n",results));SessionState.SetBool("TruckPending",true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("TruckPending",false))return;
        SessionState.SetBool("TruckPending",false);checks=SessionState.GetInt("TruckChecks",0);results.Clear();results.AddRange(SessionState.GetString("TruckResults","").Split('\n'));
        config=Object.FindFirstObjectByType<GlobalData>().Settings;baseline=SessionState.GetString("TruckBaseline","");
        FieldSet(config,"gameplayDebugLogs",true);
        Application.logMessageReceived+=(s,t,type)=>{if(s=="Food Truck destroyed")destroyedLogs++;};
        new GameObject("TruckValidationRunner").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current=null;try{next=routine.MoveNext();if(next)current=routine.Current;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        Restore();File.WriteAllText("TRUCK_HEALTH_RESULTS.txt","PASS: "+checks+" checks\n"+string.Join("\n",results));Debug.Log("TRUCK_HEALTH_VALIDATION_PASS "+checks);EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var s in Object.FindObjectsByType<EnemySpawner>())s.enabled=false;
        foreach(var e in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(e.gameObject);yield return null;
        var player=Object.FindFirstObjectByType<PlayerController>();player.GetComponent<Rigidbody2D>().simulated=false;player.transform.position=new Vector3(12,0);
        var truck=Object.FindFirstObjectByType<FoodTruckHealth>();var ui=Object.FindFirstObjectByType<FoodTruckStatusUI>();var bar=Field<Slider>(ui,"hpBar");
        Check(truck.CurrentHP==500 && bar.value==1,"Truck starts at max and HUD full");
        yield return new WaitForFixedUpdate(); Physics2D.SyncTransforms();
        var collider=Field<Collider2D>(truck,"damageCollider");
        Debug.Log("Truck closest surface: "+truck.ClosestPoint(new Vector2(3.65f,0))+" bounds "+collider.bounds);
        Check(Vector2.Distance(truck.ClosestPoint(new Vector2(3.65f,0)),new Vector2(3.05f,0))<.02f,"Closest point uses actual truck surface instead of root pivot");
        truck.TakeDamage(0);truck.TakeDamage(-5);Check(truck.CurrentHP==500,"Invalid damage ignored");
        var ai=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab"),new Vector3(3.65f,0),Quaternion.identity);
        ai.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;ai.SetTarget(player.transform);ai.SetFoodTruckTarget(truck);
        var indicator=ai.GetComponentInChildren<ZombieAttackTelegraph>();
        Tick(ai,"FixedUpdate");Check(ai.IsAttackingTruck && indicator.IsVisible && truck.CurrentHP==500,"Far Player and near truck begins truck wind-up without immediate damage");
        Check(ai.AttackDirection.x<-.99f,"Telegraph points toward closest truck surface");
        var mesh=indicator.GetComponent<MeshFilter>().sharedMesh;
        Check(Mathf.Abs(mesh.vertices[1].magnitude-config.FoodTruckAttackableRange)<.001f,"Truck telegraph radius uses truck eligibility range");
        yield return new WaitForSeconds(.2f);Check(truck.CurrentHP==500,"Truck takes no damage before wind-up");
        yield return new WaitForSeconds(.23f);Check(truck.CurrentHP==490 && Mathf.Approximately(bar.value,.98f) && player.HP==100,"Shared wind-up damages truck and updates bottom-left bar only");
        Check(!indicator.IsVisible && !ai.IsWindingUp,"Truck hit hides shared telegraph");
        yield return new WaitForSeconds(.15f);Check(truck.CurrentHP==490,"Truck attack respects post-hit cooldown");
        player.transform.position=new Vector3(4.15f,0);Restart(ai);Tick(ai,"FixedUpdate");
        Check(ai.IsWindingUp && !ai.IsAttackingTruck && ai.AttackDirection.x>.99f,"Player priority when both targets are in range");
        yield return new WaitForSeconds(.43f);Check(player.HP==90 && truck.CurrentHP==490,"Player attack does not also damage truck");
        player.transform.position=new Vector3(12,0);Restart(ai);Tick(ai,"FixedUpdate");
        player.transform.position=new Vector3(4.15f,0);yield return new WaitForSeconds(.43f);
        Check(truck.CurrentHP==480 && player.HP==90,"Recipient stays locked during truck warning; no invisible mid-wind-up retarget");
        Restart(ai);Tick(ai,"FixedUpdate");Check(!ai.IsAttackingTruck,"Next attack reevaluates Player priority");
        ai.enabled=false;player.transform.position=new Vector3(12,0);ai.enabled=true;Restart(ai);Tick(ai,"FixedUpdate");
        Set("foodTruckAttackableRange",.3f);Tick(indicator,"LateUpdate");
        Check(Mathf.Abs(indicator.GetComponent<MeshFilter>().sharedMesh.vertices[1].magnitude-.3f)<.001f,"Live truck range edit redraws shared telegraph");
        yield return new WaitForSeconds(.43f);Check(truck.CurrentHP==480,"Shrunk eligibility range before impact makes attack miss");
        Set("foodTruckAttackableRange",1.1f);Restart(ai);Tick(ai,"FixedUpdate");Check(ai.IsAttackingTruck,"Expanded live range changes eligibility");
        Set("zombieAttackDamage",7f);yield return new WaitForSeconds(.43f);Check(truck.CurrentHP==473 && player.HP==90,"Truck reads same live Zombie damage");Set("zombieAttackDamage",10f);
        Restart(ai);Tick(ai,"FixedUpdate");truck.transform.position=new Vector3(-5,0);Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.43f);Check(truck.CurrentHP==473,"Moved truck outside locked cone/range cannot be hit");
        truck.transform.position=Vector3.zero;Physics2D.SyncTransforms();Set("foodTruckAttackableRange",.8f);
        Restart(ai);Tick(ai,"FixedUpdate");ai.enabled=false;yield return new WaitForSeconds(.43f);
        Check(truck.CurrentHP==473 && !indicator.IsVisible,"AI disable cancels pending truck damage");
        ai.enabled=true;ai.SetTarget(null);Restart(ai);Tick(ai,"FixedUpdate");Check(ai.IsAttackingTruck,"Truck remains a valid target with no Player reference");
        ai.enabled=false;ai.transform.position=new Vector3(10,10);ai.SetTarget(player.transform);ai.enabled=true;Physics2D.SyncTransforms();Tick(ai,"FixedUpdate");
        Check(!ai.IsWindingUp && ai.GetComponent<Rigidbody2D>().linearVelocity.sqrMagnitude>0,"Neither target in range preserves existing chase behavior");
        Object.Destroy(ai.gameObject);yield return null;

        Set("foodTruckMaxHP",800);Tick(truck,"Update");Tick(ui,"LateUpdate");
        Check(truck.CurrentHP==473 && Mathf.Abs(bar.value-473f/800)<.001f,"Raising live max updates ratio without healing existing truck");
        var fresh=Object.Instantiate(AssetDatabase.LoadAssetAtPath<FoodTruckHealth>("Assets/Prototype/Prefabs/FoodTruck.prefab"),new Vector3(30,30),Quaternion.identity);
        Check(fresh.CurrentHP==800 && fresh.MaxHP==800,"New truck uses edited maximum HP");Object.Destroy(fresh.gameObject);yield return null;
        Set("foodTruckMaxHP",500);Tick(truck,"Update");
        ui.enabled=false;truck.TakeDamage(3);ui.enabled=true;
        Check(Mathf.Abs(bar.value-.94f)<.001f,"Truck HUD reenable refreshes and resubscribes");
        var canvas=ui.GetComponentInParent<Canvas>();var canvasRect=(RectTransform)canvas.transform;canvas.renderMode=RenderMode.WorldSpace;
        foreach(var size in new[]{new Vector2(1280,720),new Vector2(1920,1080),new Vector2(777,510)})
        {
            canvasRect.sizeDelta=size;Canvas.ForceUpdateCanvases();var rect=(RectTransform)ui.transform;var corners=new Vector3[4];rect.GetLocalCorners(corners);
            var bottomLeft=canvasRect.InverseTransformPoint(rect.TransformPoint(corners[0]));
            Check(Vector2.Distance(bottomLeft,new Vector2(canvasRect.rect.xMin+20,canvasRect.rect.yMin+20))<.01f,"Bottom-left anchoring at "+size);
        }
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;
        Set("foodTruckMaxHP",100);Tick(truck,"Update");Check(truck.CurrentHP==100 && bar.value==1,"Lowered max clamps HP without resetting to another state");
        truck.TakeDamage(1000);truck.TakeDamage(1000);
        Check(truck.IsDestroyed && truck.CurrentHP==0 && bar.value==0 && destroyedLogs==1,"Destroyed truck clamps zero and logs exactly once");
        var last=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab"),new Vector3(3.65f,0),Quaternion.identity);
        last.SetTarget(player.transform);last.SetFoodTruckTarget(truck);Tick(last,"FixedUpdate");
        Check(!last.IsWindingUp,"Zombies skip destroyed truck");Object.Destroy(last.gameObject);yield return null;
        var spawner=Object.FindFirstObjectByType<EnemySpawner>();FieldSet(spawner,"hasSpawned",false);FieldSet(spawner,"spawnedCount",0);Tick(spawner,"Update");
        var spawned=Object.FindObjectsByType<EnemySimpleAI>().Single();
        Check(Field<FoodTruckHealth>(spawned,"foodTruck")==truck,"Runtime spawner passes scene truck reference to Zombie");Object.Destroy(spawned.gameObject);yield return null;
        Check(Object.FindObjectsByType<PlayerStatusUI>().Length==1 && Object.FindObjectsByType<FoodTruckStatusUI>().Length==1,"Player and truck HUD panels remain independent");
    }
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void FieldSet(object target,string name,object value)=>target.GetType().GetField(name,Private).SetValue(target,value);
    static void Tick(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
    static void Restart(EnemySimpleAI ai)=>FieldSet(ai,"lastAttackTime",float.NegativeInfinity);
    static void Set(string name,float value){FieldSet(config,name,value);config.Validate();}
    static void Check(bool ok,string message){if(!ok)throw new Exception("CHECK FAILED: "+message);results.Add("CHECK "+(++checks)+": "+message);Debug.Log(results[^1]);}
    static void Restore(){if(config!=null&&!string.IsNullOrEmpty(baseline)){EditorJsonUtility.FromJsonOverwrite(baseline,config);config.Validate();EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);}}
    static void Fail(Exception e){Restore();Debug.LogException(e);File.WriteAllText("TRUCK_HEALTH_RESULTS.txt","FAIL after "+checks+" checks\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}



