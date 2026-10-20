using System;
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
public static class DashValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new();
    static DashValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            foreach (string path in new[] { "Assets/Prototype/CombatCollection.unity", "Assets/Prototype/CookingCollection.unity" })
            {
                EditorSceneManager.OpenScene(path);
                var player = Object.FindFirstObjectByType<PlayerController>();
                var config = Object.FindFirstObjectByType<GlobalData>().Settings;
                if (Get<GlobalGameSettings>(player, "settings") != config || config.Dash.bindings[0].path != "<Keyboard>/space")
                    throw new Exception(path + " shared settings / Space binding");
            }
            SessionState.SetBool("DashPending", true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("DashPending", false)) return;
        SessionState.SetBool("DashPending", false);
        try { Tests(); File.WriteAllText("DASH_RESULTS.txt", "PASS: " + results.Count + " checks\n" + string.Join("\n", results)); EditorApplication.Exit(0); }
        catch (Exception e) { Fail(e); }
    }
    static void Tests()
    {
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        var p = Object.FindFirstObjectByType<PlayerController>();
        var cfg = Object.Instantiate(Object.FindFirstObjectByType<GlobalData>().Settings);
        p.Configure(cfg); p.enabled = false;
        var body = p.GetComponent<Rigidbody2D>();
        Physics2D.simulationMode = SimulationMode2D.Script;
        body.position = new Vector2(30, 30); body.linearVelocity = Vector2.zero;
        Tick(p, "RebuildInputs"); Tick(p, "EnableInputs");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); Tick(p, "Update");
        p.SetCurrentStamina(100);
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); InputSystem.Update(); Tick(p, "Update");
        Check(Get<bool>(p, "dashRequested"), "Space queues dash through real InputAction");
        Tick(p, "FixedUpdate");
        Check(p.IsDashing && p.CurrentStamina == 80, "Physics step starts dash and deducts cost once");
        Vector2 start = body.position;
        Physics2D.Simulate(Time.fixedDeltaTime);
        for (int i = 0; i < 7; i++) { Tick(p, "FixedUpdate"); Physics2D.Simulate(Time.fixedDeltaTime); }
        Check(Mathf.Abs(Vector2.Distance(start, body.position) - 3) < .005f, "Default dash travels exactly 3 units including fractional last step");
        Tick(p, "FixedUpdate");
        Check(!p.IsDashing && body.linearVelocity == Vector2.zero, "Dash stops and returns to normal movement");
        Check(body.collisionDetectionMode == CollisionDetectionMode2D.Discrete, "Original collision detection restored");
        InputSystem.Update(); Tick(p, "Update"); Tick(p, "FixedUpdate");
        Check(!p.IsDashing && p.CurrentStamina == 80, "Held Space does not repeatedly dash");
        p.SetCurrentStamina(19); Check(!Start(p) && p.CurrentStamina == 19, "Insufficient stamina rejects dash without spending");
        p.SetCurrentStamina(20); Check(Start(p) && p.CurrentStamina == 0, "Exact stamina cost is accepted");
        Check(!Start(p) && p.CurrentStamina == 0, "Active dash cannot restart or spend twice");
        Tick(p, "ResetInputState");
        Check(!p.IsDashing && body.linearVelocity == Vector2.zero, "Disable/reset cancels dash and velocity");
        Set(cfg, "dashDistance", 6f); Set(cfg, "dashDuration", .3f); Set(cfg, "dashStaminaCost", 7f); cfg.Validate();
        p.SetCurrentStamina(100); Set(p, "movement", new Vector2(1, 1).normalized); start = body.position;
        Check(Start(p) && p.CurrentStamina == 93, "Edited shared cost applies to next dash");
        Set(cfg, "dashDistance", 1f);
        for (int i = 0; i < 15; i++) { Tick(p, "FixedUpdate"); Physics2D.Simulate(Time.fixedDeltaTime); }
        Check(Mathf.Abs(Vector2.Distance(start, body.position) - 6) < .01f, "Edited duration/distance apply; in-progress dash keeps its snapshot");
        Check(Mathf.Abs(body.position.x - start.x - (body.position.y - start.y)) < .01f, "Diagonal direction normalized");
        Tick(p, "ResetInputState");
        Set(cfg, "dashDistance", 3f); Set(cfg, "dashDuration", .15f);
        var wall = new GameObject("Dash test thin wall"); wall.transform.position = new Vector3(32, 30); wall.AddComponent<BoxCollider2D>().size = new Vector2(.05f, 10);
        body.position = new Vector2(30, 30); Set(p, "movement", Vector2.right); Physics2D.SyncTransforms(); Start(p);
        for (int i = 0; i < 9; i++) { Tick(p, "FixedUpdate"); Physics2D.Simulate(Time.fixedDeltaTime); }
        Check(body.position.x < 32, "Continuous physics stops dash at a thin solid wall");
        Object.DestroyImmediate(wall);
        Tick(p, "ResetInputState"); Set(cfg, "dashStaminaCost", 0f); p.SetCurrentStamina(0);
        Check(Start(p), "Zero cost allows dash at zero stamina"); Tick(p, "ResetInputState");
        Set(cfg, "dashDistance", 0f); Check(!Start(p), "Zero distance disables dash without spending");
        Set(cfg, "dashDistance", 3f); p.SetCurrentStamina(100); Start(p);
        cfg.Dash.ChangeBinding(0).WithPath("<Keyboard>/q"); cfg.Validate(); Tick(p, "Update");
        Check(!p.IsDashing && !Get<bool>(p, "dashRequested"), "Runtime rebinding cancels dash and clears queued input");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.Update(); Tick(p, "Update");
        InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Q)); InputSystem.Update(); Tick(p, "Update"); Tick(p, "FixedUpdate");
        Check(p.IsDashing, "Runtime rebound key starts dash");
        Tick(p, "ResetInputState"); Set(p, "movement", Vector2.up); Tick(p, "FixedUpdate");
        Check(body.linearVelocity == Vector2.up * cfg.PlayerMoveSpeed, "Regular movement still uses shared speed");
        Set(cfg, "dashDuration", -1f); Set(cfg, "dashStaminaCost", -1f); cfg.Validate();
        Check(cfg.DashDuration == .02f && cfg.DashStaminaCost == 0, "Dash values are clamped to valid ranges");
        InputSystem.RemoveDevice(keyboard);
    }
    static bool Start(PlayerController p) => (bool)Tick(p, "TryStartDash");
    static object Tick(object o, string name) => o.GetType().GetMethod(name, Private).Invoke(o, null);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); results.Add("PASS " + message); }
    static void Fail(Exception e) { Debug.LogException(e); File.WriteAllText("DASH_RESULTS.txt", "FAIL\n" + e); EditorApplication.Exit(1); }
}
