using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CombatSanValidation
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int checks;
    private static GlobalGameSettings config;
    private static string baseline;
    private static readonly System.Collections.Generic.List<string> logs = new();
    static CombatSanValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            config = GlobalDataPrototypeSetup.GetOrCreateSettings();
            baseline = EditorJsonUtility.ToJson(config);
            SessionState.SetString("CombatSANBaseline", baseline);
            Check(config.PlayerMaxHP == 100 && config.PlayerMaxSAN == 100, "Migrated player maxima preserved");
            Check(config.AttackDamage == 10 && config.AttackRange == 3 && Mathf.Approximately(config.AttackCooldown, .1f), "Existing combat tuning preserved");
            Check(config.MonsterHP == 50 && config.CustomerMaxHP == 50, "Zombie and Customer HP defaults");
            Check(config.ZombieAttackDamage == 10 && config.ZombieAttackRange == 1.2f && config.ZombieAttackCooldown == 1, "Zombie attack defaults");
            Check(config.SANPenaltyForKillingCustomer == 10, "Customer SAN penalty default");
            Check(config.Move.bindings.Any(b => b.path == "<Keyboard>/w"), "Existing movement bindings preserved");
            Check(new SerializedObject(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab")).FindProperty("ingredientPrefab").objectReferenceValue == AssetDatabase.LoadAssetAtPath<IngredientPickup>(CombatStatusPrototypeSetup.CustomerMeatPath), "Customer drop reference assigned");
            foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                EditorSceneManager.OpenScene(path);
                Check(Object.FindObjectsByType<GlobalData>().Length == 1, path + " one Global Data");
                Check(Object.FindFirstObjectByType<GlobalData>().Settings == config, path + " shared config");
                Check(Object.FindObjectsByType<PlayerStatusUI>().Length == 1, path + " one HUD");
                Check(Object.FindObjectsByType<Canvas>().Count(c=>c.renderMode==RenderMode.ScreenSpaceOverlay) == 1, path + " one Canvas");
                var hud = Object.FindFirstObjectByType<PlayerStatusUI>();
                var rect = (RectTransform)hud.transform;
                Check(rect.anchorMin == new Vector2(0,1) && rect.anchorMax == new Vector2(0,1), path + " top-left anchors");
                Check(Field<PlayerController>(hud,"player") == Object.FindFirstObjectByType<PlayerController>(), path + " local Player reference");
                Check(hud.GetComponentsInChildren<Slider>().Length == 3, path + " HP, SAN and Stamina status bars");
            }
            // Repeat migration to ensure no duplicated panels/assets when generators are reused.
            GlobalDataPrototypeSetup.Upgrade();
            Check(Object.FindObjectsByType<PlayerStatusUI>().Length == 1, "Migration is idempotent");
            EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);
            SessionState.SetString("CombatSANEditChecks", string.Join("\n",logs));
            SessionState.SetInt("CombatSANChecks", checks);
            SessionState.SetBool("CombatSANPending",true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Fail(error); }
    }
    private static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CombatSANPending",false)) return;
        SessionState.SetBool("CombatSANPending",false);
        checks = SessionState.GetInt("CombatSANChecks",0);
        logs.Clear(); logs.AddRange(SessionState.GetString("CombatSANEditChecks", "").Split('\n'));
        config = Object.FindFirstObjectByType<GlobalData>().Settings;
        baseline = SessionState.GetString("CombatSANBaseline", "");
        typeof(GlobalGameSettings).GetField("gameplayDebugLogs", Private).SetValue(config, true);
        Application.logMessageReceived += CaptureLog;
        new GameObject("CombatSanValidationRunner").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    private static IEnumerator Guard(IEnumerator tests)
    {
        while (true)
        {
            bool next; object current = null;
            try { next = tests.MoveNext(); if (next) current = tests.Current; }
            catch (Exception error) { Fail(error); yield break; }
            if (!next) break;
            yield return current;
        }
        Restore();
        File.WriteAllText("COMBAT_SAN_RESULTS.txt", "PASS: " + checks + " checks\n" + string.Join("\n", logs.Where(l => l.StartsWith("CHECK"))));
        Debug.Log("COMBAT_SAN_VALIDATION_PASS " + checks);
        EditorApplication.Exit(0);
    }
    private static IEnumerator Tests()
    {
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
        foreach (var existing in Object.FindObjectsByType<EnemySimpleAI>()) Object.Destroy(existing.gameObject);
        yield return null;
        var player = Object.FindFirstObjectByType<PlayerController>();
        var combat = player.GetComponent<PlayerCombat>();
        var hud = Object.FindFirstObjectByType<PlayerStatusUI>();
        var hpBar = Field<Slider>(hud,"hpBar"); var sanBar = Field<Slider>(hud,"sanBar");
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        var playerBody = player.GetComponent<Rigidbody2D>(); playerBody.simulated = false;
        player.transform.position = new Vector3(20,20,0);
        Check(player.HP == 100 && player.SAN == 100, "Player starts at configured maxima");
        player.TakeDamage(20);
        Check(player.HP == 80 && Mathf.Approximately(hpBar.value,.8f), "Damage updates HP and HUD synchronously");
        player.LoseSAN(10);
        Check(player.SAN == 90 && Mathf.Approximately(sanBar.value,.9f), "SAN loss updates HUD synchronously");
        player.TakeDamage(-10); player.LoseSAN(-10);
        Check(player.HP == 80 && player.SAN == 90, "Negative damage/loss ignored");
        player.SetCurrentHP(1000); player.SetCurrentSAN(1000);
        Check(player.HP == 100 && player.SAN == 100, "Debug setters clamp maxima");
        player.TakeDamage(1000); player.TakeDamage(10);
        Check(player.HP == 0 && hpBar.value == 0 && logs.Count(l=>l=="Player died") == 1, "Zero HP clamped and death logged once");
        player.LoseSAN(1000);
        Check(player.SAN == 0 && sanBar.value == 0, "SAN clamps at zero");
        player.SetCurrentHP(88); player.SetCurrentSAN(77);
        Set("playerMaxHP",200f); Set("playerMaxSAN",200f); Tick(player,"Update"); Tick(hud,"LateUpdate");
        Check(player.HP == 88 && player.SAN == 77 && Mathf.Approximately(hpBar.value,.44f), "Live maxima edits do not refill state");
        Set("playerMaxHP",50f); Set("playerMaxSAN",50f); Tick(player,"Update");
        Check(player.HP == 50 && player.SAN == 50, "Lowered maxima clamp current values");
        Set("playerMaxHP",100f); Set("playerMaxSAN",100f); player.SetCurrentHP(100); player.SetCurrentSAN(100);
        hud.enabled = false; player.TakeDamage(10); hud.enabled = true;
        Check(Mathf.Approximately(hpBar.value,.9f), "HUD component reenable refreshes and resubscribes");
        player.SetCurrentHP(100);

        Set("zombieAttackWindupTime",0f); // Legacy damage/cooldown regression; real wind-up is tested separately.
        var enemyPrefab = AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab");
        var customerPrefab = AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab");
        var zombie = Object.Instantiate(enemyPrefab,player.transform.position+Vector3.right*3,Quaternion.identity);
        zombie.enabled = false; zombie.SetTarget(player.transform);
        zombie.GetComponent<Rigidbody2D>().simulated = false;
        Tick(zombie,"FixedUpdate");
        Check(player.HP == 100 && zombie.GetComponent<Rigidbody2D>().linearVelocity.x < 0, "Zombie pursues outside attack range without damage");
        zombie.transform.position = player.transform.position + Vector3.right;
        Physics2D.SyncTransforms(); Tick(zombie,"FixedUpdate"); Tick(zombie,"FixedUpdate");
        Check(player.HP == 90 && zombie.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "Zombie attacks in range and stops");
        for (int i=0;i<100;i++) Tick(zombie,"FixedUpdate");
        Check(player.HP == 90, "Cooldown prevents per-frame damage");
        Set("zombieAttackDamage",7f); Set("zombieAttackCooldown",.05f);
        yield return new WaitForSeconds(.07f); Tick(zombie,"FixedUpdate"); Tick(zombie,"FixedUpdate");
        Check(player.HP == 83, "Live damage/cooldown edits used for next attack");
        zombie.transform.position = player.transform.position + Vector3.right*2;
        Set("zombieAttackRange",2.1f); Physics2D.SyncTransforms();
        yield return new WaitForSeconds(.07f); Tick(zombie,"FixedUpdate"); Tick(zombie,"FixedUpdate");
        Check(player.HP == 76, "Live attack range edit takes effect");
        zombie.GetComponent<EnemyHealth>().TakeDamage(5);
        Set("zombieMaxHP",80f);
        Check(zombie.GetComponent<EnemyHealth>().CurrentHP == 45, "Existing injured Zombie not reset by max HP edit");
        var newZombie = Object.Instantiate(enemyPrefab, new Vector3(40,40),Quaternion.identity);
        newZombie.enabled=false;
        Check(newZombie.GetComponent<EnemyHealth>().CurrentHP == 80, "New Zombie uses edited maximum HP");
        Object.Destroy(zombie.gameObject); Object.Destroy(newZombie.gameObject); yield return null;
        Set("zombieMaxHP",50f); Set("zombieAttackDamage",10f); Set("zombieAttackRange",1.2f); Set("zombieAttackCooldown",1f);
        player.SetCurrentHP(100); player.SetCurrentSAN(100);

        Set("customerMaxHP",25f);
        var customer = Object.Instantiate(customerPrefab,player.transform.position+Vector3.right,Quaternion.identity);
        customer.GetComponent<EnemySimpleAI>().enabled=false;
        customer.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        var extra = new GameObject("ExtraCollider"); extra.transform.SetParent(customer.transform,false); extra.AddComponent<BoxCollider2D>();
        Physics2D.SyncTransforms();
        int meatBefore = Object.FindObjectsByType<IngredientPickup>().Length;
        TickField(combat,"lastAttackTime",float.NegativeInfinity); combat.TryAttack();
        Check(customer.CurrentHP == 15 && player.SAN == 100, "One melee hit per Customer despite multiple colliders; no SAN penalty while alive");
        combat.TryAttack();
        Check(customer.CurrentHP == 15, "Existing player attack cooldown preserved");
        Set("customerMaxHP",80f);
        Check(customer.CurrentHP == 15, "Existing Customer HP not reset");
        var freshCustomer = Object.Instantiate(customerPrefab,new Vector3(40,40),Quaternion.identity);
        freshCustomer.GetComponent<EnemySimpleAI>().enabled=false;
        Check(freshCustomer.CurrentHP==80, "New Customer reads edited max HP");
        Object.Destroy(freshCustomer.gameObject);
        Set("sanPenaltyForKillingCustomer",13f);
        customer.TakeDamage(15,player);
        Check(customer.IsDead && !customer.IsPurified && player.SAN == 87 && Mathf.Approximately(sanBar.value,.87f), "Combat death applies configured SAN penalty once and updates HUD");
        Check(Object.FindObjectsByType<IngredientPickup>().Length == meatBefore+1, "Customer drops exactly one Meat");
        customer.TakeDamage(100,player); customer.Purify();
        Check(customer.IsDead && !customer.IsPurified && player.SAN==87 && Object.FindObjectsByType<IngredientPickup>().Length==meatBefore+1, "Dead Customer cannot be damaged or purified twice");
        var customerMeat = Object.FindObjectsByType<IngredientPickup>().Single(m=>m.SourceType==IngredientPickup.MeatSourceType.Customer);
        var zombieMeat = Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab"),new Vector3(30,30),Quaternion.identity);
        Color zombieColor = zombieMeat.GetComponent<SpriteRenderer>().color;
        Check(customerMeat.GetComponent<SpriteRenderer>().color != zombieColor && zombieMeat.SourceType==IngredientPickup.MeatSourceType.Zombie, "Source types and raw colors distinguish Meat");
        Check(customerMeat.AttachTo(Field<Transform>(player.GetComponent<PlayerCarry>(),"carryPoint")), "Customer Meat uses existing one-item carry attachment");
        customerMeat.Cook();
        Check(customerMeat.IsCooked && zombieMeat.GetComponent<SpriteRenderer>().color==zombieColor, "Cooking Customer Meat does not recolor Zombie Meat");
        Object.Destroy(customerMeat.gameObject); Object.Destroy(zombieMeat.gameObject); yield return null;
        Set("customerMaxHP",50f); Set("sanPenaltyForKillingCustomer",10f); player.SetCurrentSAN(100);

        var purified = Object.Instantiate(customerPrefab,player.transform.position+Vector3.right,Quaternion.identity);
        purified.GetComponent<EnemySimpleAI>().enabled=false;
        purified.TakeDamage(1,player); // Purification must cancel the white flash permanently.
        meatBefore=Object.FindObjectsByType<IngredientPickup>().Length;
        purified.Purify(); purified.Purify(); purified.TakeDamage(1000,player);
        Check(purified.IsPurified && !purified.IsDead && player.SAN==100, "Purification and death are mutually exclusive, no SAN loss");
        Check(Object.FindObjectsByType<IngredientPickup>().Length==meatBefore, "Purification drops no Meat");
        Check(purified.GetComponent<SpriteRenderer>().color==Color.green && !purified.GetComponent<Rigidbody2D>().simulated && purified.GetComponentsInChildren<Collider2D>().All(c=>!c.enabled), "Purification feedback disables movement/collisions");
        Set("customerRemovalDelay",.01f);
        yield return new WaitForSeconds(.15f);
        Check(purified!=null && purified.GetComponent<SpriteRenderer>().color==Color.green, "Existing purification timer is not restarted and flash cannot override green");
        yield return new WaitForSeconds(.9f);
        Check(purified==null, "Purified Customer removed after captured delay");
        Set("customerRemovalDelay",1f);

        player.transform.position=new Vector3(20,20); Physics2D.SyncTransforms();
        var meleeZombie=Object.Instantiate(enemyPrefab,new Vector3(21,20),Quaternion.identity); meleeZombie.enabled=false;
        Set("playerAttackDamage",50f); TickField(combat,"lastAttackTime",float.NegativeInfinity); combat.TryAttack();
        Check(meleeZombie.GetComponent<EnemyHealth>().IsDead && player.SAN==100, "Melee Zombie kill leaves SAN unchanged");
        Check(Object.FindObjectsByType<IngredientPickup>().Count(m=>m.SourceType==IngredientPickup.MeatSourceType.Zombie)==1, "Melee Zombie drops normal Meat once");
        meleeZombie.GetComponent<EnemyHealth>().KillByFoodProjectile();
        Check(Object.FindObjectsByType<IngredientPickup>().Length==1, "Zombie cannot drop twice");
        foreach(var meat in Object.FindObjectsByType<IngredientPickup>()) Object.Destroy(meat.gameObject);
        yield return null;
        Set("playerAttackDamage",10f);

        // Actual locked-target flight and AoE regression, including live gravity/radius edits.
        var aoeZombie=Object.Instantiate(enemyPrefab,new Vector3(31,20),Quaternion.identity); aoeZombie.enabled=false;
        var aoeCustomer=Object.Instantiate(customerPrefab,new Vector3(30,21),Quaternion.identity); aoeCustomer.GetComponent<EnemySimpleAI>().enabled=false;
        aoeZombie.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll; aoeCustomer.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;
        Physics2D.SyncTransforms();
        var shot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<FoodProjectile>("Assets/Prototype/Prefabs/FoodProjectile.prefab"),new Vector3(20,20,-.2f),Quaternion.identity);
        Vector3 target=new Vector3(30,20,-shot.GroundClearance);
        shot.Launch(FoodBallistics.LaunchVelocity(shot.transform.position,target,shot.Gravity,.2f),target,.2f);
        Set("projectileGravity",50f); Set("explosionRadius",1.5f);
        yield return new WaitForSeconds(.3f);
        Check(shot==null, "Targeted projectile lands and resolves after its locked flight duration");
        var splash=GameObject.Find("FoodSplash").GetComponent<LineRenderer>();
        Vector3 center=Vector3.zero; for(int i=0;i<splash.positionCount;i++) center+=splash.GetPosition(i); center/=splash.positionCount;
        Check(Vector2.Distance(center,target)<.001f, "Projectile splash matches locked target after live gravity edit");
        Check(Mathf.Abs(Vector2.Distance(center,splash.GetPosition(0))-1.5f)<.001f, "Live explosion radius used at impact");
        Check(aoeZombie==null && aoeCustomer.IsPurified && !aoeCustomer.IsDead && player.SAN==100, "Food AoE kills Zombie and purifies Customer without SAN penalty");
        Check(Object.FindObjectsByType<IngredientPickup>().Length==1 && Object.FindFirstObjectByType<IngredientPickup>().SourceType==IngredientPickup.MeatSourceType.Zombie, "Food AoE produces only Zombie Meat");
        Object.Destroy(aoeCustomer.gameObject);
        foreach(var meat in Object.FindObjectsByType<IngredientPickup>()) Object.Destroy(meat.gameObject);
        Set("projectileGravity",9.81f); Set("explosionRadius",2f);
        yield return null;

        // Existing complete carry -> cook -> retrieve -> load pipeline for new source.
        player.transform.position = new Vector3(0,-.8f); Physics2D.SyncTransforms();
        playerBody.simulated=true;
        yield return new WaitForFixedUpdate();
        var carry=player.GetComponent<PlayerCarry>();
        var station=Object.FindFirstObjectByType<CookingStation>();
        var cannon=Object.FindFirstObjectByType<FoodCannon>();
        var ingredient=Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>(CombatStatusPrototypeSetup.CustomerMeatPath),player.transform.position,Quaternion.identity);
        Physics2D.SyncTransforms();
        carry.TryPickup();
        Check(Field<IngredientPickup>(carry,"currentHeldItem")==ingredient && ingredient.IsCarried, "Customer Meat picked up via existing PlayerCarry");
        carry.TryInteract(); Check(station.CurrentMeat==ingredient && carry.CurrentHeldItem==null, "Customer Meat placed through existing CookingStation");
        Check(station.TryCook(player.transform), "Customer Meat cooked through existing CookingStation");
        carry.TryInteract(); Check(carry.CurrentHeldItem==ingredient && station.CurrentMeat==null, "Cooked Customer Meat retrieved through existing CookingStation");
        player.transform.position=new Vector3(0,1); Physics2D.SyncTransforms(); playerBody.simulated=true;
        yield return new WaitForFixedUpdate();
        carry.TryInteract(); Check(cannon.IsLoaded, "Cooked Customer Meat loads the existing cannon");
        Check(cannon.IsLoaded && Field<IngredientPickup>(carry,"currentHeldItem")==null, "Loading consumes only carried Meat and retains ammunition");
        // UI layout follows the same top-left anchors at three viewport dimensions.
        var canvas = hud.GetComponentInParent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        var canvasRect=(RectTransform)canvas.transform;
        foreach(var size in new[] {new Vector2(1280,720),new Vector2(1920,1080),new Vector2(777,510)})
        {
            canvasRect.sizeDelta=size;
            Canvas.ForceUpdateCanvases();
            var panelRect=(RectTransform)hud.transform;
            var corners=new Vector3[4]; panelRect.GetLocalCorners(corners);
            var topLeft=canvasRect.InverseTransformPoint(panelRect.TransformPoint(corners[1]));
            Check(Vector2.Distance(topLeft,new Vector2(canvasRect.rect.xMin+20,canvasRect.rect.yMax-20))<.01f,
                "HUD stays 20 units inside top-left at "+size);
        }
        canvas.renderMode=RenderMode.ScreenSpaceOverlay;

        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        Application.runInBackground=true;
        var keyboard=InputSystem.AddDevice<Keyboard>();
        var mouse=InputSystem.AddDevice<Mouse>();
        Vector2 mousePoint=Camera.main.WorldToScreenPoint(new Vector3(8,1,0));
        InputSystem.QueueStateEvent(mouse,new MouseState {position=mousePoint});
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        InputSystem.Update(); Tick(player,"Update");
        InputSystem.QueueStateEvent(mouse,new MouseState {position=mousePoint,buttons=2});
        InputSystem.Update(); Tick(player,"Update"); cannon.HandleAimInput(player,true);
        Check(cannon.IsAiming && cannon.IsLoaded && player.IsCannonAiming, "Configured mouse input enters aim with Customer Meat ammunition");
        int shotsBefore=Object.FindObjectsByType<FoodProjectile>().Length;
        TickField(config,"aim",new InputAction("Aim",InputActionType.Button,"<Keyboard>/q")); config.Validate();
        Tick(player,"Update");
        Check(!cannon.IsAiming && !player.IsCannonAiming && cannon.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length==shotsBefore,
            "Rebinding while aiming cancels aim, retains ammo and does not fire");
        InputSystem.QueueStateEvent(mouse,new MouseState {position=mousePoint});
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); Tick(player,"Update");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q)); InputSystem.Update(); Tick(player,"Update"); cannon.HandleAimInput(player,true);
        Check(cannon.IsAiming, "New aim binding becomes active after release");
        player.enabled=false;
        Check(!cannon.IsAiming && cannon.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length==shotsBefore, "Disabling Player cancels aim without firing");
        player.enabled=true;
        InputSystem.Update(); Tick(player,"Update"); cannon.HandleAimInput(player,true);
        Check(!cannon.IsAiming, "Reenable with held input does not trigger phantom aim");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); Tick(player,"Update");
        InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Q)); InputSystem.Update(); Tick(player,"Update"); cannon.HandleAimInput(player,true);
        Check(cannon.IsAiming, "Aim works again after enable and release");
        Vector3 cannonTarget=cannon.Target;
        InputSystem.QueueStateEvent(keyboard,new KeyboardState()); InputSystem.Update(); Tick(player,"Update"); cannon.HandleAimInput(player,true);
        Check(!cannon.IsAiming && !cannon.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length==shotsBefore+1, "Release fires exactly one projectile after rebind");
        var firedShot=Object.FindFirstObjectByType<FoodProjectile>();
        Check(Vector3.Distance(Field<Vector3>(firedShot,"landingTarget"),cannonTarget)<.001f, "Cannon launch retains the preview target");
        Object.Destroy(firedShot.gameObject); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
        Check(config.AttackRange==3 && config.AttackCooldown==.1f && config.MonsterHP==50 && config.CustomerMaxHP==50 && config.PlayerMaxHP==100, "Baseline shared tuning restored for scene export");
    }
    private static void CaptureLog(string message,string trace,LogType type) { if (!message.StartsWith("CHECK")) logs.Add(message); }
    private static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,Private).GetValue(target);
    private static void TickField(object target,string name,object value) => target.GetType().GetField(name,Private).SetValue(target,value);
    private static void Tick(object target,string name) => target.GetType().GetMethod(name,Private).Invoke(target,null);
    private static void Set(string name,float value) { TickField(config,name,value); config.Validate(); }
    private static void Check(bool condition,string message)
    { if(!condition) throw new Exception("CHECK FAILED: "+message); checks++; logs.Add("CHECK "+checks+": "+message); Debug.Log("CHECK "+checks+": "+message); }
    private static void Restore()
    {
        if(config!=null && !string.IsNullOrEmpty(baseline))
        { EditorJsonUtility.FromJsonOverwrite(baseline,config); config.Validate(); EditorUtility.SetDirty(config); AssetDatabase.SaveAssetIfDirty(config); }
    }
    private static void Fail(Exception error)
    {
        Restore(); Debug.LogException(error);
        File.WriteAllText("COMBAT_SAN_RESULTS.txt","FAIL after "+checks+" checks\n"+error+"\n"+string.Join("\n",logs));
        EditorApplication.Exit(1);
    }
}




