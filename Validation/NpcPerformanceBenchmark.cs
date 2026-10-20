using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Rendering;
using UnityEditor.Build;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class NpcPerformanceBenchmark
{
    internal static bool StripUnusedPostProcessing;
    const string ScenePath="Assets/Benchmarks/NpcBenchmark.unity";
    public static void Prepare()
    {
        var scene=EditorSceneManager.OpenScene(CookingPrototypeBuilder.ScenePath);
        foreach(var camera in UnityEngine.Object.FindObjectsByType<Camera>())
        {
            var data=camera.GetComponent<UniversalAdditionalCameraData>();
            if(data!=null&&(data.renderPostProcessing||data.antialiasing!=AntialiasingMode.None))
                throw new Exception("Benchmark shader stripping requires cameras without post-processing or post-process antialiasing.");
        }
        var obj=new GameObject("NPC performance fixture");var runner=obj.AddComponent<NpcPerformanceRunner>();
        runner.sourceSettings=AssetDatabase.LoadAssetAtPath<GlobalGameSettings>(GlobalDataPrototypeSetup.SettingsPath);
        runner.zombiePrefab=AssetDatabase.LoadAssetAtPath<EnemySimpleAI>("Assets/Prototype/Prefabs/Enemy.prefab");
        runner.customerPrefab=AssetDatabase.LoadAssetAtPath<Customer>("Assets/Prototype/Prefabs/Customer.prefab");
        runner.waitingPrefab=AssetDatabase.LoadAssetAtPath<CustomerWaitingArea>("Assets/Prototype/Prefabs/CustomerWaitingArea.prefab");
        runner.projectilePrefab=AssetDatabase.LoadAssetAtPath<FoodProjectile>("Assets/Prototype/Prefabs/FoodProjectile.prefab");
        runner.variant=new DirectoryInfo(Directory.GetCurrentDirectory()).Name;
        var quick=Environment.GetEnvironmentVariable("NPC_BENCH_SECONDS");if(!string.IsNullOrEmpty(quick))runner.sampleSeconds=float.Parse(quick,System.Globalization.CultureInfo.InvariantCulture);
        EditorSceneManager.SaveScene(scene,ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        AssetDatabase.SaveAssets();
    }
    public static void Build()
    {
        Prepare();
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64,false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64,new[]{GraphicsDeviceType.Direct3D11});
        StripUnusedPostProcessing=true;
        var result=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="Build/NpcBenchmark.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        StripUnusedPostProcessing=false;
        if(result.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Standalone benchmark build failed: "+result.summary.result);
    }
    public static void RunEditor(){Prepare();EditorApplication.EnterPlaymode();}
}

// Isolated benchmark builds only: these passes are not used by the prototype cameras.
public class NpcBenchmarkUnusedPostShaders : IPreprocessShaders
{
    public int callbackOrder=>int.MaxValue;
    public void OnProcessShader(Shader shader,ShaderSnippetData snippet,IList<ShaderCompilerData> variants)
    {
        if(!NpcPerformanceBenchmark.StripUnusedPostProcessing)return;
        if(shader.name=="Hidden/Universal Render Pipeline/UberPost"||shader.name=="Hidden/Universal Render Pipeline/FinalPost")variants.Clear();
    }
}
