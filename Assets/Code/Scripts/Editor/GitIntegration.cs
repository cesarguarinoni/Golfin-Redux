using UnityEngine;  
using UnityEditor;  
using System.Diagnostics;  
  
public class GitIntegration  
{  
    [MenuItem(\"Tools/GOLFIN/Commit and Push Changes\")]  
    public static void CommitAndPush()  
    {  
        CommitChanges(\"Automated Unity script updates\");  
    }  
  
    public static void CommitChanges(string message)  
    {  
        Debug.Log(\"[Git] Starting commit process...\");  
        var process = new Process();  
        process.StartInfo.FileName = \"git\";  
        process.StartInfo.Arguments = \"add .\";  
        process.StartInfo.WorkingDirectory = Application.dataPath + \"/../\";  
        process.StartInfo.UseShellExecute = false;  
        process.Start();  
        process.WaitForExit();  
        Debug.Log(\"[Git] Changes added to staging\");  
    }  
} 
