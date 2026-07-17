#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace FACS01.Utilities
{
    [CustomEditor(typeof(FACSFastPhoto))]
    internal class FACSFastPhotoEditor : Editor
    {
        FACSFastPhoto _target;
        SerializedProperty photoWidth;
        SerializedProperty photoHeight;
        SerializedProperty saveInProj;
        SerializedProperty camBg;
        SerializedProperty bgColor;
        SerializedProperty settingsFoldout;
        SerializedProperty previewFoldout;
        bool _previewFoldout;
        Camera _cam;
        bool camModified = false;
        bool showCam;
        [Range(0.00001f, 179)]
        float camFOV;
        [Min(0.01f)]
        float nearPlane;
        [Min(1.01f)]
        float farPlane;

        static readonly GUIContent label = new GUIContent("");

        void OnEnable()
        {
            photoWidth = serializedObject.FindProperty("PhotoWidth");
            photoHeight = serializedObject.FindProperty("PhotoHeight");
            saveInProj = serializedObject.FindProperty("saveInProj");
            camBg = serializedObject.FindProperty("camBg");
            bgColor = serializedObject.FindProperty("bgColor");
            settingsFoldout = serializedObject.FindProperty("settingsFoldout");
            previewFoldout = serializedObject.FindProperty("previewFoldout");
            _previewFoldout = previewFoldout.boolValue;
            _target = (FACSFastPhoto)target;
            _cam = _target.cam;
            showCam = !_cam.hideFlags.HasFlag(HideFlags.HideInInspector);
            camFOV = _target.cam.GetGateFittedFieldOfView();
            nearPlane = _cam.nearClipPlane;
            farPlane = _cam.farClipPlane;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var labelWidth = EditorGUIUtility.labelWidth;

            try
            {
                EditorGUI.BeginChangeCheck();
                var _settingsFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(settingsFoldout.boolValue, "图像和相机设置");
                if (EditorGUI.EndChangeCheck()) settingsFoldout.boolValue = _settingsFoldout;
                if (_settingsFoldout)
                {
                    RunSettingsGUI();
                    EditorGUILayout.Space(15);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical();
                EditorGUI.BeginChangeCheck();
                _previewFoldout = EditorGUILayout.BeginFoldoutHeaderGroup(previewFoldout.boolValue, "图像预览");
                if (EditorGUI.EndChangeCheck()) previewFoldout.boolValue = _previewFoldout;
                if (_previewFoldout)
                {
                    EditorGUILayout.Space(8);
                    var previewHeight = _settingsFoldout ? 300 : 500;
                    var rect = EditorGUILayout.GetControlRect(false, previewHeight);
                    EditorGUI.DrawPreviewTexture(rect, _cam.targetTexture, null, ScaleMode.ScaleToFit, 0);
                }
                EditorGUILayout.EndFoldoutHeaderGroup();

                EditorGUILayout.Space(12);

                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                var takePhoto = GUILayout.Button("拍照！", GUILayout.Height(30), GUILayout.Width(120));
                if (takePhoto) { GUI.FocusControl(null); }
                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.EndVertical();
                EditorGUILayout.Space(12);
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space(12);

                serializedObject.ApplyModifiedProperties();
                if (camModified) { camModified = false; EditorUtility.SetDirty(_cam); }
                if (takePhoto) _target.TakePhoto();
            }
            catch
            {
                throw;
            }
            finally
            {
                EditorGUIUtility.labelWidth = labelWidth;
            }
        }

        private void RunSettingsGUI()
        {
            EditorGUIUtility.labelWidth = 163;
            EditorGUI.BeginChangeCheck();
            showCam = EditorGUILayout.Toggle("显示相机组件", showCam);
            if (EditorGUI.EndChangeCheck())
            {
                if (showCam) _cam.hideFlags = HideFlags.None;
                else _cam.hideFlags = HideFlags.HideInInspector;
                _cam.enabled = _target.enabled;
                camModified = true;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("图像分辨率", GUILayout.Width(120));
            EditorGUIUtility.labelWidth = 40; label.text = "宽度";
            EditorGUILayout.PropertyField(photoWidth, label, GUILayout.MinWidth(40));
            EditorGUIUtility.labelWidth = 46; label.text = " 高度";
            EditorGUILayout.PropertyField(photoHeight, label, GUILayout.MinWidth(40));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(2);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("HD")) { GUI.FocusControl(null); SetPhotoSize(1920, 1080); }
            if (GUILayout.Button("2K")) { GUI.FocusControl(null); SetPhotoSize(2560, 1440); }
            if (GUILayout.Button("4K")) { GUI.FocusControl(null); SetPhotoSize(3840, 2160); }
            if (GUILayout.Button("8K")) { GUI.FocusControl(null); SetPhotoSize(7680, 4320); }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("旋转横屏/竖屏"))
            {
                GUI.FocusControl(null);
                var tmp = photoWidth.intValue;
                photoWidth.intValue = photoHeight.intValue;
                photoHeight.intValue = tmp;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("背景", GUILayout.Width(120));
            label.text = "";
            EditorGUILayout.PropertyField(camBg, label, GUILayout.MinWidth(40));
            EditorGUILayout.PropertyField(bgColor, label, GUILayout.MinWidth(40));
            EditorGUILayout.EndHorizontal();

            camFOV = _cam.fieldOfView;
            EditorGUIUtility.labelWidth = 120;
            EditorGUI.BeginChangeCheck();
            camFOV = EditorGUILayout.Slider("视野", camFOV, 0.00001f, 179);
            if (EditorGUI.EndChangeCheck()) { _cam.fieldOfView = camFOV; camModified = true; }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("裁剪平面", GUILayout.Width(120));
            EditorGUIUtility.labelWidth = 35;
            EditorGUI.BeginChangeCheck();
            nearPlane = EditorGUILayout.FloatField("近", nearPlane);
            if (EditorGUI.EndChangeCheck()) _cam.nearClipPlane = nearPlane;
            EditorGUIUtility.labelWidth = 28;
            EditorGUI.BeginChangeCheck();
            farPlane = EditorGUILayout.FloatField(" 远", farPlane);
            if (EditorGUI.EndChangeCheck()) _cam.farClipPlane = farPlane;
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(3);

            label.text = "保存到Assets"; EditorGUIUtility.labelWidth = 120;
            EditorGUILayout.PropertyField(saveInProj, label, GUILayout.MinWidth(40));
        }

        private void SetPhotoSize(int w, int h)
        {
            var landscape = photoWidth.intValue >= photoHeight.intValue;
            if (landscape)
            {
                photoWidth.intValue = w;
                photoHeight.intValue = h;
            }
            else
            {
                photoWidth.intValue = h;
                photoHeight.intValue = w;
            }
        }

        public override bool RequiresConstantRepaint()
        {
            return _previewFoldout;
        }

        [MenuItem("FACS Utils/杂项/快速拍照", false, 1100)]
        private static void GetFFP()
        {
            GameObject ffpGO;
            var ffps = FindObjectsByType<FACSFastPhoto>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (ffps.Length != 0 && ffps[0] && ffps[0].gameObject)
            {
                ffpGO = ffps[0].gameObject;
            }
            else
            {
                ffpGO = new GameObject("快速拍照", typeof(FACSFastPhoto));
            }
            EditorGUIUtility.PingObject(ffpGO);
            Selection.activeObject = ffpGO;
        }
    }
}
#endif