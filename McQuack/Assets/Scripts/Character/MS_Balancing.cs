using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MS_Balancing : AMovementState
{
    [Header("OBJECT REFS")]
    [SerializeField] private BalancePath _currentBalanceLine;
    public BalancePath CurrentBalanceLine { get { return _currentBalanceLine; } }

    [Header("BALANCING ROTATION VALUES")]
    [SerializeField] private float _balancingRotationSharpness = 10f;

    private CharacterMovement _characterMovement;

    private int _currentPoint = 0;
    private int _nextPoint = 0;
    private bool _facingForward = true;

    public override void OnStateEnter(CharacterMovement character)
    {
        base.OnStateEnter(character);

        character.SetStateType(EMovementStates.Balancing);
        _characterMovement = character;
    }

    public override void OnStateExit(CharacterMovement character)
    {
        base.OnStateExit(character);

        _currentBalanceLine = null;
    }

    public override void OnStateUpdate(CharacterMovement character)
    {
        base.OnStateUpdate(character);
    }

    public override void OnStateFixedUpdate(CharacterMovement character)
    {
        base.OnStateFixedUpdate(character);


        if (_currentBalanceLine == null) return;
        HandleRotation();
        HandleVelocity();
    }

    protected override void HandleRotation()
    {
        //Set forward rotation to direction of current point to next point.
        //Depending on facing (forward/backward), next point is i+1 or i-1.
        //If at last point, use previous rotation direction.

        //If moveinput direction is beyond a certain dotproduct compared to current facing and sides, then TurnAround();

        //Vector3 currentPointNoY = new Vector3(_currentBalanceLine.BalanceLinePoints[_currentPoint].position.x, 0f, _currentBalanceLine.BalanceLinePoints[_currentPoint].position.z);
        //Vector3 nextPointNoY = new Vector3(_currentBalanceLine.BalanceLinePoints[_nextPoint].position.x, 0f, _currentBalanceLine.BalanceLinePoints[_nextPoint].position.z);
        //Vector3 nextPointDir = Vector3.Normalize(nextPointNoY - currentPointNoY);  
        //Vector3 toLookVector = nextPointDir;

        //float toRotationSharpness = _balancingRotationSharpness;

        //Vector3 smoothedLookInputDirection = _characterMovement.transform.forward;

        //if (toLookVector.sqrMagnitude > 0f && toRotationSharpness > 0f)
        //{
        //    smoothedLookInputDirection = Vector3.Slerp(transform.forward, toLookVector, 1 - Mathf.Exp(-toRotationSharpness * Time.fixedDeltaTime)).normalized;

        //    _characterMovement.transform.forward = smoothedLookInputDirection;
        //}
        //else
        //{
        //    smoothedLookInputDirection = Vector3.Slerp(transform.forward, transform.forward, 1 - Mathf.Exp(-toRotationSharpness * Time.fixedDeltaTime)).normalized;

        //    _characterMovement.transform.forward = smoothedLookInputDirection;
        //}
    }

    protected override void HandleVelocity()
    {
        //Move character in direction of next point on currentbalanceline.
        //If character has arrived at next point, set current point as that point and next point to the one further.
        //In the case where they've reached the end of all points, can Transition off of balancing movementmode.
    }

    public void SetBalanceLine(BalancePath balanceLine)
    {
        _currentBalanceLine = balanceLine;
    }

    private void SetNextPoint()
    {
        if (_facingForward)
        {
            _currentPoint++;
            _nextPoint++;
        }
        else
        {
            _currentPoint--;
            _nextPoint--;
        }
    }
    private void TurnAround()
    {
        int localCurrent = _currentPoint;
        int localNext = _nextPoint;

        _currentPoint = localNext;
        _nextPoint = localCurrent;
    }
}
