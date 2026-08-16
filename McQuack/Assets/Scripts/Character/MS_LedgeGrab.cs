using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_LedgeGrab : AMovementState
{
    [Header("LEDGE GRAB VALUES")]
    [SerializeField] private float _activeLedgeGrabDuration = 1f;
    [SerializeField] private float _passiveLedgeGrabDuration = .5f;
    [SerializeField] private AnimationCurve _ledgeGrabCurve;
    [SerializeField] private float _hangWallClearanceDistance = .25f;
    [SerializeField] private float _hangVerticalOffset = .1f;

    [Header("ROTATION VALUES")]
    [SerializeField] private float _ledgeGrabRotationSharpness = 10f;

    private CharacterMovement _characterMovement;
    private LedgeDetectionDescriptor _ledge;

    private Vector3 _startPosition;
    private Vector3 _targetPosition;
    private float _usedLedgeGrabDuration;
    public float LedgeGrabDuration { get { return _usedLedgeGrabDuration; } }

    private float _elapsedTime = 0f;
    public float ElapsedTime { get { return _elapsedTime; } }

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(_stateEnum);
        _characterMovement = character;

        _elapsedTime = 0f;
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);

        _elapsedTime = 0f;
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

        _elapsedTime += Time.fixedDeltaTime;
    }

    protected override void HandleRotation()
    {
        Vector3 toLookVector = -_ledge.WallNormal;

        float toRotationSharpness = _ledgeGrabRotationSharpness;

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
        //Anchor to mantling position (final positioning based on rootmotion animation)
        //Then handle movement (with or without rootmotion)

        float alpha = Mathf.Clamp01(_elapsedTime / _activeLedgeGrabDuration);

        Vector3 targetPos = GetGrabPosition(alpha);

        Vector3 targetVel = (targetPos - _characterMovement.RB.position) / Time.fixedDeltaTime;

        _characterMovement.RB.velocity = targetVel;
    }

    private Vector3 GetGrabPosition(float alpha)
    {
        float movement = _ledgeGrabCurve.Evaluate(alpha);
        Vector3 targetPos = Vector3.Lerp(_startPosition, _targetPosition, movement);

        //wall clearance to prevent clipping

        float wallClearance = _hangWallClearanceDistance * movement;

        targetPos += _ledge.WallNormal * wallClearance;

        return targetPos;
    }

    public void GrabLedge(LedgeDetectionDescriptor descriptor, CapsuleCollider capsule, bool activeLedgeGrab = true)
    {
        _ledge = descriptor;

        _startPosition = transform.position;

        float halfHeight = capsule.height * .5f;
        float hangVerticalOffset = halfHeight + _hangVerticalOffset;
        float wallClearance = capsule.radius + _hangWallClearanceDistance;
        Vector3 hangPoint = _ledge.GroundPoint - (Vector3.up * hangVerticalOffset) - (_ledge.WallNormal * wallClearance);

        _targetPosition = hangPoint;

        _usedLedgeGrabDuration = activeLedgeGrab ? _activeLedgeGrabDuration : _passiveLedgeGrabDuration;
    }
}
