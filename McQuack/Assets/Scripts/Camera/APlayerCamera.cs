using Cinemachine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class APlayerCamera : MonoBehaviour
{
    [Header("OBJECT REFS")]
    [SerializeField] protected CinemachineVirtualCamera _cam;

    public virtual void SetInputs(ref PlayerInput input)
    {

    }
}
