using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditorInternal;

// Offline analysis of the separate diagnostic capture, not the scored 30-second matrix.
public static class NpcProfilerAnalysis
{
    struct Parent { public string name; public int end; public double time, children; }
    public static void Run()
    {
        CultureInfo.CurrentCulture=CultureInfo.InvariantCulture;
        string root=Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath,"../../Results"));
        foreach(string directory in Directory.GetDirectories(root))
        {
            string path=Path.Combine(directory,"200-mixed.raw");if(!File.Exists(path)||new FileInfo(path).Length==0)continue;
            string gcReport=Path.Combine(directory,"diagnostic-gc-parents.csv");
            if(Environment.GetEnvironmentVariable("NPC_REANALYZE")!="1"&&File.Exists(gcReport)&&File.Exists(Path.Combine(directory,"diagnostic-cpu-self.csv"))&&File.GetLastWriteTimeUtc(gcReport)>=File.GetLastWriteTimeUtc(path))continue;
            if(!ProfilerDriver.LoadProfile(path,false))throw new Exception("Unable to read profiler capture "+path);
            var names=new HashSet<string>{"NPC AI","NPC Separation","NPC Waiting Barrier","NPC HUD","NPC World Health Bar","NPC Spawner","Physics2D.Simulate","Instantiate","Destroy","GC.Collect","LogStringToConsole","PlayerLoop","EditorLoop","Profiler.WriteLog"};
            var samples=new Dictionary<string,List<double>>();var calls=new Dictionary<string,long>();foreach(string name in names){samples[name]=new List<double>();calls[name]=0;}
            var markerNames=new Dictionary<int,string>();int frameCount=0;
            var gcParents=new Dictionary<string,long>();var selfTimes=new Dictionary<string,double>();long gcBytes=0;
            var parents=new List<Parent>(128);
            for(int index=ProfilerDriver.firstFrameIndex;index<=ProfilerDriver.lastFrameIndex;index++)
            {
                using(var frame=ProfilerDriver.GetRawFrameDataView(index,0))
                {
                    if(!frame.valid||frame.threadName!="Main Thread")continue;
                    parents.Clear();
                    var totals=new Dictionary<string,double>();
                    for(int sample=0;sample<frame.sampleCount;sample++)
                    {
                        int id=frame.GetSampleMarkerId(sample);
                        if(!markerNames.TryGetValue(id,out string name)){name=frame.GetSampleName(sample);markerNames[id]=name;}
                        while(parents.Count>0&&parents[parents.Count-1].end<sample)FinishParent(parents,selfTimes);
                        double duration=frame.GetSampleTimeMs(sample);
                        string parentName=parents.Count>0?parents[parents.Count-1].name:"<root>";
                        if(name=="GC.Alloc"&&frame.GetSampleMetadataCount(sample)>0)
                        {
                            long bytes=frame.GetSampleMetadataAsLong(sample,0);gcBytes+=bytes;
                            gcParents.TryGetValue(parentName,out long totalBytes);gcParents[parentName]=totalBytes+bytes;
                        }
                        if(parents.Count>0){var parent=parents[parents.Count-1];parent.children+=duration;parents[parents.Count-1]=parent;}
                        parents.Add(new Parent{name=name,end=sample+frame.GetSampleChildrenCountRecursive(sample),time=duration});
                        if(!names.Contains(name))continue;
                        totals.TryGetValue(name,out double time);totals[name]=time+duration;calls[name]++;
                    }
                    while(parents.Count>0)FinishParent(parents,selfTimes);
                    foreach(string name in names){totals.TryGetValue(name,out double time);samples[name].Add(time);}frameCount++;
                }
            }
            var output=new StringBuilder("marker,frames,calls,inclusive_mean_ms_per_frame,p95_ms,p99_ms,max_ms\n");
            foreach(var pair in samples)
            {
                var times=pair.Value;times.Sort();if(times.Count==0)continue;double total=0;foreach(double value in times)total+=value;
                output.AppendLine(FormattableString.Invariant($"{pair.Key},{times.Count},{calls[pair.Key]},{total/times.Count:F6},{Percentile(times,.95):F6},{Percentile(times,.99):F6},{times[times.Count-1]:F6}"));
            }
            File.WriteAllText(Path.Combine(directory,"diagnostic-markers.csv"),output.ToString());
            File.WriteAllText(Path.Combine(directory,"diagnostic-analysis.txt"),"Main-thread frames read: "+frameCount+"\nMarker times are inclusive; nested markers must not be added together. Binary capture overhead is excluded from scored matrix sampling.\n");
            var allocations=new List<KeyValuePair<string,long>>(gcParents);allocations.Sort((a,b)=>b.Value.CompareTo(a.Value));
            var allocationCsv=new StringBuilder("parent,bytes,bytes_per_frame\n");foreach(var pair in allocations)allocationCsv.AppendLine("\""+pair.Key.Replace("\"","\"\"")+"\","+pair.Value+","+((double)pair.Value/Math.Max(1,frameCount)).ToString("F1",CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(directory,"diagnostic-gc-parents.csv"),allocationCsv.ToString());
            var cpu=new List<KeyValuePair<string,double>>(selfTimes);cpu.Sort((a,b)=>b.Value.CompareTo(a.Value));
            var cpuCsv=new StringBuilder("marker,self_ms,self_mean_ms_per_frame\n");foreach(var pair in cpu)cpuCsv.AppendLine("\""+pair.Key.Replace("\"","\"\"")+"\","+pair.Value.ToString("F6",CultureInfo.InvariantCulture)+","+(pair.Value/Math.Max(1,frameCount)).ToString("F6",CultureInfo.InvariantCulture));
            File.WriteAllText(Path.Combine(directory,"diagnostic-cpu-self.csv"),cpuCsv.ToString());
            ProfilerDriver.ClearAllFrames();
        }
        UnityEngine.Profiling.Profiler.enabled=false;
        GC.Collect();GC.WaitForPendingFinalizers();
        UnityEditor.EditorApplication.delayCall+=()=>UnityEditor.EditorApplication.Exit(0);
    }
    static void FinishParent(List<Parent> parents,Dictionary<string,double> selfTimes)
    {
        var parent=parents[parents.Count-1];parents.RemoveAt(parents.Count-1);selfTimes.TryGetValue(parent.name,out double time);selfTimes[parent.name]=time+Math.Max(0,parent.time-parent.children);
    }
    static double Percentile(List<double> times,double p)=>times[Math.Max(0,(int)Math.Ceiling(times.Count*p)-1)];
}
