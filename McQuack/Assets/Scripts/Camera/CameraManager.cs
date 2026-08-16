using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

public struct CameraParameters
{
    public float Distance;
    public Vector3 TargetOffset;

    public float PitchMin;
    public float PitchMax;

    public float FOV;
}

public class CameraManager : Singleton<CameraManager>
{
    [Header("OBJECT REFS")]
    [SerializeField] private CinemachineVirtualCamera _cam;
    [SerializeField] private Transform _followTarget;
    [SerializeField] private CharacterMovement _character;
    private Cinemachine3rdPersonFollow _follow;

    [Header("CAMERA STATES")]
    [SerializeField] private SO_CameraState _currentState;
    [SerializeField] private SO_CameraState _targetState;
    [Space(10f)]
    [SerializeField] private SO_CameraState _explorationState;
    [Space(10f)]
    [SerializeField] private SO_CameraState _mantleModifier;
    [SerializeField] private SO_CameraState _ledgeGrabModifier;
    [SerializeField] private SO_CameraState _slidingModifier;
    [SerializeField] private SO_CameraState _balancingModifier;
    [SerializeField] private List<SO_CameraState> _currentCamModifiers = new List<SO_CameraState>();

    [Header("ORBITING PARAMETERS")]
    [SerializeField] private float _rotationDamping = 5f;
    [SerializeField] private float _xSpeed = 5f;
    [SerializeField] private float _ySpeed = 5f;

    private CameraParameters _currentParameters;
    private CameraParameters _initialParameters;
    private CameraParameters _targetParameters;
    private float _transitionTimer = 0f;
    private float _transitionDuration = 0f;
    private AnimationCurve _transitionCurve;

    private Vector2 _aimVector = Vector2.zero;
    private float _targetYaw;
    private float _targetPitch;
    private float _currentYaw;
    private float _currentPitch;

    private bool _targetParamsDirty = false;

    protected override void Awake()
    {
        base.Awake();

        if (Instance != this)
        {
            return;
        }
    }

    private void Start()
    {
        _follow = _cam.GetCinemachineComponent<Cinemachine3rdPersonFollow>();

        InitializeCameraManager();
        _character.MovementStateTransitioned -= OnMovementStateTransitioned;
        _character.MovementStateTransitioned += OnMovementStateTransitioned;
    }

    private void LateUpdate()
    {
        HandleCameraTransition();
        ApplyCurrentCameraParameters();

        HandleOrbiting();
    }

    #region STATE HANDLING
    public void TransitionToState(SO_CameraState toState, bool clearModifiers = false, bool noTransition = false)
    {
        if (_targetState == toState || toState == null) return;

        _targetState = toState;

        float transitionDuration = noTransition ? 0f : toState.TransitionDuration;

        RecalculateTargetParameters(transitionDuration, toState.TransitionCurve);
    }

    public void TransitionToState(SO_CameraState toState, bool clearModifiers) => TransitionToState(toState, clearModifiers, false);

    public void ApplyCameraModifier(SO_CameraState modifier)
    {
        if (_currentCamModifiers.Contains(modifier)) return;
        _currentCamModifiers.Add(modifier);

        RecalculateTargetParameters(modifier.TransitionDuration, modifier.TransitionCurve);
    }

    public void RemoveCameraModifier(SO_CameraState modifier)
    {
        _currentCamModifiers.Remove(modifier);

        RecalculateTargetParameters(_targetState.TransitionDuration, _targetState.TransitionCurve);
    }

    public void InitializeCameraManager()
    {
        _currentCamModifiers.Clear();

        TransitionToState(_explorationState, false, true);
    }

    private void HandleCameraTransition()
    {
        if (_targetState == null) return;

        _transitionTimer += Time.deltaTime;

        float alpha = Mathf.Clamp01(_transitionTimer / _transitionDuration);
        alpha = _targetState.TransitionCurve.Evaluate(alpha);

        if (alpha >= 1f)
        {
            _currentState = _targetState;
        }

        _currentParameters = InterpolateParameters(_initialParameters, _targetParameters, alpha);
    }

    private CameraParameters InterpolateParameters(CameraParameters fromParameters, CameraParameters toParameters, float alpha)
    {
        return new CameraParameters
        {
            Distance = Mathf.Lerp(fromParameters.Distance, toParameters.Distance, alpha),
            TargetOffset = Vector3.Lerp(fromParameters.TargetOffset, toParameters.TargetOffset, alpha),
            PitchMin = Mathf.Lerp(fromParameters.PitchMin, toParameters.PitchMin, alpha),
            PitchMax = Mathf.Lerp(fromParameters.PitchMax, toParameters.PitchMax, alpha),
            FOV = Mathf.Lerp(fromParameters.FOV, toParameters.FOV, alpha)
        };
    }

    private CameraParameters GetModifierTotals(List<SO_CameraState> modifiers)
    {
        CameraParameters camMods = new CameraParameters();

        foreach(SO_CameraState modifier in modifiers)
        {
            camMods.Distance += modifier.Distance;
            camMods.TargetOffset += modifier.TargetOffset;

            camMods.PitchMin += modifier.PitchMin;
            camMods.PitchMax += modifier.PitchMax;

            camMods.FOV += modifier.FOV;
        }

        return camMods;
    }

    private CameraParameters GetModifiedCameraParameters(CameraParameters baseParams, CameraParameters modifierParams)
    {
        return new CameraParameters
        {
            Distance = baseParams.Distance + modifierParams.Distance,
            TargetOffset = baseParams.TargetOffset + modifierParams.TargetOffset,

            PitchMin = baseParams.PitchMin + modifierParams.PitchMin,
            PitchMax = baseParams.PitchMax + modifierParams.PitchMax,

            FOV = baseParams.FOV + modifierParams.FOV
        };
    }

    private CameraParameters GetStateParameters(SO_CameraState state)
    {
        return new CameraParameters
        {
            Distance = state.Distance,
            TargetOffset = state.TargetOffset,

            PitchMin = state.PitchMin,
            PitchMax = state.PitchMax,

            FOV = state.FOV
        };
    }

    private void ApplyCurrentCameraParameters()
    {
        _follow.CameraDistance = _currentParameters.Distance;
        _follow.ShoulderOffset = _currentParameters.TargetOffset;
        _cam.m_Lens.FieldOfView = _currentParameters.FOV;
    }

    private void RecalculateTargetParameters(float transitionDuration, AnimationCurve transitionCurve)
    {
        CameraParameters targetParams = GetModifiedCameraParameters(GetStateParameters(_targetState), GetModifierTotals(_currentCamModifiers));

        _initialParameters = transitionDuration == 0f ? targetParams : _currentParameters;
        _targetParameters = targetParams;

        _transitionTimer = 0f;
        _transitionDuration = transitionDuration;
        _transitionCurve = transitionCurve;

        _targetParamsDirty = false;
    }

    public void OnMovementStateTransitioned(EMovementStates fromState, EMovementStates toState)
    {
        //Remove modifiers according to fromState, Apply modifier according to toState
        switch (fromState)
        {
            case EMovementStates.Balancing:
                RemoveCameraModifier(_balancingModifier);
                break;
            case EMovementStates.Sliding:
                RemoveCameraModifier(_slidingModifier);
                break;
            case EMovementStates.Mantling:
                RemoveCameraModifier(_mantleModifier);
                break;
            case EMovementStates.LedgeGrabbing:
                RemoveCameraModifier(_ledgeGrabModifier);
                break;
            default:
                break;
        }

        switch (toState)
        {
            case EMovementStates.Balancing:
                ApplyCameraModifier(_balancingModifier);
                break;
            case EMovementStates.Sliding:
                ApplyCameraModifier(_slidingModifier);
                break;
            case EMovementStates.Mantling:
                ApplyCameraModifier(_mantleModifier);
                break;
            case EMovementStates.LedgeGrabbing:
                ApplyCameraModifier(_ledgeGrabModifier);
                break;
            default:
                break;
        }
    }
    #endregion
    #region ORBITING
    private void HandleOrbiting()
    {
        _targetYaw += _aimVector.x * _xSpeed;
        _targetPitch += _aimVector.y * _ySpeed;

        _targetPitch = Mathf.Clamp(_targetPitch, _currentParameters.PitchMin, _currentParameters.PitchMax);

        float damping = 1f - Mathf.Exp(-_rotationDamping * Time.deltaTime);

        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, damping);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, damping);
        _currentPitch = Mathf.Clamp(_currentPitch, _currentParameters.PitchMin, _currentParameters.PitchMax);

        ApplyOrbit(_currentPitch, _currentYaw);
    }

    private void ApplyOrbit(float pitch, float yaw)
    {
        if (_followTarget == null) return;
        _followTarget.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }
    #endregion
    #region INPUT
    public void SetInputs(ref PlayerInput input)
    {
        _aimVector = input.AimVector;
    }

    #endregion
}
