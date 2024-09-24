// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UxrUnityXRControllerTracking.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using UltimateXR.Core;
using UnityEngine;
using UnityEngine.XR;


namespace UltimateXR.Devices.Integrations
{
    /// <summary>
    ///     Base class for tracking devices based on OpenVR.
    /// </summary>
    public abstract class UxrUnityXRControllerTracking : UxrControllerTracking
    {
        #region Protected Overrides UxrTrackingDevice

        /// <inheritdoc />
        protected override void UpdateSensors()
        {
            base.UpdateSensors();

            if (Avatar.CameraComponent == null)
            {
                Debug.LogWarning("No camera has been setup for this avatar");

                return;
            }

            var nodeStates = new List<XRNodeState>();
            InputTracking.GetNodeStates(nodeStates);

            foreach (var nodeState in nodeStates)
            {
                if (nodeState.nodeType == XRNode.LeftHand)
                {
                    var localAvatarLeftHandSensorPos = LocalAvatarLeftHandSensorPos;
                    var localAvatarLeftHandSensorRot = LocalAvatarLeftHandSensorRot;

                    nodeState.TryGetRotation(out localAvatarLeftHandSensorRot);
                    nodeState.TryGetPosition(out localAvatarLeftHandSensorPos);

                    UpdateSensor(UxrHandSide.Left, localAvatarLeftHandSensorPos, localAvatarLeftHandSensorRot);
                }
                else if (nodeState.nodeType == XRNode.RightHand)
                {
                    var localAvatarRightHandSensorPos = LocalAvatarRightHandSensorPos;
                    var localAvatarRightHandSensorRot = LocalAvatarRightHandSensorRot;

                    nodeState.TryGetRotation(out localAvatarRightHandSensorRot);
                    nodeState.TryGetPosition(out localAvatarRightHandSensorPos);

                    UpdateSensor(UxrHandSide.Right, localAvatarRightHandSensorPos, localAvatarRightHandSensorRot);
                }
            }
        }

        #endregion
    }
}