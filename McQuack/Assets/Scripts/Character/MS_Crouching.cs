using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_Crouching : AMovementState
{
    [Header("CROUCHED CAPSULE PARAMETERS")]
    [SerializeField] private CapsuleParameterDescriptor _capsuleParams;

    [Header("ROTATION VALUES")]
    [SerializeField] private float _crouchingRotationSharpness = 10f;
    [SerializeField] private float _uprightRotationSharpness = 10f;

    [Header("CROUCHING MOVEMENT VALUES")]
    [SerializeField] private float _maxCrouchingMoveSpeed = 7f;
    [SerializeField] private float _crouchingMovementSharpness = 15f;

    [Header("SNAP TO GROUND")]
    [SerializeField] private float _groundSnapSharpness = -30f;

    private CharacterMovement _characterMovement;
    private CapsuleCollider _capsule;
    private EnvironmentDetector _detector;

    bool _uncrouchAttemptSuccess = false;
    public bool UncrouchAttemptSuccess { get { return _uncrouchAttemptSuccess; } }

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(_stateEnum);
        _characterMovement = character;
        _capsule = _characterMovement.Capsule;
        _detector = _characterMovement.Detector;

        _characterMovement.ApplyCapsuleParams(_capsuleParams);

        _characterMovement.SwitchToCrouchingMesh(true);
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);

        _uncrouchAttemptSuccess = false;
        _characterMovement.ApplyCapsuleParams(_characterMovement.CapsuleOriginalParameters);

        _characterMovement.SwitchToCrouchingMesh(false);
    }

    public override void OnStateUpdate(CharacterMovement character)
    {
        base.OnStateUpdate(character);
    }

    public override void OnStateFixedUpdate(CharacterMovement character)
    {
        base.OnStateFixedUpdate(character);

        HandleRotation();
        HandleVelocity();

        if (_characterMovement.UncrouchRequested)
        {
            AttemptUncrouch();
        }
    }

    protected override void HandleRotation()
    {
        Vector3 toLookVector = Vector3.zero;
        if (_characterMovement.ForcedLookAtDir != Vector3.zero)
        {
            toLookVector = _characterMovement.ForcedLookAtDir;
        }
        else
        {
            toLookVector = _characterMovement.LookInputVector;
        }

        float toRotationSharpness = _crouchingRotationSharpness;

        Vector3 smoothedLookInputDirection = _characterMovement.transform.forward;

        if (toLookVector.sqrMagnitude > 0f && toRotationSharpness > 0f)
        {
            smoothedLookInputDirection = Vector3.Slerp(transform.forward, toLookVector, 1 - Mathf.Exp(-toRotationSharpness * Time.fixedDeltaTime)).normalized;

            _characterMovement.transform.forward = smoothedLookInputDirection;
        }
        else
        {
            smoothedLookInputDirection = Vector3.Slerp(transform.forward, transform.forward, 1 - Mathf.Exp(-toRotationSharpness * Time.fixedDeltaTime)).normalized;

            _characterMovement.transform.forward = smoothedLookInputDirection;
        }

        //Upright character after cases like going down sliding

        Vector3 forward = _characterMovement.transform.forward;
        forward = Vector3.ProjectOnPlane(forward, Vector3.up);

        if (forward.sqrMagnitude > 0.0001f)
        {
            Quaternion rightedUpRot = Quaternion.LookRotation(forward, Vector3.up);
            _characterMovement.RB.MoveRotation(Quaternion.Slerp(_characterMovement.transform.rotation, rightedUpRot, 1f - Mathf.Exp(-_uprightRotationSharpness * Time.fixedDeltaTime)));
        }
    }

    protected override void HandleVelocity()
    {
        //Find reoriented input depending on groundhit normal, for moving on slopes.
        Vector3 groundNormal = _characterMovement.GroundDetectionDescriptor.Normal;
        Vector3 inputRight = Vector3.Cross(_characterMovement.MoveInputVector, Vector3.up);
        Vector3 reorientedInput = Vector3.Cross(groundNormal, inputRight).normalized * _characterMovement.MoveInputVector.magnitude;

        //Set velocity, add inheritedVelocity given by pushes and moving pillars.
        float toMaxSpeed = _maxCrouchingMoveSpeed;
        Vector3 targetMovementVelocity = reorientedInput * toMaxSpeed;
        if (!_characterMovement.CanMove) targetMovementVelocity = Vector3.zero;

        //Snap character to appropriate groundY
        GroundDetectionDescriptor ground = _characterMovement.GroundDetectionDescriptor;

        Vector3 feetPosition = _characterMovement.RB.position;
        float groundDistance = Vector3.Dot(feetPosition - ground.Point, ground.Normal);
        float error = -groundDistance;

        Vector3 correctionVelocity = ground.Normal * error * _groundSnapSharpness;
        Vector3 groundSnapForce = ground.Normal * _groundSnapSharpness * error;

        _characterMovement.RB.velocity = Vector3.Lerp(_characterMovement.RB.velocity, targetMovementVelocity, 1f - Mathf.Exp(-_crouchingMovementSharpness * Time.fixedDeltaTime));
        _characterMovement.RB.velocity += groundSnapForce * Time.fixedDeltaTime;
    }

    private void AttemptUncrouch()
    {
        _uncrouchAttemptSuccess = _detector.CanCharacterFit(_characterMovement.RB.position, _characterMovement.CapsuleOriginalParameters, _characterMovement.EnvironmentMask);
    }
}
