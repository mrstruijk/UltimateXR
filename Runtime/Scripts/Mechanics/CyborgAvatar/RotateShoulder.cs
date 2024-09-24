// --------------------------------------------------------------------------------------------------------------------
// <copyright file="RotateShoulder.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using UltimateXR.Core;
using UltimateXR.Core.Components;
using UnityEngine;


namespace UltimateXR.Mechanics.CyborgAvatar
{
    /// <summary>
    ///     Component that rotates the Cyborg shoulder so that the opening points in the arm direction to leave it
    ///     more space.
    /// </summary>
    public class RotateShoulder : UxrComponent
    {
        #region Private Types & Data

        private float _currentAngleVelocity;

        #endregion

        #region Event Handling Methods

        /// <summary>
        ///     Performs the shoulder rotation.
        /// </summary>
        private void UxrManager_AvatarsUpdated()
        {
            var armForward = _arm.TransformDirection(_armLocalForward);
            var rotatingShoulderAxis = _rotatingShoulder.TransformDirection(_rotatingShoulderAxis);

            var armAngle = Vector3.Angle(armForward, rotatingShoulderAxis);

            if (armAngle > _armAngleToRotateMin)
            {
                var t = Mathf.Clamp01((armAngle - _armAngleToRotateMin) / (_armAngleToRotateMax - _armAngleToRotateMin));

                var openingCurrent = _rotatingShoulder.TransformDirection(_rotatingShoulderOpeningAxis);
                var openingTarget = Vector3.ProjectOnPlane(armForward, rotatingShoulderAxis);
                var currentAngle = Vector3.SignedAngle(openingCurrent, openingTarget, rotatingShoulderAxis);
                var dampedAngle = Mathf.SmoothDampAngle(currentAngle, 0.0f, ref _currentAngleVelocity, Mathf.Lerp(_rotationDampingMin, _rotationDampingMax, t));

                _rotatingShoulder.Rotate(_rotatingShoulderAxis, currentAngle - dampedAngle, Space.Self);
            }
            else
            {
                _currentAngleVelocity = 0.0f;
            }
        }

        #endregion

        #region Inspector Properties/Serialized Fields

        [SerializeField] private Transform _rotatingShoulder;
        [SerializeField] private Vector3 _rotatingShoulderAxis;
        [SerializeField] private Vector3 _rotatingShoulderOpeningAxis;
        [SerializeField] private Transform _arm;
        [SerializeField] private Vector3 _armLocalForward;
        [SerializeField] private float _rotationDampingMin = 1.0f;
        [SerializeField] private float _rotationDampingMax = 0.2f;
        [SerializeField] private float _armAngleToRotateMin = 30.0f;
        [SerializeField] private float _armAngleToRotateMax = 60.0f;

        #endregion

        #region Unity

        /// <summary>
        ///     Subscribes to events.
        /// </summary>
        protected override void OnEnable()
        {
            base.OnEnable();

            UxrManager.AvatarsUpdated += UxrManager_AvatarsUpdated;
        }


        /// <summary>
        ///     Unsubscribes from events.
        /// </summary>
        protected override void OnDisable()
        {
            base.OnDisable();

            UxrManager.AvatarsUpdated -= UxrManager_AvatarsUpdated;
        }

        #endregion
    }
}