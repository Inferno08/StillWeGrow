using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 6.5f;
    [SerializeField] private float gravity = -20f;

    [Header("Turning")]
    [SerializeField] private bool instantTurn = true;
    [SerializeField] private float turnSpeed = 25f;

    [Header("Scripted walk")]
    [Tooltip("How close counts as 'arrived' when walking to a dig spot.")]
    [SerializeField] private float arriveThreshold = 0.15f;
    [Tooltip("If the player moves less than this per second, they're considered stuck.")]
    [SerializeField] private float stuckSpeedThreshold = 0.35f;
    [Tooltip("How long to be stuck before giving up and arriving anyway.")]
    [SerializeField] private float stuckTime = 0.35f;
    [Tooltip("Hard cap on a scripted walk, in seconds.")]
    [SerializeField] private float walkTimeout = 4f;

    [Header("Visual / Bob")]
    [SerializeField] private Transform visual;
    [SerializeField] private float bobFrequency = 9f;
    [SerializeField] private float bobAmplitude = 0.05f;
    [SerializeField] private float bobSway = 0.03f;
    [SerializeField] private float bobRoll = 2.5f;
    [SerializeField] private float bobEase = 10f;

    /// <summary>When true, WASD is ignored (e.g. while digging).</summary>
    public bool MovementLocked { get; set; }

    private CharacterController controller;
    private Transform cam;
    private Vector3 planarVelocity;
    private float verticalVelocity;
    private float bobTimer;
    private float bobStrength;

    private Vector3? walkTarget;
    private Action onArrive;
    private Vector3? facePoint;

    private Vector3 lastPosition;
    private float stuckTimer;
    private float walkTimer;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (Camera.main != null) cam = Camera.main.transform;
    }

    private void Update()
    {
        Move();
        Bob();
    }

    /// <summary>Walk to a world position, then fire the callback. Ignores player input meanwhile.</summary>
    public void WalkTo(Vector3 target, Action arrived)
    {
        Vector3 flat = target - transform.position;
        flat.y = 0f;

        if (flat.magnitude <= arriveThreshold)
        {
            arrived?.Invoke();   // already there
            return;
        }

        walkTarget = target;
        onArrive = arrived;

        lastPosition = transform.position;
        stuckTimer = 0f;
        walkTimer = 0f;
    }

    public void CancelWalk()
    {
        walkTarget = null;
        onArrive = null;
        stuckTimer = 0f;
        walkTimer = 0f;
    }

    /// <summary>Turn to face a world point (used while digging). Pass null to clear.</summary>
    public void FacePoint(Vector3? point) => facePoint = point;

    private void Move()
    {
        Vector3 dir = Vector3.zero;
        float targetSpeed = walkSpeed;

        if (walkTarget.HasValue)
        {
            Vector3 to = walkTarget.Value - transform.position;
            to.y = 0f;

            bool arrived = to.magnitude <= arriveThreshold;

            if (!arrived)
            {
                // --- Stuck detection: blocked by a wall, or target unreachable ---
                walkTimer += Time.deltaTime;

                Vector3 moved = transform.position - lastPosition;
                moved.y = 0f;
                float actualSpeed = moved.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
                lastPosition = transform.position;

                if (actualSpeed < stuckSpeedThreshold) stuckTimer += Time.deltaTime;
                else stuckTimer = 0f;

                if (stuckTimer >= stuckTime || walkTimer >= walkTimeout)
                    arrived = true;   // give up here; close enough
            }

            if (arrived)
            {
                walkTarget = null;
                planarVelocity = Vector3.zero;
                stuckTimer = 0f;
                walkTimer = 0f;

                var cb = onArrive;
                onArrive = null;
                cb?.Invoke();
            }
            else
            {
                dir = to.normalized;
            }
        }
        else if (!MovementLocked)
        {
            Vector2 input = Vector2.zero;
            var kb = Keyboard.current;
            bool sprinting = false;
            if (kb != null)
            {
                if (kb.wKey.isPressed) input.y += 1f;
                if (kb.sKey.isPressed) input.y -= 1f;
                if (kb.dKey.isPressed) input.x += 1f;
                if (kb.aKey.isPressed) input.x -= 1f;
                sprinting = kb.leftShiftKey.isPressed;
            }

            Vector3 camForward = cam != null ? cam.forward : Vector3.forward;
            Vector3 camRight = cam != null ? cam.right : Vector3.right;
            camForward.y = 0f; camRight.y = 0f;
            camForward.Normalize(); camRight.Normalize();

            dir = camForward * input.y + camRight * input.x;
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            targetSpeed = sprinting ? sprintSpeed : walkSpeed;
        }

        planarVelocity = dir * targetSpeed;

        // --- Facing ---
        if (dir.sqrMagnitude > 0.01f)
        {
            ApplyFacing(Quaternion.LookRotation(dir, Vector3.up));
        }
        else if (facePoint.HasValue)
        {
            Vector3 toFace = facePoint.Value - transform.position;
            toFace.y = 0f;
            if (toFace.sqrMagnitude > 0.01f)
                ApplyFacing(Quaternion.LookRotation(toFace.normalized, Vector3.up));
        }

        // --- Gravity ---
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;

        // --- Apply ---
        Vector3 velocity = planarVelocity + Vector3.up * verticalVelocity;
        controller.Move(velocity * Time.deltaTime);
    }

    private void ApplyFacing(Quaternion target)
    {
        transform.rotation = instantTurn
            ? target
            : Quaternion.Slerp(transform.rotation, target, turnSpeed * Time.deltaTime);
    }

    private void Bob()
    {
        if (visual == null) return;

        float speed = planarVelocity.magnitude;
        float targetStrength = walkSpeed > 0.01f ? speed / walkSpeed : 0f;

        bobStrength = Mathf.Lerp(bobStrength, targetStrength, bobEase * Time.deltaTime);

        float freq = bobFrequency * Mathf.Clamp(targetStrength, 0f, 2f);
        bobTimer += freq * Time.deltaTime;

        float bobY = Mathf.Abs(Mathf.Sin(bobTimer)) * bobAmplitude * bobStrength;
        float swayX = Mathf.Sin(bobTimer) * bobSway * bobStrength;
        float rollZ = -Mathf.Sin(bobTimer) * bobRoll * bobStrength;

        visual.localPosition = new Vector3(swayX, bobY, 0f);
        visual.localRotation = Quaternion.Euler(0f, 0f, rollZ);
    }
}