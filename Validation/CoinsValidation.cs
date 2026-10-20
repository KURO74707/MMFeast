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
public static class CoinsValidation
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> results = new();
    static GlobalGameSettings config;
    static TruckShop shop;
    static PlayerStatusUI hud;
    static Text coins;
    static CoinsValidation() { EditorApplication.playModeStateChanged += OnPlay; }
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
                hud = Object.FindAnyObjectByType<PlayerStatusUI>();
                coins = Field<Text>(hud, "coinsText");
                var rect = (RectTransform)coins.transform;
                Check(rect.name == "CoinsHUD" && rect.anchorMin == Vector2.one && rect.anchorMax == Vector2.one && rect.pivot == Vector2.one && rect.anchoredPosition == new Vector2(-20,-20), path + " coins anchored at top right with 20px margins");
                Check(Field<TruckShop>(hud,"shop") == Object.FindAnyObjectByType<TruckShop>() && !coins.raycastTarget, path + " HUD uses local shop wallet without blocking input");
                Check(coins.transform.parent == hud.GetComponentInParent<Canvas>().transform && coins.transform.parent.Find("CoinsHUD") == coins.transform, path + " persistent HUD outside shop modal");
                int count=0; foreach(var text in hud.GetComponentInParent<Canvas>().GetComponentsInChildren<Text>(true)) if(text.name=="CoinsHUD")count++;
                Check(count==1, path + " repeated setup creates one coins label");
            }
            AssetDatabase.SaveAssets(); SessionState.SetString("CoinsChecks",string.Join("\n",results));
            SessionState.SetBool("CoinsPending",true); EditorApplication.EnterPlaymode();
        }
        catch(Exception e) { Fail(e); }
    }
    static void OnPlay(PlayModeStateChange state)
    {
        if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool("CoinsPending",false))return;
        SessionState.SetBool("CoinsPending",false); results.Clear();results.AddRange(SessionState.GetString("CoinsChecks","").Split('\n'));
        new GameObject("Coins validation").AddComponent<CombatSanValidationRunner>().Begin(Guard(Tests()));
    }
    static IEnumerator Guard(IEnumerator routine)
    {
        while(true)
        {
            bool next; object current;
            try { next=routine.MoveNext();current=next?routine.Current:null; } catch(Exception e){Fail(e);yield break;}
            if(!next)break;yield return current;
        }
        File.WriteAllText("COINS_RESULTS.txt","PASS: "+results.Count+" checks\n"+string.Join("\n",results));Time.timeScale=1;EditorApplication.Exit(0);
    }
    static IEnumerator Tests()
    {
        foreach(var spawner in Object.FindObjectsByType<EnemySpawner>())spawner.enabled=false;
        foreach(var ai in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(ai.gameObject);yield return null;
        var shared=Object.FindAnyObjectByType<GlobalData>().Settings;
        config=Object.Instantiate(shared);hud=Object.FindAnyObjectByType<PlayerStatusUI>();coins=Field<Text>(hud,"coinsText");
        shop=Object.FindAnyObjectByType<TruckShop>();var player=shop.GetComponent<PlayerController>();
        Check(config.CustomerPurificationCoins==25 && shop.CurrentBalance==shared.InitialCoins,"Default 25 coins per purification; existing starting balance preserved");
        Tick(hud,"LateUpdate");Check(coins.text=="COINS  500","HUD shows actual initial wallet balance while shop closed");
        shop.SetCurrentBalance(0);var first=Spawn(new Vector3(4,-3));first.Purify();
        Check(first.IsPurified && shop.CurrentBalance==25,"First purification immediately rewards wallet");
        first.Purify();Check(shop.CurrentBalance==25,"Repeated purification cannot pay twice");
        var existing=Spawn(new Vector3(5,-3));Set(config,"customerPurificationCoins",40);config.Validate();existing.Purify();
        Check(shop.CurrentBalance==65,"Live reward edit applies to existing unpurified Customer");
        Tick(hud,"LateUpdate");Check(coins.text=="COINS  65","Closed shop HUD updates after reward");
        var dead=Spawn(new Vector3(6,-3));float san=player.SAN;dead.TakeDamage(10000,player);dead.Purify();
        Check(dead.IsDead && shop.CurrentBalance==65 && player.SAN==san-config.SANPenaltyForKillingCustomer,"Combat death gives no coins and retains SAN penalty");
        Set(config,"customerPurificationCoins",0);config.Validate();Spawn(new Vector3(7,-3)).Purify();Check(shop.CurrentBalance==65,"Zero reward disables payouts");
        Set(config,"customerPurificationCoins",-10);config.Validate();Check(config.CustomerPurificationCoins==0,"Negative reward clamps to zero");
        Set(config,"customerPurificationCoins",75);config.Validate();shop.SetCurrentBalance(0);
        var a=Spawn(new Vector3(8,4));var b=Spawn(new Vector3(8.5f,4));a.gameObject.AddComponent<CircleCollider2D>();
        var zombie=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemyHealth>("Assets/Prototype/Prefabs/Enemy.prefab"),new Vector3(8,4.5f),Quaternion.identity);
        zombie.GetComponent<EnemySimpleAI>().enabled=false;
        var shot=Object.Instantiate(AssetDatabase.LoadAssetAtPath<FoodProjectile>("Assets/Prototype/Prefabs/FoodProjectile.prefab"),new Vector3(8,4,-.2f),Quaternion.identity);
        Physics2D.SyncTransforms();shot.Impact();shot.Impact();
        Check(a.IsPurified && b.IsPurified && zombie.IsDead && shop.CurrentBalance==150,"Real food AoE rewards each Customer once, including multiple colliders; Zombie gives no coins");
        Tick(hud,"LateUpdate");Check(coins.text=="COINS  150","HUD matches accumulated AoE rewards");
        var grid=Object.FindAnyObjectByType<TruckGridLayout>();player.transform.position=new Vector3(1,-.2f);player.GetComponent<Rigidbody2D>().position=player.transform.position;
        Physics2D.SyncTransforms();typeof(FoodTruckInteriorTrigger).GetMethod("OnTriggerEnter2D",Private).Invoke(grid.Interior,new object[]{player.GetComponent<Collider2D>()});
        ShopProductDefinition waiting=null;foreach(var p in shop.Catalog.Products)if(p.Placement==ShopProductDefinition.PlacementKind.RearWaitingArea)waiting=p;
        Check(shop.OpenShop() && shop.CanPurchase(waiting,out _),"Waiting-area upgrade is available without resource gating");
        Tick(hud,"LateUpdate");Check(coins.text=="COINS  150" && Time.timeScale==0,"Coins HUD remains correct with paused shop");
        Check(shop.TryBeginPurchase(waiting) && shop.CurrentBalance==150 && shop.HasPendingPurchase,"Earned coins reserve waiting-area purchase without spending");
        var layout=player.GetComponent<PlayerKitchenLayout>();Set(player,"<Facing>k__BackingField",Vector2.down);Tick(layout,"Update");
        Check(layout.TryInteract() && shop.CurrentBalance==150 && Object.FindAnyObjectByType<CustomerWaitingArea>()!=null,"Upgrade installation preserves earned coins");
        Tick(hud,"LateUpdate");Check(coins.text=="COINS  150","Free upgrade leaves HUD coins intact");
        var area=Object.FindAnyObjectByType<CustomerWaitingArea>();var carry=player.GetComponent<PlayerCarry>();
        var seated=Spawn(player.transform.position+Vector3.right*.5f);Check(carry.TryGrabCustomer(),"Customer can still be carried for waiting area");
        player.transform.position=area.transform.position+Vector3.right*2.1f;player.GetComponent<Rigidbody2D>().position=player.transform.position;Physics2D.SyncTransforms();
        Check(area.TryAccept(seated,carry,out _),"Customer can still be seated");seated.Purify();
        Check(area.OccupiedCount==0 && shop.CurrentBalance==225 && carry.CurrentHeldCustomer==null,"Waiting Customer purification frees seat and pays exactly once");
        shop.SetCurrentBalance(int.MaxValue-5);shop.AddCoins(75);Check(shop.CurrentBalance==int.MaxValue,"Coin addition cannot overflow into negative balance");
        shop.AddCoins(-10);Check(shop.CurrentBalance==int.MaxValue,"Nonpositive coin grants cannot spend money");
        shop.SetCurrentBalance(321);hud.enabled=false;hud.enabled=true;Tick(hud,"LateUpdate");
        Check(coins.text=="COINS  321" && shared.CustomerPurificationCoins==25 && shared.InitialCoins==500,"HUD re-enable/debug edits work without changing initial configuration");
        foreach(var customer in Object.FindObjectsByType<Customer>())Object.Destroy(customer.gameObject);
        foreach(var item in Object.FindObjectsByType<IngredientPickup>())Object.Destroy(item.gameObject);
        yield return new WaitForSeconds(1.2f);Tick(hud,"LateUpdate");Capture();
    }
    static Customer Spawn(Vector3 position)
    {
        var c=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),position,Quaternion.identity);
        Set(c,"settings",config);c.GetComponent<EnemySimpleAI>().enabled=false;return c;
    }
    static void Capture()
    {
        var camera=Camera.main;camera.GetComponent<SimpleCameraShake>().enabled=false;var canvas=hud.GetComponentInParent<Canvas>();
        canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.overrideSorting=true;canvas.sortingOrder=100;
        var render=new RenderTexture(1280,720,24);camera.targetTexture=render;Canvas.ForceUpdateCanvases();camera.Render();RenderTexture.active=render;
        var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("COINS_PREVIEW.png",pixels.EncodeToPNG());
        RenderTexture.active=null;camera.targetTexture=null;render.Release();Object.Destroy(pixels);Object.Destroy(render);
    }
    static void Tick(object o,string method)=>o.GetType().GetMethod(method,Private).Invoke(o,null);
    static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private).GetValue(o);
    static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private).SetValue(o,value);
    static void Check(bool value,string name){if(!value)throw new Exception(name);results.Add("PASS "+name);}
    static void Fail(Exception e){Time.timeScale=1;Debug.LogException(e);File.WriteAllText("COINS_RESULTS.txt","FAIL\n"+e+"\n"+string.Join("\n",results));EditorApplication.Exit(1);}
}
