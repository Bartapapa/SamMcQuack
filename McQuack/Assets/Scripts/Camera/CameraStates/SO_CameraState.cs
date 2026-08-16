using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "CameraState_00", menuName = "McQuack/Camera/Camera State")]

public class SO_CameraState : ScriptableObject
{
    [Header("POSITION")]
    public float Distance;
    public Vector3 TargetOffset;

    [Header("LENS")]
    public float FOV;

    [Header("ROTATION")]
    public float PitchMin;
    public float PitchMax;

    [Space(20f)]
    [Header("TRANSITIONS")]
    public float TransitionDuration;
    public AnimationCurve TransitionCurve;
}
