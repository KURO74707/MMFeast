using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class MedkitPrefabValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> checks = new();
    static MedkitPrefabValidation() { EditorApplication.playModeStateChanged += OnPlay; }
    public static void Run()
    {
        try
        {
            // Intentionally do not run Global Data setup or re-save any prefab first.
            foreach (var path in new[] { CombatPrototypeBuilder.ScenePath, CookingPrototypeBuilder.ScenePath })
            {
                EditorSceneManager.OpenScene(path);
                var shop = Object.FindAnyObjectByType<ItemShop>();
                Check(shop != null && shop.Catalog != null && shop.Catalog.Items[0].IsValid(out _), path + " default medkit valid directly after loading");
            }
            SessionState.SetString("MedkitFixChecks", string.Join("\n", checks));
            SessionState.SetBool("MedkitFixPending", true); EditorApplication.EnterPlaymode();
        }
        catch (Exception error) { Fail(error); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("MedkitFixPending", false)) return;
        SessionState.SetBool("MedkitFixPending", false); checks.Clear(); checks.AddRange(SessionState.GetString("MedkitFixChecks", "").Split('\n'));
        new GameObject("Medkit prefab validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator test)
    {
        while (true)
        {
            bool next; object current;
            try { next = test.MoveNext(); current = next ? test.Current : null; } catch (Exception error) { Fail(error); yield break; }
            if (!next) break; yield return current;
        }
        File.WriteAllText("MEDKIT_PREFAB_RESULTS.txt", "PASS: " + checks.Count + " checks\n" + string.Join("\n", checks)); Time.timeScale = 1; EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach (var spawner in Object.FindObjectsByType<EnemySpawner>()) spawner.enabled = false;
        var shop = Object.FindAnyObjectByType<ItemShop>(); var wallet = shop.GetComponent<TruckShop>();
        var defaultItem = shop.Catalog.Items[0];
        Check(shop.OpenShop() && shop.CanPurchase(defaultItem, out _), "Default item available on entering Play Mode without migration");
        int balance = wallet.CurrentBalance;
        Check(shop.TryPurchase(defaultItem) && wallet.CurrentBalance == balance - defaultItem.Price, "Default medkit purchase succeeds and deducts exact price");
        var item = Object.Instantiate(defaultItem);
        var settings = Object.Instantiate(shop.Settings); var catalog = Object.Instantiate(shop.Catalog);
        Set(catalog, "items", new List<ShopItemDefinition> { item }); Set(settings, "itemShopCatalog", catalog); Set(shop, "settings", settings);
        var source = Object.Instantiate(item.Prefab, new Vector3(100,100), Quaternion.identity); Set(item, "prefab", source);
        source.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Dynamic;
        source.GetComponent<Rigidbody2D>().gravityScale = 0;
        Check(item.IsValid(out _), "Trigger and Rigidbody2D validate without an unnecessary Kinematic-only restriction");
        Check(shop.TryPurchase(item), "Valid dynamic zero-gravity medkit can be purchased");
        source.GetComponent<Rigidbody2D>().simulated = false; balance = wallet.CurrentBalance;
        Check(item.IsValid(out _), "Asset structural validation does not depend on live simulated-body readiness");
        Check(!shop.TryPurchase(item) && wallet.CurrentBalance == balance && shop.Message.Contains("Simulated"), "Spawned unsimulated pickup is rejected before charging with a precise reason");
        source.GetComponent<Rigidbody2D>().simulated = true;
        source.GetComponent<CircleCollider2D>().isTrigger = false;
        Check(!item.IsValid(out string reason) && reason.Contains("Is Trigger"), "Non-trigger collider explains its exact failure");
        source.GetComponent<CircleCollider2D>().isTrigger = true; source.GetComponent<CircleCollider2D>().enabled = false;
        Check(!item.IsValid(out reason) && reason.Contains("CircleCollider2D"), "Disabled collider explains its exact failure");
        source.GetComponent<CircleCollider2D>().enabled = true; source.enabled = false;
        Check(!item.IsValid(out reason) && reason.Contains("MedkitPickup"), "Disabled pickup explains its exact failure");
        source.enabled = true; source.gameObject.SetActive(false);
        Check(!item.IsValid(out reason) && reason.Contains("Activate"), "Inactive prefab explains its exact failure");
        Set(item, "prefab", null);
        Check(!item.IsValid(out reason) && reason.Contains("Global Data > Item Shop"), "Missing reference directs Designer to the correct item");
        shop.CloseShop(); Object.Destroy(source.gameObject); yield return null;
    }
    static void Set(object owner, string name, object value) => owner.GetType().GetField(name, Private).SetValue(owner, value);
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks.Add("PASS " + name); }
    static void Fail(Exception error) { Time.timeScale = 1; Debug.LogException(error); File.WriteAllText("MEDKIT_PREFAB_RESULTS.txt", "FAIL\n" + error + "\n" + string.Join("\n", checks)); EditorApplication.Exit(1); }
}
