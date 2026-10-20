using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class CrowdHitValidation
{
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static GlobalGameSettings config;static string baseline;static int checks;
    static readonly System.Collections.Generic.List<string> results=new();
    static CrowdHitValidation(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();config=GlobalDataPrototypeSetup.GetOrCreateSettings();baseline=EditorJsonUtility.ToJson(config);
            Check(config.ZombieSeparationRadius==1.2f && config.ZombieSeparationStrength==.65f,"Separation defaults");
            Check(config.PlayerAttackShakeStrength==.035f && config.PlayerAttackShakeDuration==.08f,"Attack feedback defaults");
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                EditorSceneManager.OpenScene(path);
                Check(Field<SimpleCameraShake>(Object.FindFirstObjectByType<PlayerCombat>(),"cameraShake")==Object.FindFirstObjectByType<SimpleCameraShake>(),path+" reuses existing camera shake");
                Check(Object.FindObjectsByType<SimpleCameraShake>().Length==1,path+" no second shake component");
            }
            Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Prefabs/Enemy.prefab").GetComponentsInChildren<Collider2D>(true).Length==1,"Zombie collider setup unchanged");
            // Single call is after the hit loop, independent of number of entities in a swing.
            var source=File.ReadAllText("Assets/Scripts/Prototype/PlayerCombat.cs");
            Check(System.Text.RegularExpressions.Regex.Matches(source,@"cameraShake\.Shake\(").Count==1 && source.IndexOf("if (causedDamage")>source.IndexOf("causedDamage |= after < before;"),"One shake call outside the damage loop");
            EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            SessionState.SetString("CrowdBaseline",baseline);SessionState.SetInt("CrowdChecks",checks);SessionState.SetString("CrowdResults",string.Join("\n",results));SessionState.SetBool("CrowdPending",true);EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("CrowdPending",false))return;
        SessionState.SetBool("CrowdPending",false);config=Object.FindFirstObjectByType<GlobalData>().Settings;baseline=SessionState.GetString("CrowdBaseline","");checks=SessionState.GetInt("CrowdChecks",0);results.Clear();results.AddRange(SessionState.GetString("CrowdResults","").Split('\n'));
        new GameObject("CrowdValidationRunner").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true){bool next;object current=null;try{next=routine.MoveNext();if(next)current=routine.Current;}catch(Exception e){Fail(e);yield break;}if(!next)break;yield return current;}
        Restore();File.WriteAllText("CROWD_HIT_RESULTS.txt","PASS: "+checks+" checks\n"+string.Join("\n",results));Debug.Log("CROWD_HIT_VALIDATION_PASS "+checks);EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        var player=Object.FindFirstObjectByType<PlayerController>();player.GetComponent<Rigidbody2D>().simulated=false;player.transform.position=new Vector3(30,20);
        var prefab=AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab");
        var a=Spawn(prefab,new Vector3(20,20),player);var b=Spawn(prefab,new Vector3(20,20),player);
        a.GetComponent<Rigidbody2D>().constraints=b.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;Physics2D.SyncTransforms();
        var randomBefore=UnityEngine.Random.state;var sepA=Separation(a);var sepB=Separation(b);
        Check(sepA.magnitude>.9f && (sepA+sepB).magnitude<.001f,"Exact stacks receive stable opposite pair corrections");
        Check(JsonUtility.ToJson(randomBefore)==JsonUtility.ToJson(UnityEngine.Random.state),"Separation does not consume gameplay Random");
        b.transform.position=new Vector3(20,20.7f);Physics2D.SyncTransforms();sepA=Separation(a);
        Check(sepA.y<0 && Mathf.Abs(sepA.x)<.001f,"Nearby Zombie steers away with distance weighting");
        var extra=new GameObject("ExtraCollider");extra.transform.SetParent(b.transform,false);extra.AddComponent<BoxCollider2D>();Physics2D.SyncTransforms();
        Check((Separation(a)-sepA).magnitude<.001f,"Multi-collider neighbors count once");
        Tick(a,"FixedUpdate");var velocity=a.GetComponent<Rigidbody2D>().linearVelocity;
        Check(velocity.x>0 && velocity.y<0 && velocity.magnitude<=config.MonsterMoveSpeed+.001f,"Chase stays primary while steering and speed stays bounded");
        Set("zombieSeparationStrength",.9f);Tick(a,"FixedUpdate");Check(a.GetComponent<Rigidbody2D>().linearVelocity.x>0,"Maximum separation weight still advances toward target");
        Set("zombieSeparationStrength",0);Check(Separation(a)==Vector2.zero,"Zero strength disables steering");Set("zombieSeparationStrength",.65f);
        Set("zombieSeparationRadius",.5f);Check(Separation(a)==Vector2.zero,"Center-distance radius excludes distant neighbor");Set("zombieSeparationRadius",1.2f);
        b.enabled=false;Check(Separation(a)==Vector2.zero,"Disabled Zombie ignored");b.enabled=true;
        var human=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),new Vector3(20.1f,20),Quaternion.identity);human.GetComponent<EnemySimpleAI>().SetTarget(player.transform);human.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        Physics2D.SyncTransforms();Check((Separation(a)-sepA).magnitude<.001f && Separation(human.GetComponent<EnemySimpleAI>())==Vector2.zero,"Customers do not participate in Zombie separation");
        b.GetComponent<EnemyHealth>().TakeDamage(100);Check(Separation(a)==Vector2.zero,"Dead Zombie immediately excluded");
        Object.Destroy(a.gameObject);Object.Destroy(human.gameObject);yield return null;
        Set("zombieAttackDamage",1);
        player.transform.position=new Vector3(24,20);player.SetCurrentHP(100);
        var crowd=Enumerable.Range(0,5).Select(i=>Spawn(prefab,new Vector3(20,20),player)).ToArray();
        yield return new WaitForSeconds(4f);
        float minDistance=float.PositiveInfinity;for(int i=0;i<crowd.Length;i++)for(int j=i+1;j<crowd.Length;j++)minDistance=Mathf.Min(minDistance,Vector2.Distance(crowd[i].transform.position,crowd[j].transform.position));
        Check(minDistance>.5f,"Five initially stacked Zombies spread with real Rigidbody2D simulation");
        Check(crowd.All(z=>Vector2.Distance(z.transform.position,player.transform.position)<3f),"Crowd continues approaching Player");
        Check(crowd.Count(z=>!float.IsNegativeInfinity(Field<float>(z,"lastAttackTime")))>=2 && player.HP<100,"Multiple crowded Zombies reach attack positions and damage Player");
        foreach(var z in crowd.Where(z=>z.IsWindingUp)) Tick(z,"FixedUpdate"); // Check AI steering command before collision solver response.
        Check(crowd.Where(z=>z.IsWindingUp).All(z=>z.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero),"Wind-up stops steering to preserve telegraph and hit validation");
        foreach(var z in crowd)Object.Destroy(z.gameObject);yield return null;
        var truck=Object.FindFirstObjectByType<FoodTruckHealth>();float truckBefore=truck.CurrentHP;player.transform.position=new Vector3(-10,0);
        var truckCrowd=Enumerable.Range(0,4).Select(i=>Spawn(prefab,new Vector3(3.65f,0),player)).ToArray();foreach(var z in truckCrowd)z.SetFoodTruckTarget(truck);
        yield return new WaitForSeconds(3f);
        minDistance=float.PositiveInfinity;for(int i=0;i<truckCrowd.Length;i++)for(int j=i+1;j<truckCrowd.Length;j++)minDistance=Mathf.Min(minDistance,Vector2.Distance(truckCrowd[i].transform.position,truckCrowd[j].transform.position));
        Check(minDistance>.5f,"Truck crowd maintains readable separation");
        Check(truck.CurrentHP<truckBefore && truckCrowd.Count(z=>!float.IsNegativeInfinity(Field<float>(z,"lastAttackTime")))>=2,"Multiple nearby Zombies still attack Food Truck");
        Check(truckCrowd.All(z=>z.transform.position.x>3f),"Truck barrier still blocks Zombie entry");
        foreach(var z in truckCrowd)Object.Destroy(z.gameObject);yield return null;
        Set("zombieAttackDamage",10);player.transform.position=new Vector3(20,20);
        var combat=player.GetComponent<PlayerCombat>();var shake=Field<SimpleCameraShake>(combat,"cameraShake");var originalCamera=shake.transform.localPosition;
        var z1=Spawn(prefab,new Vector3(21,20),player);var z2=Spawn(prefab,new Vector3(21.2f,20.5f),player);z1.enabled=z2.enabled=false;
        var customer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),new Vector3(21.1f,19.5f),Quaternion.identity);customer.GetComponent<EnemySimpleAI>().enabled=false;
        Physics2D.SyncTransforms();Swing(combat);
        Check(z1.GetComponent<EnemyHealth>().CurrentHP==40 && z2.GetComponent<EnemyHealth>().CurrentHP==40 && customer.CurrentHP==40,"One swing damages three valid entities");
        Check(Field<float>(shake,"strength")==config.PlayerAttackShakeStrength && Field<float>(shake,"duration")==config.PlayerAttackShakeDuration,"Multi-hit uses single configured attack shake envelope");
        Tick(shake,"LateUpdate");Check(shake.transform.localPosition!=originalCamera,"Successful melee hit visibly offsets camera");
        var started=Field<float>(shake,"startedAt");combat.TryAttack();Check(Field<float>(shake,"startedAt")==started && z1.GetComponent<EnemyHealth>().CurrentHP==40,"Cooldown-blocked swing neither damages nor retriggers shake");
        yield return new WaitForSeconds(.12f);Check(Vector3.Distance(shake.transform.localPosition,originalCamera)<.001f,"Attack shake restores camera without drift");
        Object.Destroy(z1.gameObject);Object.Destroy(z2.gameObject);Object.Destroy(customer.gameObject);yield return null;
        started=Field<float>(shake,"startedAt");Swing(combat);Check(Field<float>(shake,"startedAt")==started,"Missed attack has no shake");
        var disabled=Spawn(prefab,new Vector3(21,20),player);disabled.enabled=false;disabled.GetComponent<EnemyHealth>().enabled=false;Physics2D.SyncTransforms();Swing(combat);
        Check(Field<float>(shake,"startedAt")==started,"Damage rejected by disabled health produces no hit shake");disabled.GetComponent<EnemyHealth>().enabled=true;
        Set("playerAttackDamage",0);Swing(combat);Check(Field<float>(shake,"startedAt")==started,"Zero actual damage produces no hit shake");Set("playerAttackDamage",10);
        Set("playerAttackShakeStrength",.02f);Set("playerAttackShakeDuration",.05f);Swing(combat);
        Check(Field<float>(shake,"strength")==.02f && Field<float>(shake,"duration")==.05f,"Live config edits affect next successful attack feedback");
        yield return new WaitForSeconds(.08f);Set("playerAttackShakeStrength",0);started=Field<float>(shake,"startedAt");Swing(combat);
        Check(Field<float>(shake,"startedAt")==started,"Zero attack shake strength disables confirmation cleanly");
        player.TakeDamage(1);Check(Field<float>(shake,"strength")==config.PlayerHitShakeStrength,"Player damage feedback still uses original same shake component");
    }
    static EnemySimpleAI Spawn(EnemySimpleAI prefab,Vector3 position,PlayerController player){var z=Object.Instantiate(prefab,position,Quaternion.identity);z.SetTarget(player.transform);return z;}
    static Vector2 Separation(EnemySimpleAI z)=>(Vector2)z.GetType().GetMethod("Separation",Private).Invoke(z,null);
    static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    static void Tick(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
    static void Set(string name,float value){config.GetType().GetField(name,Private).SetValue(config,value);config.Validate();}
    static void Swing(PlayerCombat combat){combat.GetType().GetField("lastAttackTime",Private).SetValue(combat,float.NegativeInfinity);combat.TryAttack();}
    static void Check(bool ok,string message){if(!ok)throw new Exception("CHECK FAILED: "+message);results.Add("CHECK "+(++checks)+": "+message);Debug.Log(results[^1]);}
    static void Restore(){if(config!=null&&!string.IsNullOrEmpty(baseline)){EditorJsonUtility.FromJsonOverwrite(baseline,config);config.Validate();EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);}}
    static void Fail(Exception e){Restore();Debug.LogException(e);File.WriteAllText("CROWD_HIT_RESULTS.txt","FAIL after "+checks+" checks\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}

