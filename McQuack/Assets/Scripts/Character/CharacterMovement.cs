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
}

public class CharacterMovement : MonoBehaviour
{
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

    [Header("GROUNDCHECK")]
    [SerializeField] private float _groundCheckDistance = .5f;
    [SerializeField] private LayerMask _groundLayers;
    [SerializeField] private float _maxGroundedAngle = 60f;

    [Header("GENERAL PARAMETERS")]
    [SerializeField] private bool _canRotate = true;
    public bool CanRotate { get { return _canRotate; } }
    [SerializeField] private bool _canMove = true;
    public bool CanMove { get { return _canMove; } }
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

    private bool _isGrounded = false;
    public bool IsGrounded { get { return _isGrounded; } }
    private RaycastHit _groundHit;
    public RaycastHit GroundHit { get { return _groundHit; } }

    public bool CanJump { get { return _isGrounded ? true : false; } }

    private bool _canTransitionFromState = true;


    private void Start()
    {
        InitializeCharacterMovement();
    }

    private void InitializeCharacterMovement()
    {
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
            case EMovementStates.Falling:
                TransitionToState(_fallingState);
                break;
            case EMovementStates.Jumping:
                TransitionToState(_jumpingState);
                break;
            case EMovementStates.Balancing:
                TransitionToState(_balancingState);
                break;
            default:
                Debug.LogWarning(this.name + " doesn't have a valid default movement state.");
                break;
        }
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
        _isGrounded = GroundCheck();

        HandleStateTransitions();

        if (CurrentState)
        {
            CurrentState.OnStateFixedUpdate(this);
        }

        _canTransitionFromState = true;
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
    }

    public void SetStateType(EMovementStates type)
    {
        _currentStateType = type;
    }

    private void HandleStateTransitions()
    {
        if (!_canTransitionFromState) return;

        switch (_currentStateType)
        {
            case EMovementStates.None:
                break;
            case EMovementStates.Walking:
                if (!_isGrounded)
                {
                    TransitionToState(_fallingState);
                }
                break;
            case EMovementStates.Falling:
                if (_isGrounded)
                {
                    TransitionToState(_walkingState);
                }
                break;
            case EMovementStates.Jumping:
                if(_rb.velocity.y <= 0)
                {
                    TransitionToState(_fallingState);
                    //Call reach jump apex
                }
                break;
            case EMovementStates.Balancing:
                if (_balancingState.CurrentBalanceLine == null)
                {
                    TransitionToState(_walkingState);
                }
                break;
            default:
                break;
        }
    }

    #endregion
    #region GROUNDING
    private bool GroundCheck()
    {
        bool localIsGrounded = Physics.SphereCast(transform.position + (transform.up * .5f), .2f, Vector3.down, out _groundHit, _groundCheckDistance, _groundLayers, QueryTriggerInteraction.Ignore);
        if (localIsGrounded)
        {
            float angle = Vector3.Angle(Vector3.up, _groundHit.normal);
            localIsGrounded = angle <= _maxGroundedAngle ? true : false;
        }

        if (localIsGrounded && !_isGrounded)
        {
            //On land
        }

        return localIsGrounded;
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
    }

    public void RequestJump()
    {
        if (CanJump)
        {
            TransitionToState(_jumpingState);
            _canTransitionFromState = false;
        }
    }
    #endregion
    #region BALANCING
    public void StartBalance(BalancePath balanceLine)
    {
        _balancingState.SetBalanceLine(balanceLine);
        TransitionToState(_balancingState);
    }
    #endregion
}
