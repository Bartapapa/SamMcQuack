using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_Falling : AMovementState
{
    [Header("ROTATION VALUES")]
    [SerializeField] private float _fallingRotationSharpness = 10f;

    [Header("MOVEMENT VALUES")]
    [SerializeField] private float _maxFallMoveSpeed = 7f;
    [SerializeField] private float _fallMovementSharpness = 10f;
    [SerializeField] private float _fallDrag = 0f;
    [SerializeField] private Vector3 _gravity = new Vector3(0f, -30f, 0f);

    private CharacterMovement _characterMovement;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(EMovementStates.Falling);
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

        float toRotationSharpness = _fallingRotationSharpness;

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
        if (_characterMovement.MoveInputVector.sqrMagnitude > 0f)
        {
            Vector3 addedVelocity = _characterMovement.MoveInputVector * _fallMovementSharpness * Time.fixedDeltaTime;
            Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(_characterMovement.RB.velocity, Vector3.up);
            if (currentVelocityOnInputsPlane.magnitude < _maxFallMoveSpeed)
            {
                Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, _maxFallMoveSpeed);
                addedVelocity = newTotal - currentVelocityOnInputsPlane;
            }
            else
            {
                if (Vector3.Dot(currentVelocityOnInputsPlane, addedVelocity) > 0f)
                {
                    addedVelocity = Vector3.ProjectOnPlane(addedVelocity, currentVelocityOnInputsPlane.normalized);
                }
            }
            if (!_characterMovement.CanMove) addedVelocity = Vector3.zero;
            _characterMovement.RB.velocity += addedVelocity;
        }
        _characterMovement.RB.velocity += _gravity * Time.fixedDeltaTime;

        _characterMovement.RB.velocity *= (1f / (1f + (_fallDrag * Time.fixedDeltaTime)));
    }
}
