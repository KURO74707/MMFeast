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
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CannonLayoutValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new();
    static CannonLayoutValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            GlobalDataPrototypeSetup.Upgrade();
            foreach (string path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path);
                GlobalDataPrototypeSetup.ApplyToScene(scene); GlobalDataPrototypeSetup.ApplyToScene(scene);
                EditorSceneManager.SaveScene(scene);
                var cannon = Object.FindAnyObjectByType<FoodCannon>(); var tool = cannon.GetComponent<PlaceableKitchenTool>();
                var grid = Object.FindAnyObjectByType<TruckGridLayout>();
                Check(tool != null && tool.Grid == grid && grid.Tools.Count == 2, path + " cannon and stove share existing grid");
                Check(tool.Footprint == new Vector2Int(2,2) && grid.ValidateLayout(out _), path + " valid cannon 2x2 footprint");
                Check(cannon.transform.parent == grid.transform && Object.FindObjectsByType<FoodCannon>().Length == 1, path + " migration retains single existing cannon");
                var reserved = Field<List<RectInt>>(grid,"reservedAreas");
                Check(!reserved.Contains(new RectInt(2,2,2,2)), path + " legacy fixed cannon reservation removed");
                reserved.Add(new RectInt(5,0,1,1)); KitchenLayoutPrototypeSetup.BindComponents(grid.gameObject);
                Check(reserved.Contains(new RectInt(5,0,1,1)), path + " repeated setup retains designer reservations"); reserved.RemoveAt(reserved.Count-1);
            }
            SessionState.SetString("CannonLayoutChecks", string.Join("\n",results)); SessionState.SetBool("CannonLayoutPending",true);
            EditorApplication.EnterPlaymode();
        }
        catch(Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CannonLayoutPending",false))return;
        SessionState.SetBool("CannonLayoutPending",false); results.Clear(); results.AddRange(SessionState.GetString("CannonLayoutChecks","").Split('\n'));
        new GameObject("Cannon layout tests").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator tests)
    {
        while(true)
        {
            bool next; object current;
            try { next=tests.MoveNext();current=next?tests.Current:null; } catch(Exception e){Fail(e);yield break;}
            if(!next)break;yield return current;
        }
        File.WriteAllText("CANNON_LAYOUT_RESULTS.txt","PASS: "+results.Count+" checks\n"+string.Join("\n",results));EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);
        yield return null;
        var p=Object.FindAnyObjectByType<PlayerController>();var layout=p.GetComponent<PlayerKitchenLayout>();var carry=p.GetComponent<PlayerCarry>();
        var cannon=Object.FindAnyObjectByType<FoodCannon>();var tool=cannon.GetComponent<PlaceableKitchenTool>();var grid=tool.Grid;
        var station=Object.FindAnyObjectByType<CookingStation>();var stationTool=station.GetComponent<PlaceableKitchenTool>();
        var anchor=Field<Transform>(layout,"toolCarryPoint");var muzzle=Field<Transform>(cannon,"muzzlePoint");var originalCell=tool.Cell;
        InputSystem.settings.updateMode=InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        var keyboard=InputSystem.AddDevice<Keyboard>();var mouse=InputSystem.AddDevice<Mouse>();
        InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.Update();Tick(p,"Update");
        Move(p,cannon.transform.position+Vector3.down*.9f);yield return new WaitForFixedUpdate();yield return null;
        Check(grid.Interior.IsPlayerInside(p.transform),"Player inside truck can reach original cannon");
        var meatPrefab=AssetDatabase.LoadAssetAtPath<IngredientPickup>("Assets/Prototype/Prefabs/Meat.prefab");
        var food=Object.Instantiate(meatPrefab);food.AttachTo(carry.CarryPoint);food.Cook();
        Check(cannon.TryLoad(food,p.transform) && cannon.IsLoaded,"Existing cooked ingredient loads cannon before relocation");yield return null;
        Vector2 screen=Camera.main.WorldToScreenPoint(cannon.transform.position+Vector3.right*6);
        Mouse(p,mouse,2,screen);Check(cannon.HandleAimInput(p,true) && cannon.IsAiming,"Loaded cannon can aim before relocation");
        var beforePosition=cannon.transform.position;
        Check(tool.BeginCarry(anchor) && !cannon.IsAiming && !p.IsCannonAiming && cannon.IsLoaded,"Direct cannon lift cancels aim and retains ammunition");
        Check(!cannon.CanInteract(p.transform) && !cannon.HandleAimInput(p,true),"Carried cannon cannot interact or aim");
        Tick(cannon,"Fire");Check(Object.FindObjectsByType<FoodProjectile>().Length==0 && cannon.IsLoaded,"Carried cannon cannot fire even if a stale fire request arrives");
        tool.CancelCarry();Check(Vector3.Distance(cannon.transform.position,beforePosition)<.001f && tool.Cell==originalCell,"Cancel restores original cannon cell and transform");
        var raw=Object.Instantiate(meatPrefab);raw.AttachTo(carry.CarryPoint);
        Check(station.TryPlace(raw,p.transform),"Fixture puts real ingredient on Cooking Station");
        tool.BeginCarry(anchor);Check(!grid.TryPlace(tool,stationTool.Cell,out _) && cannon.IsLoaded,"Loaded cannon cannot swap with a Cooking Station containing food");tool.CancelCarry();
        station.TryTake(carry.CarryPoint,p.transform);Object.Destroy(raw.gameObject);
        Mouse(p,mouse,0,screen);Check(layout.TryEnterMode() && layout.TryInteract() && layout.HeldTool==tool,"Kitchen mode selects and lifts nearby cannon using existing interaction");
        var movedCell=originalCell+Vector2Int.left*2;Set(p,"<Facing>k__BackingField",Vector2.left);
        var settings=Object.FindAnyObjectByType<GlobalData>().Settings;
        Move(p,grid.CellCenter(movedCell,tool.Footprint)+Vector3.right*settings.KitchenPlacementDistance);
        Check(layout.TryInteract() && tool.Cell==movedCell && !tool.IsCarried,"Cannon places exactly two columns left through normal layout targeting");
        Check(cannon.IsLoaded && Field<Transform>(cannon,"muzzlePoint")==muzzle && Vector3.Distance(muzzle.position,cannon.transform.position)>.5f,"Relocation preserves loaded state and actual muzzle child");
        Check(grid.EvaluatePlacement(stationTool,originalCell,out _,out var obstruction,out _) && obstruction==null,"Old cannon cells become available to another tool");
        tool.BeginCarry(anchor);Check(!grid.TryPlace(tool,new Vector2Int(-1,0),out _) && tool.IsCarried && cannon.IsLoaded,"Invalid cannon placement retains held tool and ammunition");
        Check(grid.TryPlace(tool,stationTool.Cell,out _) && tool.Cell==new Vector2Int(2,0) && stationTool.Cell==movedCell,"Cannon 2x2 and stove 2x1 swap when both fit");
        layout.ExitMode();
        Move(p,cannon.transform.position+Vector3.down*.9f);Mouse(p,mouse,0,screen);
        var live=Object.Instantiate(settings);Set(live,"flightTime",.2f);Set(cannon,"settings",live);
        screen=Camera.main.WorldToScreenPoint(cannon.transform.position+Vector3.right*6);
        Mouse(p,mouse,2,screen);Check(cannon.HandleAimInput(p,true) && cannon.IsAiming,"Moved cannon can aim from its new location");
        Vector3 target=cannon.Target;Check(Mathf.Abs(Vector2.Distance(cannon.transform.position,target)-6)<.01f,"Moved cannon range uses new base position");
        var launchOrigin=muzzle.position;
        Mouse(p,mouse,0,screen);cannon.HandleAimInput(p,true);var shot=Object.FindAnyObjectByType<FoodProjectile>();
        Check(shot!=null && !cannon.IsLoaded && Vector3.Distance(shot.transform.position,launchOrigin)<.001f,"Moved cannon fires exactly from new muzzle and consumes retained ammunition once");
        Check(Vector3.Distance(Field<Vector3>(shot,"landingTarget"),target)<.001f,"Moved cannon projectile retains selected preview target");
        yield return new WaitForSeconds(.3f);
        var splash=GameObject.Find("FoodSplash").GetComponent<LineRenderer>();Vector3 center=Vector3.zero;
        for(int i=0;i<splash.positionCount;i++)center+=splash.GetPosition(i);center/=splash.positionCount;
        Check(shot==null && Vector2.Distance(center,target)<.001f,"Actual ballistic impact from moved cannon matches selected target");
        // Leave a clear in-engine visual of the two tools at their swapped grid positions.
        Set(cannon,"isLoaded",true);layout.TryEnterMode();Tick(layout,"Update");
        var camera=Camera.main;camera.GetComponent<SimpleCameraShake>().enabled=false;camera.orthographicSize=5f;
        var canvas=Object.FindAnyObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;
        yield return null;
        var render=new RenderTexture(1280,720,24);camera.targetTexture=render;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;
        var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("CANNON_LAYOUT_PREVIEW.png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);layout.ExitMode();InputSystem.RemoveDevice(mouse);InputSystem.RemoveDevice(keyboard);
    }
    static void Mouse(PlayerController p,Mouse mouse,ushort buttons,Vector2 screen){InputSystem.QueueStateEvent(mouse,new MouseState{buttons=buttons,position=screen});InputSystem.Update();Tick(p,"Update");}
    static void Move(PlayerController p,Vector3 pos){p.transform.position=pos;p.GetComponent<Rigidbody2D>().position=pos;p.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;Physics2D.SyncTransforms();}
    static object Tick(object o,string name)=>o.GetType().GetMethod(name,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);results.Add("PASS "+name);}
    static void Fail(Exception e){Debug.LogException(e);File.WriteAllText("CANNON_LAYOUT_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}
