#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class BundleMaker : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[资源包制作器]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static bool makeScenesBundle;
        private static string saveFolder;
        private static string bundleName;
        private static Compression compression = Compression.DefaultCompression;
        private static List<AssetItem> selectedAssets = new() { new() };
        private static readonly List<int> selectionCleanup = new();
        private static Vector2 scrollView = new();

        private static BuildTarget[] buildTargets;
        private static string[] buildTargetNames;

        [MenuItem("FACS Utils/资源包/资源包制作器", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(BundleMaker), false, "资源包制作器", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnEnable()
        {
            saveFolder = string.IsNullOrEmpty(saveFolder) ? Application.temporaryCachePath : saveFolder;
            buildTargets =
            (from bt in System.Enum.GetValues(typeof(BuildTarget)) as BuildTarget[]
             where BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(bt), bt)
             orderby bt.ToString()
             select bt).ToArray();
            buildTargetNames = buildTargets.Select(bt => bt.ToString()).ToArray();
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Button.wordWrap = true; }
            EditorGUILayout.LabelField($"<color=cyan><b>资源包制作器</b></color>\n\n" +
                $"从一组资产或一组场景创建资源包\n", FacsGUIStyles.Helpbox);
            var windowWidth = this.position.size.x;

            EditorGUILayout.Space();
            EditorGUILayout.BeginHorizontal();
            var activebuildtarget = EditorUserBuildSettings.activeBuildTarget;
            var activeBuildTargetIndex = System.Array.IndexOf(buildTargets, activebuildtarget);
            EditorGUILayout.LabelField("构建目标：", FacsGUIStyles.Label, GUILayout.Width(90));
            EditorGUI.BeginChangeCheck();
            var selectedIndex = EditorGUILayout.Popup(activeBuildTargetIndex, buildTargetNames);
            if (EditorGUI.EndChangeCheck() && selectedIndex != activeBuildTargetIndex)
            {
                var newBuildTarget = buildTargets[selectedIndex];
                if (EditorUtility.DisplayDialog("FACS工具 - 资源包制作器",
                    $"是否要将项目的构建目标从\"{activebuildtarget}\"更改为\"{newBuildTarget}\"？\n" +
                    $"这将需要一些时间。",
                    "是", "否"))
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(BuildPipeline.GetBuildTargetGroup(newBuildTarget), newBuildTarget);
                }
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("压缩方式：", FacsGUIStyles.Label, GUILayout.Width(90));
            compression = (Compression)EditorGUILayout.EnumPopup(compression);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            if (GUILayout.Button($"<b>保存文件夹</b>:\n{saveFolder}", FacsGUIStyles.Button))
            {
                string newFolderPath = EditorUtility.OpenFolderPanel("选择文件夹", saveFolder, "");
                if (!string.IsNullOrEmpty(newFolderPath)) saveFolder = newFolderPath;
            }
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            if (GUITools.ToggleButton(!makeScenesBundle, "资源包", GUILayout.Height(40), GUILayout.MaxWidth(windowWidth/2)))
            {
                if (makeScenesBundle) NullVars();
                else SelectionCleanup();
                makeScenesBundle = false;
            }
            if (GUITools.ToggleButton(makeScenesBundle, "场景包", GUILayout.Height(40), GUILayout.MaxWidth(windowWidth/2)))
            {
                if (!makeScenesBundle) NullVars();
                else SelectionCleanup();
                makeScenesBundle = true;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("包名称：", FacsGUIStyles.Label, GUILayout.Width(90));
            EditorGUI.BeginChangeCheck();
            bundleName = EditorGUILayout.DelayedTextField(bundleName);
            if (EditorGUI.EndChangeCheck()) bundleName = SanitizeFileName(bundleName);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("资源", FacsGUIStyles.Helpbox, GUILayout.MaxWidth(windowWidth/2));
            EditorGUILayout.LabelField("地址", FacsGUIStyles.Helpbox, GUILayout.MaxWidth(windowWidth/2));
            EditorGUILayout.EndHorizontal();
            scrollView = EditorGUILayout.BeginScrollView(scrollView);
            var shouldCleanup = false;
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                Object selAsset = makeScenesBundle ? EditorGUILayout.ObjectField(selectedAssets[i].obj, typeof(SceneAsset), false, GUILayout.MaxWidth(windowWidth/2)) :
                    EditorGUILayout.ObjectField(selectedAssets[i].obj, typeof(Object), false, GUILayout.MaxWidth(windowWidth/2));
                if (EditorGUI.EndChangeCheck()) shouldCleanup = true;

                if (selectedAssets[i].obj != selAsset)
                {
                    if (selAsset == null) { selectedAssets[i] = new(); EditorGUILayout.EndHorizontal(); continue; }
                    if (!makeScenesBundle && selAsset is SceneAsset)
                    {
                        Logger.LogWarning($"{RichToolName} 不允许将{Logger.TypeTag}场景{Logger.EndTag}资源添加到{Logger.ConceptTag}资源包{Logger.EndTag}中。");
                        EditorGUILayout.EndHorizontal(); continue;
                    }
                    var path = AssetDatabase.GetAssetPath(selAsset);
                    if (!AssetDatabase.IsMainAsset(selAsset))
                    {
                        Logger.LogWarning($"{RichToolName} {Logger.AssetTag}\"{selAsset.name}\" [{selAsset.GetType().Name}]{Logger.EndTag} 不是\"{path}\"中的主资源。");
                        EditorGUILayout.EndHorizontal(); continue;
                    }
                    if (string.IsNullOrEmpty(path))
                    {
                        Logger.LogWarning($"{RichToolName} {Logger.AssetTag}{selAsset.name}{Logger.EndTag} 需要先保存到资源文件。");
                        EditorGUILayout.EndHorizontal(); continue;
                    }
                    selectedAssets[i].obj = selAsset;
                    selectedAssets[i].objPath = path;
                }
                selectedAssets[i].addr = EditorGUILayout.TextField(selectedAssets[i].addr, GUILayout.MaxWidth(windowWidth/2));
                EditorGUILayout.EndHorizontal();
            }
            if (shouldCleanup) SelectionCleanup();
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();

            GUILayout.FlexibleSpace();
            if (!string.IsNullOrEmpty(bundleName) && selectedAssets.Count > 1)
            {
                if (GUILayout.Button($"生成\"{bundleName}\"！", FacsGUIStyles.Button, GUILayout.Height(40)))
                {
                    GenerateBundle();
                }
            }
        }

        private static void GenerateBundle()
        {
            if (!Directory.Exists(saveFolder))
            {
                Logger.LogError($"{RichToolName} 未找到保存文件夹：\"{saveFolder}\""); return;
            }
            var saveLocation = Path.GetFullPath(Path.Combine(saveFolder, bundleName));
            if (File.Exists(saveLocation))
            {
                if (!EditorUtility.DisplayDialog("FACS工具 - 资源包制作器", "目标文件夹中已存在同名文件。\n是否要覆盖？", "是", "否"))
                { Logger.Log(RichToolName + " 资源包生成已取消。"); return; }
            }

            AssetDatabase.SaveAssets();
            var items = selectedAssets.Where(s => s.obj);
            var assetPaths = items.Select(i => i.objPath).ToArray();
            var assetAddresses = items.Select(i => i.addr).ToArray();
            var bundleData = new AssetBundleBuild
            {
                assetBundleName = bundleName,
                assetNames = assetPaths,
                addressableNames = assetAddresses
            };
            var options = BuildAssetBundleOptions.ForceRebuildAssetBundle;
            if (compression == Compression.Uncompressed) options |= BuildAssetBundleOptions.UncompressedAssetBundle;
            else if (compression == Compression.ChunkBasedCompression) options |= BuildAssetBundleOptions.ChunkBasedCompression;

            var tempLoc = Path.GetTempPath();
            var tempbundle = Path.GetFullPath(Path.Combine(tempLoc, bundleName));
            if (File.Exists(tempbundle)) File.Delete(tempbundle);

            try
            {
                BuildPipeline.BuildAssetBundles(tempLoc, new AssetBundleBuild[1] { bundleData }, options, EditorUserBuildSettings.activeBuildTarget);
            }
            catch (System.Exception e)
            {
                Logger.LogError($"{RichToolName} 生成新资源包时发生异常。\n{e}");
                return;
            }

            if (!File.Exists(tempbundle))
            {
                Logger.LogWarning($"{RichToolName} {Logger.RichAssetBundle}未生成。请检查控制台。");
                return;
            }

            if (tempbundle != saveLocation)
            {
                if (File.Exists(saveLocation)) File.Delete(saveLocation);
                File.Move(tempbundle, saveLocation);
            }
            Logger.Log($"{RichToolName} 资源包\"{bundleName}\"创建成功！\n{Logger.RichAssetBundle}清单位于：\"{tempbundle}.manifest\"");
        }

        private static void SelectionCleanup()
        {
            var distincts = new HashSet<Object>();
            for (int i = 0; i < selectedAssets.Count; i++)
            {
                if ((!selectedAssets[i].obj && i != selectedAssets.Count - 1) || !distincts.Add(selectedAssets[i].obj))
                    selectionCleanup.Insert(0, i);
            }
            if (selectionCleanup.Count > 0)
            {
                foreach (var removeI in selectionCleanup) selectedAssets.RemoveAt(removeI);
            }
            if (selectedAssets[^1].obj) selectedAssets.Add(new());
            selectionCleanup.Clear();
        }

        private string SanitizeFileName(string input)
        {
            var tmp = Regex.Replace(input, @"[^a-zA-Z0-9 \._-]", "").Trim();
            return Regex.Replace(tmp, @"^[.\s]+|[.\s]+$", "");
        }

        private void OnDestroy()
        {
            FacsGUIStyles = null;
            makeScenesBundle = false;
            bundleName = null;
            buildTargets = null;
            buildTargetNames = null;
            NullVars();
        }

        private void NullVars()
        {
            selectedAssets = new() { new() };
        }

        private class AssetItem
        {
            public Object obj;
            public string objPath;
            public string addr = "";
        }

        private enum Compression
        {
            [Tooltip("最快的资产访问速度，但文件体积最大。")]
            Uncompressed,
            [Tooltip("即LZ4HC。中等体积，较好的访问速度。")]
            ChunkBasedCompression,
            [Tooltip("即LZMA。最小体积，但访问速度较慢。")]
            DefaultCompression
        }
    }
}
#endif