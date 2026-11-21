using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;
namespace BBPC.UnityRuntime
{
    [Serializable] public sealed class RecoveredPreparationTrace
    {
        [Serializable] public sealed class Step {public string phase;public double seconds;}
        public string sid,aid,status="running",phase,error;public List<Step> steps=new List<Step>();
        private readonly Stopwatch watch=Stopwatch.StartNew();
        public RecoveredPreparationTrace(string sid,string aid){this.sid=sid;this.aid=aid;}
        public void Mark(string value){phase=value;steps.Add(new Step{phase=value,seconds=watch.Elapsed.TotalSeconds});Save();}
        public void Finish(string value,Exception failure=null){status=value;error=failure?.ToString();Mark(value);}
        private void Save()
        {
            string folder=Path.Combine(RecoveredStorage.Root,"BBPC.Content606/preparation");
            try{Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"last.json"),JsonUtility.ToJson(this,true));}
            catch(IOException failure){UnityEngine.Debug.LogException(failure);}
        }
        // Unity doesn't propagate a nested coroutine exception to the caller. Drain nested enumerators
        // explicitly so a failed Open/Ready callback cannot abandon the loading overlay forever.
        public static IEnumerator Guard(IEnumerator routine,Action<Exception> failed,Func<bool> cancelled,double timeoutSeconds=300)
        {
            var pending=new Stack<IEnumerator>();pending.Push(routine);var elapsed=Stopwatch.StartNew();
            try
            {
                while(pending.Count>0)
                {
                    if(cancelled())yield break;
                    if(elapsed.Elapsed.TotalSeconds>=timeoutSeconds){failed(new TimeoutException("Content preparation exceeded "+timeoutSeconds+" seconds"));yield break;}
                    var top=pending.Peek();bool moved=false;object current=null;Exception error=null;
                    try{moved=top.MoveNext();if(moved)current=top.Current;}
                    catch(Exception failure){error=failure;}
                    if(error!=null){failed(error);yield break;}
                    if(!moved)
                    {
                        pending.Pop();try{(top as IDisposable)?.Dispose();}catch(Exception failure){error=failure;}
                        if(error!=null){failed(error);yield break;}continue;
                    }
                    if(current is IEnumerator nested){pending.Push(nested);continue;}
                    if(current is AsyncOperation operation)
                    {
                        pending.Push(WaitForUnity(operation,elapsed,failed,cancelled,timeoutSeconds));
                    }
                    else yield return current;
                }
            }
            finally
            {
                while(pending.Count>0)
                {
                    try{(pending.Pop() as IDisposable)?.Dispose();}
                    catch(Exception failure){failed(failure);}
                }
            }
        }
        // Keep native Unity property access outside the managed-only guard tests.
        private static IEnumerator WaitForUnity(AsyncOperation operation,Stopwatch elapsed,Action<Exception> failed,Func<bool> cancelled,double timeout)
        {
            while(!operation.isDone)
            {
                if(cancelled())yield break;
                if(elapsed.Elapsed.TotalSeconds>=timeout){failed(new TimeoutException("Pending "+operation.GetType().Name));yield break;}
                yield return null;
            }
        }
    }
}
