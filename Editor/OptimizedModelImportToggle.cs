#if UNITY_EDITOR
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
	internal static class OptimizedModelImportToggle
    {
		private const string RichToolName = Logger.ToolTag + "[优化模型导入]" + Logger.EndTag;
		private const string MenuPath = "FACS Utils/模型工具/优化模型导入/";
		private static bool IsEnable;

		[InitializeOnLoadMethod]
		private static void OnInitialized()
		{
			IsEnable = GetAsmdefEnabled();
		}

		private static AsmdefData GetAsmdef(out string omiAsmPath)
        {
			var omiAsmGUID = AssetDatabase.FindAssets("a:packages t:asmdef FACS01.Utilities.Editor.OMI")[0];
			omiAsmPath = AssetDatabase.GUIDToAssetPath(omiAsmGUID);
			string omiJson = File.ReadAllText(omiAsmPath);
			var omiAsmDef = JsonUtility.FromJson<AsmdefData>(omiJson);
			return omiAsmDef;
		}

		private static bool GetAsmdefEnabled()
        {
			var oniAsmDef = GetAsmdef(out _);
			return oniAsmDef.defineConstraints.Length == 0;
		}

		private static void SetAsmdefEnabled(bool enable)
		{
			var oniAsmDef = GetAsmdef(out string omiAsmPath);
			if (enable) oniAsmDef.defineConstraints = new string[0];
			else oniAsmDef.defineConstraints = new string[1] { "FACSUTILS_DISABLE_OMI" };

			File.WriteAllText(omiAsmPath, JsonUtility.ToJson(oniAsmDef, true));
			AssetDatabase.ImportAsset(omiAsmPath, ImportAssetOptions.ForceUpdate);
		}

		[MenuItem(MenuPath + "启用", true, 1101)]
		private static bool IsDisabled() => !IsEnable;

		[MenuItem(MenuPath + "禁用", true, 1102)]
		private static bool IsEnabled() => IsEnable;

		[MenuItem(MenuPath + "启用", false, 1101)]
		private static void ToggleOn()
		{
			SetAsmdefEnabled(true);
			Logger.Log($"{RichToolName} 已启用！正在重新加载脚本...");
		}

		[MenuItem(MenuPath + "禁用", false, 1102)]
		private static void ToggleOff()
		{
			SetAsmdefEnabled(false);
			Logger.Log($"{RichToolName} 已禁用。正在重新加载脚本...");
		}

		[MenuItem(MenuPath + "重新导入全部", false, 1103)]
		private static void ReimportAll()
		{
			var msg = "确定要重新导入项目中的所有模型吗？这可能需要一些时间。\n\n";
			if (IsEnable) msg += "将使用优化模型导入！";
			else msg += "优化模型导入未启用。";
			if (!EditorUtility.DisplayDialog("FACS工具 - 重新导入模型", msg, "是", "否"))
				return;

			string[] modelGUIDs = AssetDatabase.FindAssets("a:assets t:model");
			if (modelGUIDs.Length == 0)
			{
				Logger.LogWarning($"{RichToolName} 在项目的{Logger.RichAssetsFolder}中未找到{Logger.RichModel}。");
				return;
			}

			string[] modelPaths = modelGUIDs.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).Where(path => !string.IsNullOrEmpty(path)).ToArray();
			if (modelPaths.Length == 0)
			{
				Logger.LogWarning($"{RichToolName} 在项目的{Logger.RichAssetsFolder}中未找到有效的{Logger.RichModel}。");
				return;
			}

			foreach (string path in modelPaths)
			{
				Logger.Log($"{RichToolName} 正在重新导入{Logger.RichModel}：{path}。");
				AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			}
			Logger.Log($"{RichToolName} 已重新导入<b>{modelPaths.Length}</b>个{Logger.RichModel}。");
		}

		[System.Serializable]
		private class AsmdefData
		{
			public string name;
			public string rootNamespace;
			public string[] references;
			public string[] includePlatforms;
			public string[] excludePlatforms;
			public bool allowUnsafeCode;
			public bool overrideReferences;
			public string[] precompiledReferences;
			public bool autoReferenced;
			public string[] defineConstraints;
			public string[] versionDefines;
			public bool noEngineReferences;
		}
	}
}
#endif