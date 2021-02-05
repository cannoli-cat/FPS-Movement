using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour {
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 500f;
    [SerializeField] private float moveMultiplier = 9f;
    [SerializeField] private float sprintMultiplier = 12f;
    [SerializeField] private float groundMaxSpeed = 20f;
    [SerializeField] private float airMaxSpeed = 30f;
    [SerializeField] [Tooltip("Adds to the original max speed")] private float sprintMaxSpeedModifier = 5f;
    [SerializeField] private float friction = 230f;
    [SerializeField] private float maxSlope = 15f;

    [Header("Ground Check Settings")]
    [SerializeField] private float groundCheckRadius = 0.4f;
    [SerializeField] private float groundCheckDistance = 0.75f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 300f;
    [SerializeField] private float jumpMultiplier = 1.5f;
    [SerializeField] private bool autoJump = true;
    [SerializeField] private float inAirAcceleration = 1000f;
    [SerializeField] private float inAirMovementModifier = 0.8f;
    [SerializeField] private float inAirDrag = 160f;
    [SerializeField] private bool enableInAirDrag = false;

    [Header("Slide & Crouch Settings")]
    [SerializeField] private float slideForce = 600f;
    [SerializeField] private float slideFriction = 100f;
    [SerializeField] private float slideCooldown = 1f;
    [SerializeField] private float slideSpeedThreshold = 5f;
    [SerializeField] private float slideStopThreshold = 2.4f;
    [SerializeField] private bool debugSlideTrajectory = false;
    [SerializeField] private float crouchMoveMultiplier = 0.5f;
    [SerializeField] private float crouchMaxSpeed = 60f;
    [SerializeField] private float crouchJumpMultiplier = 0.5f;
    [SerializeField] private Vector3 crouchScale = new Vector3(1f, 0.5f, 1f);

    [Header("Mouse Look Settings")]
    [SerializeField] private Transform playerCamera = null;
    [SerializeField] private Transform orientation = null;
    [SerializeField] private float maxAngle = 90f;
    [SerializeField] private Vector2 sensitivity = new Vector2(20f, 20f);
    [SerializeField] private float sensMultiplier = 0.2f;

    private Rigidbody rb = null;
    private Controls controls = null;

    private Vector2 mouse = Vector2.zero;
    private Vector2 moveInput = Vector2.zero;

    private Vector3 originalScale = Vector3.zero;

    private float xRotation = 0f, currentSlope = 0f, timeSinceLastSlide = Mathf.Infinity;
    private bool jumping, crouching, sliding, sprinting;

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        controls = new Controls();

        originalScale = transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private void Start() {
        controls.Gameplay.Jump.performed += ctx => {
            jumping = true;
            OnJump();
        };
        controls.Gameplay.Jump.canceled += ctx => jumping = false;

        controls.Gameplay.Crouch.performed += ctx => ToggleCrouch(true);
        controls.Gameplay.Crouch.canceled += ctx => ToggleCrouch(false);

        controls.Gameplay.Sprint.performed += ctx => sprinting = true;
        controls.Gameplay.Sprint.canceled += ctx => sprinting = false;
    }

    private void FixedUpdate() => UpdateMovement();

    private void Update() {
        if (timeSinceLastSlide < slideCooldown) timeSinceLastSlide += Time.deltaTime;

        UpdateInputs();
        UpdateMouseLook();
    }

    private void UpdateInputs() {
        mouse = controls.Gameplay.Mouse.ReadValue<Vector2>() * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        moveInput = controls.Gameplay.Movement.ReadValue<Vector2>();
    }

    private void UpdateMouseLook() {
        var rot = playerCamera.localRotation.eulerAngles;
        float xTo = rot.y + mouse.x;

        xRotation -= mouse.y;
        xRotation = Mathf.Clamp(xRotation, -maxAngle, maxAngle);

        playerCamera.localRotation = Quaternion.Euler(xRotation, xTo, 0f);
        orientation.localRotation = Quaternion.Euler(0f, xTo, 0f);
    }

    private void UpdateMovement() {
        Vector3 dir = orientation.right * moveInput.x + orientation.forward * moveInput.y;
        rb.AddForce(Vector3.down * Time.fixedDeltaTime * 10f);

        if (autoJump && jumping && GroundCheck()) OnJump();

        if (crouching && GroundCheck() && currentSlope >= maxSlope) {
            rb.AddForce(Vector3.down * Time.fixedDeltaTime * 5000f);
            return;
        }

        float multiplier = GroundCheck() && crouching ? crouchMoveMultiplier : 1f;
        float currentMaxSpeed = (!GroundCheck() || jumping) ? airMaxSpeed : (GroundCheck() && !crouching && sprinting) ? groundMaxSpeed + sprintMaxSpeedModifier : (crouching && GroundCheck()) ? crouchMaxSpeed : groundMaxSpeed;
        float currentMoveSpeed = moveSpeed * (sprinting ? sprintMultiplier : moveMultiplier);

        if (rb.velocity.magnitude > currentMaxSpeed) {
            dir = Vector3.zero;
            rb.velocity = Vector3.ClampMagnitude(rb.velocity, currentMaxSpeed);
        }

        Vector3 velocity = (GroundCheck() && !jumping) ? MoveGround(dir.normalized, rb.velocity, currentMoveSpeed) * multiplier
                                                       : MoveAir(dir.normalized, rb.velocity) * inAirMovementModifier;

        rb.AddForce(velocity);
    }

    private Vector3 MoveGround(Vector3 dir, Vector3 prevVelocity, float accelerate) {
        float speed = prevVelocity.magnitude;
        accelerate *= Time.fixedDeltaTime;

        if (speed != 0 && crouching && currentSlope >= maxSlope || sliding && timeSinceLastSlide < slideCooldown) {
            float drop = speed * slideFriction * Time.fixedDeltaTime;
            prevVelocity *= Mathf.Min(speed - drop, 0f) / speed;

            return prevVelocity + dir * accelerate;
        }

        if (speed != 0) {
            float drop = speed * friction * Time.fixedDeltaTime;
            prevVelocity *= Mathf.Min(speed - drop, 0f) / speed;
        }

        return prevVelocity + dir * accelerate;
    }

    private Vector3 MoveAir(Vector3 dir, Vector3 prevVelocity) {
        if (enableInAirDrag) {
            float drop = prevVelocity.magnitude * inAirDrag * Time.fixedDeltaTime;
            prevVelocity *= (drop != 0 ? Mathf.Min(prevVelocity.magnitude - drop, 0f) / prevVelocity.magnitude : 1f);
        }

        return prevVelocity + dir * (inAirAcceleration * Time.fixedDeltaTime);
    }

    private void OnJump() {
        if (GroundCheck()) {
            //If crouching and not sliding: crouch jump multiplier, if sliding: slide jump multiplier, and if all else is false: normal jump multiplier.
            float slideJumpMultiplier = rb.velocity.y < 0 ? rb.velocity.magnitude * 0.1f + jumpMultiplier : crouchJumpMultiplier; //scales to speed
            float currentMultiplier = crouching && currentSlope < maxSlope ? crouchJumpMultiplier : crouching && currentSlope >= maxSlope ? slideJumpMultiplier : jumpMultiplier;
            rb.AddForce(Vector2.up * jumpForce * currentMultiplier);
        }
    }

    private void ToggleCrouch(bool crouched) {
        if (crouched) {
            crouching = true;
            transform.localScale = crouchScale;
            transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);

            if (CanSlide()) Slide();
        }
        else {
            crouching = false;
            sliding = false;
            transform.localScale = originalScale;
            transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
        }
    }

    private bool GroundCheck() {
        float distance = crouching ? groundCheckDistance - (groundCheckDistance / 2) : groundCheckDistance;

        bool grounded = Physics.SphereCast(transform.position, groundCheckRadius, Vector3.down, out RaycastHit hit, distance, groundLayer);
        currentSlope = Vector3.Angle(Vector3.up, hit.normal);

        return grounded;
    }

    private void Slide() {
        rb.AddForce(orientation.forward * slideForce);

        sliding = true;
        timeSinceLastSlide = 0f;

        StartCoroutine(StopProjectedSlide(rb.velocity));
    }

    private IEnumerator StopProjectedSlide(Vector3 momentum) {
        //f*ing stupid f*ing physics dumb dumb math go BRRRR
        Vector3 velocity = momentum / rb.mass; //find velocity after slide
        Vector3 finalPos = transform.position + velocity; //estimated final position
        float distToPos = Vector3.Distance(transform.position, finalPos); //distance between final position and current position

        if (debugSlideTrajectory) Debug.DrawLine(transform.position, finalPos, Color.blue, 5f);

        while (crouching && distToPos > slideStopThreshold && currentSlope < maxSlope) {
            distToPos = Vector3.Distance(transform.position, finalPos); // update distance to final position

            if (debugSlideTrajectory)
                print($"Distance to final pos: {distToPos} | Arrived: {distToPos < slideStopThreshold}");

            yield return null;
        }

        sliding = false;
    }

    private bool CanSlide() {
        return rb.velocity.magnitude > slideSpeedThreshold && GroundCheck() && crouching && !sliding && timeSinceLastSlide >= slideCooldown && currentSlope < maxSlope;
    }

    private void OnCollisionEnter(Collision other) {
        if (CanSlide()) Slide();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();
}
