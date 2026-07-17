#if UNITY_EDITOR
using UnityEngine;

namespace FACS01.Utilities
{
    public static class Logger
    {
        private static readonly object[] LogFormatArgs = new object[0];

        public const string ToolTag = "<color=cyan>";
        public const string TypeTag = "<color=lime>";
        public const string AssetTag = "<color=lightblue>";
        public const string ConceptTag = "<color=#F0A0A0ff>";
        public const string OffTag = "<color=grey>";
        public const string EndTag = "</color>";

        public const string RichAnimationClip = TypeTag + "动画剪辑" + EndTag;
        public const string RichAnimationClips = TypeTag + "动画剪辑" + EndTag;
        public const string RichAnimatorController = TypeTag + "动画控制器" + EndTag;
        public const string RichAsset = TypeTag + "资源" + EndTag;
        public const string RichAssetBundle = TypeTag + "资源包" + EndTag;
        public const string RichBlendTree = TypeTag + "混合树" + EndTag;
        public const string RichGameObject = TypeTag + "游戏对象" + EndTag;
        public const string RichScene = TypeTag + "场景" + EndTag;
        public const string RichSkinnedMeshRenderer = TypeTag + "蒙皮网格渲染器" + EndTag;

        public const string RichAssetsFolder = ConceptTag + "资源文件夹" + EndTag;
        public const string RichModel = ConceptTag + "模型" + EndTag;
        public const string RichModelPrefab = ConceptTag + "模型预制体" + EndTag;

        public static void Log(object message)
        {
            LogFormat(LogType.Log, message, null);
        }

        public static void Log(object message, Object context)
        {
            LogFormat(LogType.Log, message, context);
        }

        public static void LogWarning(object message)
        {
            LogFormat(LogType.Warning, message, null);
        }

        public static void LogWarning(object message, Object context)
        {
            LogFormat(LogType.Warning, message, context);
        }

        public static void LogError(object message)
        {
            LogFormat(LogType.Error, message, null);
        }

        public static void LogError(object message, Object context)
        {
            LogFormat(LogType.Error, message, context);
        }

        private static void LogFormat(LogType type, object message, Object context)
        {
            if (message is not string msg) msg = message.ToString();
            Debug.LogFormat(type, LogOption.NoStacktrace, context, msg, LogFormatArgs);
        }
    }
}
#endif