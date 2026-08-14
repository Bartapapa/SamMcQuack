using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_Balancing : AMovementState
{
    [Header("OBJECT REFS")]
    [SerializeField] private BalancePath _currentBalancePath;
    public BalancePath CurrentBalanceLine { get { return _currentBalancePath; } }
    [SerializeField] private float _currentDistanceAlongPath = 0f;

    [Header("END POINT TOLERANCE")]
    [SerializeField] private float _endPointTolerance = 0.05f;

    [Header("BREAKING DISTANCE")]
    [SerializeField] private float _breakingDistance = .75f;

    [Header("BALANCING MOVEMENT VALUES")]
    [SerializeField] private float _maxBalancingMoveSpeed = 7f;
    [SerializeField] private float _balancingMovementSharpness = 10f;
    [SerializeField] private float _pathTrackingSharpness = 10f;

    private float _currentBalancingSpeed = 0f;

    [Header("BALANCING ROTATION VALUES")]
    [SerializeField] private float _balancingRotationSharpness = 10f;

    private CharacterMovement _characterMovement;
    private SplinePath.SplineSample _currentSample;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(EMovementStates.Balancing);
        _characterMovement = character;

        AnchorToCurrentPath();
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);

        _currentBalancePath = null;
    }

    public override void OnStateUpdate(CharacterMovement character)
    {
        base.OnStateUpdate(character);
    }

    public override void OnStateFixedUpdate(CharacterMovement character)
    {
        base.OnStateFixedUpdate(character);


        if (_currentBalancePath == null) return;

        _currentSample = _currentBalancePath.Path.EvaluateAtDistance(_currentDistanceAlongPath);

        HandleRotation();
        HandleVelocity();
    }

    protected override void HandleRotation()
    {
        //Set forward rotation to direction of current point to next point.
        //Depending on facing (forward/backward), next point is i+1 or i-1.
        //If at last point, use previous rotation direction.

        //If moveinput direction is beyond a certain dotproduct compared to current facing and sides, then TurnAround();
        Vector3 toLookVector = GetLookDirFromSplineTangent(_currentSample.Tangent) * Mathf.Sign(_currentBalancingSpeed);

        Vector3 smoothedLookInputDirection = _characterMovement.transform.forward;

        if (toLookVector.sqrMagnitude > 0f && _balancingRotationSharpness > 0f)
        {
            smoothedLookInputDirection = Vector3.Slerp(transform.forward, toLookVector, 1 - Mathf.Exp(-_balancingRotationSharpness * Time.fixedDeltaTime)).normalized;

            _characterMovement.transform.forward = smoothedLookInputDirection;
        }
        else
        {
            smoothedLookInputDirection = Vector3.Slerp(transform.forward, transform.forward, 1 - Mathf.Exp(-_balancingRotationSharpness * Time.fixedDeltaTime)).normalized;

            _characterMovement.transform.forward = smoothedLookInputDirection;
        }
    }

    protected override void HandleVelocity()
    {
        //Find reoriented input depending on groundhit normal, for moving on slopes.
        float inputMagnitude = Mathf.Clamp01(_characterMovement.MoveInputVector.magnitude);
        Vector3 balanceNormal = _currentSample.Up.normalized;
        Vector3 input = _characterMovement.MoveInputVector;
        Vector3 reorientedInput = Vector3.zero;

        if(inputMagnitude > 0.001f)
        {
            Vector3 inputRight = Vector3.Cross(input, Vector3.up);

            reorientedInput = Vector3.Cross(balanceNormal, inputRight).normalized;
        }

        Vector3 currentSampleTangent = _currentSample.Tangent.normalized;

        float dotProductAlignment = 0f;

        if (inputMagnitude > 0.001f)
        {
            dotProductAlignment = Vector3.Dot(reorientedInput, currentSampleTangent);
        }

        float targetSpeed = dotProductAlignment * inputMagnitude * _maxBalancingMoveSpeed;
        _currentBalancingSpeed = Mathf.Lerp(_currentBalancingSpeed, targetSpeed, 1f - Mathf.Exp(-_balancingMovementSharpness * Time.fixedDeltaTime));

        _currentDistanceAlongPath += _currentBalancingSpeed * Time.fixedDeltaTime;
        _currentDistanceAlongPath = Mathf.Clamp(_currentDistanceAlongPath, 0f, _currentBalancePath.Path.Length);

        SplinePath.SplineSample sample = _currentBalancePath.Path.EvaluateAtDistance(_currentDistanceAlongPath);

        Vector3 positionError = sample.Position - _characterMovement.RB.position;
        Vector3 correctionVelocity = positionError * _pathTrackingSharpness;

        Vector3 targetVelocity = sample.Tangent.normalized * _currentBalancingSpeed;
        targetVelocity += correctionVelocity;
        _characterMovement.RB.velocity = targetVelocity;
    }

    public void UseBalancePath(BalancePath balancePath)
    {
        _currentBalancePath = balancePath;
    }

    private void AnchorToCurrentPath()
    {
        float distanceOnPath = _currentBalancePath.Path.GetClosestDistance(_characterMovement.transform.position);
        _currentDistanceAlongPath = distanceOnPath;
        _currentBalancingSpeed = 0f;

        Vector3 rbVel = _characterMovement.RB.velocity;
        _currentBalancingSpeed = GetInitialSpeed();

        _characterMovement.RB.velocity = new Vector3(rbVel.x, 0f, rbVel.z);
    }

    private float GetInitialSpeed()
    {
        Vector3 rbVel = _characterMovement.RB.velocity;
        Vector3 rbVelNoY = new Vector3(rbVel.x, 0f, rbVel.z);
        float velMagnitude = rbVelNoY.sqrMagnitude;
        float dotProductAlignment = 0f;
        if (velMagnitude > 0.001f)
        {
            dotProductAlignment = Vector3.Dot(rbVelNoY, _currentBalancePath.Path.EvaluateAtDistance(_currentDistanceAlongPath).Tangent);
        }

        float initialSpeed = Mathf.Sqrt(velMagnitude) * Mathf.Sign(dotProductAlignment);
        return initialSpeed;
    }

    private Vector3 GetLookDirFromSplineTangent(Vector3 tangent)
    {
        Vector3 lookDir = _characterMovement.transform.forward;
        lookDir = new Vector3(tangent.x, 0f, tangent.z).normalized;

        return lookDir;
    }

    public bool IsOnBalancePath()
    {
        if (!_characterMovement.IsGrounded)
        {
            return false;
        }
        else
        {
            Vector3 offset = _characterMovement.RB.position - _currentSample.Position;
            Vector3 splineRight = Vector3.Cross(_currentSample.Up, _currentSample.Tangent).normalized;
            float lateralDistance = Mathf.Abs(Vector3.Dot(offset, splineRight));

            //If too far away laterally, or reached the end and moving in that direction, transition out of balancing.
            if (lateralDistance > _breakingDistance ||
                (_currentDistanceAlongPath <= _endPointTolerance && _currentBalancingSpeed < 0f) ||
                (_currentDistanceAlongPath >= (_currentBalancePath.Path.Length - _endPointTolerance) && _currentBalancingSpeed > 0f))
            {
                return false;
            }
        }
        return true;
    }
}
