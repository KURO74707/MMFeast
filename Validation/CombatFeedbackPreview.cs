using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class CombatFeedbackPreview
{
    static CombatFeedbackPreview(){EditorApplication.playModeStateChanged+=OnPlay;}
    public static void Run(){EditorSceneManager.OpenScene(CombatPrototypeBuilder.ScenePath);SessionState.SetBool("FeedbackPreview",true);EditorApplication.EnterPlaymode();}
    static void OnPlay(PlayModeStateChange s)
    {
        if(s!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool("FeedbackPreview",false))return;
        SessionState.SetBool("FeedbackPreview",false);new GameObject("PreviewRunner").AddComponent<CombatSanValidationRunner>().Begin(Render());
    }
    static IEnumerator Render()
    {
        foreach(var s in Object.FindObjectsByType<EnemySpawner>())s.enabled=false;
        foreach(var e in Object.FindObjectsByType<EnemySimpleAI>())Object.Destroy(e.gameObject);
        yield return null;
        var player=Object.FindFirstObjectByType<PlayerController>();player.transform.position=new Vector3(-.4f,-3.8f);player.GetComponent<Rigidbody2D>().simulated=false;
        var zombie=Object.Instantiate(AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab"),new Vector3(-1.4f,-3.8f),Quaternion.identity);
        zombie.enabled=false;zombie.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;zombie.SetTarget(player.transform);zombie.GetComponent<EnemyHealth>().TakeDamage(10);
        var customer=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab"),new Vector3(1.8f,-3.8f),Quaternion.identity);
        customer.GetComponent<EnemySimpleAI>().enabled=false;customer.GetComponent<Rigidbody2D>().constraints=RigidbodyConstraints2D.FreezeAll;customer.TakeDamage(20,player);
        player.SetCurrentHP(90);player.SetCurrentSAN(90);
        yield return new WaitForSeconds(.2f);
        zombie.GetType().GetMethod("FixedUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(zombie,null);
        var camera=Camera.main;camera.GetComponent<SimpleCameraShake>().enabled=false;camera.orthographicSize=5f;
        var hud=Object.FindFirstObjectByType<PlayerStatusUI>().GetComponentInParent<Canvas>();
        hud.renderMode=RenderMode.ScreenSpaceCamera;hud.worldCamera=camera;hud.planeDistance=1f;
        var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Canvas.ForceUpdateCanvases();
        yield return null;
        try
        {
            camera.Render();RenderTexture.active=target;var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);
            pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("COMBAT_FEEDBACK_PREVIEW.png",pixels.EncodeToPNG());
            Debug.Log("COMBAT_FEEDBACK_PREVIEW_PASS");RenderTexture.active=null;camera.targetTexture=null;Object.Destroy(pixels);target.Release();Object.Destroy(target);EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
