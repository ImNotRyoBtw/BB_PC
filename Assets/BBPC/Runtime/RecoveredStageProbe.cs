using System;using System.IO;using System.Collections;using UnityEngine;
namespace BBPC.UnityRuntime
{
    public static class RecoveredStageProbe
    {
        public static IEnumerator Run(RecoveredFrontend host,string output)
        {
            Directory.CreateDirectory(output);var list=SongCatalog.Read<RecoveredStageRegressionList>(Resources.Load<TextAsset>("RemoteStages606/regressions").text);
            foreach(var entry in list.entries)
            {
                var result=new RecoveredRemoteStageValidation.Result();string directory=Path.Combine(output,entry.sid);
                yield return RecoveredRemoteStageValidation.Run(host,entry.resource,directory,result);
                File.WriteAllText(Path.Combine(directory,"stage-results.json"),JsonUtility.ToJson(result,true));
                if(!result.success){Application.Quit(2);yield break;}
            }
            Application.Quit(0);
        }
    }
}
