#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class RemoveComponents : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[移除组件]" + Logger.EndTag;

        private static readonly Type TransformType = typeof(Transform);
        private static readonly Type RectTransformType = typeof(RectTransform);
        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject toRemoveFrom;
        private static bool isPrefabAsset = false;
        private static bool hasMissingScripts = false;
        private static ComponentHierarchy componentHierarchy;
        private static bool toggleSelection = true;
        private static bool toggleHideSelection = true;

        [MenuItem("FACS Utils/杂项/移除组件", false, 1100)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(RemoveComponents), false, "移除组件", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter; }
            
            EditorGUILayout.LabelField($"<color=cyan><b>移除组件</b></color>\n\n" +
                $"扫描选中的游戏对象，列出其中所有可用的组件，" +
                $"并让您选择要删除的组件。\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            toRemoveFrom = (GameObject)EditorGUILayout.ObjectField(toRemoveFrom, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() && toRemoveFrom)
            {
                if (PrefabUtility.IsPartOfPrefabAsset(toRemoveFrom) && PrefabUtility.GetPrefabAssetType(toRemoveFrom) == PrefabAssetType.Model)
                {
                    Logger.LogWarning($"{RichToolName} 无法编辑{Logger.RichModelPrefab}：{Logger.AssetTag}{toRemoveFrom.name}{Logger.EndTag}", toRemoveFrom);
                    toRemoveFrom = null;
                }
                ClearOldSelection();
            }
            if (!toRemoveFrom) ClearOldSelection();

            if (toRemoveFrom != null && GUILayout.Button("扫描!", FacsGUIStyles.Button, GUILayout.Height(40))) ScanSelection();

            if (componentHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>可删除的组件</b></color>:", FacsGUIStyles.Helpbox);

                bool anyOn = componentHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                if (componentHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("全部折叠", FacsGUIStyles.Button, GUILayout.Height(30))) CollapseAll();
                }
                else
                {
                    if (GUILayout.Button($"{(toggleSelection ? "全选" : "全不选")}", FacsGUIStyles.Button, GUILayout.Height(30)))
                    {
                        foreach (var ft in componentHierarchy.toggles_by_type.Values)
                        {
                            ft.SetAllToggles(toggleSelection);
                        }
                        componentHierarchy.SetAllGOEnables(toggleSelection);
                        toggleSelection = !toggleSelection;
                    }
                    if (GUILayout.Button($"{(toggleHideSelection ? "全部隐藏" : "全部显示")}", FacsGUIStyles.Button, GUILayout.Height(30)))
                    {
                        componentHierarchy.HideShowAll(toggleHideSelection);
                        toggleHideSelection = !toggleHideSelection;
                    }
                }
                if (GUILayout.Button($"{(componentHierarchy.hierarchyDisplay ? "列表" : "层级")}视图", FacsGUIStyles.Button, GUILayout.Height(30)))
                {
                    componentHierarchy.hierarchyDisplay = !componentHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();
                if (isPrefabAsset && hasMissingScripts) { EditorGUILayout.LabelField($"<color=orange>将删除一些缺失脚本以保存预制体</color>", FacsGUIStyles.HelpboxSmall); }
                if (anyOn && toRemoveFrom != null && GUILayout.Button("执行!", FacsGUIStyles.Button, GUILayout.Height(40))) RunDelete();
            }
        }

        private static void ScanSelection()
        {
            ClearOldSelection();
            toggleSelection = toggleHideSelection = true;
            isPrefabAsset = TrullyPersistent(toRemoveFrom);
            GetAvailableComponentTypes();
        }

        private static void ClearOldSelection()
        {
            isPrefabAsset = false;
            if (componentHierarchy != null) NullCompHierarchy();
        }

        private static bool TrullyPersistent(GameObject go)
        {
            if (!EditorUtility.IsPersistent(go)) return false;
            return !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(go));
        }

        private void RunDelete()
        {
            var toDelete = componentHierarchy.GetAllToggles(1);
            var actualDeletions = 0;
            var missingScripts = 0;
            if (toDelete.Length > 0)
            {
                Undo.SetCurrentGroupName("移除组件");
                if (isPrefabAsset && hasMissingScripts)
                {
                    missingScripts = RemoveMissingScripts.RemoveMissingScriptsRecursive(toRemoveFrom, out _);
                }
                foreach (var c in toDelete)
                {
                    if (c) { Undo.DestroyObjectImmediate(c); actualDeletions++; }
                }
                AssetDatabase.SaveAssets();
                if (isPrefabAsset) AssetDatabase.Refresh();
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
            ScanSelection();
            Logger.Log($"{RichToolName} 已完成删除 <b>{actualDeletions}</b> 个组件{(missingScripts>0?$" 和 {missingScripts} 个缺失脚本" :"")}！");
        }

        private void CollapseAll()
        {
            componentHierarchy.CollapseHierarchy();
        }

        private static void GetAvailableComponentTypes()
        {
            componentHierarchy = null;

            componentHierarchy = new(toRemoveFrom.transform);

            Transform[] gos_t = toRemoveFrom.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in gos_t)
            {
                Component[] components = t.GetComponents(typeof(Component));
                foreach (Component component in components)
                {
                    if (component)
                    {
                        var cT = component.GetType();
                        if (cT != TransformType && cT != RectTransformType) componentHierarchy.AddComponentToggle(component);
                    }
                    else if (!hasMissingScripts) hasMissingScripts = true;
                }
            }

            componentHierarchy.SortElements(false, true);
            componentHierarchy.SortTypes();
        }

        private static void NullCompHierarchy()
        {
            componentHierarchy = null;
            toggleSelection = true;
            toggleHideSelection = true;
            hasMissingScripts = false;
        }

        private void OnDestroy()
        {
            NullVars();
        }

        private void NullVars()
        {
            FacsGUIStyles = null;
            toRemoveFrom = null;
            NullCompHierarchy();
            ClearOldSelection();
        }
    }
}
#endif