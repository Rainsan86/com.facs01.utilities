#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    internal class BetterBounds : EditorWindow
    {
        private const string RichToolName = Logger.ToolTag + "[优化边界]" + Logger.EndTag;

        private static FACSGUIStyles FacsGUIStyles;
        private static GameObject avatar;
        private static bool IsNotPartOfPrefabAsset;
        private static ComponentHierarchy skinnedRendHierarchy;
        private static bool toggleSelection = true;
        private static float looseMultiplier = 1.4f;
        private static int missingRBs = 0;

        [MenuItem("FACS Utils/模型工具/优化模型边界", false, 1101)]
        private static void ShowWindow()
        {
            var window = GetWindow(typeof(BetterBounds), false, "优化边界", true);
            window.maxSize = new(1000, 700); window.minSize = new(160, 160);
            window.autoRepaintOnSceneChange = true;
        }

        private void OnGUI()
        {
            if (FacsGUIStyles == null) { FacsGUIStyles = new(); FacsGUIStyles.Helpbox.alignment = TextAnchor.MiddleCenter; }
            EditorGUILayout.LabelField($"<color=cyan><b>Better Avatar Bounds</b></color>\n\n" +
                $"Scans the selected GameObject (avatar), lists all available Skinned Mesh Renderers (skipping the ones inside the avatar's Armature), and lets you choose which ones to give a new bounding box.\n\n" +
                $"The 'Loose Bounds' option should fix issues with parts of the avatar disappearing when viewed from certain angles.\n", FacsGUIStyles.Helpbox);

            EditorGUI.BeginChangeCheck();
            avatar = (GameObject)EditorGUILayout.ObjectField(avatar, typeof(GameObject), true, GUILayout.Height(40));
            if (EditorGUI.EndChangeCheck())
            {
                NullskinnedRend();
                if (avatar)
                {
                    IsNotPartOfPrefabAsset = !(PrefabUtility.IsPartOfPrefabAsset(avatar) && !string.IsNullOrEmpty(AssetDatabase.GetAssetPath(avatar)));
                    if (!IsNotPartOfPrefabAsset && PrefabUtility.GetPrefabAssetType(avatar) == PrefabAssetType.Model)
                    {
                        Logger.LogWarning($"{RichToolName} 无法编辑{Logger.RichModelPrefab}：{Logger.AssetTag}{avatar.name}{Logger.EndTag}", avatar);
                        avatar = null; return;
                    }
                }
            }
            if (!avatar && skinnedRendHierarchy != null) { NullskinnedRend(); return; }
            if (avatar && GUILayout.Button("扫描!", FacsGUIStyles.Button, GUILayout.Height(40))) GetAvailableComponents();

            if (skinnedRendHierarchy != null)
            {
                EditorGUILayout.LabelField($"<color=green><b>可修改的蒙皮网格渲染器</b></color>：", FacsGUIStyles.Helpbox);

                bool anyOn = skinnedRendHierarchy.DisplayGUI();
                GUILayout.FlexibleSpace();
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button($"{(toggleSelection ? "全选" : "全不选")}", FacsGUIStyles.Button, GUILayout.Height(30)))
                {
                    SelectAll(toggleSelection); toggleSelection = !toggleSelection;
                }
                if (skinnedRendHierarchy.hierarchyDisplay)
                {
                    if (GUILayout.Button("全部折叠", FacsGUIStyles.Button, GUILayout.Height(30))) CollapseAll();
                }
                if (GUILayout.Button($"{(skinnedRendHierarchy.hierarchyDisplay ? "列表" : "层级")}视图", FacsGUIStyles.Button, GUILayout.Height(30)))
                {
                    skinnedRendHierarchy.hierarchyDisplay = !skinnedRendHierarchy.hierarchyDisplay;
                }
                EditorGUILayout.EndHorizontal();

                if (anyOn)
                {
                    if (GUILayout.Button("精确边界", FacsGUIStyles.Button, GUILayout.Height(40)))
                    {
                        missingRBs = 0;
                        var tmp = skinnedRendHierarchy.GetAllToggles<SkinnedMeshRenderer>(1);
                        float N = 1.0f / tmp.Count; float n = N / 2;
                        foreach (var smr in tmp)
                        {
                            EditorUtility.DisplayProgressBar("优化模型边界 - 精确边界", $"正在处理：{smr.name}", n);
                            Undo.RecordObject(smr, "优化边界");
                            ExactBounds(smr);
                            if (IsNotPartOfPrefabAsset) SaveChangesOnPrefabInstance(smr);
                            n += N;
                        }
                        EditorUtility.ClearProgressBar();
                        if (!IsNotPartOfPrefabAsset) PrefabUtility.SavePrefabAsset(avatar);
                        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                        Logger.Log($"{RichToolName} 已为 {Logger.AssetTag}{avatar.name}{Logger.EndTag} 应用{Logger.ConceptTag}精确边界{Logger.EndTag}{(missingRBs > 0 ? $"（<b>{missingRBs}</b> 个警告）" : "")}", avatar);
                    }
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField("松散倍率", GUILayout.Width(100));
                    looseMultiplier = EditorGUILayout.Slider(looseMultiplier, 1, 2);
                    EditorGUILayout.EndHorizontal();
                    if (GUILayout.Button("松散边界", FacsGUIStyles.Button, GUILayout.Height(40))) LooseBoundsRoutine();
                }
            }
        }

        private static void SaveChangesOnPrefabInstance(Object obj)
        {
            switch (PrefabUtility.GetPrefabAssetType(obj))
            {
                case PrefabAssetType.Regular:
                case PrefabAssetType.Variant:
                case PrefabAssetType.Model:
                    {
                        PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
                        break;
                    }
                case PrefabAssetType.MissingAsset:
                    {
                        PrefabUtility.UnpackPrefabInstance(PrefabUtility.GetOutermostPrefabInstanceRoot(obj),
                            PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
                        SaveChangesOnPrefabInstance(obj);
                        break;
                    }
                default:
                    break;
            }
        }

        private static void ExactBounds(SkinnedMeshRenderer smr)
        {
            smr.updateWhenOffscreen = false;

            var missingRB = false;
            var rootBone = smr.rootBone;
            if (!rootBone)
            {
                rootBone = smr.bones[0];
                if (!rootBone)
                {
                    missingRB = true; missingRBs++;
                    rootBone = smr.transform;
                    Logger.LogWarning($"{RichToolName} 发现一个没有有效{Logger.ConceptTag}根骨骼{Logger.EndTag}的{Logger.RichSkinnedMeshRenderer}：{Logger.AssetTag}{smr.name}{Logger.EndTag}", smr);
                }
            }

            var deltaPos = smr.transform.position - rootBone.position;
            var deltaRot = smr.transform.rotation;
            var smrMatrix = Matrix4x4.TRS(deltaPos, deltaRot, Vector3.one);
            var rootBoneMatrix = rootBone.worldToLocalMatrix;
            Mesh tempMesh = new(); smr.BakeMesh(tempMesh);
            tempMesh.vertices = tempMesh.vertices.Select(v => (Vector3)(rootBoneMatrix*smrMatrix.MultiplyPoint3x4(v))).ToArray();
            tempMesh.RecalculateBounds();
            smr.localBounds = tempMesh.bounds;
            if (!missingRB) smr.rootBone = rootBone;
        }

        private static void LooseBoundsRoutine()
        {
            missingRBs = 0;
            var tmp = skinnedRendHierarchy.GetAllToggles<SkinnedMeshRenderer>(1);
            float N = 1.0f / tmp.Count; float n = N / 2;
            foreach (var smr in tmp)
            {
                EditorUtility.DisplayProgressBar("优化模型边界 - 松散边界", $"正在处理：{smr.name}", n);
                Undo.RecordObject(smr, "优化边界");
                ExactBounds(smr);
                n += N;
            }
            EditorUtility.ClearProgressBar();
            Dictionary<Transform, List<SkinnedMeshRenderer>> groups = new();
            foreach (var smr in tmp)
            {
                var smrKey = smr.rootBone ? smr.rootBone : smr.transform;
                if (groups.ContainsKey(smrKey)) groups[smrKey].Add(smr);
                else groups.Add(smrKey, new List<SkinnedMeshRenderer> { smr });
            }
            foreach (var l in groups.Values) LooseBounds(l);
            if (!IsNotPartOfPrefabAsset) PrefabUtility.SavePrefabAsset(avatar);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            Logger.Log($"{RichToolName} 已为 {Logger.AssetTag}{avatar.name}{Logger.EndTag} 应用{Logger.ConceptTag}松散边界{Logger.EndTag}{(missingRBs>0?$"（<b>{missingRBs}</b> 个警告）":"")}", avatar);
        }

        private static void LooseBounds(List<SkinnedMeshRenderer> smrL)
        {
            Bounds finalBounds = smrL[0].localBounds;
            if (smrL.Count > 1)
            {
                for (int i = 1; i < smrL.Count; i++)
                {
                    finalBounds.Encapsulate(smrL[i].localBounds);
                }
            }
            finalBounds.Encapsulate(Vector3.zero);

            var minX = finalBounds.min.x; var maxX = finalBounds.max.x;
            var minY = finalBounds.min.y; var maxY = finalBounds.max.y;
            var minZ = finalBounds.min.z; var maxZ = finalBounds.max.z;

            var MaxSize = Mathf.Max(maxX, -minX, maxY, -minY, maxZ, -minZ) * looseMultiplier * 2;
            Bounds finalBounds2 = new(Vector3.zero, Vector3.one * MaxSize);
            foreach (SkinnedMeshRenderer smr in smrL)
            {
                smr.localBounds = finalBounds2;
                if (IsNotPartOfPrefabAsset) SaveChangesOnPrefabInstance(smr);
            }
        }

        private static void SelectAll(bool yesno)
        {
            skinnedRendHierarchy.SetAllToggles(typeof(SkinnedMeshRenderer), yesno);
        }

        private static void CollapseAll()
        {
            skinnedRendHierarchy.CollapseHierarchy();
        }

        private static void GetAvailableComponents()
        {
            NullskinnedRend();
            skinnedRendHierarchy = new(avatar.transform); skinnedRendHierarchy.hideTypeFoldout = true;
            bool any = false;
            var avtSMRs = avatar.GetComponents<SkinnedMeshRenderer>();
            foreach (var avtSMR in avtSMRs)
            {
                if (avtSMR.sharedMesh && avtSMR.bones.Length > 0)
                {
                    skinnedRendHierarchy.AddComponentToggle(avtSMR);
                    any = true;
                }
            }
            foreach (Transform childT in avatar.transform)
            {
                if (childT.name == "Armature") continue;
                SkinnedMeshRenderer[] childSMRL = childT.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                foreach (var childSMR in childSMRL)
                {
                    if (childSMR.sharedMesh && childSMR.bones.Length > 0)
                    {
                        skinnedRendHierarchy.AddComponentToggle(childSMR);
                        any = true;
                    }
                }
            }
            skinnedRendHierarchy.SortElements(false, true);
            if (!any)
            {
                Logger.LogWarning($"{RichToolName} {Logger.AssetTag}{avatar.name}{Logger.EndTag} 中没有可编辑的有效{Logger.RichSkinnedMeshRenderer}", avatar);
                NullskinnedRend();
            }
        }

        private static void NullskinnedRend()
        {
            skinnedRendHierarchy = null;
            toggleSelection = true;
        }

        private void OnDestroy()
        {
            NullVars();
        }

        private void NullVars()
        {
            NullskinnedRend();
            FacsGUIStyles = null;
            avatar = null;
        }
    }
}
#endif