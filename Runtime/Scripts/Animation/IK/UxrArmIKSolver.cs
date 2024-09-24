// --------------------------------------------------------------------------------------------------------------------
// <copyright file="UxrArmIKSolver.cs" company="VRMADA">
//   Copyright (c) VRMADA, All rights reserved.
// </copyright>
// --------------------------------------------------------------------------------------------------------------------

using UltimateXR.Core;
using UltimateXR.Core.Math;
using UltimateXR.Extensions.Unity;
using UnityEngine;


namespace UltimateXR.Animation.IK
{
    /// <summary>
    ///     IK component that implements basic Inverse Kinematics for an arm.
    /// </summary>
    public class UxrArmIKSolver : UxrIKSolver
    {
        #region Public Methods

        /// <summary>
        ///     Solves a pass in the Inverse Kinematics.
        /// </summary>
        /// <param name="armSolveOptions">Arm solving options</param>
        /// <param name="armOverExtendMode">What happens when the hand moves farther than the actual arm length</param>
        public void SolveIKPass(UxrArmSolveOptions armSolveOptions, UxrArmOverExtendMode armOverExtendMode)
        {
            if (Hand == null || Forearm == null || Arm == null)
            {
                return;
            }

            var localClaviclePos = ToLocalAvatarPos(Clavicle.position);
            var localForearmPos = ToLocalAvatarPos(Forearm.position);
            var localHandPos = ToLocalAvatarPos(Hand.position);

            if (Clavicle != null)
            {
                if (armSolveOptions.HasFlag(UxrArmSolveOptions.ResetClavicle))
                {
                    Clavicle.transform.localRotation = _clavicleUniversalLocalAxes.InitialLocalRotation;
                }

                if (armSolveOptions.HasFlag(UxrArmSolveOptions.SolveClavicle))
                {
                    // Compute the rotation to make the clavicle look at the elbow.
                    // Computations are performed in local avatar space to allow avatars with pitch/roll and improve precision.

                    var avatarClavicleLookAt = (localForearmPos - localClaviclePos).normalized;
                    avatarClavicleLookAt = Vector3.Scale(avatarClavicleLookAt, _clavicleDeformationAxesScale) + _clavicleDeformationAxesBias;

                    var avatarClavicleRotation = ToLocalAvatarRot(Clavicle.rotation);

                    var avatarClavicleRotationLookAt = Quaternion.Slerp(avatarClavicleRotation,
                        Quaternion.LookRotation(avatarClavicleLookAt) * _clavicleUniversalLocalAxes.UniversalToActualAxesRotation,
                        _clavicleDeformation);

                    var deformationAngle = Quaternion.Angle(avatarClavicleRotationLookAt, avatarClavicleRotation);

                    if (deformationAngle > _clavicleRangeOfMotionAngle)
                    {
                        avatarClavicleRotationLookAt = Quaternion.Slerp(avatarClavicleRotation, avatarClavicleRotationLookAt, _clavicleRangeOfMotionAngle / deformationAngle);
                    }

                    // Smooth out:

                    var totalDegrees = Quaternion.Angle(_lastClavicleLocalRotation, avatarClavicleRotationLookAt);
                    var degreesRot = ClavicleMaxDegreesPerSecond * Time.deltaTime;

                    if (_smooth == false)
                    {
                        _lastClavicleRotationInitialized = false;
                    }

                    if (_lastClavicleRotationInitialized == false || totalDegrees < 0.001f)
                    {
                        Clavicle.rotation = ToWorldRot(avatarClavicleRotationLookAt);
                    }
                    else
                    {
                        Clavicle.rotation = Quaternion.Slerp(ToWorldRot(_lastClavicleLocalRotation),
                            ToWorldRot(avatarClavicleRotationLookAt),
                            Mathf.Clamp01(degreesRot / totalDegrees));
                    }
                }

                Hand.position = ToWorldPos(localHandPos);
            }

            // Find the plane of intersection between 2 spheres (sphere with "upper arm" radius and sphere with "forearm" radius).
            // Computations are performed in local avatar space to allow avatars with pitch/roll and improve precision.

            localForearmPos = ToLocalAvatarPos(Forearm.position);
            var localArmPos = ToLocalAvatarPos(Arm.position);

            var a = 2.0f * (localHandPos.x - localArmPos.x);
            var b = 2.0f * (localHandPos.y - localArmPos.y);
            var c = 2.0f * (localHandPos.z - localArmPos.z);

            var d = localArmPos.x * localArmPos.x - localHandPos.x * localHandPos.x + localArmPos.y * localArmPos.y - localHandPos.y * localHandPos.y +
                localArmPos.z * localArmPos.z - localHandPos.z * localHandPos.z - _upperArmLocalLength * _upperArmLocalLength + _forearmLocalLength * _forearmLocalLength;

            // Find the center of the circle intersecting the 2 spheres. Check if the intersection exists (hand may be stretched over the limits)
            var t = (localArmPos.x * a + localArmPos.y * b + localArmPos.z * c + d) / (a * (localArmPos.x - localHandPos.x) + b * (localArmPos.y - localHandPos.y) + c * (localArmPos.z - localHandPos.z));

            var localArmToCenter = (localHandPos - localArmPos) * t;
            var localCenter = localForearmPos;
            var safeDistance = 0.001f;
            var maxHandDistance = _upperArmLocalLength + _forearmLocalLength - safeDistance;
            var circleRadius = 0.0f;

            if (localArmToCenter.magnitude + _forearmLocalLength > maxHandDistance)
            {
                // Too far from shoulder and arm is over-extending. Solve depending on selected mode, but some are applied at the end of this method.
                localArmToCenter = localArmToCenter.normalized * (_upperArmLocalLength - safeDistance * 0.5f);
                localCenter = localArmPos + localArmToCenter;

                if (armOverExtendMode == UxrArmOverExtendMode.LimitHandReach)
                {
                    // Clamp hand distance
                    Hand.position = ToWorldPos(localArmPos + localArmToCenter.normalized * maxHandDistance);
                }

                var angleRadians = Mathf.Acos((localCenter - localArmPos).magnitude / _upperArmLocalLength);
                circleRadius = Mathf.Sin(angleRadians) * _upperArmLocalLength;
            }
            else if (localArmToCenter.magnitude < 0.04f)
            {
                // Too close to shoulder: keep current elbow position.
                localArmToCenter = localForearmPos - localArmPos;
                localCenter = localForearmPos;
            }
            else
            {
                localCenter = localArmPos + localArmToCenter;

                // Find the circle radius
                var angleRadians = Mathf.Acos((localCenter - localArmPos).magnitude / _upperArmLocalLength);
                circleRadius = Mathf.Sin(angleRadians) * _upperArmLocalLength;
            }

            var finalLocalHandPosition = ToLocalAvatarPos(Hand.position);
            var finalHandRotation = Hand.rotation;

            // Compute the point inside this circle using the elbowAperture parameter.
            // Possible range is from bottom to exterior (far left or far right for left arm and right arm respectively).
            var planeNormal = -new Vector3(a, b, c);

            var otherLocalArmPos = ToLocalAvatarPos(_otherArm.Arm.position);
            var rotToShoulder = Quaternion.LookRotation(Vector3.Cross((localArmPos - otherLocalArmPos) * (_side == UxrHandSide.Left ? -1.0f : 1.0f), Vector3.up).normalized, Vector3.up);
            var armToHand = (finalLocalHandPosition - localArmPos).normalized;
            var rotArmForward = rotToShoulder * Quaternion.LookRotation(Quaternion.Inverse(rotToShoulder) * localArmToCenter, Quaternion.Inverse(rotToShoulder) * armToHand);

            var vectorFromCenterSide = Vector3.Cross(_side == UxrHandSide.Left ? rotArmForward * Vector3.up : rotArmForward * -Vector3.up, planeNormal);

            if (_otherArm != null)
            {
                var isBack = Vector3.Cross(localArmPos - otherLocalArmPos, localCenter - localArmPos).y * (_side == UxrHandSide.Left ? -1.0f : 1.0f) > 0.0f;

                /*
                 * Do stuff with isBack
                 */
            }

            // Compute elbow aperture value [0.0, 1.0] depending on the relaxedElbowAperture parameter and the current wrist torsion
            var wristDegrees = _side == UxrHandSide.Left ? -Avatar.AvatarRigInfo.GetArmInfo(UxrHandSide.Left).WristTorsionInfo.WristTorsionAngle : Avatar.AvatarRigInfo.GetArmInfo(UxrHandSide.Right).WristTorsionInfo.WristTorsionAngle;
            var elbowApertureBiasDueToWrist = wristDegrees / WristTorsionDegreesFactor * _elbowApertureRotation;
            var elbowAperture = Mathf.Clamp01(_relaxedElbowAperture + elbowApertureBiasDueToWrist);

            _elbowAperture = _elbowAperture < 0.0f ? elbowAperture : Mathf.SmoothDampAngle(_elbowAperture, elbowAperture, ref _elbowApertureVelocity, ElbowApertureRotationSmoothTime);

            // Now compute the elbow position using it
            var vectorFromCenterBottom = _side == UxrHandSide.Left ? Vector3.Cross(vectorFromCenterSide, planeNormal) : Vector3.Cross(planeNormal, vectorFromCenterSide);

            var elbowPosition = localCenter + Vector3.Lerp(vectorFromCenterBottom, vectorFromCenterSide, _elbowAperture).normalized * circleRadius;

            // Compute the desired rotation
            var armForward = (elbowPosition - localArmPos).normalized;

            // Check range of motion of the arm
            if (Arm.parent != null)
            {
                var armNeutralForward = ToLocalAvatarDir(Arm.parent.TransformDirection(_armNeutralForwardInParent));

                if (Vector3.Angle(armForward, armNeutralForward) > _armRangeOfMotionAngle)
                {
                    armForward = Vector3.RotateTowards(armNeutralForward, armForward, _armRangeOfMotionAngle * Mathf.Deg2Rad, 0.0f);
                    elbowPosition = localArmPos + armForward * _upperArmLocalLength;
                }
            }

            // Compute the position and rotation of the rest
            var forearmForward = (ToLocalAvatarPos(Hand.position) - elbowPosition).normalized;
            var elbowAngle = Vector3.Angle(armForward, forearmForward);
            var elbowAxis = elbowAngle > ElbowMinAngleThreshold ? Vector3.Cross(forearmForward, armForward).normalized : Vector3.up;

            elbowAxis = _side == UxrHandSide.Left ? -elbowAxis : elbowAxis;

            var armRotationTarget = Quaternion.LookRotation(armForward, elbowAxis);
            var forearmRotationTarget = Quaternion.LookRotation(forearmForward, elbowAxis);

            // Transform from top hierarchy to bottom to avoid jitter. Since we consider Z forward and Y the elbow rotation axis, we also
            // need to transform from this "universal" space to the actual axes the model uses.
            Arm.rotation = ToWorldRot(armRotationTarget * _armUniversalLocalAxes.UniversalToActualAxesRotation);

            if (Vector3.Distance(finalLocalHandPosition, localArmPos) > maxHandDistance)
            {
                // Arm over extended: solve if the current mode is one of the remaining 2 to handle:
                if (armOverExtendMode == UxrArmOverExtendMode.ExtendUpperArm)
                {
                    // Move the elbow away to reach the hand. This will stretch the arm.
                    elbowPosition = finalLocalHandPosition - (finalLocalHandPosition - elbowPosition).normalized * _forearmLocalLength;
                }
                else if (armOverExtendMode == UxrArmOverExtendMode.ExtendArm)
                {
                    // Stretch both the arm and forearm
                    var elbowPosition2 = finalLocalHandPosition - (finalLocalHandPosition - elbowPosition).normalized * _forearmLocalLength;
                    elbowPosition = (elbowPosition + elbowPosition2) * 0.5f;
                }
            }

            Forearm.SetPositionAndRotation(ToWorldPos(elbowPosition), ToWorldRot(forearmRotationTarget * _forearmUniversalLocalAxes.UniversalToActualAxesRotation));
            Hand.SetPositionAndRotation(ToWorldPos(finalLocalHandPosition), finalHandRotation);
        }

        #endregion

        #region Event Handling Methods

        /// <summary>
        ///     Stores the clavicle orientation to smooth it out the next frame.
        /// </summary>
        private void UxrManager_AvatarsUpdated()
        {
            if (Clavicle != null)
            {
                _lastClavicleLocalRotation = Quaternion.Inverse(Avatar.transform.rotation) * Clavicle.rotation;
                _lastClavicleRotationInitialized = true;
            }
        }

        #endregion

        #region Protected Overrides UxrIKSolver

        /// <summary>
        ///     Solves the IK for the current frame.
        /// </summary>
        protected override void InternalSolveIK()
        {
            if (Clavicle != null)
            {
                // If we have a clavicle, perform another pass this time taking it into account.
                // The first pass won't clamp the hand distance because thanks to the clavicle rotation there is a little more reach.
                SolveIKPass(UxrArmSolveOptions.ResetClavicle, UxrArmOverExtendMode.ExtendForearm);
                SolveIKPass(UxrArmSolveOptions.ResetClavicle | UxrArmSolveOptions.SolveClavicle, _overExtendMode);
            }
            else
            {
                SolveIKPass(UxrArmSolveOptions.None, _overExtendMode);
            }
        }

        #endregion

        #region Inspector Properties/Serialized Fields

        [Header("General")] [SerializeField] private UxrHandSide _side;
        [SerializeField] private UxrArmOverExtendMode _overExtendMode = UxrArmOverExtendMode.LimitHandReach;

        [Header("Clavicle")] [SerializeField] [Range(0, 1)] private float _clavicleDeformation = DefaultClavicleDeformation;
        [SerializeField] private float _clavicleRangeOfMotionAngle = DefaultClavicleRangeOfMotionAngle;
        [SerializeField] private bool _clavicleAutoComputeBias = true;
        [SerializeField] private Vector3 _clavicleDeformationAxesBias = Vector3.zero;
        [SerializeField] private Vector3 _clavicleDeformationAxesScale = new(1.0f, 0.8f, 1.0f);

        [Header("Arm (shoulder), forearm & hand")] [SerializeField] private float _armRangeOfMotionAngle = DefaultArmRangeOfMotionAngle;
        [SerializeField] [Range(0, 1)] private float _relaxedElbowAperture = DefaultElbowAperture;
        [SerializeField] [Range(0, 1)] private float _elbowApertureRotation = DefaultElbowApertureRotation;

        [SerializeField] private bool _smooth = true;

        #endregion

        #region Public Types & Data

        public const float DefaultClavicleDeformation = 0.4f;
        public const float DefaultClavicleRangeOfMotionAngle = 30.0f;
        public const float DefaultArmRangeOfMotionAngle = 100.0f;
        public const float DefaultElbowAperture = 0.5f;
        public const float DefaultElbowApertureRotation = 0.3f;

        /// <summary>
        ///     Gets the clavicle bone.
        /// </summary>
        public Transform Clavicle { get; private set; }

        /// <summary>
        ///     Gets the arm bone.
        /// </summary>
        public Transform Arm { get; private set; }

        /// <summary>
        ///     Gets the forearm bone.
        /// </summary>
        public Transform Forearm { get; private set; }

        /// <summary>
        ///     Gets the hand bone.
        /// </summary>
        public Transform Hand { get; private set; }

        /// <summary>
        ///     Gets whether it is the left or right arm.
        /// </summary>
        public UxrHandSide Side
        {
            get => _side;
            set => _side = value;
        }

        /// <summary>
        ///     Gets or sets how far [0.0, 1.0] the elbow will from the body when solving the IK. Lower values will bring the elbow
        ///     closer to the body.
        /// </summary>
        public float RelaxedElbowAperture
        {
            get => _relaxedElbowAperture;
            set => _relaxedElbowAperture = value;
        }

        /// <summary>
        ///     Gets or sets what happens when the real hand makes the VR arm to over-extend. This may happen if the user has a
        ///     longer arm than the VR model, if the controller is placed far away or if the avatar is grabbing an object with
        ///     constraints that lock the hand position.
        /// </summary>
        public UxrArmOverExtendMode OverExtendMode
        {
            get => _overExtendMode;
            set => _overExtendMode = value;
        }

        #endregion

        #region Unity

        /// <summary>
        ///     Subscribe to events.
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


        /// <summary>
        ///     Computes internal IK parameters.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            ComputeParameters();
        }

        #endregion

        #region Private Methods

        /// <summary>
        ///     Transforms a point from world space to local avatar space.
        /// </summary>
        /// <param name="pos">World space position</param>
        /// <returns>Avatar space position</returns>
        private Vector3 ToLocalAvatarPos(Vector3 pos)
        {
            return Avatar.transform.InverseTransformPoint(pos);
        }


        /// <summary>
        ///     Transforms a point from local avatar space to world space.
        /// </summary>
        /// <param name="pos">Avatar space position</param>
        /// <returns>World space position</returns>
        private Vector3 ToWorldPos(Vector3 pos)
        {
            return Avatar.transform.TransformPoint(pos);
        }


        /// <summary>
        ///     Transforms a direction from world space to local avatar space.
        /// </summary>
        /// <param name="dir">World space direction</param>
        /// <returns>Avatar space direction</returns>
        private Vector3 ToLocalAvatarDir(Vector3 dir)
        {
            return Avatar.transform.InverseTransformDirection(dir);
        }


        /// <summary>
        ///     Transforms a rotation from world space to local avatar space.
        /// </summary>
        /// <param name="rot">World space rotation</param>
        /// <returns>Avatar space rotation</returns>
        private Quaternion ToLocalAvatarRot(Quaternion rot)
        {
            return Quaternion.Inverse(Avatar.transform.rotation) * rot;
        }


        /// <summary>
        ///     Transforms a rotation from local avatar space to world space.
        /// </summary>
        /// <param name="rot">Avatar space rotation</param>
        /// <returns>World space rotation</returns>
        private Quaternion ToWorldRot(Quaternion rot)
        {
            return Avatar.transform.rotation * rot;
        }


        /// <summary>
        ///     Computes the internal parameters for the IK.
        /// </summary>
        private void ComputeParameters()
        {
            // Try to figure out which arm it is
            var armElement = TransformExt.GetFirstNonNullTransformFromSet(Hand, Forearm, Arm, Clavicle);

            if (armElement)
            {
                _side = Avatar.transform.InverseTransformPoint(Hand.position).x < 0.0f ? UxrHandSide.Left : UxrHandSide.Right;
            }

            // Try to find opposite arm
            var otherArms = transform.root.GetComponentsInChildren<UxrArmIKSolver>();

            _otherArm = null;

            foreach (var solver in otherArms)
            {
                if (solver != this)
                {
                    _otherArm = solver;

                    break;
                }
            }

            // Set up references
            if (Clavicle == null)
            {
                Clavicle = _side == UxrHandSide.Left ? Avatar.AvatarRig.LeftArm.Clavicle : Avatar.AvatarRig.RightArm.Clavicle;
            }

            if (Arm == null)
            {
                Arm = _side == UxrHandSide.Left ? Avatar.AvatarRig.LeftArm.UpperArm : Avatar.AvatarRig.RightArm.UpperArm;
            }

            if (Forearm == null)
            {
                Forearm = _side == UxrHandSide.Left ? Avatar.AvatarRig.LeftArm.Forearm : Avatar.AvatarRig.RightArm.Forearm;
            }

            if (Hand == null)
            {
                Hand = Avatar.GetHandBone(_side);
            }

            var arm = Avatar.GetArm(_side);

            if (arm != null && arm.UpperArm && arm.Forearm && arm.Hand.Wrist)
            {
                // Compute lengths in local avatar coordinates in case avatar has scaling

                var localUpperArm = ToLocalAvatarPos(arm.UpperArm.position);
                var localForearm = ToLocalAvatarPos(arm.Forearm.position);
                var localHand = ToLocalAvatarPos(arm.Hand.Wrist.position);

                _upperArmLocalLength = Vector3.Distance(localUpperArm, localForearm);
                _forearmLocalLength = Vector3.Distance(localForearm, localHand);
            }

            _clavicleUniversalLocalAxes = Avatar.AvatarRigInfo.GetArmInfo(_side).ClavicleUniversalLocalAxes;
            _armUniversalLocalAxes = Avatar.AvatarRigInfo.GetArmInfo(_side).ArmUniversalLocalAxes;
            _forearmUniversalLocalAxes = Avatar.AvatarRigInfo.GetArmInfo(_side).ForearmUniversalLocalAxes;

            // Compute arm range of motion neutral direction
            _armNeutralForwardInParent = Vector3.forward;
            _armNeutralForwardInParent = Quaternion.AngleAxis(30.0f * (_side == UxrHandSide.Left ? -1.0f : 1.0f), Vector3.up) * _armNeutralForwardInParent;
            _armNeutralForwardInParent = Quaternion.AngleAxis(30.0f, Vector3.right) * _armNeutralForwardInParent;

            if (Arm.parent != null)
            {
                _armNeutralForwardInParent = Arm.parent.InverseTransformDirection(Avatar.transform.TransformDirection(_armNeutralForwardInParent));
            }

            if (Clavicle && Avatar)
            {
                // If we have a clavicle, set it up too
                if (_clavicleAutoComputeBias)
                {
                    var clavicleLookAt = (Forearm.position - Clavicle.position).normalized;
                    Avatar.transform.InverseTransformDirection(clavicleLookAt);

                    _clavicleDeformationAxesBias = new Vector3(0.0f, -clavicleLookAt.y + 0.25f, -clavicleLookAt.z);
                }
            }

            _elbowAperture = -1.0f;
            _elbowApertureVelocity = 0.0f;
        }

        #endregion

        #region Private Types & Data

        private const float ClavicleMaxDegreesPerSecond = 360.0f;
        private const float WristTorsionDegreesFactor = 150.0f;
        private const float ElbowApertureRotationSmoothTime = 0.1f;
        private const float ElbowMinAngleThreshold = 3.0f;

        private UxrArmIKSolver _otherArm;
        private UxrUniversalLocalAxes _clavicleUniversalLocalAxes;
        private UxrUniversalLocalAxes _armUniversalLocalAxes;
        private UxrUniversalLocalAxes _forearmUniversalLocalAxes;
        private float _upperArmLocalLength;
        private float _forearmLocalLength;
        private float _elbowAperture = -1.0f;
        private float _elbowApertureVelocity;
        private Vector3 _armNeutralForwardInParent = Vector3.zero;
        private Quaternion _lastClavicleLocalRotation = Quaternion.identity;
        private bool _lastClavicleRotationInitialized;

        #endregion
    }
}