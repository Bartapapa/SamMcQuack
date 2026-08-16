using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_Mantle : AMovementState
{
    [Header("MANTLE VALUES")]
    [SerializeField] private float _mantleDuration = 1f;
    public float MantleDuration { get { return _mantleDuration; } }
    [SerializeField] private AnimationCurve _mantleCurve;
    [SerializeField] private float _mantleArcHeight = 2f;
    [SerializeField] private float _mantleWallClearanceDistance = .5f;

    [Header("ROTATION VALUES")]
    [SerializeField] private float _mantleRotationSharpness = 10f;

    private CharacterMovement _characterMovement;
    private LedgeDetectionDescriptor _ledge;

    private Vector3 _startPosition;
    private Vector3 _targetPosition;

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

        float toRotationSharpness = _mantleRotationSharpness;

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

        float alpha = _elapsedTime / _mantleDuration;

        Vector3 targetPos = GetMantlePosition(alpha);

        Vector3 targetVel = (targetPos - _characterMovement.RB.position) / Time.fixedDeltaTime;

        _characterMovement.RB.velocity = targetVel;
    }

    private Vector3 GetMantlePosition(float alpha)
    {
        float movement = _mantleCurve.Evaluate(alpha);
        Vector3 targetPos = Vector3.Lerp(_startPosition, _targetPosition, movement);
        float arc = Mathf.Sin(movement * Mathf.PI) * _mantleArcHeight;

        targetPos += Vector3.up * arc;

        //wall clearance to prevent clipping on top angle

        float wallClearanceAlpha = (4 * movement) * (1 - movement);
        float wallClearance = wallClearanceAlpha * _mantleWallClearanceDistance;

        targetPos += _ledge.WallNormal * wallClearance;

        return targetPos;
    }

    public void SetLedgeDescriptor(LedgeDetectionDescriptor descriptor)
    {
        _ledge = descriptor;

        _startPosition = transform.position;
        _targetPosition = _ledge.StandPoint;
    }
}
