using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Input Actions")]
    [SerializeField] private InputActionReference moveAction;
    [SerializeField] private InputActionReference riseAction;
    [SerializeField] private InputActionReference rotateHoldAction;
    [SerializeField] private InputActionReference lookDeltaAction;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 8f;
    [SerializeField] private float verticalSpeed = 8f;

    [Header("Rotation")]
    [SerializeField] private float lookSensitivity = 0.1f;
    [SerializeField] private bool invertY = false;
    [SerializeField] private float minPitch = -89f;
    [SerializeField] private float maxPitch = 89f;

    private float yaw;
    private float pitch;

    private void Awake()
    {
        Vector3 euler = transform.rotation.eulerAngles;
        yaw = euler.y;
        pitch = NormalizePitch(euler.x);
    }

    private void OnEnable()
    {
        moveAction?.action.Enable();
        riseAction?.action.Enable();
        rotateHoldAction?.action.Enable();
        lookDeltaAction?.action.Enable();
    }

    private void OnDisable()
    {
        moveAction?.action.Disable();
        riseAction?.action.Disable();
        rotateHoldAction?.action.Disable();
        lookDeltaAction?.action.Disable();
    }

    private void Update()
    {
        UpdateMovement();
        UpdateRotation();
    }

    private void UpdateMovement()
    {
        Vector2 moveInput = moveAction != null ? moveAction.action.ReadValue<Vector2>() : Vector2.zero;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude > 0f)
            forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0f;
        if (right.sqrMagnitude > 0f)
            right.Normalize();

        Vector3 planar = right * moveInput.x + forward * moveInput.y;

        float vertical = 0f;
        bool risePressed = riseAction != null && riseAction.action.IsPressed();
        if (risePressed)
        {
            bool shiftPressed = IsShiftPressed();
            vertical = shiftPressed ? -1f : 1f;
        }

        Vector3 delta = planar * moveSpeed + Vector3.up * (vertical * verticalSpeed);
        transform.position += delta * Time.deltaTime;
    }

    private void UpdateRotation()
    {
        bool rotateHeld = rotateHoldAction != null && rotateHoldAction.action.IsPressed();
        if (!rotateHeld)
            return;

        Vector2 lookDelta = lookDeltaAction != null ? lookDeltaAction.action.ReadValue<Vector2>() : Vector2.zero;
        if (lookDelta == Vector2.zero)
            return;

        yaw += lookDelta.x * lookSensitivity;
        float yDelta = invertY ? lookDelta.y : -lookDelta.y;
        pitch = Mathf.Clamp(pitch + yDelta * lookSensitivity, minPitch, maxPitch);
        transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    private static bool IsShiftPressed()
    {
        if (Keyboard.current == null)
            return false;

        return Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed;
    }

    private static float NormalizePitch(float angle)
    {
        if (angle > 180f)
            angle -= 360f;
        return angle;
    }
}
