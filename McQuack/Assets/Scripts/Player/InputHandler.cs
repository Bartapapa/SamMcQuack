using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public struct PlayerInput
{
    public float MoveX;
    public float MoveY;
    public Camera CameraRef;

    public bool JumpInputHeld;

    public Vector2 AimVector;
}

public class InputHandler : MonoBehaviour
{
    [Header("Controlled objects")]
    [SerializeField] private CharacterMovement _character;
    [SerializeField] private APlayerCamera _cam;

    private bool _jumpInputHeld = false;

    private Vector2 _movement = Vector2.zero;
    private Vector2 _aim = Vector2.zero;

    private void Update()
    {
        if (_character != null && _cam != null)
        {
            SendPlayerInputs();
        }
    }

    public void OnMovement(InputAction.CallbackContext context)
    {
        _movement = context.ReadValue<Vector2>();
    }

    public void OnAim(InputAction.CallbackContext context)
    {
        _aim = context.ReadValue<Vector2>();
        _aim.x = Mathf.Clamp(_aim.x, -1f, 1f);
        _aim.y = Mathf.Clamp(_aim.y, -1f, 1f);
    }

    public void OnJump(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            _character.RequestJump();
        }

        if (context.started)
        {
            _jumpInputHeld = true;
        }
        if (context.canceled)
        {
            _jumpInputHeld = false;
        }
    }

    private void SendPlayerInputs()
    {
        PlayerInput playerInput = new PlayerInput();

        playerInput.MoveX = _movement.x;
        playerInput.MoveY = _movement.y;

        playerInput.JumpInputHeld = _jumpInputHeld;

        playerInput.CameraRef = Camera.main;

        playerInput.AimVector = _aim;

        _character.SetInputs(ref playerInput);
        _cam.SetInputs(ref playerInput);
    }
}
