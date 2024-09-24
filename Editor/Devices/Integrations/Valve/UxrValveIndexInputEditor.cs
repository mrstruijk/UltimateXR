// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UxrValveIndexInputEditor.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Linq;
using UltimateXR.Avatar;
using UltimateXR.Devices.Integrations.Valve;
using UltimateXR.Editor.Manipulation.HandPoses;
using UltimateXR.Extensions.System.Collections;
using UltimateXR.Extensions.Unity;
using UnityEditor;
using UnityEngine;


namespace UltimateXR.Editor.Devices.Integrations.Valve
{
    /// <summary>
    ///     Custom Unity editor for the <see cref="UxrValveIndexInput" /> component.
    /// </summary>
    [CustomEditor(typeof(UxrValveIndexInput))]
    public class UxrValveIndexInputEditor : UxrControllerInputEditor
    {
        #region Unity

        /// <summary>
        ///     Caches the serialized properties.
        /// </summary>
        public void OnEnable()
        {
            _propertyOpenHandPoseName = serializedObject.FindProperty("_openHandPoseName");
            _propertyIndexCurlAmount = serializedObject.FindProperty("_indexCurlAmount");
            _propertyMiddleCurlAmount = serializedObject.FindProperty("_middleCurlAmount");
            _propertyRingCurlAmount = serializedObject.FindProperty("_ringCurlAmount");
            _propertyLittleCurlAmount = serializedObject.FindProperty("_littleCurlAmount");
            _propertyThumbCurlAmount = serializedObject.FindProperty("_thumbCurlAmount");
            _propertyThumbSpreadAmount = serializedObject.FindProperty("_thumbSpreadAmount");
        }


        /// <inheritdoc />
        public override void OnInspectorGUI()
        {
            // Draw base GUI
            base.OnInspectorGUI();

            serializedObject.Update();

            var avatar = (serializedObject.targetObject as UxrValveIndexInput)?.gameObject.SafeGetComponentInParent<UxrAvatar>();
            var poseNames = UxrHandPoseEditorWindow.GetAvatarPoseNames(avatar);

            if (poseNames.Count == 0 || _propertyOpenHandPoseName == null)
            {
                EditorGUILayout.HelpBox("Avatar has no hand poses available to set the open hand pose when grabbing the Index controllers", MessageType.Warning);
            }
            else
            {
                EditorGUILayout.Space();

                EditorGUI.BeginChangeCheck();
                var openPoseNameIndex = EditorGUILayout.Popup(ContentOpenHandPoseName, poseNames.IndexOf(_propertyOpenHandPoseName.stringValue), UxrEditorUtils.ToGUIContentArray(poseNames.ToArray()));

                if (EditorGUI.EndChangeCheck())
                {
                    _propertyOpenHandPoseName.stringValue = poseNames[openPoseNameIndex];
                }

                EditorGUILayout.Slider(_propertyIndexCurlAmount, 0.0f, 90.0f, ContentIndexCurlAmount);
                EditorGUILayout.Slider(_propertyMiddleCurlAmount, 0.0f, 90.0f, ContentMiddleCurlAmount);
                EditorGUILayout.Slider(_propertyRingCurlAmount, 0.0f, 90.0f, ContentRingCurlAmount);
                EditorGUILayout.Slider(_propertyLittleCurlAmount, 0.0f, 90.0f, ContentLittleCurlAmount);
                EditorGUILayout.Slider(_propertyThumbCurlAmount, 0.0f, 90.0f, ContentThumbCurlAmount);
                EditorGUILayout.Slider(_propertyThumbSpreadAmount, 0.0f, 90.0f, ContentThumbSpreadAmount);
            }

            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
        }

        #endregion

        #region Private Types & Data

        private GUIContent ContentOpenHandPoseName { get; } = new("Open Hand Pose", "Selects the hand pose that will be used as default when Index Controllers are enabled. Fingers will be curled using the tracking values starting from this pose.");
        private GUIContent ContentIndexCurlAmount { get; } = new("Index Curl Amount", "");
        private GUIContent ContentMiddleCurlAmount { get; } = new("Middle Curl Amount", "");
        private GUIContent ContentRingCurlAmount { get; } = new("Ring Curl Amount", "");
        private GUIContent ContentLittleCurlAmount { get; } = new("Little Curl Amount", "");
        private GUIContent ContentThumbCurlAmount { get; } = new("Thumb Curl Amount", "");
        private GUIContent ContentThumbSpreadAmount { get; } = new("Thumb Spread Amount", "");

        private SerializedProperty _propertyOpenHandPoseName;
        private SerializedProperty _propertyIndexCurlAmount;
        private SerializedProperty _propertyMiddleCurlAmount;
        private SerializedProperty _propertyRingCurlAmount;
        private SerializedProperty _propertyLittleCurlAmount;
        private SerializedProperty _propertyThumbCurlAmount;
        private SerializedProperty _propertyThumbSpreadAmount;

        #endregion
    }
}