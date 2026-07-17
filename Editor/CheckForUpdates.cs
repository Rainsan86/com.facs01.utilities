#if UNITY_EDITOR
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace FACS01.Utilities
{
    [InitializeOnLoad]
    internal static class CheckForUpdates
    {
        private const string RichToolName = Logger.ToolTag + "[FACS工具 - 检查更新]" + Logger.EndTag;

        static CheckForUpdates()
        {
            if (SessionState.GetBool("FACSUtilities_CheckUpdatesStartup", true))
            {
                SessionState.SetBool("FACSUtilities_CheckUpdatesStartup", false);
                if (PlayerPrefs.GetInt("FACSUtilities_CheckUpdates", 1) == 1) CheckForUpdatesTask();
            }
        }

        [MenuItem("FACS Utils/检查更新", false, 999)]
        private static void CheckForUpdatesManual()
        {
            CheckForUpdatesTask(true);
        }

        [MenuItem("FACS Utils/打开GitHub", false, 999)]
        private static void OpenFACSUtilitiesGitHub()
        {
            Application.OpenURL("https://github.com/FACS01-01/FACS_Utilities");
        }

        private static void CheckForUpdatesTask(bool manual = false)
        {
            var myVersion = PackageInfo.FindForAssembly(typeof(CheckForUpdates).Assembly).version;
            if (manual) PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 1);
            Task.Run(async ()=>
            {
                string latestVersion;
                using (var client = new HttpClient())
                {
                    client.Timeout = new(0, 0, 5);
                    client.DefaultRequestHeaders.CacheControl = new() { NoCache=true };
                    try
                    {
                        latestVersion = await client.GetStringAsync("https://raw.githubusercontent.com/FACS01-01/FACS_Utilities/main/version.txt");
                    }
                    catch (System.Exception e)
                    {
                        Logger.LogWarning($"{RichToolName} 无法检查更新。无网络连接？GitHub是否宕机？（当前版本：{myVersion}）\n{e.Message}");
                        return;
                    }
                }
                RunCFU(myVersion, latestVersion);
            });
        }

        private static void RunCFU(string myVersion, string latestVersion)
        {
            var hasBeta = myVersion.EndsWith("-beta");
            if (hasBeta) myVersion = myVersion[0..^5];
            string[] myVersionSplit = myVersion.Split('.');

            string[] latestVersions = latestVersion.Split('\n', System.StringSplitOptions.RemoveEmptyEntries);
            var latestIsBeta = latestVersions[0].EndsWith("-beta");
            string latestRelease; string latestBeta;
            if (latestIsBeta)
            {
                latestBeta = latestVersions[0][0..^5];
                latestRelease = latestVersions[1];
            }
            else
            {
                latestRelease = latestBeta = latestVersions[0];
            }

            var result = 0;
            string[] latestVersionSplit = hasBeta ? latestBeta.Split('.') : latestRelease.Split('.');
            for (int i = 0; i < 3; i++)
            {
                var myVer_i = int.Parse(myVersionSplit[i]);
                var latestVer_i = int.Parse(latestVersionSplit[i]);
                if (myVer_i > latestVer_i) { result = 1; break; }
                else if (myVer_i < latestVer_i) { result = -1; break; }
            }
            if (result == 0)
            {
                if (hasBeta) Logger.Log($"{RichToolName} 已是最新测试版！（{myVersion}）");
                else Logger.Log($"{RichToolName} 工具已是最新版本！（{myVersion}）");
                return;
            }
            if (result == 1)
            {
                Logger.LogWarning($"{RichToolName} 你是时间旅行者吗？你的{(hasBeta ? "测试版" : "版本")}（{myVersion}）" +
                    $"高于源（{(hasBeta ? latestBeta : latestRelease)}）！");
                return;
            }

            int updateN;
            if (hasBeta)
            {
                if (latestIsBeta)
                {
                    string[] latestReleaseSplit = latestRelease.Split('.');
                    result = 0;
                    for (int i = 0; i < 3; i++)
                    {
                        var myVer_i = int.Parse(myVersionSplit[i]);
                        var latestVer_i = int.Parse(latestReleaseSplit[i]);
                        if (myVer_i > latestVer_i) { result = 1; break; }
                        else if (myVer_i < latestVer_i) { result = -1; break; }
                    }
                    if (result == -1)
                    {
                        Logger.Log($"{RichToolName} 你的测试版（{myVersion}）落后于最新正式版和最新测试版！最新正式版为：{latestRelease}");
                        updateN = EditorUtility.DisplayDialogComplex("FACS工具：检查更新", "你的测试版已过期！\n\n" +
                            "前往FACS工具Discord服务器获取最新测试版！", "打开GitHub", "忽略", "不再提醒");
                    }
                    else
                    {
                        Logger.Log($"{RichToolName} 你的测试版（{myVersion}）已过期。最新测试版为：{latestBeta}");
                        updateN = EditorUtility.DisplayDialogComplex("FACS工具：检查更新", "你的测试版已过期！\n\n" +
                            "前往FACS工具Discord服务器获取最新测试版！", "打开GitHub", "忽略", "不再提醒");
                    }
                }
                else
                {
                    Logger.Log($"{RichToolName} 你的测试版（{myVersion}）落后于最新正式版！最新版本为：{latestRelease}");
                    updateN = EditorUtility.DisplayDialogComplex("FACS工具：检查更新", "你的测试版已过期！\n\n" +
                        "前往FACS Utilities GitHub页面获取最新正式版！", "打开GitHub", "忽略", "不再提醒");
                }
            }
            else
            {
                Logger.Log($"{RichToolName} 你的工具（{myVersion}）已过期。最新版本为：{latestRelease}");
                updateN = EditorUtility.DisplayDialogComplex("FACS工具：检查更新", "你的工具已过期！\n\n" +
                    "前往FACS Utilities GitHub页面获取最新正式版！", "打开GitHub", "忽略", "不再提醒");
            }

            if (updateN == 0) OpenFACSUtilitiesGitHub();
            else if (updateN == 2) { PlayerPrefs.SetInt("FACSUtilities_CheckUpdates", 0); }
        }
    }
}
#endif