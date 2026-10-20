using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class StaminaValidation
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly List<string> results = new();
    static StaminaValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            foreach (string path in new[] { "Assets/Prototype/CombatCollection.unity", "Assets/Prototype/CookingCollection.unity" })
            {
                var scene = EditorSceneManager.OpenScene(path);
                CombatStatusPrototypeSetup.ApplyToScene(scene);
                CombatStatusPrototypeSetup.ApplyToScene(scene);
                EditorSceneManager.SaveScene(scene);
                var hud = Object.FindAnyObjectByType<PlayerStatusUI>();
                if (hud.transform.childCount != 3 || Field<Slider>(hud, "staminaBar") == null)
                    throw new Exception("HUD migration must create exactly one third row: " + path);
                var stamina = (RectTransform)hud.transform.Find("StaminaGroup");
                var san = (RectTransform)hud.transform.Find("SANGroup");
                if (stamina.anchoredPosition.y >= san.anchoredPosition.y) throw new Exception("Stamina must be below SAN");
                if (Field<GlobalGameSettings>(Object.FindAnyObjectByType<PlayerController>(), "settings") != Object.FindAnyObjectByType<GlobalData>().Settings)
                    throw new Exception("Shared settings reference must be preserved");
            }
            EditorSceneManager.OpenScene("Assets/Prototype/CombatCollection.unity");
            SessionState.SetBool("StaminaPending", true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("StaminaPending", false)) return;
        SessionState.SetBool("StaminaPending", false);
        new GameObject("Stamina test runner").AddComponent<CombatSanValidationRunner>().Begin(Guard());
    }
    static IEnumerator Guard()
    {
        try { Tests(); }
        catch (Exception e) { Fail(e); yield break; }
        yield return null;
        try
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            player.SetCurrentHP(85); player.SetCurrentSAN(70); player.SetCurrentStamina(60);
            var camera = Camera.main;
            camera.GetComponent<SimpleCameraShake>().enabled = false;
            var canvas = Object.FindAnyObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            var target = new RenderTexture(1280, 720, 24); camera.targetTexture = target; Canvas.ForceUpdateCanvases();
            camera.Render(); RenderTexture.active = target;
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); pixels.Apply();
            File.WriteAllBytes("STAMINA_PREVIEW.png", pixels.EncodeToPNG());
            RenderTexture.active = null; camera.targetTexture = null; target.Release();
            Object.Destroy(pixels); Object.Destroy(target);
            File.WriteAllText("STAMINA_RESULTS.txt", "PASS: " + results.Count + " checks\nBoth scenes: idempotent HUD migration, third row below SAN, shared settings preserved.\n" + string.Join("\n", results));
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Fail(e); }
    }
    static void Tests()
    {
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
        foreach (var ai in Object.FindObjectsByType<EnemySimpleAI>()) Object.Destroy(ai.gameObject);
        var player = Object.FindAnyObjectByType<PlayerController>();
        var cfg = Object.Instantiate(Object.FindAnyObjectByType<GlobalData>().Settings);
        player.Configure(cfg); player.enabled = false;
        var hud = Object.FindAnyObjectByType<PlayerStatusUI>();
        var bar = Field<Slider>(hud, "staminaBar"); var label = Field<Text>(hud, "staminaText");
        Check(cfg.StaminaRecoveryDelay == 1 && cfg.StaminaRecoveryPerSecond == 20, "Default delay 1 second and recovery 20 per second");
        player.SetCurrentStamina(50); Recover(player, .5f);
        Check(player.CurrentStamina == 60, "Initial partial stamina can recover without a prior dash");
        player.SetCurrentStamina(100); Start(player);
        Check(player.CurrentStamina == 80 && Mathf.Approximately(bar.value, .8f), "Dash deduction updates stamina HUD synchronously");
        Recover(player, 2); Check(player.CurrentStamina == 80, "No recovery during dash");
        Tick(player, "StopDash"); Recover(player, .5f);
        Check(player.CurrentStamina == 80, "Wait after dash ends before recovering");
        Recover(player, .5f); Check(player.CurrentStamina == 80, "Delay boundary does not add a full extra step");
        Recover(player, .5f); Check(player.CurrentStamina == 90, "Recovers the configured amount per second");
        Check(Mathf.Approximately(bar.value, .9f) && label.text == "STAMINA 90 / 100", "Recovery updates bar and numeric label");
        Recover(player, 5); Check(player.CurrentStamina == 100 && bar.value == 1, "Recovery clamps at stamina maximum");
        Start(player); Tick(player, "StopDash"); Recover(player, .75f);
        Start(player); Tick(player, "StopDash"); Recover(player, .75f);
        Check(player.CurrentStamina == 60, "Each successful dash resets recovery delay");
        Recover(player, .5f); Check(player.CurrentStamina == 65, "Only elapsed time beyond delay contributes to recovery");
        player.SetCurrentStamina(5); FieldInfo elapsed = typeof(PlayerController).GetField("staminaRecoveryElapsed", Private);
        elapsed.SetValue(player, .8f); Check(!(bool)Tick(player, "TryStartDash"), "Insufficient stamina still rejects dash");
        Recover(player, .4f); Check(Mathf.Abs(player.CurrentStamina - 9) < .001f, "Failed dash does not restart delay");
        Set(cfg, "staminaRecoveryDelay", .5f); Set(cfg, "staminaRecoveryPerSecond", 40f); cfg.Validate(); Recover(player, .25f);
        Check(Mathf.Abs(player.CurrentStamina - 19) < .001f, "Live delay and rate edits apply to current recovery");
        Set(cfg, "staminaRecoveryDelay", 0f); player.SetCurrentStamina(50); Start(player); Tick(player, "StopDash"); Recover(player, .25f);
        Check(player.CurrentStamina == 40, "Zero delay starts recovery immediately after dash");
        Set(cfg, "staminaRecoveryPerSecond", 0f); Recover(player, 2);
        Check(player.CurrentStamina == 40, "Zero rate disables recovery");
        Set(cfg, "maxStamina", 80f); Tick(hud, "LateUpdate");
        Check(bar.value == .5f && label.text == "STAMINA 40 / 80", "HUD reflects live maximum edits");
        Set(cfg, "maxStamina", 0f); player.SetCurrentStamina(40); Tick(hud, "LateUpdate");
        Check(player.CurrentStamina == 0 && bar.value == 0 && !float.IsNaN(bar.value), "Zero maximum remains valid");
        Set(cfg, "staminaRecoveryDelay", -1f); Set(cfg, "staminaRecoveryPerSecond", -5f); cfg.Validate();
        Check(cfg.StaminaRecoveryDelay == 0 && cfg.StaminaRecoveryPerSecond == 0, "Negative recovery settings clamp to zero");
        Set(cfg, "maxStamina", 100f); Set(cfg, "staminaRecoveryDelay", 1f); Set(cfg, "staminaRecoveryPerSecond", 20f);
        player.SetCurrentStamina(100); Start(player);
        for (int i = 0; i < 8; i++) Tick(player, "FixedUpdate");
        Check(player.IsDashing && player.CurrentStamina == 80, "Real FixedUpdate dash steps suppress recovery");
        Tick(player, "FixedUpdate"); Check(!player.IsDashing && player.CurrentStamina == 80, "Real dash completion begins delay");
        for (int i = 0; i < 75; i++) Tick(player, "FixedUpdate");
        Check(Mathf.Abs(player.CurrentStamina - 90.4f) < .02f, "FixedUpdate integrates recovery after end delay");
        player.SetCurrentHP(85); player.SetCurrentSAN(70);
        Check(Mathf.Approximately(Field<Slider>(hud, "hpBar").value, .85f) && Mathf.Approximately(Field<Slider>(hud, "sanBar").value, .7f), "Existing HP and SAN HUD still update");
        Check(!bar.interactable && !bar.GetComponent<Image>().raycastTarget, "Stamina display does not consume player input");
    }
    static void Start(PlayerController p) { if (!(bool)Tick(p, "TryStartDash")) throw new Exception("Expected dash start"); }
    static void Recover(PlayerController p, float dt) => typeof(PlayerController).GetMethod("RecoverStamina", Private).Invoke(p, new object[] { dt });
    static object Tick(object o, string name) => o.GetType().GetMethod(name, Private).Invoke(o, null);
    static T Field<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); results.Add("PASS " + name); }
    static void Fail(Exception e) { Debug.LogException(e); File.WriteAllText("STAMINA_RESULTS.txt", "FAIL\n" + e); EditorApplication.Exit(1); }
}
