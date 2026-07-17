#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class CopyMeshSettings : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[复制网格设置]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject copyFrom;
        private static GameObject copyTo;
        private static ComponentHierarchy componentHierarchy;
        private static Dictionary<Transform, Transform> GO_Dictionary; //from,to
        private static Dictionary<Component, Component> C_Dictionary; //from,to
        private bool toggleSelection = false;

        [MenuItem("FACS Utils/复制/复制网格设置", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(CopyMeshSettings), false, "复制网格设置", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter; }
            EditorGUILayout.LabelField($"<color=cyan><b>复制网格设置</b></color>\n\n" +
                $"扫描选中的源游戏对象，列出所有可用的网格渲染器和蒙皮网格渲染器组件，" +
                $"并让您选择将哪些组件的材质和设置复制到目标游戏对象。\n" +
                $"此操作不会替换当前的网格。\n", FacsGUIStyles.Helpbox);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField($"<b>复制源</b>", FacsGUIStyles.Helpbox);
            EditorGUI.BeginChangeCheck();
            copyFrom = (GameObject)EditorGUILayout.ObjectField(copyFrom, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck() || (!copyFrom && componentHierarchy != null)) NulldRends();
            EditorGUILayout.EndVertical();
            if (componentHierarchy != null)
            {
                EditorGUILayout.BeginVertical();
                EditorGUILayout.LabelField($"<b>复制到</b>", FacsGUIStyles.Helpbox);
                EditorGUI.BeginChangeCheck();
                copyTo = (GameObject)EditorGUILayout.ObjectField(copyTo, typeof(GameObject), true, GUILayout.Height(40));
                if (EditorGUI.EndChangeCheck())
                {
                    if (copyTo && PrefabUtility.IsPartOfPrefabAsset(copyTo) && PrefabUtility.GetPrefabAssetType(copyTo) == PrefabAssetType.Model)
                    {
                        Logger.LogWarning($"{RichToolName} 无法编辑{Logger.RichModelPrefab}：{Logger.AssetTag}{copyTo.name}{Logger.EndTag}", copyTo);
                        copyTo = null;
                    }
                }
                EditorGUILayout.EndVertical();
            }
            EditorGUILayout.EndHorizontal();

            if (copyFrom != null && GUILayout.Button("扫描!", FacsGUIStyles.Button, GUILayout.Height(40))) GetAvailableComponents();

            if (componentHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>可复制设置的渲染器</b></color>:", FacsGUIStyles.Helpbox);

                bool anyOn = componentHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();

                EditorGUILayout.BeginHorizontal();
                if (!componentHierarchy.hierarchyDisplay && GUILayout.Button($"{(toggleSelection ? "全选" : "全不选")}", FacsGUIStyles.Button, GUILayout.Height(30)))
                {
                    SelectAll(toggleSelection); toggleSelection = !toggleSelection;
                }
                if (componentHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("全部折叠", FacsGUIStyles.Button, GUILayout.Height(30))) CollapseAll();
                }
                if (GUILayout.Button($"{(componentHierarchy.hierarchyDisplay ? "简单" : "层级")}视图", FacsGUIStyles.Button, GUILayout.Height(30)))
                {
                    componentHierarchy.hierarchyDisplay = !componentHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();

                if (anyOn && copyTo != null && GUILayout.Button("复制!", FacsGUIStyles.Button, GUILayout.Height(40))) RunCopy();
            }
        }

        private void SelectAll(bool yesno)
        {
            componentHierarchy.SetAllToggles(typeof(SkinnedMeshRenderer), yesno);
            componentHierarchy.SetAllToggles(typeof(MeshRenderer), yesno);
        }

        private void RunCopy()
        {
            GODictionary(copyFrom.transform, copyTo.transform);
            CollapseAll();
            componentHierarchy.UseUnfoldedAsMarker();
            ComponentDictionary();
            CopySettings();
            CollapseAll();
            if (PrefabUtility.IsPartOfPrefabAsset(copyTo) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(copyTo))) PrefabUtility.SavePrefabAsset(copyTo);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Logger.Log(RichToolName + " 已完成复制设置！");
        }

        private void CopySettings()
        {
            foreach (var pair in C_Dictionary)
            {
                var rendFrom = (Renderer)pair.Key; var rendTo = (Renderer)pair.Value;

                var rendCopyFrom_mats = rendFrom.sharedMaterials;
                var rendCopyTo_mats = rendTo.sharedMaterials;
                int IndexFrom = rendCopyFrom_mats.Length; int IndexTo = rendCopyTo_mats.Length;
                var tempMats = new Material[IndexTo];

                int CommonIndex = Math.Min(IndexFrom, IndexTo);
                for (int j = 0; j < CommonIndex; j++)
                {
                    if (rendCopyFrom_mats[j] && AssetDatabase.IsMainAsset(rendCopyFrom_mats[j]))
                    {
                        tempMats[j] = rendCopyFrom_mats[j];
                    }
                    else tempMats[j] = rendCopyTo_mats[j];
                }
                if (IndexTo > IndexFrom)
                {
                    for (int j = CommonIndex; j < IndexTo; j++)
                    {
                        tempMats[j] = rendCopyTo_mats[j];
                    }
                }

                Undo.RecordObject(rendTo, "复制网格设置");
                rendTo.gameObject.SetActive(rendFrom.gameObject.activeSelf);
                rendTo.enabled = rendFrom.enabled;
                rendTo.sharedMaterials = tempMats;
                rendTo.shadowCastingMode = rendFrom.shadowCastingMode;
                rendTo.receiveShadows = rendFrom.receiveShadows;
                rendTo.lightProbeUsage = rendFrom.lightProbeUsage;
                rendTo.reflectionProbeUsage = rendFrom.reflectionProbeUsage;
                rendTo.allowOcclusionWhenDynamic = rendFrom.allowOcclusionWhenDynamic;

                if (rendTo.GetType() == typeof(SkinnedMeshRenderer))
                {
                    var smrFrom = (SkinnedMeshRenderer)rendFrom;
                    var smrTo = (SkinnedMeshRenderer)rendTo;
                    var meshFrom = smrFrom.sharedMesh; var meshTo = smrTo.sharedMesh;
                    if (meshFrom && meshTo)
                    {
                        for (int i = 0; i < meshFrom.blendShapeCount; i++)
                        {
                            var bsToIndex = meshTo.GetBlendShapeIndex(meshFrom.GetBlendShapeName(i));
                            if (bsToIndex != -1) smrTo.SetBlendShapeWeight(bsToIndex, smrFrom.GetBlendShapeWeight(i));
                        }
                    }

                    smrTo.localBounds = smrFrom.localBounds;
                    smrTo.updateWhenOffscreen = smrFrom.updateWhenOffscreen;
                }

                if (PrefabUtility.IsPartOfPrefabInstance(rendTo) && PrefabUtility.GetPrefabAssetType(rendTo) != PrefabAssetType.MissingAsset)
                    PrefabUtility.RecordPrefabInstancePropertyModifications(rendTo);
            }
        }

        private void ComponentDictionary()
        {
            C_Dictionary = new();
            foreach (var THC in componentHierarchy.toggles_by_type.Keys)
            {
                var T = ComponentHierarchy.Types[THC].type;
                var CtoCopy = componentHierarchy.GetAllToggles(THC, 1); if (CtoCopy == null) continue;
                var GOtoCopy = new HashSet<Transform>();
                foreach (var c in CtoCopy) if (c) GOtoCopy.Add(c.transform);
                foreach (var t_from in GOtoCopy)
                {
                    var t_to = GO_Dictionary[t_from];
                    if (t_to == null)
                    {
                        Logger.LogWarning($"{RichToolName} 无法复制 (<b>{CtoCopy.Count(c=>c&&c.transform==t_from)}</b>) {Logger.TypeTag}{T.Name}{Logger.EndTag}，因为目标" +
                            $"{Logger.RichGameObject} 不存在：{AnimationUtility.CalculateTransformPath(t_from, copyFrom.transform)}");
                        continue;
                    }
                    var Cs_from = t_from.GetComponents(T); if (Cs_from.Length == 0) continue;
                    var Cs_to = t_to.GetComponents(T);
                    for (int i = 0; i < Cs_from.Length; i++)
                    {
                        var Cs_from_i = Cs_from[i];
                        if (!CtoCopy.Contains(Cs_from_i)) continue;
                        else if (i < Cs_to.Length) C_Dictionary.Add(Cs_from_i, Cs_to[i]);
                        else
                        {
                            Logger.LogWarning($"{RichToolName} 无法复制 {Logger.TypeTag}{T.Name}{Logger.EndTag}，因为目标上的匹配" +
                                $"{Logger.TypeTag}组件{Logger.EndTag}较少 {Logger.RichGameObject}：{AnimationUtility.CalculateTransformPath(t_from, copyFrom.transform)}");
                        }
                    }
                }
            }
        }

        private void GODictionary(Transform from, Transform to)
        {
            var TDict = new Dictionary<Transform, Transform>() { {from,to} };
            GODictionaryRecursive(TDict, from, to);
            GO_Dictionary = TDict;
        }

        private void GODictionaryRecursive(Dictionary<Transform, Transform> dict, Transform from, Transform to)
        {
            int toChildsN = to.childCount;
            if (toChildsN == 0)
            {
                GODictionaryNullRecursive(dict, from); return;
            }
            var toList = new List<Transform>();
            foreach (Transform t in to)
            {
                toList.Add(t);
            }
            foreach (Transform ch_from in from)
            {
                bool match = false;
                for (int i = 0; i < toList.Count; i++)
                {
                    if (ch_from.name == toList[i].name)
                    {
                        dict.Add(ch_from, toList[i]); toList.RemoveAt(i); match = true;
                        if (ch_from.childCount > 0) GODictionaryRecursive(dict, ch_from, dict[ch_from]);
                        break;
                    }
                }
                if (!match)
                {
                    dict.Add(ch_from, null);
                    GODictionaryNullRecursive(dict, ch_from);
                }
            }
        }

        private void GODictionaryNullRecursive(Dictionary<Transform, Transform> dict, Transform from)
        {
            foreach (Transform t in from)
            {
                dict.Add(t, null);
                GODictionaryNullRecursive(dict, t);
            }
        }

        private void CollapseAll()
        {
            componentHierarchy.CollapseHierarchy();
        }

        private void GetAvailableComponents()
        {
            componentHierarchy = null;
            componentHierarchy = new(copyFrom.transform); componentHierarchy.hideTypeFoldout = true;
            var smr = copyFrom.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in smr) componentHierarchy.AddComponentToggle(r);
            var mr = copyFrom.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in mr) componentHierarchy.AddComponentToggle(r);
            if (smr.Length + mr.Length == 0)
            {
                Logger.LogWarning($"{RichToolName} 没有可复制设置的{Logger.TypeTag}渲染器{Logger.EndTag}。", copyFrom);
                NulldRends();
            }
            else componentHierarchy.SortElements(false, true);
        }

        public void OnDestroy()
        {
            NullVars();
        }

        private void NulldRends()
        {
            componentHierarchy = null;
            GO_Dictionary = null;
            C_Dictionary = null;
            toggleSelection = true;
        }

        private void NullVars()
        {
            FacsGUIStyles = null;
            copyFrom = null;
            copyTo = null;
            NulldRends();
        }
    }
}
#endif