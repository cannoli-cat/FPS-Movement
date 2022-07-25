using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour {
    [Header("Movement Settings")]
    [SerializeField] private bool hasControl = true;
    [SerializeField] private float moveSpeed = 500f;
    [SerializeField] private float moveMultiplier = 9f;
    [SerializeField] private float groundMaxSpeed = 20f;
    [SerializeField] private float friction = 230f;
    [SerializeField] private float maxSlope = 15f;

    [Space(15)]
    [SerializeField] private bool enableInAirDrag = false;
    [SerializeField] private float inAirMaxSpeed = 30f;
    [SerializeField] private float inAirMovementModifier = 0.8f;
    [SerializeField] private float inAirDrag = 160f;

    [Header("Sprint Settings")]
    [SerializeField] private bool enableSprint = true;
    [SerializeField] private float sprintMultiplier = 12f;
    [SerializeField][Tooltip("Adds to the original max speed.")] private float sprintMaxSpeedModifier = 5f;

    [Header("Ground Check Settings")]
    [SerializeField] private float slopeRaycastDistance = 1f;
    [SerializeField] private LayerMask groundLayer;

    [Header("Jump Settings")]
    [SerializeField] private bool enableJump = true;
    [SerializeField] private bool autoJump = true;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private float jumpMultiplier = 1.5f;

    [Header("Crouch Settings")]
    [SerializeField] private bool enableCrouch = true;
    [SerializeField] private float crouchMoveMultiplier = 0.5f;
    [SerializeField] private float crouchMaxSpeed = 10f;
    [SerializeField] private float crouchJumpMultiplier = 1.4f;
    [SerializeField] private Vector3 crouchScale = new Vector3(1f, 0.5f, 1f);

    [Header("Slide Settings")]
    [SerializeField] private bool enableSlide = true;
    [SerializeField] private bool debugSlideTrajectory = false;
    [SerializeField] private float slideForce = 25f;
    [SerializeField] private float slideFriction = 3f;
    [SerializeField] private float slideCooldown = 1f;
    [SerializeField] private float slideSpeedThreshold = 5f;
    [SerializeField] private float slideStopThreshold = 2.4f;

    [Header("Mouse Look Settings")]
    [SerializeField] private bool enableMouseLook = true;
    [SerializeField] private Transform playerCamera = null;
    [SerializeField] private Transform orientation = null;
    [SerializeField] private Vector2 sensitivity = new Vector2(20f, 20f);
    [SerializeField] private float sensMultiplier = 0.2f;
    [SerializeField] private float maxAngle = 90f;

    private Rigidbody rb = null;
    private Controls controls = null;

    private Vector2 moveInput = Vector2.zero, mouseInput = Vector2.zero;

    private Vector3 originalScale = Vector3.zero;

    private float xRotation = 0f, currentSlope = 0f, timeSinceLastSlide = Mathf.Infinity;
    private bool jumping, crouching, sliding, sprinting;

    public float curSpeed {
        get {
            if (!enableSprint) return moveSpeed * moveMultiplier;
            return moveSpeed * (sprinting ? sprintMultiplier : moveMultiplier);
        }
    }

    public float maxSpeed {
        get {
            if (!enableSprint) return (!grounded || jumping) ? inAirMaxSpeed : (crouching && grounded) ? crouchMaxSpeed : groundMaxSpeed;
            return (!grounded || jumping) ? inAirMaxSpeed : (grounded && !crouching && sprinting) ? groundMaxSpeed + sprintMaxSpeedModifier : (crouching && grounded) ? crouchMaxSpeed : groundMaxSpeed;
        }
    }

    public bool canSlide => rb.velocity.magnitude > slideSpeedThreshold && grounded && crouching && !sliding && timeSinceLastSlide >= slideCooldown && currentSlope < maxSlope;

    public bool grounded { get; private set; }

    private void Awake() {
        rb = GetComponent<Rigidbody>();
        controls = new Controls();

        originalScale = transform.localScale;
        Cursor.lockState = CursorLockMode.Locked;

        AssignControls();
    }

    private void AssignControls() {
        controls.Gameplay.Movement.performed += ctx => moveInput = ctx.ReadValue<Vector2>();
        controls.Gameplay.Movement.canceled += ctx => moveInput = Vector2.zero;

        controls.Gameplay.Mouse.performed += ctx => mouseInput = controls.Gameplay.Mouse.ReadValue<Vector2>() * sensitivity * Time.fixedDeltaTime * sensMultiplier;
        controls.Gameplay.Mouse.canceled += ctx => mouseInput = Vector2.zero;

        controls.Gameplay.Jump.performed += ctx => {
            if (enableJump) {
                jumping = true;
                OnJump();
            }
        };
        controls.Gameplay.Jump.canceled += ctx => jumping = false;

        controls.Gameplay.Crouch.performed += ctx => ToggleCrouch(true);
        controls.Gameplay.Crouch.canceled += ctx => ToggleCrouch(false);

        controls.Gameplay.Sprint.performed += ctx => sprinting = true;
        controls.Gameplay.Sprint.canceled += ctx => sprinting = false;
    }

    private void FixedUpdate() => UpdateMovement();

    private void Update() {
        if (enableSlide && timeSinceLastSlide < slideCooldown) timeSinceLastSlide += Time.deltaTime;
        UpdateMouseLook();
    }

    private void UpdateMouseLook() {
        if (!enableMouseLook) return;

        var rot = playerCamera.localRotation.eulerAngles;
        float xTo = rot.y + mouseInput.x;

        xRotation -= mouseInput.y;
        xRotation = Mathf.Clamp(xRotation, -maxAngle, maxAngle);

        playerCamera.localRotation = Quaternion.Euler(xRotation, xTo, 0f);
        orientation.localRotation = Quaternion.Euler(0f, xTo, 0f);
    }

    private void UpdateMovement() {
        if (!hasControl) return;

        Vector3 dir = orientation.right * moveInput.x + orientation.forward * moveInput.y;
        rb.AddForce(Vector3.down * Time.fixedDeltaTime * 10f);

        if (enableJump && autoJump && jumping && grounded) OnJump();

        if (crouching && grounded && currentSlope >= maxSlope) {
            rb.AddForce(Vector3.down * Time.fixedDeltaTime * 5000f);
            return;
        }

        float multiplier = grounded && crouching ? crouchMoveMultiplier : 1f;

        if (rb.velocity.magnitude > maxSpeed) dir = Vector3.zero;
        rb.AddForce(GetMovementVector(-rb.velocity, dir.normalized, curSpeed * Time.fixedDeltaTime) * ((grounded && !jumping) ? multiplier : inAirMovementModifier));
    }

    private Vector3 GetMovementVector(Vector3 velocity, Vector3 dir, float speed) {
        if (!grounded && velocity.magnitude != 0 && enableInAirDrag || velocity.magnitude != 0 && enableInAirDrag && jumping) {
            float drop = inAirDrag * Time.fixedDeltaTime;
            velocity *= drop != 0f ? drop : 1f;

            return new Vector3(velocity.x, 0f, velocity.z) + dir * speed;
        }

        if (grounded && velocity.magnitude != 0f && crouching && currentSlope >= maxSlope || grounded && sliding && timeSinceLastSlide < slideCooldown) {
            velocity *= slideFriction * Time.fixedDeltaTime;
            return velocity + dir * speed;
        }

        if (grounded && velocity.magnitude != 0f) velocity *= friction * Time.fixedDeltaTime;
        return velocity + dir * speed;
    }

    private void OnJump() {
        if (!enableJump) return;

        if (grounded) {
            //If crouching and not sliding: crouch jump multiplier, if sliding: slide jump multiplier, and if all else is false: normal jump multiplier.
            float slideJumpMultiplier = rb.velocity.y < 0 ? rb.velocity.magnitude * 0.1f + jumpMultiplier : crouchJumpMultiplier; //scales to speed
            float currentMultiplier = crouching && currentSlope < maxSlope ? crouchJumpMultiplier : crouching && currentSlope >= maxSlope ? slideJumpMultiplier : jumpMultiplier;
            rb.AddForce(Vector2.up * jumpForce * currentMultiplier, ForceMode.Impulse);
            grounded = false;
        }
    }

    private void ToggleCrouch(bool crouched) {
        if (!enableCrouch) return;

        if (crouched) {
            crouching = true;
            transform.localScale = crouchScale;
            transform.position = new Vector3(transform.position.x, transform.position.y - 0.5f, transform.position.z);

            if (canSlide) Slide();
        }
        else {
            crouching = false;
            sliding = false;
            transform.localScale = originalScale;
            transform.position = new Vector3(transform.position.x, transform.position.y + 0.5f, transform.position.z);
        }
    }

    private void OnCollisionStay(Collision other) {
        if (((1 << other.gameObject.layer) & groundLayer) != 0) {
            for (int i = 0; i < other.contactCount; i++) {
                if (Mathf.Round(other.GetContact(i).normal.y) == 1.0f) {
                    grounded = true;
                    break;
                }
            }

            Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, slopeRaycastDistance, groundLayer);
            currentSlope = Vector3.Angle(Vector3.up, hit.normal);
        }
    }
    private void OnCollisionExit(Collision other) => grounded = false;

    private void Slide() {
        if (!enableSlide) return;

        rb.AddForce(orientation.forward * slideForce, ForceMode.Impulse);

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

    private void OnCollisionEnter(Collision other) {
        if (canSlide) Slide();
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();
}
