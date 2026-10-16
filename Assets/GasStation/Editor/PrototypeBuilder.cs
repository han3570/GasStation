using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GasStation.Editor
{
    public static class PrototypeBuilder
    {
        const string ScenePath="Assets/Scenes/GasStationPrototype.unity";
        const string AndroidBuildPending="GasStation.AndroidBuildPending";

        [InitializeOnLoadMethod]
        static void ResumeAndroidBuild()
        {
            if (!UnityEditor.SessionState.GetBool(AndroidBuildPending, false)) return;
            UnityEditor.SessionState.SetBool(AndroidBuildPending, false);
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
                EditorApplication.delayCall += BuildAndroid;
        }

        [MenuItem("Gas Station/Build Android APK")]
        public static void BuildAndroid()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new System.InvalidOperationException("Android Build Support is not installed for this Unity editor.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                // Resume after Android's editor assemblies have finished reloading.
                UnityEditor.SessionState.SetBool(AndroidBuildPending, true);
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                {
                    UnityEditor.SessionState.SetBool(AndroidBuildPending, false);
                    throw new System.InvalidOperationException("Could not switch to Android.");
                }
                return;
            }
            if (!File.Exists(ScenePath)) Prepare();
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.pressurelab.pressurestation");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64 | AndroidArchitecture.ARMv7;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.SplashScreen.show = false;
            EditorUserBuildSettings.buildAppBundle = false;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("Builds/Android");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = new[] { ScenePath }, locationPathName = "Builds/Android/PressureStation.apk",
                target = BuildTarget.Android, options = BuildOptions.Development
            });
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/android-build-summary.txt", $"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nOutput: {report.summary.outputPath}\n");
            if (report.summary.result != BuildResult.Succeeded)
                throw new System.Exception("Android build failed: " + report.summary.result);
            Debug.Log("Pressure Station Android APK build succeeded.");
        }
        [MenuItem("Gas Station/Prepare Prototype Scene")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/GasStation/Resources");
            var settings=AssetDatabase.LoadAssetAtPath<PrototypeSettings>("Assets/GasStation/Resources/PrototypeSettings.asset");
            if(settings==null)
            {
                settings=ScriptableObject.CreateInstance<PrototypeSettings>();
                AssetDatabase.CreateAsset(settings,"Assets/GasStation/Resources/PrototypeSettings.asset");
            }
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var camera=new GameObject("Main Camera",typeof(Camera),typeof(AudioListener));
            camera.tag="MainCamera";
            var view=camera.GetComponent<Camera>(); view.clearFlags=CameraClearFlags.SolidColor; view.backgroundColor=new Color(.047f,.133f,.133f); view.orthographic=true;
            var game=new GameObject("Gas Station Prototype",typeof(GameSessionManager));
            game.GetComponent<GameSessionManager>().settings=settings;
            EditorSceneManager.SaveScene(scene,ScenePath);
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            PlayerSettings.companyName="Pressure Lab";
            PlayerSettings.productName="Pressure Station";
            PlayerSettings.defaultScreenWidth=540;
            PlayerSettings.defaultScreenHeight=960;
            PlayerSettings.defaultIsNativeResolution=false;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
            PlayerSettings.resizableWindow=true;
            PlayerSettings.defaultInterfaceOrientation=UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait=true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
            PlayerSettings.allowedAutorotateToLandscapeLeft=false;
            PlayerSettings.allowedAutorotateToLandscapeRight=false;
            AssetDatabase.SaveAssets();
            Debug.Log("Gas Station prototype scene prepared: "+ScenePath);
        }
        [MenuItem("Gas Station/Build Windows Prototype")]
        public static void BuildWindows()
        {
            if(!File.Exists(ScenePath)) Prepare();
            PlayerSettings.SplashScreen.show=false;
            Directory.CreateDirectory("Builds/Windows");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{ScenePath}, locationPathName="Builds/Windows/PressureStation.exe",
                target=BuildTarget.StandaloneWindows64, options=BuildOptions.Development
            });
            Directory.CreateDirectory("Verification");
            File.WriteAllText("Verification/build-summary.txt",$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nOutput: {report.summary.outputPath}\n");
            if(report.summary.result!=BuildResult.Succeeded) throw new System.Exception("Windows build failed: "+report.summary.result);
            Debug.Log("Pressure Station Windows build succeeded.");
        }
    }
}
