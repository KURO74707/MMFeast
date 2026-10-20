using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

// Benchmark fixture only: copied into an isolated project's Assets, never into the game.
public class NpcPerformanceRunner : MonoBehaviour
{
    public GlobalGameSettings sourceSettings;
    public EnemySimpleAI zombiePrefab;
    public Customer customerPrefab;
    public CustomerWaitingArea waitingPrefab;
    public FoodProjectile projectilePrefab;
    public string variant;
    public float sampleSeconds = 30f;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    GlobalGameSettings settings;
    EnemySpawner spawner;
    PlayerController player;
    FoodTruckHealth truck;
    CustomerWaitingAreaMount mount;
    CustomerWaitingArea area;
    Transform[] points;
    string output;
    readonly List<Frame> frames = new List<Frame>(65536);
    readonly List<Meter> meters = new List<Meter>();
    readonly StringBuilder summary = new StringBuilder();
    readonly List<EnemySimpleAI> actors = new List<EnemySimpleAI>();
    readonly List<Customer> humans = new List<Customer>();
    readonly List<EnemyHealth> zombies = new List<EnemyHealth>();
    struct Frame { public int phase; public double wall, main; public long gc; public double physics, ai, ui, create, destroy, collect, log; }
    class Meter { public string key, name; public ProfilerRecorder recorder; }
    readonly Dictionary<string, string[]> meterNames = new Dictionary<string, string[]>
    {
        {"main",new[]{"Main Thread"}}, {"gc",new[]{"GC Allocated In Frame"}},
        {"physics",new[]{"Physics2D.Simulate"}}, {"ai",new[]{"EnemySimpleAI.FixedUpdate()","EnemySimpleAI.FixedUpdate"}},
        {"ui",new[]{"Canvas.BuildBatch"}}, {"create",new[]{"Object.Instantiate","Instantiate"}},
        {"destroy",new[]{"Destroy"}}, {"collect",new[]{"GC.Collect"}}, {"log",new[]{"Debug.Log","Debug.LogStringToConsole"}}
    };
    IEnumerator Start()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        output = Path.GetFullPath(Path.Combine(Application.dataPath,Application.isEditor?"../../Results":"../../../Results",variant + (Application.isEditor?"-Editor":"-Player")));
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"status.txt"),"Starting");
        Application.runInBackground = true; Application.targetFrameRate = -1; QualitySettings.vSyncCount = 0;
        Profiler.enabled = true;
        Profiler.SetAreaEnabled(ProfilerArea.CPU, true);
        Screen.SetResolution(1280,720,false);
        File.WriteAllText(Path.Combine(output,"environment.txt"),"Unity="+Application.unityVersion+"\nEditor="+Application.isEditor+"\nGPU="+SystemInfo.graphicsDeviceName+"\nAPI="+SystemInfo.graphicsDeviceType+"\nCPU="+SystemInfo.processorType+"\nCores="+SystemInfo.processorCount+"\nResolution=1280x720\nVSync=0\nTargetFrameRate=-1\nWarmup=3 seconds per group\nSampling=30 seconds per population/composition group, four equal phases\nBinary profiler capture is separate from the measured matrix.\n");
        settings = Instantiate(sourceSettings);
        Set(settings,"playerMaxHP",1000000000f); Set(settings,"foodTruckMaxHP",1000000000f);
        Set(settings,"spawnInterval",.1f); Set(settings,"maximumAliveEnemies",200);
        // All runtime edits are on a transient clone, never on the shared asset.
        foreach(var component in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
        {
            var field = component.GetType().GetField("settings",Private);
            if(field != null) field.SetValue(component,settings);
        }
        player=FindAnyObjectByType<PlayerController>(); truck=FindAnyObjectByType<FoodTruckHealth>();
        player.SetCurrentHP(settings.PlayerMaxHP);Set(truck,"currentHP",settings.FoodTruckMaxHP);
        player.transform.position=Vector3.zero;player.GetComponent<Rigidbody2D>().position=Vector2.zero;
        spawner=FindAnyObjectByType<EnemySpawner>();spawner.enabled=false;mount=FindAnyObjectByType<CustomerWaitingAreaMount>();
        foreach(var npc in FindObjectsByType<EnemySimpleAI>())Destroy(npc.gameObject);
        points=new Transform[16];
        for(int i=0;i<points.Length;i++){points[i]=new GameObject("Benchmark spawn point").transform;float angle=i*Mathf.PI*2/points.Length;points[i].position=new Vector3(Mathf.Cos(angle)*4.2f,Mathf.Sin(angle)*3.6f);}
        yield return null;
        summary.AppendLine("count,composition,phase,seconds,frames,wall_mean_ms,wall_p95_ms,wall_p99_ms,wall_max_ms,main_mean_ms,main_p95_ms,main_p99_ms,main_max_ms,gc_bytes_per_frame,gc_bytes_total,physics_mean_ms,ai_mean_ms,canvas_mean_ms,instantiate_mean_ms,destroy_mean_ms,gc_collect_mean_ms,log_mean_ms");
        // Each population/composition gets 30 measured seconds: four consecutive 7.5-second phases.
        // This is a combined stress protocol, not 30 seconds of independent sampling per phase.
        foreach(int count in new[]{20,100,200})
        foreach(string composition in new[]{"zombies","customers","mixed"})
        {
            DisposeMeters();
            if(area!=null)Destroy(area.gameObject);
            foreach(var npc in FindObjectsByType<EnemySimpleAI>())Destroy(npc.gameObject);
            foreach(var pickup in FindObjectsByType<IngredientPickup>())Destroy(pickup.gameObject);
            yield return null;
            ((IList)Get(spawner,"aliveEnemies")).Clear();Set(spawner,"spawnedCount",0);Set(spawner,"hasSpawned",false);
            Set(settings,"maximumAliveEnemies",count);
            spawner.Configure(composition=="customers"?customerPrefab.GetComponent<EnemySimpleAI>():zombiePrefab,player.transform,points);
            spawner.ConfigureCustomers(composition=="zombies"?null:customerPrefab);spawner.ConfigureFoodTruck(truck);
            UnityEngine.Random.InitState(7241+count);
            for(int i=0;i<count;i++){Set(spawner,"lastSpawnTime",float.NegativeInfinity);Tick(spawner,"Update");}
            spawner.enabled=true;
            double warmEnd=Time.realtimeSinceStartupAsDouble+3;
            while(Time.realtimeSinceStartupAsDouble<warmEnd)yield return null;
            StartMeters();
            frames.Clear();double start=Time.realtimeSinceStartupAsDouble;double nextSpawn=.25,nextBurst=.25;int pendingSpawns=0;
            int previousPhase=-1;
            while(Time.realtimeSinceStartupAsDouble-start<sampleSeconds)
            {
                double elapsed=Time.realtimeSinceStartupAsDouble-start;
                int phase=Math.Min(3,(int)(elapsed/(sampleSeconds/4)));
                if(pendingSpawns>0)
                {
                    for(int i=0;i<pendingSpawns;i++){Set(spawner,"lastSpawnTime",float.NegativeInfinity);Tick(spawner,"Update");}
                    pendingSpawns=0;
                }
                if(phase!=previousPhase)
                {
                    previousPhase=phase;RefreshActors();
                    if(phase==3){area=mount.CreatePreview(waitingPrefab,settings);mount.PositionPreview(area,Vector2.down);if(!mount.TryInstallPreview(area))throw new Exception("Waiting area installation failed");}
                }
                // Frame represents work already completed in the previous frame.
                if(frames.Count>0 || elapsed>.02)
                    frames.Add(new Frame{phase=phase,wall=Time.unscaledDeltaTime*1000d,main=Read("main")/1000000d,gc=Read("gc"),physics=Read("physics")/1000000d,ai=Read("ai")/1000000d,ui=Read("ui")/1000000d,create=Read("create")/1000000d,destroy=Read("destroy")/1000000d,collect=Read("collect")/1000000d,log=Read("log")/1000000d});
                if(phase==1&&elapsed>=sampleSeconds/4+nextSpawn)
                {
                    nextSpawn+=.5;RefreshActors();int amount=Math.Max(1,count/20);
                    using(new ProfilerMarker("NPC benchmark spawn cycle").Auto())
                    {
                        for(int i=0;i<amount&&i<actors.Count;i++)Destroy(actors[i].gameObject);
                        // Exercise the actual spawner, including its live-count maintenance.
                        pendingSpawns=amount;
                    }
                }
                if(phase==2&&elapsed>=sampleSeconds/2+nextBurst)
                {
                    nextBurst+=1;RefreshActors();
                    using(new ProfilerMarker("NPC benchmark burst deaths and purification").Auto())
                    {
                        int amount=Math.Max(1,count/10);
                        for(int i=0;i<amount&&i<zombies.Count;i++)zombies[i].TakeDamage(100000);
                        for(int i=0;i<amount&&i<humans.Count;i++)humans[i].Purify();
                        // Keep the target population up while the real spawner replaces retired entities.
                        for(int i=0;i<amount*2;i++){Set(spawner,"lastSpawnTime",float.NegativeInfinity);Tick(spawner,"Update");}
                    }
                }
                yield return null;
            }
            spawner.enabled=false;
            Save(count,composition);
            File.WriteAllText(Path.Combine(output,"status.txt"),count+" "+composition+" completed");
        }
        // Capture a separate diagnostic window so binary-log I/O does not distort the matrix.
        File.WriteAllText(Path.Combine(output,"status.txt"),"Capturing diagnostic profiler trace");
        Profiler.enabled=false;Profiler.logFile=Path.Combine(output,"200-mixed.raw");Profiler.enableBinaryLog=true;Profiler.enabled=true;spawner.enabled=true;
        double traceEnd=Time.realtimeSinceStartupAsDouble+5;double traceEvent=0;
        while(Time.realtimeSinceStartupAsDouble<traceEnd)
        {
            if(Time.realtimeSinceStartupAsDouble>=traceEvent)
            {
                traceEvent=Time.realtimeSinceStartupAsDouble+1;RefreshActors();
                using(new ProfilerMarker("NPC benchmark diagnostic burst").Auto())
                {
                    for(int i=0;i<10&&i<zombies.Count;i++)zombies[i].TakeDamage(100000);
                    for(int i=0;i<10&&i<humans.Count;i++)humans[i].Purify();
                    for(int i=0;i<20;i++){Set(spawner,"lastSpawnTime",float.NegativeInfinity);Tick(spawner,"Update");}
                }
            }
            yield return null;
        }
        DisposeMeters();Profiler.enabled=false;Profiler.enableBinaryLog=false;File.WriteAllText(Path.Combine(output,"summary.csv"),summary.ToString());
        ScreenCapture.CaptureScreenshot(Path.Combine(output,"200-mixed.png"));
        yield return null; yield return null;
        File.WriteAllText(Path.Combine(output,"status.txt"),"COMPLETE");
        #if UNITY_EDITOR
        UnityEditor.EditorApplication.Exit(0);
        #else
        Application.Quit(0);
        #endif
    }
    void RefreshActors()
    {
        actors.Clear();humans.Clear();zombies.Clear();
        foreach(var npc in FindObjectsByType<EnemySimpleAI>())
        {
            var human=npc.GetComponent<Customer>();var zombie=npc.GetComponent<EnemyHealth>();
            if(human!=null&&(human.IsDead||human.IsPurified))continue;if(zombie!=null&&zombie.IsDead)continue;
            actors.Add(npc);if(human!=null)humans.Add(human);if(zombie!=null)zombies.Add(zombie);
        }
    }
    void StartMeters()
    {
        var handles=new List<ProfilerRecorderHandle>();ProfilerRecorderHandle.GetAvailable(handles);
        var available=new StringBuilder();foreach(var h in handles){var d=ProfilerRecorderHandle.GetDescription(h);available.AppendLine(d.Category+" | "+d.Name+" | "+d.UnitType);}
        File.WriteAllText(Path.Combine(output,"available-profiler-counters.txt"),available.ToString());
        foreach(var pair in meterNames)
        {
            foreach(var name in pair.Value)
            {
                bool found=false;
                foreach(var handle in handles)
                {
                    var d=ProfilerRecorderHandle.GetDescription(handle);if(d.Name!=name)continue;
                    var meter=new Meter{key=pair.Key,name=d.Name,recorder=ProfilerRecorder.StartNew(d.Category,d.Name,1)};
                    if(meter.recorder.Valid){meters.Add(meter);found=true;}break;
                }
                if(found)break;
            }
        }
        var names=new StringBuilder();foreach(var m in meters)names.AppendLine(m.key+"="+m.name);File.WriteAllText(Path.Combine(output,"used-counters.txt"),names.ToString());
        if(!meters.Exists(m=>m.key=="main")||!meters.Exists(m=>m.key=="gc"))throw new Exception("Main Thread and GC Allocated In Frame counters are required for this benchmark.");
    }
    long Read(string key){foreach(var m in meters)if(m.key==key)return m.recorder.LastValue;return 0;}
    void DisposeMeters(){foreach(var m in meters)m.recorder.Dispose();meters.Clear();}
    void Save(int count,string composition)
    {
        using(var file=new StreamWriter(Path.Combine(output,count+"-"+composition+".csv")))
        {
            file.WriteLine("phase,wall_ms,main_ms,gc_bytes,physics_ms,ai_ms,canvas_ms,instantiate_ms,destroy_ms,gc_collect_ms,log_ms");
            foreach(var f in frames)file.WriteLine(FormattableString.Invariant($"{f.phase},{f.wall:F6},{f.main:F6},{f.gc},{f.physics:F6},{f.ai:F6},{f.ui:F6},{f.create:F6},{f.destroy:F6},{f.collect:F6},{f.log:F6}"));
        }
        foreach(int phase in new[]{-1,0,1,2,3})
        {
            var walls=new List<double>();var mains=new List<double>();long gc=0;double physics=0,ai=0,ui=0,create=0,destroy=0,collect=0,log=0;
            foreach(var f in frames){if(phase!=-1&&f.phase!=phase)continue;walls.Add(f.wall);mains.Add(f.main);gc+=f.gc;physics+=f.physics;ai+=f.ai;ui+=f.ui;create+=f.create;destroy+=f.destroy;collect+=f.collect;log+=f.log;}
            walls.Sort();mains.Sort();int n=walls.Count;if(n==0)throw new Exception("No benchmark frames");
            double wallTotal=0,mainTotal=0;foreach(double v in walls)wallTotal+=v;foreach(double v in mains)mainTotal+=v;
            string phaseName=phase==-1?"combined":new[]{"crowd","spawn","burst","waiting"}[phase];
            summary.AppendLine(FormattableString.Invariant($"{count},{composition},{phaseName},{wallTotal/1000:F3},{n},{wallTotal/n:F6},{Percentile(walls,.95):F6},{Percentile(walls,.99):F6},{walls[n-1]:F6},{mainTotal/n:F6},{Percentile(mains,.95):F6},{Percentile(mains,.99):F6},{mains[n-1]:F6},{(double)gc/n:F1},{gc},{physics/n:F6},{ai/n:F6},{ui/n:F6},{create/n:F6},{destroy/n:F6},{collect/n:F6},{log/n:F6}"));
        }
        File.WriteAllText(Path.Combine(output,"summary.csv"),summary.ToString());
    }
    static double Percentile(List<double> values,double p)=>values[Math.Max(0,(int)Math.Ceiling(values.Count*p)-1)];
    static object Get(object obj,string name)=>obj.GetType().GetField(name,Private).GetValue(obj);
    static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
    static void Tick(object obj,string name)=>obj.GetType().GetMethod(name,Private).Invoke(obj,null);
    void OnDestroy()=>DisposeMeters();
}
