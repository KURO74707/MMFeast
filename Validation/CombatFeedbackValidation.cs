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
public static class CombatFeedbackValidation
{
    private static GlobalGameSettings config;
    private static string baseline;
    private static int checks;
    private static readonly System.Collections.Generic.List<string> results=new();
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static CombatFeedbackValidation() { EditorApplication.playModeStateChanged+=OnPlay; }
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            config=GlobalDataPrototypeSetup.GetOrCreateSettings();
            baseline=EditorJsonUtility.ToJson(config);
            Check(config.ZombieAttackWindupTime==.4f && config.ZombieAttackAngle==60f,"Wind-up and sector defaults");
            Check(config.PlayerHitFlashDuration==.15f && config.PlayerHitShakeStrength==.08f && config.PlayerHitShakeDuration==.15f,"Player feedback defaults");
            foreach(var path in new[]{CombatPrototypeBuilder.ScenePath,CookingPrototypeBuilder.ScenePath})
            {
                EditorSceneManager.OpenScene(path);
                Check(Object.FindObjectsByType<PlayerHitFeedback>().Length==1 && Object.FindObjectsByType<SimpleCameraShake>().Length==1,path+" local feedback wiring");
                Check(Object.FindObjectsByType<PlayerStatusUI>().Length==1,path+" original HUD retained");
            }
            foreach(var path in new[]{"Assets/Prototype/Prefabs/Enemy.prefab","Assets/Prototype/Prefabs/Customer.prefab"})
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Check(prefab.GetComponentsInChildren<WorldHealthBar>(true).Length==1,path+" single shared health bar");
                Check(prefab.transform.Find("HealthBarAnchor")!=null,path+" local anchor");
                Check(prefab.GetComponentInChildren<Canvas>(true).renderMode==RenderMode.WorldSpace,path+" world-space Canvas");
                Check(prefab.GetComponentsInChildren<Collider2D>(true).Length==1,path+" no visual colliders added");
                if(prefab.GetComponent<Customer>()!=null) Check(prefab.GetComponentInChildren<ZombieAttackTelegraph>()==null,"Customers do not get Zombie attacks");
                else Check(prefab.GetComponentsInChildren<ZombieAttackTelegraph>(true).Length==1,"Zombie has runtime sector visual");
            }
            GlobalDataPrototypeSetup.Upgrade();
            Check(Object.FindObjectsByType<PlayerHitFeedback>().Length==1,"Repeated setup does not duplicate feedback");
            EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            SessionState.SetString("FeedbackBaseline",baseline); SessionState.SetInt("FeedbackChecks",checks);
            SessionState.SetString("FeedbackResults",string.Join("\n",results)); SessionState.SetBool("FeedbackPending",true);
            EditorApplication.EnterPlaymode();
        }
        catch(Exception e){Fail(e);}
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("FeedbackPending",false)) return;
        SessionState.SetBool("FeedbackPending",false);
        checks=SessionState.GetInt("FeedbackChecks",0); results.Clear(); results.AddRange(SessionState.GetString("FeedbackResults","").Split('\n'));
        config=Object.FindFirstObjectByType<GlobalData>().Settings; baseline=SessionState.GetString("FeedbackBaseline","");
        new GameObject("FeedbackValidationRunner").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    private static IEnumerator Guard(IEnumerator routine)
    {
        while(true)
        {
            bool next; object current=null;
            try {next=routine.MoveNext(); if(next)current=routine.Current;} catch(Exception e){Fail(e);yield break;}
            if(!next)break; yield return current;
        }
        Restore(); File.WriteAllText("COMBAT_FEEDBACK_RESULTS.txt","PASS: "+checks+" checks\n"+string.Join("\n",results));
        Debug.Log("COMBAT_FEEDBACK_VALIDATION_PASS "+checks); EditorApplication.Exit(0);
    }
    private static IEnumerator Tests()
    {
        foreach(var s in Object.FindObjectsByType<EnemySpawner>())s.enabled=false;
        foreach(var e in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(e.gameObject);
        yield return null;
        var player=Object.FindFirstObjectByType<PlayerController>();
        player.GetComponent<Rigidbody2D>().simulated=false; player.transform.position=new Vector3(21,20);
        var feedback=player.GetComponent<PlayerHitFeedback>();
        var sprite=Field<SpriteRenderer>(feedback,"playerRenderer"); var normal=sprite.color;
        var shake=Object.FindFirstObjectByType<SimpleCameraShake>(); var camera=shake.GetComponent<Camera>(); var cameraPosition=camera.transform.localPosition;
        var hud=Object.FindFirstObjectByType<PlayerStatusUI>(); var playerHP=Field<Slider>(hud,"hpBar");
        var prefab=AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab");
        var ai=Object.Instantiate(prefab,new Vector3(20,20),Quaternion.identity); ai.SetTarget(player.transform);
        ai.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        var health=ai.GetComponent<EnemyHealth>(); var bar=ai.GetComponentInChildren<WorldHealthBar>();
        var slider=bar.GetComponentInChildren<Slider>(); var canvas=bar.GetComponent<Canvas>();
        var indicator=ai.GetComponentInChildren<ZombieAttackTelegraph>(); var renderer=indicator.GetComponent<MeshRenderer>();
        Tick(ai,"FixedUpdate");
        Check(ai.IsWindingUp && indicator.IsVisible && renderer.enabled && player.HP==100,"Telegraph appears before any damage");
        Check(ai.GetComponent<Rigidbody2D>().linearVelocity==Vector2.zero,"Zombie stops during wind-up");
        var mesh=indicator.GetComponent<MeshFilter>().sharedMesh;
        Check(mesh!=null && mesh.vertexCount==34,"Sector mesh exists during gameplay");
        var edge=indicator.transform.TransformPoint(mesh.vertices[1]);
        Check(Mathf.Abs(Vector2.Distance(edge,ai.transform.position)-config.ZombieAttackRange)<.0001f,"World-space radius matches damage range despite scaled prefab");
        var arc=mesh.vertices.Skip(1).ToArray();
        Check(Mathf.Abs(Vector2.Angle(arc[0],Vector2.right)-30f)<.001f && Mathf.Abs(Vector2.Angle(arc[^1],Vector2.right)-30f)<.001f,"Mesh boundaries match half the configured attack angle");
        Check(ai.IsInsideAttackArea(Vector2.right) && !ai.IsInsideAttackArea(Vector2.left) && !ai.IsInsideAttackArea(Vector2.up),"Directional hit test excludes rear and side");
        yield return new WaitForSeconds(.2f);
        Check(player.HP==100 && ai.IsWindingUp,"No damage before captured wind-up completes");
        yield return new WaitForSeconds(.23f);
        Check(player.HP==90 && Mathf.Approximately(playerHP.value,.9f),"Successful wind-up applies damage and updates HUD");
        Check(!ai.IsWindingUp && !indicator.IsVisible && !renderer.enabled,"Telegraph disappears at hit moment");
        Check(sprite.color!=normal,"Successful damage flashes Player renderer");
        Tick(shake,"LateUpdate"); // Shake applies its render offset after Update.
        Check(camera.transform.localPosition!=cameraPosition,"Successful damage shakes camera");
        yield return new WaitForSeconds(.2f);
        Check(sprite.color==normal && Vector3.Distance(camera.transform.localPosition,cameraPosition)<.0001f,"Flash and camera shake recover without drift");
        Check(player.HP==90 && !ai.IsWindingUp,"Cooldown starts after completed attack");
        Check(canvas.enabled && slider.value==1,"Zombie world HP bar starts full");
        health.TakeDamage(10);
        Check(Mathf.Approximately(slider.value,.8f),"Zombie HP bar updates synchronously");
        Set("zombieMaxHP",100);
        Tick(bar,"LateUpdate");
        Check(Mathf.Approximately(slider.value,.8f) && health.MaxHP==50,"Existing bar uses instance max HP after config edit");
        Set("zombieMaxHP",50);
        var oldBarPosition=bar.transform.position; ai.transform.position+=Vector3.up;
        Check(Vector3.Distance(bar.transform.position,oldBarPosition+Vector3.up)<.0001f,"HealthBarAnchor follows entity movement");
        ai.transform.position=new Vector3(20,20);
        var oldRotation=camera.transform.rotation; camera.transform.rotation=Quaternion.Euler(0,0,12); Tick(bar,"LateUpdate");
        Check(Quaternion.Angle(bar.transform.rotation,camera.transform.rotation)<.001f,"Shared bar faces active camera without LookAt flipping");
        camera.transform.rotation=oldRotation;

        // Retreat beyond radius before the hit moment.
        Restart(ai); Tick(ai,"FixedUpdate"); player.transform.position=new Vector3(23,20);
        yield return new WaitForSeconds(.43f);
        Check(player.HP==90 && !indicator.IsVisible && sprite.color==normal,"Leaving range during wind-up misses and gives no hit feedback");
        // Dodge behind without leaving radius.
        player.transform.position=new Vector3(21,20); Restart(ai); Tick(ai,"FixedUpdate");
        var locked=ai.AttackDirection; player.transform.position=new Vector3(19,20);
        yield return new WaitForSeconds(.43f);
        Check(player.HP==90 && ai.AttackDirection==locked,"Moving behind locked sector misses without homing");
        // Live range/angle changes update both rendering and hit validation.
        player.transform.position=new Vector3(21,20); Restart(ai); Tick(ai,"FixedUpdate");
        Set("zombieAttackRange",2); Set("zombieAttackAngle",90); Tick(indicator,"LateUpdate");
        mesh=indicator.GetComponent<MeshFilter>().sharedMesh; edge=indicator.transform.TransformPoint(mesh.vertices[1]);
        Check(Mathf.Abs(Vector2.Distance(edge,ai.transform.position)-2f)<.0001f && Mathf.Abs(Vector2.Angle(mesh.vertices[1],Vector2.right)-45f)<.001f,"Live range and angle edits redraw the same sector");
        Check(ai.IsInsideAttackArea(new Vector2(1,1)) && !ai.IsInsideAttackArea(new Vector2(-1,1)),"Live angle also changes hit area");
        Set("zombieAttackRange",.5f); Tick(indicator,"LateUpdate");
        yield return new WaitForSeconds(.43f);
        Check(player.HP==90,"Shrinking visible range before impact causes a miss");
        Set("zombieAttackRange",1.2f); Set("zombieAttackAngle",60);
        player.transform.position=new Vector3(21,20); Restart(ai); Tick(ai,"FixedUpdate");
        Set("zombieAttackWindupTime",.01f);
        yield return new WaitForSeconds(.08f);
        Check(ai.IsWindingUp && player.HP==90,"Changing wind-up config does not restart an existing warning");
        ai.enabled=false;
        Check(!indicator.IsVisible && !renderer.enabled && !ai.IsWindingUp,"Disabling AI cancels pending hit and hides telegraph");
        yield return new WaitForSeconds(.4f);
        Check(player.HP==90,"Disabled pending attack cannot damage later");
        ai.enabled=true; Restart(ai); Tick(ai,"FixedUpdate");
        yield return new WaitForSeconds(.04f);
        Check(player.HP==80,"Next wind-up uses edited duration");
        Set("zombieAttackWindupTime",.4f);
        yield return new WaitForSeconds(.2f);
        Restart(ai); Tick(ai,"FixedUpdate");
        int meatBefore=Object.FindObjectsByType<IngredientPickup>().Length;
        health.TakeDamage(100);
        Check(!ai.IsWindingUp && !indicator.IsVisible && !canvas.enabled,"Zombie death immediately cancels attack and hides HP bar");
        Check(Object.FindObjectsByType<IngredientPickup>().Length==meatBefore+1,"Zombie Meat drop remains intact");
        var barObject=bar.gameObject; yield return null;
        Check(barObject==null,"Zombie removal leaves no orphan HP bar");

        var customerPrefab=AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab");
        var customer=Object.Instantiate(customerPrefab,new Vector3(25,20),Quaternion.identity);
        customer.GetComponent<EnemySimpleAI>().enabled=false;
        var customerBar=customer.GetComponentInChildren<WorldHealthBar>(); var customerSlider=customerBar.GetComponentInChildren<Slider>();
        var customerCanvas=customerBar.GetComponent<Canvas>();
        Check(customerCanvas.enabled && customerSlider.value==1,"Customer shares full world HP bar");
        customer.TakeDamage(15,player);
        Check(Mathf.Approximately(customerSlider.value,.7f),"Customer HP bar updates synchronously");
        float sanBefore=player.SAN; meatBefore=Object.FindObjectsByType<IngredientPickup>().Length;
        customer.Purify();
        Check(!customerCanvas.enabled && customer.IsPurified && !customer.IsDead,"Purification hides bar without death");
        Check(player.SAN==sanBefore && Object.FindObjectsByType<IngredientPickup>().Length==meatBefore,"Purification still has no SAN loss or meat");
        customer.TakeDamage(100,player); customer.Purify();
        Check(!customerCanvas.enabled && player.SAN==sanBefore,"Purified bar stays hidden on repeated resolution");
        var doomed=Object.Instantiate(customerPrefab,new Vector3(26,20),Quaternion.identity); doomed.GetComponent<EnemySimpleAI>().enabled=false;
        var doomedBar=doomed.GetComponentInChildren<WorldHealthBar>();
        doomed.TakeDamage(100,player);
        Check(!doomedBar.GetComponent<Canvas>().enabled && doomed.IsDead,"Combat death immediately hides Customer bar");
        Check(player.SAN==sanBefore-10 && Object.FindObjectsByType<IngredientPickup>().Length==meatBefore+1,"Customer death still applies one SAN penalty and one meat drop");
        var doomedBarObject=doomedBar.gameObject; yield return null;
        Check(doomedBarObject==null,"Customer death leaves no orphan bar");

        // Feedback fires only for damage, and repeated hits restore the original color.
        player.SetCurrentHP(100); yield return new WaitForSeconds(.2f);
        player.TakeDamage(0); player.TakeDamage(-1); player.LoseSAN(1); player.SetCurrentHP(95);
        Check(sprite.color==normal && Vector3.Distance(camera.transform.localPosition,cameraPosition)<.0001f,"No hit feedback for zero/negative damage, SAN or debug setters");
        player.TakeDamage(5); player.TakeDamage(5);
        Check(sprite.color!=normal,"Repeated actual damage still flashes");
        feedback.enabled=false;
        Check(sprite.color==normal,"Disabling feedback restores original color");
        feedback.enabled=true; player.TakeDamage(5);
        Check(sprite.color!=normal,"Feedback reenable resubscribes without losing normal color");
        shake.enabled=false;
        Check(Vector3.Distance(camera.transform.localPosition,cameraPosition)<.0001f,"Disabling shake removes its offset");
        yield return new WaitForSeconds(.2f);
        Check(sprite.color==normal,"Player color returns after repeated hits");
        shake.enabled=true;
        player.SetCurrentHP(0); yield return new WaitForSeconds(.2f); player.TakeDamage(5);
        Check(sprite.color==normal && Vector3.Distance(camera.transform.localPosition,cameraPosition)<.0001f,"No feedback when zero-HP Player takes no actual damage");
        Set("playerHitFlashDuration",0); Set("playerHitShakeStrength",0); Set("playerHitShakeDuration",0); player.SetCurrentHP(100); player.TakeDamage(5);
        Check(sprite.color==normal && Vector3.Distance(camera.transform.localPosition,cameraPosition)<.0001f,"Zero config values disable optional feedback cleanly");
    }
    private static void Restart(EnemySimpleAI ai) {ai.GetType().GetField("lastAttackTime",Private).SetValue(ai,float.NegativeInfinity);}
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,Private).GetValue(target);
    private static void Tick(object target,string name)=>target.GetType().GetMethod(name,Private).Invoke(target,null);
    private static void Set(string name,float value){config.GetType().GetField(name,Private).SetValue(config,value);config.Validate();}
    private static void Check(bool condition,string message){if(!condition)throw new Exception("CHECK FAILED: "+message);results.Add("CHECK "+(++checks)+": "+message);Debug.Log(results[^1]);}
    private static void Restore(){if(config!=null && !string.IsNullOrEmpty(baseline)){EditorJsonUtility.FromJsonOverwrite(baseline,config);config.Validate();EditorUtility.SetDirty(config);AssetDatabase.SaveAssetIfDirty(config);}}
    private static void Fail(Exception e){Restore();Debug.LogException(e);File.WriteAllText("COMBAT_FEEDBACK_RESULTS.txt","FAIL after "+checks+" checks\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}

