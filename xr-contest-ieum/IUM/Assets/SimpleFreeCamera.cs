using UnityEngine;
using UnityEngine.InputSystem; // 새로운 Input System 패키지 사용

public class SimpleFreeCamera : MonoBehaviour
{
    [Header("이동 속도 설정")]
    public float walkSpeed = 100.0f;     // 기본 이동 속도 (대폭 상향하여 답답함 해결)
    public float runSpeed = 250.0f;      // Shift 눌렀을 때 속도 (엄청나게 빠른 대시)
    public float riseSpeed = 80.0f;      // 상승/하강 속도

    [Header("카메라 감도 설정")]
    public float lookSensitivity = 0.01f; // 마우스 회전 감도 (튀지 않게 아주 정밀하고 확 낮춤)
    
    private float rotationX = 0.0f;
    private float rotationY = 0.0f;

    void Start()
    {
        // 시작 시 카메라의 원래 회전값을 마우스 각도 변수에 연동
        Vector3 currentRot = transform.localRotation.eulerAngles;
        rotationY = currentRot.y;
        rotationX = currentRot.x;

        // 혹시 카메라에 Rigidbody(물리)가 붙어서 중력으로 떨어지는 경우 방지
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    void Update()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        
        if (keyboard == null || mouse == null) return;

        // Alt(알트) 키가 눌려 있는지 감지
        bool isAltPressed = keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed;

        // ----------------------------------------------------
        // 1. 마우스 회전 및 커서 상태 제어
        // ----------------------------------------------------
        if (!isAltPressed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 마우스 이동 델타값을 아주 작고 부드럽게 변환
            Vector2 mouseDelta = mouse.delta.ReadValue() * lookSensitivity;

            rotationY += mouseDelta.x;
            rotationX -= mouseDelta.y;
            
            // 위아래 카메라 회전 각도 제한 (-85도 ~ 85도)
            rotationX = Mathf.Clamp(rotationX, -85f, 85f);

            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0.0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ----------------------------------------------------
        // 2. 키보드 이동 (수평 XZ 평면 이동)
        // ----------------------------------------------------
        float currentSpeed = (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed) ? runSpeed : walkSpeed;

        float moveForward = 0.0f;
        if (keyboard.wKey.isPressed) moveForward = 1.0f;
        if (keyboard.sKey.isPressed) moveForward = -1.0f;

        float moveSideways = 0.0f;
        if (keyboard.dKey.isPressed) moveSideways = 1.0f;
        if (keyboard.aKey.isPressed) moveSideways = -1.0f;

        Vector3 forward = transform.forward;
        forward.y = 0.0f;
        forward.Normalize();

        Vector3 right = transform.right;
        right.y = 0.0f;
        right.Normalize();

        Vector3 moveDirection = (forward * moveForward) + (right * moveSideways);

        // Space바로 공중 상승 / Ctrl 이나 C 키로 하강
        float moveUpDown = 0.0f;
        if (keyboard.spaceKey.isPressed)
        {
            moveUpDown = 1.0f;  // 위로 상승
        }
        else if (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed)
        {
            moveUpDown = -1.0f; // 아래로 하강
        }

        // 수직 이동 성분을 수평 이동 성분과 최종 합산
        moveDirection += Vector3.up * moveUpDown * (riseSpeed / currentSpeed);

        // 최종 이동 계산 및 적용
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            transform.position += moveDirection.normalized * currentSpeed * Time.deltaTime;
        }
    }
}
