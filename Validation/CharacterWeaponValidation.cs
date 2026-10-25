using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class CharacterWeaponValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new List<string>();
    static double deadline;
    static CharacterWeaponValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void RunDeferred() { EditorApplication.delayCall += Run; }
    public static void Run()
    {
        try
        {
            Debug.Log("CharacterWeaponValidation: preparing scenes");
            Application.runInBackground = true; deadline = EditorApplication.timeSinceStartup + 120;
            EditorApplication.update += Pump;
            EditorSettings.enterPlayModeOptionsEnabled = false;
            CharacterWeaponPrototypeSetup.Upgrade();
            var settings = GlobalDataPrototypeSetup.GetOrCreateSettings();
            Check(settings.DefaultCharacter.id == "cat" && settings.Characters.Length == 2 && settings.Weapons.Length == 2, "Default cat and both character/weapon catalogs installed");
            Check(settings.DefaultWeapon.id == "spatula" && settings.DefaultCharacter.talent == null, "Default spatula preserves current combat; unspecified cat cost mechanic inactive");
            string before = EditorJsonUtility.ToJson(settings);
            CharacterWeaponPrototypeSetup.EnsureDefinitions(settings);
            Check(before == EditorJsonUtility.ToJson(settings), "Setup is idempotent and preserves designer selections");
            foreach (var path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                EditorSceneManager.OpenScene(path);
                var player = Object.FindFirstObjectByType<PlayerController>();
                Check(player.Loadout != null && player.Loadout.Character == settings.DefaultCharacter, path + " has bound loadout");
                Check(player.HP == 100 && player.SAN == 100, path + " migration preserves serialized player state");
                var hud = Object.FindFirstObjectByType<PlayerStatusUI>();
                Check(Field<Text>(hud, "loadoutText") != null, path + " shows character/weapon HUD");
            }
            Debug.Log("CharacterWeaponValidation: entering Play Mode");
            SessionState.SetString("CharacterWeaponEditChecks", string.Join("\n", checks));
            SessionState.SetBool("CharacterWeaponsPending", true); EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Fail(e); }
    }
    static void Pump()
    {
        if (EditorApplication.isPlaying) EditorApplication.QueuePlayerLoopUpdate();
        if (deadline > 0 && EditorApplication.timeSinceStartup > deadline) Fail(new Exception("Validation timeout; last check: " + (checks.Count > 0 ? checks[checks.Count - 1] : "none")));
    }
    static void OnPlay(PlayModeStateChange state)
    {
        Debug.Log("CharacterWeaponValidation play state: " + state + "; pending=" + SessionState.GetBool("CharacterWeaponsPending", false));
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CharacterWeaponsPending", false)) return;
        SessionState.SetBool("CharacterWeaponsPending", false);
        checks.Clear(); checks.AddRange(SessionState.GetString("CharacterWeaponEditChecks", "").Split('\n'));
        deadline = EditorApplication.timeSinceStartup + 120; EditorApplication.update += Pump;
        EditorApplication.delayCall += () => { Debug.Log("CharacterWeaponValidation: starting runtime checks"); new GameObject("Character weapon validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests())); };
    }
    static IEnumerator Guard(IEnumerator tests)
    {
        while (true)
        {
            bool more; object current;
            try { more = tests.MoveNext(); current = more ? tests.Current : null; }
            catch (Exception e) { Fail(e); yield break; }
            if (!more) break; yield return current;
        }
        File.WriteAllText("CHARACTER_WEAPON_RESULTS.txt", "PASS: " + checks.Count + " checks\n" + string.Join("\n", checks));
        Time.timeScale = 1f; EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        Time.timeScale = 1f;
        Debug.Log("CharacterWeaponValidation: removing initial NPCs");
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
        foreach (var npc in Object.FindObjectsByType<EnemySimpleAI>()) Object.Destroy(npc.gameObject);
        yield return null;
        InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
        foreach (var device in InputSystem.devices) InputSystem.DisableDevice(device);
        InputSystem.AddDevice<Keyboard>(); InputSystem.AddDevice<Mouse>(); InputSystem.Update();
        var shared = Object.FindFirstObjectByType<GlobalData>().Settings;
        var config = Object.Instantiate(shared);
        var cat = Object.Instantiate(shared.Characters[0]); var bird = Object.Instantiate(shared.Characters[1]);
        var spatula = Object.Instantiate(shared.Weapons[0]); var mallet = Object.Instantiate(shared.Weapons[1]);
        var fan = Object.Instantiate((FanAttackDefinition)spatula.attack); spatula.attack = fan;
        mallet.attack = Object.Instantiate((FanAttackDefinition)mallet.attack);
        var knockback = Object.Instantiate((KnockbackEffectDefinition)mallet.hitEffect); mallet.hitEffect = knockback;
        var song = Object.Instantiate((BirdSongDefinition)bird.talent); bird.talent = song;
        cat.startingWeapon = spatula; bird.startingWeapon = spatula;
        Set(config, "characters", new[] { cat, bird }); Set(config, "weapons", new[] { spatula, mallet }); Set(config, "defaultCharacter", cat);
        var player = Object.FindFirstObjectByType<PlayerController>(); player.Configure(config);
        var combat = player.GetComponent<PlayerCombat>(); Set(combat, "settings", config);
        player.transform.position = new Vector3(50, 50); var body = player.GetComponent<Rigidbody2D>(); body.position = player.transform.position;
        var loadout = player.Loadout; loadout.UseGlobalSelection();
        Check(player.MaxHP == 100 && player.MaxSAN == 100 && player.MaxStamina == 100, "Migrated character preserves HP/SAN/stamina");
        Check(cat.moveSpeed == 5 && cat.dashDistance == 3 && cat.dashStaminaCost == 20, "Migrated movement and dash values preserved");
        Check(spatula.damage == 10 && spatula.range == 3 && spatula.cooldown == .1f && fan.angle == 120, "Spatula preserves prior 120 degree attack and tuning");
        player.SetCurrentHP(70); player.SetCurrentSAN(45); player.SetCurrentStamina(30);
        loadout.SelectCharacter(bird);
        Check(player.HP == 70 && player.SAN == 45 && player.CurrentStamina == 30, "Changing character does not refill runtime values");
        bird.maxHP = 50; player.ClampCurrentStats(); Check(player.HP == 50, "Character maximum clamps current HP");
        bird.maxHP = 200; player.ClampCurrentStats(); Check(player.HP == 50, "Raising maximum does not heal player");
        loadout.SelectCharacter(cat); player.SetCurrentHP(100);
        bird.damageMultiplier = 2; bird.attackSpeedMultiplier = 2;
        loadout.SelectCharacter(bird); Check(combat.Damage == 20 && combat.Cooldown == .05f, "Effective stats combine weapon and character multipliers");
        loadout.SelectCharacter(cat);
        var front = Enemy(new Vector2(52, 50), config);
        var duplicate = new GameObject("Extra collider"); duplicate.transform.SetParent(front.transform, false); duplicate.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
        var behind = Enemy(new Vector2(48, 50), config);
        var side = Enemy(new Vector2(50, 52), config);
        Physics2D.SyncTransforms(); Swing(combat);
        Check(front.GetComponent<EnemyHealth>().CurrentHP == 40, "Fan damages target once despite multiple colliders");
        Check(behind.GetComponent<EnemyHealth>().CurrentHP == 50 && side.GetComponent<EnemyHealth>().CurrentHP == 50, "Fan excludes back and outside-angle targets");
        loadout.EquipWeapon(mallet); combat.TryAttack();
        Check(front.GetComponent<EnemyHealth>().CurrentHP == 40, "Switching weapon does not reset previous cooldown");
        Swing(combat); Check(front.GetComponent<EnemyHealth>().CurrentHP == 30 && front.IsKnockedBack, "Mallet deals damage and starts knockback");
        Physics2D.simulationMode = SimulationMode2D.Script;
        front.SetTarget(null); front.SetFoodTruckTarget(null);
        Vector2 start = front.GetComponent<Rigidbody2D>().position;
        for (int i = 0; i < 10; i++) { Tick(front, "FixedUpdate"); Physics2D.Simulate(.02f); }
        Check(Mathf.Abs(front.GetComponent<Rigidbody2D>().position.x - start.x - knockback.distance) < .03f, "Knockback travels configured distance in unobstructed space");
        Tick(front, "FixedUpdate"); Check(!front.IsKnockedBack && front.GetComponent<Rigidbody2D>().linearVelocity == Vector2.zero, "AI resumes after knockback without a residual velocity");
        knockback.distance = 1; knockback.duration = .1f; front.TryKnockback(Vector2.right, knockback.distance, knockback.duration);
        var wall = new GameObject("Knockback wall"); wall.transform.position = front.transform.position + Vector3.right * .65f; wall.AddComponent<BoxCollider2D>().size = new Vector2(.05f, 3f);
        Physics2D.SyncTransforms(); float wallX = wall.transform.position.x;
        for (int i = 0; i < 5; i++) { Tick(front, "FixedUpdate"); Physics2D.Simulate(.02f); }
        Check(front.transform.position.x < wallX, "Knockback obeys existing continuous collision at solid walls");
        Object.Destroy(wall); Object.Destroy(front.gameObject); Object.Destroy(behind.gameObject); Object.Destroy(side.gameObject); yield return null;
        var customerPrefab = AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab");
        var customer = Object.Instantiate(customerPrefab, new Vector3(52, 50), Quaternion.identity); Set(customer, "settings", config);
        var customerAI = customer.GetComponent<EnemySimpleAI>();
        var carry = player.GetComponent<PlayerCarry>(); var point = new GameObject("Test carry point").transform; point.position = customer.transform.position;
        Check(customer.TryCarry(carry, point), "Customer can be carried through existing path");
        Check(!customerAI.TryKnockback(Vector2.right, 2, .2f), "Carried customers cannot be knocked out of ownership");
        customer.ReleaseToWorld(new Vector3(52, 50));
        Check(customerAI.TryKnockback(Vector2.right, 2, .2f), "Free customer accepts knockback");
        customer.Purify(); Check(!customerAI.TryKnockback(Vector2.right, 2, .2f), "Purified customers cannot be knocked back");
        Object.Destroy(customer.gameObject); Object.Destroy(point.gameObject);
        Physics2D.simulationMode = SimulationMode2D.FixedUpdate;
        var ally = MinimalPlayer(config, new Vector2(51, 50));
        ally.gameObject.AddComponent<CircleCollider2D>().radius = .2f;
        var far = MinimalPlayer(config, new Vector2(60, 50));
        var otherScene = SceneManager.CreateScene("Other song scene");
        var foreign = MinimalPlayer(config, new Vector2(51, 50)); SceneManager.MoveGameObjectToScene(foreign.gameObject, otherScene);
        foreach (var target in new[] { player, ally, far, foreign }) { target.SetCurrentHP(70); target.SetCurrentSAN(40); target.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic; }
        player.transform.position = new Vector3(50, 50); body.position = player.transform.position;
        loadout.UseGlobalSelection(); Set(config, "defaultCharacter", bird);
        song.idleDelay = .05f; song.sanPerSecond = 10; song.radius = 3;
        yield return null;
        Check(loadout.Character == bird, "Global character selector updates player without a component override");
        var talent = player.GetComponent<BirdSongTalent>(); talent.ResetActivity();
        float before = player.SAN; yield return new WaitForSeconds(.16f);
        Check(loadout.IsSinging && player.SAN > before, "Bird waits then restores its own SAN" + $" (SAN={player.SAN}, idle={Field<float>(talent, "idleElapsed")}, move={player.HasMovementInput}, blocked={player.GameplayInputsBlocked}, aim={player.IsCannonAiming}, HP={player.HP}, enabled={player.enabled}, velocity={body.linearVelocity}, dash={player.IsDashing}, attack={player.AttackPressed}, aimHeld={player.RightMouseHeld})");
        Check(Mathf.Abs((ally.SAN - 40) - (player.SAN - before)) < .03f, "Nearby ally restores once per update despite duplicate colliders");
        Check(far.SAN == 40 && foreign.SAN == 40, "Song respects radius and scene isolation");
        player.SetCurrentSAN(player.MaxSAN); yield return null; Check(player.SAN == player.MaxSAN, "Song caps SAN at character maximum");
        player.SetCurrentSAN(40); Set(player, "movement", Vector2.right); Tick(talent, "Update");
        Check(!talent.IsSinging, "Movement input interrupts singing even without body movement"); Set(player, "movement", Vector2.zero);
        song.idleDelay = 0; yield return null; Check(loadout.IsSinging, "Live idle delay edit affects singing");
        player.TakeDamage(1); Check(!talent.IsSinging, "Damage interrupts singing when configured");
        song.idleDelay = .05f; talent.ResetActivity(); Time.timeScale = 0; before = player.SAN;
        yield return new WaitForSecondsRealtime(.1f); Check(player.SAN == before && !talent.IsSinging, "Pause neither accumulates idle delay nor restores SAN"); Time.timeScale = 1;
        yield return new WaitForSeconds(.12f); Check(loadout.IsSinging, "Song resumes after pause using scaled time");
        loadout.NotifyActivity(); Check(!loadout.IsSinging, "Accepted weapon actions interrupt singing");
        song.idleDelay = 0; song.sanPerSecond = 0; before = player.SAN; yield return new WaitForSeconds(.05f);
        Check(player.SAN == before, "Zero SAN rate disables recovery immediately"); song.sanPerSecond = 10;
        ally.SetCurrentHP(0); before = ally.SAN; yield return new WaitForSeconds(.05f); Check(ally.HP == 0 && ally.SAN == before, "Song does not restore a dead player's SAN or revive it");
        Set(config, "weaponOverride", mallet); yield return null; Check(loadout.Weapon == mallet, "Global weapon selector changes equipment independently");
        loadout.SelectCharacter(cat); yield return null; Check(player.GetComponent<BirdSongTalent>() == null && !loadout.IsSinging, "Changing to cat removes bird talent");
        loadout.SelectCharacter(bird); loadout.enabled = false; yield return null;
        Check(player.GetComponent<BirdSongTalent>() == null, "Disabling loadout removes its runtime talent");
        loadout.enabled = true; yield return null; Check(player.GetComponents<BirdSongTalent>().Length == 1, "Re-enabling recreates exactly one talent");
        var hud = Object.FindFirstObjectByType<PlayerStatusUI>(); Tick(hud, "LateUpdate");
        Check(Field<Text>(hud, "loadoutText").text.Contains("Bird") && Field<Text>(hud, "loadoutText").text.Contains("Meat Mallet"), "HUD follows character and weapon changes");
        Check(shared.DefaultCharacter.id == "cat" && shared.DefaultWeapon.id == "spatula" && shared.DefaultCharacter.maxHP == 100, "Runtime test states never mutate shared original configuration");
        Check(ally.Loadout.Character == cat && ally.Loadout.Weapon == mallet && ally.HP == 0 && player.HP > 0, "Shared definitions preserve independent player choices and state");
        loadout.SelectCharacter(cat); loadout.EquipWeapon(spatula); player.SetCurrentStamina(100);
        body.bodyType = RigidbodyType2D.Dynamic;
        Set(player, "movement", Vector2.right);
        Check((bool)Tick(player, "TryStartDash") && player.CurrentStamina == 80, "Character configuration drives dash and stamina spending");
        float hpBefore = player.HP; player.TakeDamage(5); Check(player.HP == hpBefore, "Dash still grants configured damage immunity");
        cat.dashDistance = 6; Check(Mathf.Abs(Field<float>(player, "dashSpeed") - 20) < .01f, "Editing dash distance preserves active dash snapshot");
        Tick(player, "StopDash"); player.SetCurrentStamina(19);
        Check(!(bool)Tick(player, "TryStartDash"), "Dash still rejects insufficient stamina");
        player.SetCurrentStamina(100); Check((bool)Tick(player, "TryStartDash") && Mathf.Abs(Field<float>(player, "dashSpeed") - 40) < .01f, "Next dash reads updated character distance");
        Tick(player, "StopDash");
        float staminaBefore = player.CurrentStamina;
        typeof(PlayerController).GetMethod("RecoverStamina", Private).Invoke(player, new object[] { .5f });
        Check(player.CurrentStamina == staminaBefore, "Character recovery delay still applies after dash");
        typeof(PlayerController).GetMethod("RecoverStamina", Private).Invoke(player, new object[] { 1f });
        Check(Mathf.Abs(player.CurrentStamina - staminaBefore - 10) < .001f, "Character stamina regeneration uses only elapsed time after delay");
        player.IsCannonAiming = true; loadout.EquipWeapon(mallet);
        Check(!player.IsCannonAiming && !player.RightMouseReleased, "Equipment changes cancel cannon aim without firing");
        var pausedEnemy = Enemy(new Vector2(52, 50), config); Physics2D.SyncTransforms(); Time.timeScale = 0; Swing(combat);
        Check(pausedEnemy.GetComponent<EnemyHealth>().CurrentHP == 50, "Pause blocks melee attacks"); Time.timeScale = 1;
        player.SetCurrentHP(0); Swing(combat); Check(pausedEnemy.GetComponent<EnemyHealth>().CurrentHP == 50, "Dead players cannot attack");
        player.SetCurrentHP(70); Object.Destroy(pausedEnemy.gameObject);
        Set(player, "movement", Vector2.zero); body.linearVelocity = Vector2.zero;
        cat.initialStamina = 999; cat.maxStamina = 20; cat.dashDuration = -1; cat.attackSpeedMultiplier = 0; cat.Validate();
        Check(cat.initialStamina == 20 && cat.dashDuration == .02f && cat.attackSpeedMultiplier == .01f, "Character values enforce valid bounds");
        mallet.damage = -1; mallet.range = -1; mallet.cooldown = -1; mallet.Validate();
        Check(mallet.damage == 0 && mallet.range == .1f && mallet.cooldown == 0, "Weapon values enforce valid bounds");
    }
    static EnemySimpleAI Enemy(Vector2 position, GlobalGameSettings config)
    {
        var npc = Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab"), position, Quaternion.identity);
        Set(npc, "settings", config); Set(npc.GetComponent<EnemyHealth>(), "settings", config); npc.SetTarget(null); npc.SetFoodTruckTarget(null); return npc;
    }
    static PlayerController MinimalPlayer(GlobalGameSettings config, Vector2 position)
    {
        var go = new GameObject("Ally"); go.SetActive(false); go.transform.position = position;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .4f;
        var player = go.AddComponent<PlayerController>(); var loadout = go.AddComponent<PlayerLoadout>(); player.Configure(config);
        Set(loadout, "characterOverride", config.Characters[0]);
        go.GetComponent<Rigidbody2D>().gravityScale = 0; go.SetActive(true); return player;
    }
    static void Swing(PlayerCombat combat) { Set(combat, "lastAttackTime", float.NegativeInfinity); combat.TryAttack(); }
    static object Tick(object target, string method) => target.GetType().GetMethod(method, Private).Invoke(target, null);
    static T Field<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); checks.Add("PASS " + message); File.WriteAllText("CHARACTER_WEAPON_PROGRESS.txt", string.Join("\n", checks)); }
    static void Fail(Exception error) { Debug.LogException(error); File.WriteAllText("CHARACTER_WEAPON_RESULTS.txt", "FAIL after " + checks.Count + "\n" + string.Join("\n", checks) + "\n" + error); EditorApplication.Exit(1); }
}
