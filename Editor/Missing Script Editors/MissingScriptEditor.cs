#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace FACS01.Utilities
{
    internal abstract class MissingScriptEditor : Editor
    {
        protected abstract FixScripts.ScriptType GetEditingType();

        private const string RichToolName = Logger.ToolTag + "[缺失脚本编辑器]" + Logger.EndTag;

        protected long FileID = 0;
        protected string GUID = "";
        protected string[] YAMLDatas = null;
        protected int YAMLDatasPage = 0;
        protected TextField textArea = null;
        protected Label YAMLPageLabel = null;

        [InitializeOnLoadMethod]
        private static void Init()
        {
            EditorApplication.delayCall += ScriptReloadCleanup;
        }

        private static void ScriptReloadCleanup()
        {
            var MSEs = Resources.FindObjectsOfTypeAll<MissingScriptEditor>();
            if (MSEs.Length == 0) return;
            var GoodMSEs = new HashSet<MissingScriptEditor>();
            var Inspectors = ReflectionTools.GetAllInspectorWindows();
            if (Inspectors.Length != 0)
            {
                foreach (var inspector in Inspectors)
                {
                    var tracker = ReflectionTools.GetInspectorWindowTracker(inspector);
                    if (tracker == null) continue;
                    foreach (var editor in tracker.activeEditors)
                    {
                        if (editor is MissingScriptEditor mse) GoodMSEs.Add(mse);
                    }
                }
            }
            foreach (var mse in MSEs)
            {
                if (!GoodMSEs.Contains(mse)) DestroyImmediate(mse);
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            VisualElement VE = new();
            VE.Add(new HelpBox(GetMissingScriptMessage(), HelpBoxMessageType.Warning));

            GetYAMLData();

            if (FileID != 0 || GUID != "")
            {
                VE.AddManipulator(new ContextualMenuManipulator((evt) =>
                {
                    evt.menu.InsertAction(0, "FACS Utils/添加到修复脚本", AddToFixScripts, DropdownMenuAction.AlwaysEnabled);
                    evt.menu.InsertSeparator("FACS Utils", 0);
                }));
                var guid = new TextField("脚本GUID：") { isReadOnly = true };
                guid.SetValueWithoutNotify($"{GUID}");
                guid.style.marginRight = 3;
                VE.Add(guid);
                var fileid = new TextField("脚本FileID：") { isReadOnly = true };
                fileid.SetValueWithoutNotify($"{FileID}");
                fileid.style.marginRight = 3;
                VE.Add(fileid);
            }

            textArea = new() { isReadOnly = true }; textArea.style.marginRight = 3;

            if (YAMLDatas != null)
            {
                YAMLDatasPage = 0;
                textArea.value = YAMLDatas[YAMLDatasPage]; textArea.style.marginLeft = -11;
                var foldout = new Foldout{ text = "<b>FACS工具 - 缺失脚本数据：</b>", value = false };

                if (YAMLDatas.Length > 1)
                {
                    var buttonContainer = new VisualElement();
                    buttonContainer.style.alignItems = Align.Center;
                    buttonContainer.style.flexDirection = FlexDirection.Row;
                    buttonContainer.style.justifyContent = Justify.SpaceBetween;
                    buttonContainer.style.marginRight = 15;

                    var prevButton = new Button { text = "<b>\u2190</b>" };
                    prevButton.clickable.clicked += () => { ChangeIndex(-1); };
                    buttonContainer.Add(prevButton);

                    YAMLPageLabel = new() { text = $"<b>第 {YAMLDatasPage+1} 页，共 {YAMLDatas.Length} 页</b>" };
                    buttonContainer.Add(YAMLPageLabel);

                    var nextButton = new Button { text = "<b>\u2192</b>" };
                    nextButton.clickable.clicked += () => { ChangeIndex(1); };
                    buttonContainer.Add(nextButton);

                    foldout.contentContainer.Add(buttonContainer);
                }

                foldout.contentContainer.Add(textArea);
                VE.Add(foldout);
            }
            else
            {
                textArea.SetEnabled(false);
                textArea.value = "\nFACS工具 - 脚本没有数据。\n";
                if (IsAnyMonoBehaviourTargetPartOfPrefabInstance())
                {
                    textArea.value += "在其预制体资产中查找此MonoBehaviour。\n";
                }
                VE.Add(textArea);
            }
            return VE;
        }

        protected void GetYAMLData()
        {
            if (object.Equals(target, null)) return;
            var yamlData = ReflectionTools.ToYAMLString(target);
            if (string.IsNullOrEmpty(yamlData)) return;
            if (ReflectionTools.TryGetScriptGUIDandFileIDfromYAML(yamlData, out var guid, out var fileID))
            { GUID = guid; FileID = fileID; }

            var TrimData = yamlData.IndexOf("\n  m_EditorClassIdentifier: ");
            if (TrimData != -1) yamlData = yamlData[(yamlData.IndexOf("\n", TrimData + 1)+1)..];
            if (yamlData.Length > 1 && yamlData[^1] == '\n') yamlData = yamlData[..^1];
            if (string.IsNullOrEmpty(yamlData)) return;

            var yamlSplit = new List<string>(); var sb = new StringBuilder();
            using (var reader = new System.IO.StringReader(yamlData))
            {
                string line = reader.ReadLine(); var newLine = false;
                while (line != null)
                {
                    if (sb.Length > 0 && line.Length + sb.Length > 10000)
                    {
                        yamlSplit.Add(sb.ToString());
                        sb.Clear(); newLine = false;
                    }
                    if (newLine) sb.Append('\n');
                    sb.Append(line); newLine = true;

                    line = reader.ReadLine();
                }
                if (sb.Length > 0) { yamlSplit.Add(sb.ToString()); sb.Clear(); }
            }
            YAMLDatas = yamlSplit.ToArray();
        }

        private void AddToFixScripts(DropdownMenuAction x)
        {
            if (FixScripts.manualFixes.Count > 0)
            {
                foreach (var mf in FixScripts.manualFixes)
                {
                    if (mf.oldGUID == GUID && mf.oldFileID == FileID)
                    {
                        Logger.LogWarning(RichToolName + " 缺失脚本已在" + Logger.ConceptTag + "手动修复" + Logger.EndTag + "列表中！");
                        return;
                    }
                }
                var lastMF = FixScripts.manualFixes[^1];
                if (string.IsNullOrEmpty(lastMF.oldGUID) && lastMF.oldFileID == 0)
                {
                    FixScripts.manualFixes.RemoveAt(FixScripts.manualFixes.Count - 1);
                }
            }
            var newmf = new FixScripts.ManualFix() { oldGUID = GUID, oldFileID = FileID, type = GetEditingType(), foldout = true };
            FixScripts.manualFixes.Add(newmf);
            FixScripts.ShowWindow(); FixScripts.manual = true;
        }

        private string GetMissingScriptMessage()
        {
            if (IsAnyMonoBehaviourTargetPartOfPrefabInstance())
                return L10n.Tr("The associated script can not be loaded.\nPlease fix any compile errors\nand open Prefab Mode and assign a valid script to the Prefab Asset.");
            else
                return L10n.Tr("The associated script can not be loaded.\nPlease fix any compile errors\nand assign a valid script.");
        }

        private void ChangeIndex(int delta)
        {
            YAMLDatasPage += delta;
            YAMLDatasPage = Mathf.Clamp(YAMLDatasPage, 0, YAMLDatas.Length - 1);
            textArea.value = YAMLDatas[YAMLDatasPage];
            YAMLPageLabel.text = $"<b>第 {YAMLDatasPage + 1} 页，共 {YAMLDatas.Length} 页</b>";
            Repaint();
        }

        private bool IsAnyMonoBehaviourTargetPartOfPrefabInstance()
        {
            if (!(target is MonoBehaviour)) return false;
            foreach (Object obj in targets)
                if (ReflectionTools.IsInstanceIDPartOfNonAssetPrefabInstance(obj.GetInstanceID())) return true;
            return false;
        }
    }
}
#endif