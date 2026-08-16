using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_Jumping : AMovementState
{
    [Header("ROTATION VALUES")]
    [SerializeField] private float _jumpRotationSharpness = 10f;
    [SerializeField] private float _uprightRotationSharpness = 2f;

    [Header("MOVEMENT VALUES")]
    [SerializeField] private float _maxJumpMoveSpeed = 7f;
    [SerializeField] private float _jumpMovementSharpness = 10f;
    [SerializeField] private float _jumpDrag = 0f;
    [SerializeField] private float _gravity = -45f;

    [Header("JUMP VALUES")]
    [SerializeField] private float _jumpInitialStrength = 5f;
    [SerializeField] private float _maxJumpHeldTime = .5f;
    [SerializeField] private float _jumpHeldForce = 15f;

    private float _jumpHoldTimer = 0f;
    private bool _jumpInputReleased = false;

    private bool _hasLeftGround = false;
    public bool HasLeftGround { get { return _hasLeftGround; } }

    private CharacterMovement _characterMovement;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(_stateEnum);
        _characterMovement = character;

        float jumpInitialStrength = _jumpInitialStrength;
        _characterMovement.RB.AddForce(Vector3.up * jumpInitialStrength, ForceMode.VelocityChange);

        _jumpHoldTimer = 0f;
        _jumpInputReleased = false;

        _hasLeftGround = false;
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);

        _jumpInputReleased = false;
        _jumpHoldTimer = 0f;

        _hasLeftGround = false;
    }

    public override void OnStateUpdate(CharacterMovement character)
    {
        base.OnStateUpdate(character);

        if (_characterMovement.JumpInputHeld && !_jumpInputReleased)
        {
            _jumpHoldTimer += Time.deltaTime;
        }
        else
        {
            _jumpInputReleased = true;
        }
    }

    public override void OnStateFixedUpdate(CharacterMovement character)
    {
        base.OnStateFixedUpdate(character);

        if (!_hasLeftGround)
        {
            _hasLeftGround = !character.GroundDetected;
        }

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

        float toRotationSharpness = _jumpRotationSharpness;

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
        if (_characterMovement.MoveInputVector.sqrMagnitude > 0f)
        {
            Vector3 addedVelocity = _characterMovement.MoveInputVector * _jumpMovementSharpness * Time.fixedDeltaTime;
            Vector3 currentVelocityOnInputsPlane = Vector3.ProjectOnPlane(_characterMovement.RB.velocity, Vector3.up);
            if (currentVelocityOnInputsPlane.magnitude < _maxJumpMoveSpeed)
            {
                Vector3 newTotal = Vector3.ClampMagnitude(currentVelocityOnInputsPlane + addedVelocity, _maxJumpMoveSpeed);
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

        _characterMovement.RB.velocity += (Vector3.up * _gravity) * Time.fixedDeltaTime;

        if (_characterMovement.JumpInputHeld && _jumpHoldTimer < _maxJumpHeldTime && !_jumpInputReleased)
        {
            _characterMovement.RB.velocity += (Vector3.up * _jumpHeldForce) * Time.fixedDeltaTime;
        }

        _characterMovement.RB.velocity *= (1f / (1f + (_jumpDrag * Time.fixedDeltaTime)));
    }
}
