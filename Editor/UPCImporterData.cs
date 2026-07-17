#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    [FilePath("FACS01 Data/UPCImporter", FilePathAttribute.Location.PreferencesFolder)]
    internal class UPCImporterData : ScriptableSingleton<UPCImporterData>
    {
        private const string RichToolName = Logger.ToolTag + "[UPC 导入器]" + Logger.EndTag;
        [SerializeField]
        internal List<UPCollection> collections;

        private UPCImporterData()
        {
            collections = new();
        }

        [System.Serializable]
        internal class UPCollection
        {
            [System.NonSerialized]
            public bool foldout = false;
            [System.NonSerialized]
            public Vector2 scrollView = default;

            [SerializeField]
            internal string collectionName;
            [SerializeField]
            internal List<UPContent> folders;
            [SerializeField]
            internal List<UPContent> packages;

            internal UPCollection(int i)
            {
                collectionName = "我的集合 #" + i;
                folders = new(); packages = new();
            }

            internal void Import()
            {
                var UPList = new HashSet<string>();
                foreach (var f in folders)
                {
                    var fpath = System.IO.Path.GetFullPath(f.path);
                    if (!System.IO.Directory.Exists(fpath))
                    { Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}文件夹{Logger.EndTag}未找到：{fpath}"); continue; }
                    var ups = System.IO.Directory.GetFiles(fpath, "*.unitypackage", System.IO.SearchOption.AllDirectories);
                    if (ups.Length == 0)
                    { Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}文件夹{Logger.EndTag}不包含任何{Logger.ConceptTag}Unity包{Logger.EndTag}：{fpath}"); continue; }
                    foreach (var up in ups) UPList.Add(up);
                }
                foreach (var p in packages)
                {
                    var ppath = System.IO.Path.GetFullPath(p.path);
                    if (!System.IO.File.Exists(ppath))
                    { Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}Unity包{Logger.EndTag}未找到：{ppath}"); continue; }
                    UPList.Add(ppath);
                }
                if (UPList.Count == 0)
                {
                    Logger.LogWarning($"{RichToolName} {Logger.ConceptTag}集合{Logger.EndTag} {Logger.AssetTag}{collectionName}{Logger.EndTag}" +
                        $"未找到任何{Logger.ConceptTag}Unity包{Logger.EndTag}可导入。"); return;
                }
                var UPNames = string.Join("\n", UPList.Select(up => System.IO.Path.GetFileNameWithoutExtension(up)));
                if (!EditorUtility.DisplayDialog("UPC 导入器", $"是否要将 {UPList.Count} 个包导入到你的项目中？\n\n" +
                    $"{UPNames}", "是", "否")) return;

                EditorApplication.LockReloadAssemblies(); AssetDatabase.DisallowAutoRefresh();
                var failedUP = "";
                try
                {
                    foreach (var UP in UPList)
                    {
                        failedUP = UP;
                        AssetDatabase.ImportPackage(UP, false);
                        Logger.Log($"{RichToolName} 已导入{Logger.ConceptTag}Unity包{Logger.EndTag}：{System.IO.Path.GetFileNameWithoutExtension(UP)}");
                    }
                    Logger.Log($"{RichToolName} 成功导入 <b>{UPList.Count}</b> 个{Logger.ConceptTag}Unity包{Logger.EndTag}！\n\n{UPNames}");
                }
                catch (System.Exception e)
                {
                    Logger.LogError($"{RichToolName} 导入{Logger.ConceptTag}Unity包{Logger.EndTag}失败：{failedUP}\n{e}");
                }
                finally { EditorApplication.UnlockReloadAssemblies(); AssetDatabase.AllowAutoRefresh(); }
            }
        }

        [System.Serializable]
        internal class UPContent
        {
            [SerializeField]
            internal string path;
            [System.NonSerialized]
            private bool initWidth = false;
            [System.NonSerialized]
            private float _width = -1;
            internal float width
            {
                get
                {
                    if (!initWidth)
                    {
                        _width = GUI.skin.label.CalcSize(new GUIContent(path)).x;
                        initWidth = true;
                    }
                    return _width;
                }
            }

            internal UPContent(string str)
            {
                path = str; initWidth = false;
            }

            internal static bool IsIn(string str, IEnumerable<UPContent> ie)
            {
                foreach (var elem in ie) if (elem.path == str) return true;
                return false;
            }
        }

        internal static void SaveData()
        {
            instance.Save(true);
        }
    }
}
#endif