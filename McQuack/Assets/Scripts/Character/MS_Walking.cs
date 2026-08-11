using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;

public class MS_Walking : AMovementState
{
    [Header("ROTATION VALUES")]
    [SerializeField] private float _groundedRotationSharpness = 10f;

    [Header("GROUNDED MOVEMENT VALUES")]
    [SerializeField] private float _maxGroundedMoveSpeed = 7f;
    [SerializeField] private float _groundedMovementSharpness = 15f;

    private CharacterMovement _characterMovement;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(EMovementStates.Walking);
        _characterMovement = character;
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
    }

    protected override void HandleVelocity()
    {
        //Find reoriented input depending on groundhit normal, for moving on slopes.
        Vector3 groundNormal = _characterMovement.GroundHit.normal;
        Vector3 inputRight = Vector3.Cross(_characterMovement.MoveInputVector, Vector3.up);
        Vector3 reorientedInput = Vector3.Cross(groundNormal, inputRight).normalized * _characterMovement.MoveInputVector.magnitude;

        //Set velocity, add inheritedVelocity given by pushes and moving pillars.
        float toMaxSpeed = _maxGroundedMoveSpeed;
        Vector3 targetMovementVelocity = reorientedInput * toMaxSpeed;
        if (!_characterMovement.CanMove) targetMovementVelocity = Vector3.zero;

        _characterMovement.RB.velocity = Vector3.Lerp(_characterMovement.RB.velocity, targetMovementVelocity, 1f - Mathf.Exp(-_groundedMovementSharpness * Time.fixedDeltaTime));
    }
}
