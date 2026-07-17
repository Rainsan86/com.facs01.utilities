#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FACS01.Utilities
{
    internal static class ContextMenus
    {
        private const string RichToolName = Logger.ToolTag + "[FACS工具 - 右键菜单]" + Logger.EndTag;

        #region Hide and Show Assets

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/隐藏子资产", true, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/隐藏子资产", true, 201)]
        private static bool CanHideSubAssets(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (string.IsNullOrEmpty(assetPath)) return false;
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (menuCommand.context != mainAsset) return false;
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            return subassets.Length != 0;
        }

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/隐藏子资产", false, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/隐藏子资产", false, 201)]
        private static void HideSubAssets(MenuCommand menuCommand)
        {
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(AssetDatabase.GetAssetPath(menuCommand.context));
            var counter = 0;
            foreach (var sa in subassets)
            {
                if (!sa) continue;
                using (var so = new SerializedObject(sa))
                {
                    so.FindProperty("m_ObjectHideFlags").intValue = 1;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                counter++;
            }
            AssetDatabase.SaveAssets();
            if (counter == 1)
                Logger.Log($"{RichToolName} {Logger.AssetTag}{subassets[0].name}{Logger.EndTag} 已隐藏于 {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
            else Logger.Log($"{RichToolName} <b>{counter}</b> 个子资产已隐藏于 {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
        }

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/显示子资产", true, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/显示子资产", true, 201)]
        private static bool CanShowSubAssets(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (string.IsNullOrEmpty(assetPath)) return false;
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (menuCommand.context != mainAsset) return false;
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var nullAssetsN = allAssets.Where(o => object.Equals(o, null)).Count();
            if (allAssets.Length - nullAssetsN < 2) return false;
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            return allAssets.Length - nullAssetsN - 1 != subassets.Length;
        }

        [MenuItem("CONTEXT/AnimatorController/FACS Utils/显示子资产", false, 201)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/显示子资产", false, 201)]
        private static void ShowSubAssets(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            var mainAsset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            var nullAssetsN = allAssets.Where(o => object.Equals(o, null)).Count();
            var subassets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
            var visibleSubAssets = new HashSet<Object>(subassets);
            var counter = 0;
            foreach (var asset in allAssets)
            {
                if (!asset || asset == mainAsset || visibleSubAssets.Contains(asset)) continue;
                using (var so = new SerializedObject(asset))
                {
                    so.FindProperty("m_ObjectHideFlags").intValue = 0;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                counter++;
            }
            AssetDatabase.SaveAssets();
            if (nullAssetsN != 0)
                Logger.LogWarning($"{RichToolName} 在 {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag} 中发现 <b>{nullAssetsN}</b> 个损坏的资产", menuCommand.context);
            Logger.Log($"{RichToolName} <b>{counter}</b> 个子资产已显示于 {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag}", menuCommand.context);
        }

        #endregion

        #region Connect and Disconnect BlendTrees

        [MenuItem("CONTEXT/BlendTree/FACS Utils/从资产断开", true, 351)]
        private static bool BlendTreeCanDisconnect(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var _ = AssetDatabase.LoadMainAssetAtPath(assetPath);
                return menuCommand.context != _;
            }
            return false;
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/从资产断开", false, 351)]
        private static void BlendTreeDisconnect(MenuCommand menuCommand)
        {
            var bt = menuCommand.context as BlendTree;
            var mainassetType = AssetDatabase.GetMainAssetTypeAtPath(AssetDatabase.GetAssetPath(bt));
            var initsavepanel = new System.IO.FileInfo(AssetDatabase.GetAssetPath(bt)).Directory.FullName;
            var savepath = EditorUtility.SaveFilePanel($"从{ObjectNames.NicifyVariableName(mainassetType.Name)}断开混合树", initsavepanel, bt.name, "asset");
            if (string.IsNullOrEmpty(savepath)) return;
            savepath = System.IO.Path.GetFullPath(savepath);
            if (System.IO.File.Exists(savepath)) { Logger.LogWarning(RichToolName + " 不支持覆盖现有资产。"); return; }
            var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar;
            if (!savepath.StartsWith(projPath))
            { Logger.LogWarning(RichToolName + " 请在此项目内选择" + Logger.RichBlendTree + "的保存位置！"); return; }
            savepath = savepath.Replace(projPath, "");

            var btCopy = new BlendTree();
            EditorUtility.CopySerialized(bt, btCopy);
            btCopy.hideFlags = HideFlags.None;
            btCopy.name = System.IO.Path.GetFileNameWithoutExtension(savepath);
            AssetDatabase.CreateAsset(btCopy, savepath);
            var newBtAssets = new Dictionary<BlendTree, BlendTree>() { { bt, btCopy } };
            ReplaceOldBTs(newBtAssets);
            AssetDatabase.RemoveObjectFromAsset(bt);
            AssetDatabase.SaveAssets();
            Selection.objects = new Object[1] { btCopy };
            EditorGUIUtility.PingObject(btCopy);
            Logger.Log($"{RichToolName} 新{Logger.RichBlendTree}已保存至：\"{savepath}\"", btCopy);
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/添加到动画控制器", true, 301)]
        [MenuItem("CONTEXT/BlendTree/FACS Utils/添加到混合树", true, 301)]
        private static bool BlendTreeCanConnect(MenuCommand menuCommand)
        {
            var assetPath = AssetDatabase.GetAssetPath(menuCommand.context);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var _ = AssetDatabase.LoadMainAssetAtPath(assetPath);
                return menuCommand.context == _;
            }
            return false;
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/添加到动画控制器", false, 301)]
        private static void BlendTreeConnect_AC(MenuCommand menuCommand)
        {
            var bt = menuCommand.context as BlendTree;
            var btPath = AssetDatabase.GetAssetPath(bt);
            var initsavepanel = new System.IO.FileInfo(btPath).Directory.FullName;
            var savepath = EditorUtility.OpenFilePanelWithFilters("添加混合树到动画控制器", initsavepanel, new string[2] { "动画控制器", "controller" });
            if (string.IsNullOrEmpty(savepath)) return;
            savepath = System.IO.Path.GetFullPath(savepath);
            var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar;
            if (!savepath.StartsWith(projPath)) { Logger.LogWarning(RichToolName + " 请在此项目内选择" + Logger.RichAnimatorController + "！"); return; }
            savepath = savepath.Replace(projPath, "");
            BlendTreeConnect(bt, btPath, savepath);
        }

        [MenuItem("CONTEXT/BlendTree/FACS Utils/添加到混合树", false, 301)]
        private static void BlendTreeConnect_BT(MenuCommand menuCommand)
        {
            var bt = menuCommand.context as BlendTree;
            var btPath = AssetDatabase.GetAssetPath(bt);
            var initsavepanel = new System.IO.FileInfo(btPath).Directory.FullName;
            var savepath = EditorUtility.OpenFilePanelWithFilters("添加混合树到混合树", initsavepanel, new string[2] { "混合树", "asset" });
            if (string.IsNullOrEmpty(savepath)) return;
            savepath = System.IO.Path.GetFullPath(savepath);
            var projPath = System.IO.Path.GetFullPath(System.IO.Directory.GetCurrentDirectory()) + System.IO.Path.DirectorySeparatorChar;
            if (!savepath.StartsWith(projPath)) { Logger.LogWarning(RichToolName + " 请在此项目内选择" + Logger.RichBlendTree + "！"); return; }
            savepath = savepath.Replace(projPath, "");
            var mainBT = AssetDatabase.LoadMainAssetAtPath(savepath);
            if (mainBT is not BlendTree) { Logger.LogWarning(RichToolName + " 目标资产必须是" + Logger.RichBlendTree); return; }
            if (mainBT == menuCommand.context) { Logger.LogWarning(RichToolName + " 不能将" + Logger.RichBlendTree + "添加到自身。"); return; }
            BlendTreeConnect(bt, btPath, savepath);
        }

        private static void BlendTreeConnect(BlendTree bt, string btPath, string savepath)
        {
            var newBtAssets = new Dictionary<BlendTree, BlendTree>();
            var btAssets = AssetDatabase.LoadAllAssetsAtPath(btPath);
            foreach (var asset in btAssets)
            {
                if (!asset || asset is not BlendTree btAsset) continue;
                var btCopy = new BlendTree();
                EditorUtility.CopySerialized(btAsset, btCopy);
                btCopy.hideFlags = HideFlags.None;
                AssetDatabase.AddObjectToAsset(btCopy, savepath);
                newBtAssets[btAsset] = btCopy;
            }
            ReplaceOldBTs(newBtAssets, true);
            AssetDatabase.DeleteAsset(btPath);
            AssetDatabase.SaveAssets();
            var mainNew = newBtAssets[bt];
            Selection.objects = new Object[1] { mainNew };
            EditorGUIUtility.PingObject(mainNew);
            Logger.Log($"{RichToolName} {Logger.RichBlendTree}已作为子资产添加至：\"{savepath}\"", mainNew);
        }

        private static void ReplaceOldBTs(Dictionary<BlendTree, BlendTree> OldNewBTs, bool addingToAsset = false)
        {
            var progBarTitle = addingToAsset ? "添加混合树到资产" : "断开混合树";

            var acPaths = AssetDatabase.FindAssets("t:AnimatorController").Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToList();
            var btPaths = AssetDatabase.FindAssets("t:BlendTree").Select(guid => AssetDatabase.GUIDToAssetPath(guid)).ToHashSet();

            int j = 0;
            for (; j < acPaths.Count; j++)
            {
                var acPath = acPaths[j];
                EditorUtility.DisplayProgressBar(progBarTitle, $"正在处理动画控制器\n  {acPath}", (float)(j + 1) / acPaths.Count);
                var animatorStates = AssetDatabase.LoadAllAssetsAtPath(acPath).Where(o => o is AnimatorState).Cast<AnimatorState>();
                foreach (var animatorState in animatorStates)
                {
                    using (var so = new SerializedObject(animatorState))
                    {
                        var sp = so.FindProperty("m_Motion");
                        if (sp == null || sp.objectReferenceValue is not BlendTree oldBT || !oldBT) continue;
                        if (OldNewBTs.TryGetValue(oldBT, out var newBT)) sp.objectReferenceValue = newBT;
                        if (so.hasModifiedProperties) so.ApplyModifiedProperties();
                    }
                }
                if (!btPaths.Contains(acPath)) continue;
                btPaths.Remove(acPath);
                var blendTrees = AssetDatabase.LoadAllAssetsAtPath(acPath).Where(o => o is BlendTree).Cast<BlendTree>();
                foreach (var blendTree in blendTrees)
                {
                    ReplaceBTsin1BT(OldNewBTs, blendTree);
                }
            }

            j = 1;
            foreach (var btPath in btPaths)
            {
                EditorUtility.DisplayProgressBar(progBarTitle, $"正在处理混合树\n  {btPath}", (float)j / btPaths.Count);
                var blendTrees = AssetDatabase.LoadAllAssetsAtPath(btPath).Where(o => o is BlendTree).Cast<BlendTree>();
                foreach (var blendTree in blendTrees)
                {
                    ReplaceBTsin1BT(OldNewBTs, blendTree);
                }
                j++;
            }
            EditorUtility.ClearProgressBar();
        }

        private static void ReplaceBTsin1BT(Dictionary<BlendTree, BlendTree> newBtAssets, BlendTree bt)
        {
            using (var so = new SerializedObject(bt))
            {
                var sp = so.FindProperty("m_Childs");
                if (sp == null || sp.arraySize == 0) return;
                for (int i = 0; i < sp.arraySize; i++)
                {
                    var sp_i = sp.GetArrayElementAtIndex(i);
                    var m = sp_i.FindPropertyRelative("m_Motion");
                    if (m == null || m.objectReferenceValue is not BlendTree oldBT || !oldBT) continue;
                    if (newBtAssets.TryGetValue(oldBT, out var newBT)) m.objectReferenceValue = newBT;
                }
                if (so.hasModifiedProperties) so.ApplyModifiedProperties();
            }
        }

        #endregion

        #region Scriptable Object Type

        [MenuItem("CONTEXT/ScriptableObject/FACS Utils/脚本化对象类型？", true, 0)]
        private static bool CanScriptableObjectType(MenuCommand menuCommand)
        {
            return menuCommand.context.GetType() != typeof(ScriptableObject);
        }

        [MenuItem("CONTEXT/ScriptableObject/FACS Utils/脚本化对象类型？", false, 0)]
        private static void ScriptableObjectType(MenuCommand menuCommand)
        {
            var t = menuCommand.context.GetType();
            Logger.Log($"{RichToolName} {Logger.AssetTag}{menuCommand.context.name}{Logger.EndTag} 是 {Logger.TypeTag}{t.Name}{Logger.EndTag}");
        }

        #endregion

    }
}
#endif