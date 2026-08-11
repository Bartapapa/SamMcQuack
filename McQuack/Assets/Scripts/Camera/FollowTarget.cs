using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowTarget : MonoBehaviour
{
    [Header("TARGET")]
    public Transform Target;

    [Header("PARAMETERS")]
    public float TargetYOffset;

    void LateUpdate()
    {
        if (Target != null)
        {
            transform.position = Target.position + new Vector3(0, TargetYOffset, 0);
        }
    }
}
