using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace GasStation.Editor
{
    public static class PrototypeValidation
    {
        static TestRunnerApi runner;
        [MenuItem("Gas Station/Run Gameplay Tests")]
        public static void RunTests()
        {
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Callbacks("gameplay",runner));
            runner.Execute(new ExecutionSettings(new Filter { assemblyNames=new[]{"GasStation.Tests"}, testMode=TestMode.EditMode }));
        }
        [MenuItem("Gas Station/Run Input Tests")]
        public static void RunInputTests()
        {
            runner=ScriptableObject.CreateInstance<TestRunnerApi>();
            runner.RegisterCallbacks(new Callbacks("input",runner));
            runner.Execute(new ExecutionSettings(new Filter { assemblyNames=new[]{"GasStation.PlayModeTests"}, testMode=TestMode.PlayMode }));
        }
        class Callbacks : ICallbacks
        {
            readonly string prefix;
            readonly TestRunnerApi owner;
            public Callbacks(string prefix,TestRunnerApi owner) { this.prefix=prefix; this.owner=owner; }
            public void RunStarted(ITestAdaptor tests) { Debug.Log("Gas Station gameplay tests started."); }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { if(result.TestStatus==TestStatus.Failed) Debug.LogError(result.Name+": "+result.Message); }
            public void RunFinished(ITestResultAdaptor result)
            {
                Directory.CreateDirectory("Verification");
                TestRunnerApi.SaveResultToFile(result,Path.GetFullPath("Verification/"+prefix+"-tests.xml"));
                File.WriteAllText("Verification/"+prefix+"-summary.txt",$"Status: {result.TestStatus}\nPassed: {result.PassCount}\nFailed: {result.FailCount}\nSkipped: {result.SkipCount}\n");
                Debug.Log($"Gas Station tests: {result.TestStatus}. Passed {result.PassCount}, failed {result.FailCount}.");
                // Test callbacks are shared across API instances; remove this one so a later
                // input test cannot overwrite the previously saved gameplay report.
                owner.UnregisterCallbacks(this);
                EditorApplication.delayCall += ()=>{
                    if(runner==owner) runner=null;
                    if(owner!=null) Object.DestroyImmediate(owner);
                };
            }
        }
        [MenuItem("Gas Station/Capture Game View")]
        public static void Capture()
        {
            if(!EditorApplication.isPlaying) { Debug.LogWarning("Start Play mode before capturing."); return; }
            Directory.CreateDirectory("Verification");
            var session=Object.FindAnyObjectByType<GameSessionManager>();
            string name=session!=null?session.Simulation.State.ToString().ToLowerInvariant():"game";
            ScreenCapture.CaptureScreenshot(Path.GetFullPath("Verification/"+name+".png"));
        }
    }
}
