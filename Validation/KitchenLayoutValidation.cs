using System;
using System.Collections;
using System.Collections.Generic;
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
public static class KitchenLayoutValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new();
    static PlayerController player; static PlayerKitchenLayout layout; static PlayerCarry carry;
    static TruckGridLayout grid; static GlobalGameSettings config; static Keyboard keyboard;
    static KitchenLayoutValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            foreach (var path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GlobalDataPrototypeSetup.ApplyToScene(scene); GlobalDataPrototypeSetup.ApplyToScene(scene);
                EditorSceneManager.SaveScene(scene);
                var sceneGrid = Object.FindAnyObjectByType<TruckGridLayout>();
                Check(Object.FindObjectsByType<TruckGridLayout>().Length == 1 && Object.FindObjectsByType<PlayerKitchenLayout>().Length == 1, path + " one grid and one layout controller");
                Check(sceneGrid.ValidateLayout(out _) && sceneGrid.Tools.Count == 2, path + " valid Cooking Station and cannon grid registration");
                var tool = Object.FindAnyObjectByType<CookingStation>().GetComponent<PlaceableKitchenTool>();
                Check(tool.transform.parent == sceneGrid.transform && tool.Footprint == new Vector2Int(2,1), path + " existing tool parent and 2x1 footprint");
                Check(Field<GlobalGameSettings>(Object.FindAnyObjectByType<PlayerKitchenLayout>(), "settings") == Object.FindAnyObjectByType<GlobalData>().Settings, path + " same shared settings");
                Check(Object.FindAnyObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>().transform.Cast<Transform>().Count(t => t.name == "KitchenLayoutStatus") == 1, path + " idempotent status panel migration");
            }
            AssetDatabase.SaveAssets();
            SessionState.SetString("KitchenChecks", string.Join("\n", results));
            SessionState.SetBool("KitchenPending", true); EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("KitchenPending", false)) return;
        SessionState.SetBool("KitchenPending", false);
        results.Clear(); results.AddRange(SessionState.GetString("KitchenChecks", "").Split('\n'));
        new GameObject("Kitchen tests").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while (true)
        {
            bool next; object current;
            try { next = routine.MoveNext(); current = next ? routine.Current : null; }
            catch (Exception e) { Fail(e); yield break; }
            if (!next) break; yield return current;
        }
        File.WriteAllText("KITCHEN_LAYOUT_RESULTS.txt", "PASS: " + results.Count + " checks\n" + string.Join("\n", results));
        EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
        foreach (var ai in Object.FindObjectsByType<EnemySimpleAI>()) Object.Destroy(ai.gameObject);
        yield return null;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        keyboard = InputSystem.AddDevice<Keyboard>();
        player = Object.FindAnyObjectByType<PlayerController>(); layout = player.GetComponent<PlayerKitchenLayout>(); carry = player.GetComponent<PlayerCarry>(); grid = Object.FindAnyObjectByType<TruckGridLayout>();
        config = Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);
        player.Configure(config); Set(layout, "settings", config); Set(carry, "settings", config); Frame(); Frame();
        var tool = Object.FindAnyObjectByType<CookingStation>().GetComponent<PlaceableKitchenTool>(); var station = tool.GetComponent<CookingStation>();
        var originalCell = tool.Cell; var stationIdentity = station; var cookingPoint = Field<Transform>(station, "cookingPoint");
        Move(new Vector3(1f, -.2f)); yield return new WaitForFixedUpdate(); yield return null;
        Check(grid.Interior.IsPlayerInside(player.transform), "Real physics detects player entry into truck");
        Frame(Key.E); Check(!player.InteractPressed && !layout.IsOrganizing, "E press alone does not execute short action");
        Frame(); Check(player.InteractPressed && !layout.IsOrganizing, "Short E interaction occurs on release");
        Frame(); Check(!player.InteractPressed, "Short interaction pulse lasts one update");
        Hold(); Check(layout.IsOrganizing && !player.InteractPressed, "Long E enters layout without triggering short interaction");
        Check(Time.timeScale == 1 && grid.transform.Find("KitchenGridPreview").gameObject.activeSelf, "Layout shows grid and game remains running");
        Frame(Key.E); Check(layout.HeldTool == null && layout.IsOrganizing, "Holding entry key does not pick up a tool or toggle repeatedly");
        Frame(); Check(!player.InteractPressed, "Releasing long press does not act as a tap");
        player.SetCurrentStamina(100);
        Check(!(bool)Tick(player, "TryStartDash") && player.CurrentStamina == 100, "Dash is blocked without stamina spending in layout");
        var combat = player.GetComponent<PlayerCombat>(); float lastAttack = Field<float>(combat, "lastAttackTime"); combat.TryAttack();
        Check(Field<float>(combat, "lastAttackTime") == lastAttack, "Melee attack blocked in layout");
        var cannon = Object.FindAnyObjectByType<FoodCannon>(); Set(cannon, "isLoaded", true);
        Check(!cannon.HandleAimInput(player, true) && cannon.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length == 0, "Cannon input blocked without consuming ammunition or firing");
        Frame(Key.D); Tick(player, "FixedUpdate"); Check(player.GetComponent<Rigidbody2D>().linearVelocity.x > 0, "Regular movement remains available in layout"); Frame();
        Move(tool.transform.position + Vector3.up * 1.1f); Tap();
        Check(layout.HeldTool == tool && tool.IsCarried && !tool.GetComponent<Collider2D>().enabled, "Tap E lifts existing Cooking Station and temporarily disables collider");
        Check(tool.Cell == originalCell && !station.CanUse(player.transform), "Original grid reservation stays occupied; carried station cannot be used");
        carry.TryPickup(); Check(carry.CurrentHeldItem == null, "Ordinary ingredient pickup blocked while organizing");
        var movedCell = originalCell + Vector2Int.left * 2; AimAt(movedCell, tool); Tap();
        Check(layout.HeldTool == null && tool.Cell == movedCell && Vector3.Distance(tool.transform.position, grid.CellCenter(movedCell, tool.Footprint)) < .001f, "Tap places stove exactly two columns left");
        Check(tool.GetComponent<CookingStation>() == stationIdentity && Field<Transform>(station, "cookingPoint") == cookingPoint && tool.GetComponent<Collider2D>().enabled, "Moving preserves original component, children and collider state");
        Move(tool.transform.position + Vector3.up * 1.1f); Tap(); var beforeInvalid = tool.Cell;
        Check(!grid.TryPlace(tool, new Vector2Int(-1, 0), out _) && tool.IsCarried && tool.Cell == beforeInvalid, "Out-of-bounds placement rejected without dropping tool");
        var reservedAreas = Field<List<RectInt>>(grid,"reservedAreas"); reservedAreas.Add(new RectInt(5,3,1,1));
        Check(!grid.TryPlace(tool, new Vector2Int(4, 3), out _), "Designer reserved cells reject placement"); reservedAreas.RemoveAt(reservedAreas.Count-1);
        Hold(); Check(!layout.IsOrganizing && !tool.IsCarried && tool.Cell == movedCell && tool.transform.parent == grid.transform, "Long E exits and restores carried tool to its original reserved cell");
        Frame(Key.E); Check(!layout.IsOrganizing && !player.InteractPressed, "Exit key hold cannot immediately re-enter or trigger ordinary interaction"); Frame();
        var meat = Object.Instantiate(AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab"), player.transform.position, Quaternion.identity);
        Physics2D.SyncTransforms(); carry.TryPickup();
        Check(carry.CurrentHeldItem == meat && !layout.TryEnterMode(), "Holding an ingredient prevents entering layout mode");
        Move(tool.transform.position + Vector3.right * 1.1f); carry.TryInteract();
        Check(station.CurrentMeat == meat && carry.CurrentHeldItem == null, "Existing ingredient placement still works after stove movement");
        Check(layout.TryEnterMode() && !layout.TryInteract() && layout.HeldTool == null, "Cooking Station containing food cannot be lifted"); layout.ExitMode(); Frame();
        Check(station.TryCook(player.transform) && meat.IsCooked, "Original cooking works at relocated station");
        carry.TryInteract(); Check(carry.CurrentHeldItem == meat && station.CurrentMeat == null, "Cooked food can still be retrieved after relocation");
        Object.DestroyImmediate(meat.gameObject);
        var other = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prototype/Prefabs/CookingStation.prefab"), grid.transform).GetComponent<PlaceableKitchenTool>();
        other.Configure(grid, new Vector2Int(3,1)); other.CommitCell(new Vector2Int(3,1)); other.GetComponent<CookingStation>().SetTruckInterior(grid.Interior);
        Check(tool.BeginCarry(Field<Transform>(layout, "toolCarryPoint")) && grid.TryPlace(tool, other.Cell, out _), "Two empty tools can swap atomically");
        Check(tool.Cell == new Vector2Int(3,1) && other.Cell == movedCell, "Swap commits both original cell assignments");
        Set(other, "footprint", Vector2Int.one); tool.BeginCarry(Field<Transform>(layout, "toolCarryPoint"));
        Check(grid.TryPlace(tool, other.Cell, out _) && tool.Cell == movedCell && other.Cell == new Vector2Int(3,1), "Different-size tools swap when both footprints fit");
        tool.CommitCell(new Vector2Int(3,1)); Set(other, "footprint", new Vector2Int(1,2)); other.CommitCell(new Vector2Int(0,1)); tool.BeginCarry(Field<Transform>(layout, "toolCarryPoint"));
        Check(!grid.TryPlace(tool, other.Cell, out _) && tool.Cell == new Vector2Int(3,1) && other.Cell == new Vector2Int(0,1) && tool.IsCarried, "Invalid reverse footprint rejects swap with both cells unchanged");
        tool.CancelCarry(); Set(other, "footprint", Vector2Int.one); Set(other, "movable", false); tool.BeginCarry(Field<Transform>(layout, "toolCarryPoint"));
        Check(!grid.TryPlace(tool, other.Cell, out _), "Fixed tool cannot participate in a swap"); tool.CancelCarry(); Set(other, "movable", true);
        var third = Object.Instantiate(other.gameObject, grid.transform).GetComponent<PlaceableKitchenTool>(); third.Configure(grid, new Vector2Int(1,1)); third.CommitCell(new Vector2Int(1,1)); tool.BeginCarry(Field<Transform>(layout, "toolCarryPoint"));
        Check(!grid.TryPlace(tool, new Vector2Int(0,1), out _), "Placement overlapping multiple tools is rejected"); tool.CancelCarry();
        Object.DestroyImmediate(other.gameObject); Object.DestroyImmediate(third.gameObject);
        Move(tool.transform.position + Vector3.down); Frame(); Check(layout.TryEnterMode() && layout.TryInteract(), "Can lift tool again after swaps");
        Vector3 reservedPosition = grid.CellCenter(tool.Cell, tool.Footprint); config.Interact.ChangeBinding(0).WithPath("<Keyboard>/f"); config.Validate(); Frame();
        Check(!layout.IsOrganizing && !tool.IsCarried && Vector3.Distance(tool.transform.position,reservedPosition) < .001f, "Runtime rebinding exits layout and restores held tool");
        Frame(); Frame(Key.F); Set(player, "interactStartedAt", Time.time - .6f); Frame(Key.F); Check(layout.IsOrganizing, "Long press uses rebound Interact key"); Frame();
        Move(tool.transform.position + Vector3.down); Check(layout.TryInteract(), "Lift using same interaction after rebind"); player.enabled = false; player.enabled = true;
        Check(!layout.IsOrganizing && !tool.IsCarried, "Disabling and re-enabling player safely restores tool"); Frame();
        Move(tool.transform.position + Vector3.down); Check(layout.TryEnterMode() && layout.TryInteract(), "Layout remains usable after enable cycle");
        Frame(Key.F); Move(new Vector3(10,10)); Tick(layout,"Update"); Check(!layout.IsOrganizing && !tool.IsCarried, "Leaving truck exits mode and restores carried tool");
        Frame(); Check(!player.InteractPressed, "Automatic exit during a held short press does not leak ordinary interaction on release");
        Check(!layout.TryEnterMode(), "Cannot enter layout outside truck");
        Move(tool.transform.position + Vector3.down); SeedInterior(); Check(layout.TryEnterMode() && layout.TryInteract(), "Can resume layout after returning to truck");
        player.TakeDamage(1000); Tick(layout,"Update"); Check(!layout.IsOrganizing && !tool.IsCarried, "Player death cancels layout without losing tool"); player.SetCurrentHP(100);
        Move(new Vector3(0,-.2f)); SeedInterior(); Frame();
        Set(cannon,"isLoaded",true); var mouse = InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(mouse,new MouseState {position=Camera.main.WorldToScreenPoint(new Vector3(8,1)),buttons=2}); InputSystem.Update(); Tick(player,"Update");
        Check(cannon.HandleAimInput(player,true) && cannon.IsAiming, "Original loaded cannon still enters aiming outside layout");
        Check(layout.TryEnterMode() && !cannon.IsAiming && cannon.IsLoaded && Object.FindObjectsByType<FoodProjectile>().Length == 0, "Entering layout while aiming cancels aim, keeps load and never fires");
        InputSystem.RemoveDevice(mouse); layout.ExitMode(); Frame();
        var c = config.KitchenOrganizeHoldDuration; Set(config,"kitchenOrganizeHoldDuration",.1f); config.Validate();
        Frame(Key.F); Set(player,"interactStartedAt",Time.time-.15f); Frame();
        Check(layout.IsOrganizing && !player.InteractPressed, "Hold threshold is live configurable and a slow-frame release never triggers a short tap"); layout.ExitMode(); Frame();
        Set(config,"kitchenOrganizeHoldDuration",c); player.SetCurrentHP(100);
        config.Interact.ChangeBinding(0).WithPath("<Keyboard>/e"); config.Validate(); Frame(); Frame();
        yield return new WaitForSeconds(.2f);
        // Actual Unity rendering of the organizing mode, with a valid placement preview.
        Move(tool.transform.position + Vector3.down); SeedInterior(); layout.TryEnterMode(); layout.TryInteract(); AimAt(new Vector2Int(0,1),tool); Tick(layout,"Update");
        var camera=Camera.main; camera.GetComponent<SimpleCameraShake>().enabled=false; camera.orthographicSize=5f;
        var canvas=Object.FindAnyObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1f;
        yield return null;
        var render = new RenderTexture(1280,720,24);camera.targetTexture=render;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;
        var pixels = new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("KITCHEN_LAYOUT_PREVIEW.png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);
        layout.ExitMode();InputSystem.RemoveDevice(keyboard);
    }
    static void Frame(params Key[] keys) { InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));InputSystem.Update();Tick(player,"Update");Tick(layout,"Update"); }
    static void Tap() { Frame(Key.E);Frame(); }
    static void Hold() { Frame(Key.E);Set(player,"interactStartedAt",Time.time-.6f);Frame(Key.E); }
    static void Move(Vector3 position) { player.transform.position=position;player.GetComponent<Rigidbody2D>().position=position;player.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms(); }
    static void SeedInterior() => typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
    static void AimAt(Vector2Int cell,PlaceableKitchenTool tool) { Set(player,"<Facing>k__BackingField",Vector2.left);Move(grid.CellCenter(cell,tool.Footprint)+Vector3.right*config.KitchenPlacementDistance); }
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool ok,string name) { if(!ok)throw new Exception(name);results.Add("PASS "+name); }
    static void Fail(Exception e) { Debug.LogException(e);File.WriteAllText("KITCHEN_LAYOUT_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1); }
}
