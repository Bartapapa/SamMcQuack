using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEditor.PackageManager.UI;
using UnityEngine;

public class MS_Walking : AMovementState
{
    [Header("ROTATION VALUES")]
    [SerializeField] private float _groundedRotationSharpness = 10f;
    [SerializeField] private float _uprightRotationSharpness = 10f;

    [Header("GROUNDED MOVEMENT VALUES")]
    [SerializeField] private float _maxGroundedMoveSpeed = 7f;
    [SerializeField] private float _groundedMovementSharpness = 15f;

    [Header("SNAP TO GROUND")]
    [SerializeField] private float _groundSnapSharpness = -30f;

    private CharacterMovement _characterMovement;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(_stateEnum);
        _characterMovement = character;

        _characterMovement.RB.velocity = new Vector3(_characterMovement.RB.velocity.x, 0f, _characterMovement.RB.velocity.z);
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);
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

        float toRotationSharpness = _groundedRotationSharpness;

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
        float toMaxSpeed = _maxGroundedMoveSpeed;
        float directionalInfluence = _characterMovement.GetDirectionalInfluence(inputRight);
        Vector3 targetMovementVelocity = reorientedInput * toMaxSpeed * directionalInfluence;
        if (!_characterMovement.CanMove) targetMovementVelocity = Vector3.zero;

        //Snap character to appropriate groundY
        GroundDetectionDescriptor ground = _characterMovement.GroundDetectionDescriptor;

        Vector3 feetPosition = _characterMovement.RB.position;
        float groundDistance = Vector3.Dot(feetPosition - ground.Point, ground.Normal);
        float error = -groundDistance;

        Vector3 groundSnapForce = ground.Normal * _groundSnapSharpness * error;

        _characterMovement.RB.velocity = Vector3.Lerp(_characterMovement.RB.velocity, targetMovementVelocity, 1f - Mathf.Exp(-_groundedMovementSharpness * Time.fixedDeltaTime));
        _characterMovement.RB.velocity += groundSnapForce * Time.fixedDeltaTime;
    }
}
