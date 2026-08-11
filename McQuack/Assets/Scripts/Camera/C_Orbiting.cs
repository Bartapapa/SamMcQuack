using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows;

public class C_Orbiting : APlayerCamera
{
    [Header("OBJECT REFERENCES")]
    [SerializeField] private Transform _follow;

    [Header("ORBIT PARAMETERS")]
    [SerializeField] private float _rotationDamping = 5f;
    [SerializeField] private float _xSpeed = 5f;
    [SerializeField] private float _ySpeed = 5f;
    [SerializeField] private float _maximumXAngle = 340f;
    [SerializeField] private float _minimumXAngle = 40f;

    private Vector2 _aimVector = Vector2.zero;

    private float _targetYaw;
    private float _targetPitch;

    private float _currentYaw;
    private float _currentPitch;

    private void Start()
    {
        _currentYaw = _targetYaw = _follow.localEulerAngles.y;
        _currentPitch = _targetPitch = _follow.localEulerAngles.x;
    }
    public void LateUpdate()
    {
        HandleAim();
    }

    public override void SetInputs(ref PlayerInput input)
    {
        _aimVector = input.AimVector;
    }

    private void HandleAim()
    {
        _targetYaw += _aimVector.x * _xSpeed;
        _targetPitch += _aimVector.y * _ySpeed;

        _targetPitch = Mathf.Clamp(_targetPitch, _minimumXAngle, _maximumXAngle);

        float damping = 1f - Mathf.Exp(-_rotationDamping * Time.deltaTime);

        _currentYaw = Mathf.Lerp(_currentYaw, _targetYaw, damping);
        _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, damping);

        _follow.localRotation = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
    }
}
