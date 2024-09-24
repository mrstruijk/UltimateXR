// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UxrPlanarReflectionBrp.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UltimateXR.Core.Components;
using UltimateXR.Devices;
using UltimateXR.Extensions.Unity;
using UltimateXR.Extensions.Unity.Math;
using UnityEngine;
using UnityEngine.XR;


namespace UltimateXR.Rendering.FX
{
    /// <summary>
    ///     Component that renders a planar reflection image of the scene on an object, using the built-in render pipeline:
    ///     <list type="bullet">
    ///         <item>
    ///             If the Mirror Transform is not set, it will use the transform on the component's GameObject.
    ///         </item>
    ///         <item>
    ///             The component requires a Renderer on the same GameObject with a material compatible with the BRP planar
    ///             reflection. They can be found in the UltimateXR/FX/ category.
    ///         </item>
    ///         <item>The mirror normal is determined by the -forward axis of Mirror Transform.</item>
    ///     </list>
    /// </summary>
    [ExecuteInEditMode]
    public class UxrPlanarReflectionBrp : UxrComponent
    {
        #region Inspector Properties/Serialized Fields

        // Inspector

        [SerializeField] private bool _forceClearSkyBox;
        [SerializeField] private Transform _mirrorTransform;
        [SerializeField] private bool _disablePixelLights = true;
        [SerializeField] private int _textureSize = 1024;
        [SerializeField] private float _clipPlaneOffset = 0.07f;
        [SerializeField] private LayerMask _reflectLayers = -1;

        #endregion

        #region Unity

        /// <summary>
        ///     Frees the allocated resources.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            foreach (var camPair in _reflectionCameras)
            {
                if (camPair.Value != null)
                {
                    DestroyImmediate(camPair.Value.gameObject);
                }
            }

            if (_reflectionTextureLeft)
            {
                DestroyImmediate(_reflectionTextureLeft);
                _reflectionTextureLeft = null;
            }

            if (_reflectionTextureRight)
            {
                DestroyImmediate(_reflectionTextureRight);
                _reflectionTextureRight = null;
            }

            _reflectionCameras.Clear();
        }


        /// <summary>
        ///     Called by Unity when the object will be rendered. It is used to render the reflection.
        /// </summary>
        private void OnWillRenderObject()
        {
            var mirrorRenderer = GetComponent<Renderer>();
            _mirrorTransform = _mirrorTransform ? _mirrorTransform : transform;

            if (!enabled || !mirrorRenderer || !mirrorRenderer.sharedMaterial || !mirrorRenderer.enabled)
            {
                return;
            }

            var cam = Camera.current;

            if (!cam)
            {
                return;
            }

            if (_reflectionCameras.ContainsValue(cam))
            {
                return;
            }

            // Avoid recursive rendering

            if (s_insideRendering)
            {
                return;
            }

            s_insideRendering = true;

            CreateResources(cam, out var reflectionCamera);

            // Lower quality for reflection

            var oldPixelLightCount = QualitySettings.pixelLightCount;

            if (_disablePixelLights)
            {
                QualitySettings.pixelLightCount = 0;
            }

            CopyCameraData(cam, reflectionCamera);

            // Update parameters

            reflectionCamera.cullingMask = ~(1 << 4) & _reflectLayers.value;

            if (TryGetComponent<Renderer>(out var theRenderer))
            {
                foreach (var m in theRenderer.sharedMaterials)
                {
                    if (m.HasProperty(VarReflectionTexLeft))
                    {
                        m.SetTexture(VarReflectionTexLeft, _reflectionTextureLeft);
                    }

                    if (m.HasProperty(VarReflectionTexRight))
                    {
                        m.SetTexture(VarReflectionTexRight, _reflectionTextureRight);
                    }

                    m.SetFloat(VarReflectionTexelSize, _reflectionTextureLeft.width == 0 ? 1.0f : 1.0f / _reflectionTextureLeft.width);
                    m.SetFloat(VarReflectionMaxLodBias, _reflectionTextureLeft.width == 0 ? 0.0f : Mathf.Log(_reflectionTextureLeft.width, 2.0f));
                    m.SetInt(VarStereo, cam.stereoEnabled ? 1 : 0);
                }
            }

            // Render

            if (cam.stereoEnabled)
            {
                if (cam.stereoTargetEye == StereoTargetEyeMask.Both || cam.stereoTargetEye == StereoTargetEyeMask.Left)
                {
                    reflectionCamera.targetTexture = _reflectionTextureLeft;
                    RenderReflection(cam, reflectionCamera, true, true, _mirrorTransform.position, -_mirrorTransform.forward);
                }

                if (cam.stereoTargetEye == StereoTargetEyeMask.Both || cam.stereoTargetEye == StereoTargetEyeMask.Right)
                {
                    reflectionCamera.targetTexture = _reflectionTextureRight;
                    RenderReflection(cam, reflectionCamera, true, false, _mirrorTransform.position, -_mirrorTransform.forward);
                }
            }
            else
            {
                reflectionCamera.targetTexture = _reflectionTextureLeft;
                RenderReflection(cam, reflectionCamera, false, false, _mirrorTransform.position, -_mirrorTransform.forward);
            }

            // Restore quality

            if (_disablePixelLights)
            {
                QualitySettings.pixelLightCount = oldPixelLightCount;
            }

            s_insideRendering = false;
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Given a plane point and normal in world coordinates, computes the plane in camera space.
        /// </summary>
        /// <param name="targetCamera">Camera</param>
        /// <param name="offset">Clip plane offset</param>
        /// <param name="position">Point in plane</param>
        /// <param name="normal">Plane normal</param>
        /// <param name="sideSign">Plane side of the camera</param>
        /// <returns>Plane in camera space</returns>
        private Vector4 CameraSpacePlane(Camera targetCamera, float offset, Vector3 position, Vector3 normal, float sideSign)
        {
            var offsetPos = position + normal * offset;
            var worldToCameraMatrix = targetCamera.worldToCameraMatrix;
            var localPos = worldToCameraMatrix.MultiplyPoint(offsetPos);
            var localNormal = worldToCameraMatrix.MultiplyVector(normal).normalized * sideSign;

            return new Vector4(localNormal.x, localNormal.y, localNormal.z, -Vector3.Dot(localPos, localNormal));
        }


        /// <summary>
        ///     Renders the reflection.
        /// </summary>
        /// <param name="renderCamera">Main camera</param>
        /// <param name="reflectionCamera">Camera that will render the reflection</param>
        /// <param name="stereo">Is stereo mode active?</param>
        /// <param name="isLeft">Is it the left eye in stereo mode?</param>
        /// <param name="pos">Reflection plane position</param>
        /// <param name="normal">Reflection plane normal</param>
        private void RenderReflection(Camera renderCamera, Camera reflectionCamera, bool stereo, bool isLeft, Vector3 pos, Vector3 normal)
        {
            reflectionCamera.ResetWorldToCameraMatrix();
            reflectionCamera.ResetCullingMatrix();

            var camPos = renderCamera.transform.position;
            var camRot = renderCamera.transform.rotation;

            // Reflect camera using reflection plane

            var d = -Vector3.Dot(normal, pos) - _clipPlaneOffset;
            var reflectionPlane = new Vector4(normal.x, normal.y, normal.z, d);

            var reflection = Matrix4x4Ext.GetReflectionMatrix(reflectionPlane);
            var offset = Vector3.zero;

            var projection = renderCamera.projectionMatrix;

            if (stereo && UxrTrackingDevice.GetHeadsetDevice(out var headsetDevice))
            {
                reflectionCamera.transform.parent = renderCamera.transform;

                headsetDevice.TryGetFeatureValue(CommonUsages.centerEyePosition, out var centerEyePos);
                headsetDevice.TryGetFeatureValue(CommonUsages.centerEyeRotation, out var centerEyeRot);
                headsetDevice.TryGetFeatureValue(CommonUsages.leftEyePosition, out var leftEyePos);
                headsetDevice.TryGetFeatureValue(CommonUsages.leftEyeRotation, out var leftEyeRot);
                headsetDevice.TryGetFeatureValue(CommonUsages.rightEyePosition, out var rightEyePos);
                headsetDevice.TryGetFeatureValue(CommonUsages.rightEyeRotation, out var rightEyeRot);

                renderCamera.transform.SetPositionAndRotation(centerEyePos, centerEyeRot);

                if (isLeft)
                {
                    reflectionCamera.transform.SetPositionAndRotation(leftEyePos, leftEyeRot);
                    projection = renderCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Left);
                }
                else
                {
                    reflectionCamera.transform.SetPositionAndRotation(rightEyePos, rightEyeRot);
                    projection = renderCamera.GetStereoProjectionMatrix(Camera.StereoscopicEye.Right);
                }

                renderCamera.transform.SetPositionAndRotation(camPos, camRot);
            }
            else
            {
                reflectionCamera.transform.SetPositionAndRotation(renderCamera.transform.position, renderCamera.transform.rotation);
            }

            // World->ReflectionCamera matrix

            reflectionCamera.worldToCameraMatrix *= reflection;

            // Create projection matrix. Near plane will be our reflection plane so that we will clip everything on the other side.

            var clipPlane = CameraSpacePlane(reflectionCamera, _clipPlaneOffset, pos, normal, 1.0f);
            projection = projection.GetObliqueMatrix(clipPlane);
            reflectionCamera.projectionMatrix = projection;
            reflectionCamera.cullingMatrix = reflectionCamera.projectionMatrix * reflectionCamera.worldToCameraMatrix;

            // Render

            GL.invertCulling = true;
            reflectionCamera.Render();
            GL.invertCulling = false;

            reflectionCamera.ResetWorldToCameraMatrix();
            reflectionCamera.ResetCullingMatrix();
        }


        /// <summary>
        ///     Copy data from one camera to another
        /// </summary>
        /// <param name="src">Source data</param>
        /// <param name="dest">Destination data</param>
        private void CopyCameraData(Camera src, Camera dest)
        {
            if (dest == null)
            {
                return;
            }

            if (_forceClearSkyBox == false)
            {
                dest.clearFlags = src.clearFlags;
                dest.backgroundColor = src.backgroundColor;

                if (src.clearFlags == CameraClearFlags.Skybox)
                {
                    var skySrc = src.GetComponent(typeof(Skybox)) as Skybox;
                    var skyDst = dest.GetComponent(typeof(Skybox)) as Skybox;

                    if (skyDst)
                    {
                        if (!skySrc || !skySrc.material)
                        {
                            skyDst.enabled = false;
                        }
                        else
                        {
                            skyDst.enabled = true;
                            skyDst.material = skySrc.material;
                        }
                    }
                }
            }

            dest.farClipPlane = src.farClipPlane;
            dest.nearClipPlane = src.nearClipPlane;
            dest.orthographic = src.orthographic;

            if (XRSettings.enabled == false)
            {
                dest.fieldOfView = src.fieldOfView;
            }

            dest.aspect = src.aspect;
            dest.orthographicSize = src.orthographicSize;
        }


        /// <summary>
        ///     Creates the internal resources if necessary.
        /// </summary>
        /// <param name="currentCamera">Render camera</param>
        /// <param name="reflectionCamera">Reflection camera</param>
        private void CreateResources(Camera currentCamera, out Camera reflectionCamera)
        {
            reflectionCamera = null;

            // Render textures

            if (_oldReflectionTextureSize != _textureSize)
            {
                CreateRenderTexture(ref _reflectionTextureLeft);
                CreateRenderTexture(ref _reflectionTextureRight);
                _oldReflectionTextureSize = _textureSize;
            }

            if (_reflectionTextureLeft == null)
            {
                CreateRenderTexture(ref _reflectionTextureLeft);
            }

            if (_reflectionTextureRight == null)
            {
                CreateRenderTexture(ref _reflectionTextureRight);
            }

            // Reflection camera

            _reflectionCameras.TryGetValue(currentCamera, out reflectionCamera);

            if (!reflectionCamera)
            {
                var go = new GameObject($"{nameof(UxrPlanarReflectionBrp)} Camera", typeof(Camera), typeof(Skybox));
                reflectionCamera = go.GetComponent<Camera>();

                if (XRSettings.enabled == false)
                {
                    reflectionCamera.fieldOfView = 60.0f;
                }

                reflectionCamera.transform.SetPositionAndRotation(transform);
                reflectionCamera.enabled = true;
                go.hideFlags = HideFlags.HideAndDontSave;
                _reflectionCameras[currentCamera] = reflectionCamera;

                if (_forceClearSkyBox)
                {
                    reflectionCamera.clearFlags = CameraClearFlags.Skybox;
                }
            }
        }


        /// <summary>
        ///     Creates a render texture.
        /// </summary>
        /// <param name="texture">Texture to create</param>
        private void CreateRenderTexture(ref RenderTexture texture)
        {
            if (texture)
            {
                DestroyImmediate(texture);
            }

            texture = new RenderTexture(_textureSize, _textureSize, 16);

            texture.name = $"{nameof(UxrPlanarReflectionBrp)} Reflection";
            texture.isPowerOfTwo = true;
            texture.hideFlags = HideFlags.DontSave;
            texture.filterMode = FilterMode.Trilinear;
            texture.autoGenerateMips = true;
            texture.useMipMap = true; // We will use auto mip-mapping in our shader for blur
        }

        #endregion

        #region Private Types & Data

        // Constants

        private const string VarReflectionTexLeft = "_ReflectionTexLeft";
        private const string VarReflectionTexRight = "_ReflectionTexRight";
        private const string VarReflectionMaxLodBias = "_ReflectionMaxLODBias";
        private const string VarReflectionTexelSize = "_ReflectionTexelSize";
        private const string VarStereo = "_Stereo";

        // Static

        private static bool s_insideRendering;

        // Internal

        private readonly Dictionary<Camera, Camera> _reflectionCameras = new();
        private RenderTexture _reflectionTextureLeft;
        private RenderTexture _reflectionTextureRight;
        private int _oldReflectionTextureSize;

        #endregion
    }
}