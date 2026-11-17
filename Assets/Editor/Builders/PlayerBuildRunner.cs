using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Builders
{
    /// <summary>
    /// Windows 64비트 플레이어 빌드
    /// - Build    : 릴리즈 빌드. 출력 Builds/{버전}/2CTRL.exe
    /// - BuildDemo: 데모 영상 촬영용. DEMO_INVINCIBLE 정의로 플레이어가 죽지 않는다. 출력 Builds/{버전}_demo/2CTRL.exe
    /// 버전은 PlayerSettings.bundleVersion 을 쓴다. 데모는 빌드 동안만 버전 뒤에 _demo 를 붙인다
    /// </summary>
    public static class PlayerBuildRunner
    {
        private const string ExeName = "2CTRL.exe";

        public static string Build()
        {
            string version = PlayerSettings.bundleVersion;
            return Run(version, null);
        }

        public static string BuildDemo()
        {
            string version = PlayerSettings.bundleVersion;
            string demoVersion = version + "_demo";
            PlayerSettings.bundleVersion = demoVersion;
            try
            {
                return Run(demoVersion, new[] { "DEMO_INVINCIBLE" });
            }
            finally
            {
                PlayerSettings.bundleVersion = version;
                AssetDatabase.SaveAssets();
            }
        }

        private static string Run(string _folder, string[] _defines)
        {
            string outDir = Path.Combine("Builds", _folder);
            if (true == Directory.Exists(outDir))
            {
                Directory.Delete(outDir, true);
            }

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => true == s.enabled).Select(s => s.path).ToArray(),
                locationPathName = Path.Combine(outDir, ExeName),
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
                extraScriptingDefines = _defines,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;
            string result = $"{summary.result} {_folder} {summary.totalSize / (1024 * 1024)}MB errors={summary.totalErrors} -> {Path.GetFullPath(outDir)}";
            Debug.Log("[PlayerBuildRunner] " + result);
            if (BuildResult.Succeeded != summary.result)
            {
                throw new System.Exception("[PlayerBuildRunner] 빌드 실패: " + result);
            }
            return result;
        }
    }
}
