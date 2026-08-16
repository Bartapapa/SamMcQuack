using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EMovementStates
{
    None,
    Walking,
    Falling,
    Jumping,
    Balancing,
    Sliding,
    Mantling,
    LedgeGrabbing,
}

public class CharacterMovement : MonoBehaviour
{
    [Header("OBJECT REFERENCES")]
    [SerializeField] private EnvironmentDetector _detector;
    public EnvironmentDetector Detector { get { return _detector; } }
    private CameraManager _camManager;
    public CameraManager CamManager { get { return _camManager; } }

    [Header("STATES")]
    [SerializeField] private AMovementState _currentState = null;
    public AMovementState CurrentState { get { return _currentState; } }
    [SerializeField] private EMovementStates _currentStateType = EMovementStates.None;
    public EMovementStates CurrentStateType { get { return _currentStateType; } }
    [SerializeField] private EMovementStates _defaultState;
    [SerializeField] private MS_Walking _walkingState;
    [SerializeField] private MS_Falling _fallingState;
    [SerializeField] private MS_Jumping _jumpingState;
    [SerializeField] private MS_Balancing _balancingState;
    [SerializeField] private MS_Sliding _slidingState;
    [SerializeField] private MS_Mantle _mantlingState;
    [SerializeField] private MS_LedgeGrab _ledgeGrabbingState;

    [Header("ENVIRONMENT")]
    [SerializeField] private LayerMask _environmentMask;

    [Header("GROUNDING")]
    [SerializeField] private float _maxGroundedAngle = 60f;
    public float MaxGroundedAngle { get { return _maxGroundedAngle; } }
    private GroundDetectionDescriptor _groundDetectionDescriptor;
    public GroundDetectionDescriptor GroundDetectionDescriptor { get { return _groundDetectionDescriptor; } }

    //Grounding vars
    private bool _isGrounded = false;
    private bool _isGroundedPreviousFrame = false;
    public bool IsGrounded { get { return _isGrounded; } }
    private bool _groundDetected = false;
    public bool GroundDetected { get { return _groundDetected; } }

    //Jumping vars
    public bool CanJump { get { return _groundDetected; } }
    private bool _jumpInputHeld = false;
    public bool JumpInputHeld { get { return _jumpInputHeld; } }

    [Header("LEDGES")]
    [SerializeField] private float _ledgeStandingPointClearance = .5f;
    [SerializeField] private float _minimumLedgeGrabHeight = 2f;
    [SerializeField] private float _grabToMantleMinimumAlignment = .5f;

    //Ledgegrabbing & mantling vars
    private LedgeDetectionDescriptor _ledgeDetectionDescriptor;
    private bool _canMantle = false;
    private bool _canGrabLedge = false;

    [Header("SLIDING")]
    [SerializeField] private float _maxSlopeAngle = 80f;
    public float MaxSlopeAngle { get { return _maxSlopeAngle; } }

    //Sliding vars
    private bool _slidePossible = false;
    private float _slideRequestTimer = 0f;

    [Header("GENERAL PARAMETERS")]
    [SerializeField] private bool _canRotate = true;
    public bool CanRotate { get { return _canRotate; } }
    [SerializeField] private bool _canMove = true;
    public bool CanMove { get { return _canMove; } }

    //General vars
    private Vector3 _forcedLookAtDir = Vector3.zero;
    public Vector3 ForcedLookAtDir { get { return _forcedLookAtDir; } }

    private Rigidbody _rb;
    public Rigidbody RB { get { return _rb; } }
    private CapsuleCollider _capsule;
    public CapsuleCollider Capsule { get { return _capsule; } }

    private Vector3 _moveInputVector;
    public Vector3 MoveInputVector { get { return _moveInputVector; } }
    private Vector3 _lookInputVector;
    public Vector3 LookInputVector { get { return _lookInputVector; } }

    public delegate void MovementStateTransitionEvent(EMovementStates from, EMovementStates to);
    public event MovementStateTransitionEvent MovementStateTransitioned;

    private void Start()
    {
        InitializeCharacterMovement();
    }

    private void InitializeCharacterMovement()
    {
        _camManager = CameraManager.Instance;

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
        {
            Debug.LogWarning(this.name + " doesn't have a Rigidbody!");
            return;
        }

        _capsule = GetComponent<CapsuleCollider>();
        if (_capsule == null)
        {
            Debug.LogWarning(this.name + " doesn't have a CapsuleCollider!");
            return;
        }

        switch (_defaultState)
        {
            case EMovementStates.Walking:
                TransitionToState(_walkingState);
                break;
            default:
                Debug.LogWarning(this.name + " doesn't have a valid default movement state.");
                break;
        }

        _camManager.InitializeCameraManagerFromState(_defaultState);
        MovementStateTransitioned -= _camManager.OnMovementStateTransitioned;
        MovementStateTransitioned += _camManager.OnMovementStateTransitioned;
    }

    private void Update()
    {
        if (CurrentState)
        {
            CurrentState.OnStateUpdate(this);
        }
    }

    private void FixedUpdate()
    {
        //WallCheck();
        LedgeCheck();
        GroundCheck();

        HandleStateTransitions();

        if (CurrentState)
        {
            CurrentState.OnStateFixedUpdate(this);
        }
    }

    #region STATEMACHINE
    public void TransitionToState(AMovementState toState)
    {
        AMovementState oldState = CurrentState;
        if (oldState)
        {
            oldState.OnStateExit(this);
            _currentState = null;
        }
        _currentState = toState;
        toState.OnStateEnter(this);

        MovementStateTransitioned?.Invoke(oldState.StateEnum, toState.StateEnum);
    }

    public void SetStateType(EMovementStates type)
    {
        _currentStateType = type;
    }

    private void HandleStateTransitions()
    {
        BalancePath balancePath; 

        switch (_currentStateType)
        {
            case EMovementStates.None:
                break;
            case EMovementStates.Walking:
                if (!_isGrounded)
                {
                    TransitionToState(_fallingState);
                    break;
                }

                balancePath = _detector.GroundHit.collider.GetComponentInParent<BalancePath>();
                if (balancePath)
                {
                    StartBalance(balancePath);
                    break;
                }
                break;
            case EMovementStates.Falling:
                if (_isGrounded)
                {
                    balancePath = _detector.GroundHit.collider.GetComponentInParent<BalancePath>();
                    if (balancePath)
                    {
                        StartBalance(balancePath);
                        break;
                    }
                    else
                    {
                        TransitionToState(_walkingState);
                        break;
                    }
                }
                else
                {
                    if (_canGrabLedge)
                    {
                        _ledgeGrabbingState.GrabLedge(_ledgeDetectionDescriptor, Capsule, false);
                        TransitionToState(_ledgeGrabbingState);
                    }

                    if (_slidePossible && (_slideRequestTimer >= _slidingState.SlideRequestTime || (_detector.FacingSlope() > .5f && _slideRequestTimer >= .2f)))
                    {
                        TransitionToState(_slidingState);
                    }
                    break;
                }
            case EMovementStates.Jumping:
                if(_rb.velocity.y <= 0 && _jumpingState.HasLeftGround)
                {
                    TransitionToState(_fallingState);
                    //Call reach jump apex
                }
                break;
            case EMovementStates.Balancing:
                if (_balancingState.CurrentBalanceLine == null)
                {
                    TransitionToState(_walkingState);
                    break;
                }
                if (!_balancingState.IsOnBalancePath())
                {
                    TransitionToState(_walkingState);
                    break;
                }
                break;
            case EMovementStates.Sliding:
                if (!_isGrounded && !_slidePossible)
                {
                    TransitionToState(_walkingState);
                    break;
                }
                else
                {
                    if (_slidingState.CurrentSlidingSpeed <= _slidingState.MinimumSafeSlidingSpeed && !_slidingState.OnSteepSlope)
                    {
                        TransitionToState(_walkingState);
                        break;
                    }
                }
                break;
            case EMovementStates.Mantling:
                if (_mantlingState.ElapsedTime >= _mantlingState.MantleDuration)
                {
                    TransitionToState(_walkingState);
                    break;
                }
                break;
            case EMovementStates.LedgeGrabbing:
                if (_moveInputVector.sqrMagnitude < 0.001f || (_ledgeGrabbingState.ElapsedTime / _ledgeGrabbingState.LedgeGrabDuration) < 1) break;

                Vector3 inputRight = Vector3.Cross(_moveInputVector, Vector3.up);
                Vector3 reorientedInput = Vector3.Cross(Vector3.up, inputRight).normalized * _moveInputVector.magnitude;
                Debug.DrawRay(transform.position + Vector3.up, reorientedInput);

                float dot = Vector3.Dot(reorientedInput, -_ledgeDetectionDescriptor.WallNormal);
                if (dot >= _grabToMantleMinimumAlignment)
                {
                    _mantlingState.SetLedgeDescriptor(_ledgeDetectionDescriptor);
                    TransitionToState(_mantlingState);
                    break;
                }
                break;
            default:
                break;
        }
    }

    #endregion
    #region ENVIRONMENT DETECTION
    private void GroundCheck()
    {
        if (Detector.GroundCheck(_maxGroundedAngle, _maxSlopeAngle, out var ground))
        {
            _groundDetectionDescriptor = ground;

            _groundDetected = true;
            _isGrounded = ground.WalkableGroundDetected;
            _slidePossible = ground.SteepSlopeDetected;

            if (_isGrounded && !_isGroundedPreviousFrame)
            {
                //On land
            }
            _isGroundedPreviousFrame = _isGrounded;

            if (_slidePossible && _currentStateType != EMovementStates.Sliding)
            {
                _slideRequestTimer += Time.fixedDeltaTime;
            }
        }
        else
        {
            _groundDetected = false;
            _isGrounded = false;
            _slidePossible = false;

            _slideRequestTimer = 0f;
        }
    }

    private void WallCheck()
    {
        if (_detector.WallCheck(
transform.position + _detector.WallCastOffset,
transform.forward,
out WallDetectionDescriptor wall))
        {
            Debug.DrawRay(
                wall.Point,
                wall.Normal,
                Color.green);

            Debug.DrawRay(
                wall.Point,
                Vector3.up * 0.2f,
                Color.green);
        }
    }

    private void LedgeCheck()
    {
        if (_detector.LedgeCheck(transform.position + _detector.WallCastOffset, transform.forward, _ledgeStandingPointClearance, _maxGroundedAngle, out var ledge))
        {
            _ledgeDetectionDescriptor = ledge;

            if (_detector.CanCharacterFit(ledge.StandPoint, _capsule.height, _capsule.radius, _environmentMask))
            {
                float ledgeHeightRelativeToPosition = ledge.WallHitToGroundHitHeight + _detector.WallCastOffset.y;
                if (ledgeHeightRelativeToPosition <= _minimumLedgeGrabHeight)
                {
                    _canMantle = true;
                    _canGrabLedge = false;
                }
                else
                {
                    _canMantle = false;
                    _canGrabLedge = true;
                }
            }
        }
        else
        {
            _canMantle = false;
            _canGrabLedge = false;
        }
    }

    #endregion
    #region INPUTS
    public void SetInputs(ref PlayerInput input)
    {
        Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(input.MoveX, 0f, input.MoveY), 1f);
        if (!_canMove) moveInputVector = Vector3.zero;

        float cameraRotation = input.CameraRef.transform.eulerAngles.y;
        Quaternion controlRotation = Quaternion.Euler(0, cameraRotation, 0);

        Vector3 desiredMoveInputVector = controlRotation * moveInputVector;

        _moveInputVector = desiredMoveInputVector;

        _lookInputVector = _moveInputVector.normalized;

        _jumpInputHeld = input.JumpInputHeld;
    }

    public void RequestJump()
    {
        if (_canMantle)
        {
            _mantlingState.SetLedgeDescriptor(_ledgeDetectionDescriptor);
            TransitionToState(_mantlingState);
        }
        else if (_canGrabLedge)
        {
            _ledgeGrabbingState.GrabLedge(_ledgeDetectionDescriptor, Capsule);
            TransitionToState(_ledgeGrabbingState);
        }
        else
        {
            if (CanJump)
            {
                TransitionToState(_jumpingState);
            }
        }

    }
    #endregion
    #region BALANCING
    public void StartBalance(BalancePath balancePath)
    {
        _balancingState.UseBalancePath(balancePath);
        TransitionToState(_balancingState);
    }
    #endregion
}
