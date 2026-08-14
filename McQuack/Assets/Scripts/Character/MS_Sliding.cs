using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

public class MS_Sliding : AMovementState
{
    [Header("SLIDE REQUEST TIME")]
    [SerializeField] private float _slideRequestTime = 1f;
    public float SlideRequestTime { get { return _slideRequestTime; } }

    [Header("MOVEMENT VALUES")]
    [SerializeField] private float _maxSlidingMoveSpeed = 10f;
    [SerializeField] private float _maxSideSlidingMoveSpeed = 5f;
    [SerializeField] private float _sideSlidingMovementSharpness = 5f;
    [SerializeField] private float _slidingAcceleration = 5f;
    [SerializeField] private float _slidingDeceleration = 5f;
    [SerializeField] private float _minimumSafeSlidingSpeed = 1f;
    public float MinimumSafeSlidingSpeed { get { return _minimumSafeSlidingSpeed; } }

    [Header("ROTATION VALUES")]
    [SerializeField] private float _slidingRotationSharpness = 3f;

    [Header("SLOPE STICK")]
    [SerializeField] private float _slopeStickForce = -15f;

    private float _currentSlidingSpeed = 0f;
    public float CurrentSlidingSpeed { get { return _currentSlidingSpeed; } }
    private bool _onSteepSlope = false;

    private Vector3 _currentSideSlidingSpeed = Vector3.zero;
    public bool OnSteepSlope { get { return _onSteepSlope; } }

    private CharacterMovement _characterMovement;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(EMovementStates.Sliding);
        _characterMovement = character;

        SetInitialSpeeds();
        _onSteepSlope = true;
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);

        _currentSlidingSpeed = 0f;
        _onSteepSlope = false;
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
        Vector3 groundNormal = _characterMovement.GroundDetectionDescriptor.Normal;
        Vector3 downhillDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        Vector3 toLookVector = downhillDir;

        float toRotationSharpness = _slidingRotationSharpness;

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
        Vector3 groundNormal = _characterMovement.GroundDetectionDescriptor.Normal.normalized;
        Vector3 downhillDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;

        float inputMagnitude = _characterMovement.MoveInputVector.magnitude;
        Vector3 slopeRight = Vector3.Cross(groundNormal, downhillDir).normalized;
        float inputRight = Vector3.Dot(_characterMovement.MoveInputVector.normalized, slopeRight);

        float slopeAngle = Vector3.Angle(Vector3.up, groundNormal);
        _onSteepSlope = slopeAngle >= _characterMovement.MaxGroundedAngle;

        Vector3 targetSideSlideVelocity = slopeRight * inputRight * _maxSideSlidingMoveSpeed * inputMagnitude;
        if (!_characterMovement.CanMove) targetSideSlideVelocity = Vector3.zero;

        if (_onSteepSlope)
        {
            _currentSlidingSpeed = Mathf.MoveTowards(_currentSlidingSpeed, _maxSlidingMoveSpeed, _slidingAcceleration * Time.fixedDeltaTime);
            _currentSideSlidingSpeed = Vector3.Lerp(_currentSideSlidingSpeed, targetSideSlideVelocity, 1f - Mathf.Exp(-_sideSlidingMovementSharpness * Time.fixedDeltaTime));
        }
        else
        {
            _currentSlidingSpeed = Mathf.MoveTowards(_currentSlidingSpeed, 0f, _slidingDeceleration * Time.fixedDeltaTime);
            _currentSideSlidingSpeed = Vector3.Lerp(_currentSideSlidingSpeed, targetSideSlideVelocity, 1f - Mathf.Exp(-_sideSlidingMovementSharpness * Time.fixedDeltaTime));
        }

        Vector3 targetMovementVelocity = downhillDir * _currentSlidingSpeed + _currentSideSlidingSpeed;
        _characterMovement.RB.velocity = targetMovementVelocity;
        _characterMovement.RB.velocity += groundNormal * _slopeStickForce;
    }

    private void SetInitialSpeeds()
    {
        Vector3 groundNormal = _characterMovement.GroundDetectionDescriptor.Normal.normalized;
        Vector3 initialVelocity = _characterMovement.RB.velocity;
        Vector3 downhillDir = Vector3.ProjectOnPlane(Vector3.down, groundNormal).normalized;
        Vector3 slopeRight = Vector3.Cross(groundNormal, downhillDir).normalized;


        float initialDownhillSpeed = 0f;
        float initialSideSpeed = 0f;

        initialDownhillSpeed = Vector3.Dot(initialVelocity, downhillDir);
        _currentSlidingSpeed = Mathf.Clamp(Mathf.Max(0f, initialDownhillSpeed), 0f, float.PositiveInfinity);

        initialSideSpeed = Vector3.Dot(initialVelocity, slopeRight);
        _currentSideSlidingSpeed = slopeRight * initialSideSpeed;
    }
}
