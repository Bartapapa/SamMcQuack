using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AMovementState : MonoBehaviour
{
    public virtual void OnStateEnter(CharacterMovement character)
    {

    }

    public virtual void OnStateExit(CharacterMovement character)
    {

    }

    public virtual void OnStateUpdate(CharacterMovement character)
    {

    }

    public virtual void OnStateFixedUpdate(CharacterMovement character)
    {

    }

    protected virtual void HandleRotation()
    {

    }

    protected virtual void HandleVelocity()
    {

    }
}
