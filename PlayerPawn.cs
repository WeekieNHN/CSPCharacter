using UnityEngine;
using ECM2;
using System;
using UnityEngine.Events;

public class PlayerPawn : MonoBehaviour
{
    #region EDITOR EXPOSED FIELDS

    [Header("Motor")]
    public float maxSpeedWalking = 5.0f;
    public float acceleration = 20.0f;
    public float deceleration = 20.0f;
    public float groundFriction = 8.0f;
    public float airFriction = 0.5f;
    [Range(0.0f, 1.0f)] public float airControl = 0.3f;
    public Vector3 gravity = Vector3.down * 9.81f;

    #endregion

    #region FIELDS

    private bool sprintButtonPressed = false;
    private bool crouchButtonPressed = false;

    private PlayerDriver playerDriver;

    private float MaxSpeed
    {
        get
        {
            if (IsCrouched()) return maxSpeedCrouched;
            if (IsSprinting()) return maxSpeedSprinting;
            else return maxSpeedWalking;
        }

        set { }
    }

    #endregion

    #region PROPERTIES

    private CharacterMovement characterMovement { get; set; }

    #endregion

    #region MONOBEHAVIOUR

    private void Awake()
    {
        // Grab our associated components
        characterMovement = GetComponent<CharacterMovement>();
        playerDriver = GetComponent<PlayerDriver>();

        // Subscribe to our events
        SubscribeToEvents();
    }

    private void LateUpdate()
    {
        // Smooth out our into/out of crouch camera
        SmoothCrouching();
    }

    #endregion

    #region EVENT DECLARATIONS

    public UnityEvent<Vector3, bool, bool> Landed = new UnityEvent<Vector3, bool, bool>();

    #endregion

    #region EVENTS

    private void SubscribeToEvents()
    {
        // Subscribe to our movement events
        Landed.AddListener(OnLanded);
    }

    Vector3 prevLandedVelocity;
    private void OnLanded(Vector3 velocity, bool isServer, bool isReplayed)
    {
        // Use isServer to calculate fall Damage
        // Use isReplayed to filter out inputs which shouldn't play sounds

        // Check if we've landed again
        if (isServer && velocity != prevLandedVelocity && characterMovement.isOnGround) Debug.Log($"Player landed with velocity of {velocity} against normal {characterMovement.groundNormal}, process fall damage here");
        // Consume the vector
        prevLandedVelocity = velocity;
    }

    public void HandleEvents(bool isServer, bool IsReplayed)
    {
        // Check if we've just landed
        if (characterMovement.landedVelocity != Vector3.zero) Landed.Invoke(characterMovement.landedVelocity, isServer, IsReplayed);
    }

    #endregion

    #region CAMERA LOOK

    [Header("Camera Look")]
    public Transform rootPivot;
    public Transform eyePivot;
    public GameObject FirstPersonCamera;
    public float maxPitch = 80.0f;
    public float minPitch = -80.0f;

    private float _cameraYaw = 0.0f;
    private float _cameraPitch = 0.0f;

    public Vector2 modelLookTarget;

    public void Look(Vector2 look, bool isOwner)
    {
        // Add our look vector to the camera
        AddYawInput(look.x);
        AddPitchInput(look.y, minPitch, maxPitch);
        // Consume the rotation
        rootPivot.localRotation = Quaternion.Euler(0.0f, _cameraYaw, 0.0f);
        eyePivot.localRotation = Quaternion.Euler(_cameraPitch, 0.0f, 0.0f);
        // Save target rotation to interpolate to
        modelLookTarget = new Vector2(_cameraPitch, _cameraYaw);
        // Only enable camera if we own the character, so we don't draw other cameras
        if (FirstPersonCamera.activeInHierarchy != isOwner) FirstPersonCamera.SetActive(isOwner);
    }

    public Vector2 GetLookRotation() => new Vector2(_cameraPitch, _cameraYaw);
    public void SetLookRotation(Vector2 newLookRotation)
    {
        _cameraPitch = newLookRotation.x;
        _cameraYaw = newLookRotation.y;
    }

    private void AddYawInput(float value)
    {
        // Add value to Yaw
        if (value != 0.0f) _cameraYaw += value;
    }

    private void AddPitchInput(float value, float minPitch, float maxPitch)
    {
        // Add pitch, max sure value is clamped
        if (value != 0.0f) _cameraPitch = Mathf.Clamp(_cameraPitch + value, minPitch, maxPitch);
    }

    private Vector3 GetRightVector() => rootPivot.right;
    private Vector3 GetUpVector() => rootPivot.up;
    private Vector3 GetForwardVector() => rootPivot.forward;

    #endregion

    #region BASE MOVEMENT

    [HideInInspector] public Vector3 modelPositionTarget;

    public void Movement(float horizontal, float vertical, float deltaTime)
    {
        Vector3 moveDirection = (GetRightVector() * horizontal) + (GetForwardVector() * vertical);
        moveDirection = Vector3.ClampMagnitude(moveDirection, 1.0f);

        Vector3 desiredVelocity = moveDirection * MaxSpeed;

        float actualAcceleration = characterMovement.isGrounded ? acceleration : acceleration * airControl;
        float actualDeceleration = characterMovement.isGrounded ? deceleration : 0.0f;

        float actualFriction = characterMovement.isGrounded ? groundFriction : airFriction;

        characterMovement.SimpleMove(desiredVelocity, MaxSpeed, actualAcceleration, actualDeceleration,
            actualFriction, actualFriction, CurrentGravity(), true, deltaTime);

        // Save the position of the character, for the player model to target
        modelPositionTarget = transform.position;
    }

    #endregion

    #region CROUCHING 

    [Header("Crouching")]
    public bool CanCrouchEver = true;
    public float UnCrouchedHeight = 1.5f;
    public float CrouchedHeight = 1.0f;
    public float maxSpeedCrouched = 3.0f;

    private Vector3 rootTarget;
    private float _smoothedPositionalTime = 0.75f;
    private float _positionalSmoothingRateMin = 17f;
    private float _positionalSmoothingRateMax = 25f;

    public void SetCrouch(bool crouch)
    {
        crouchButtonPressed = crouch;
    }

    public bool IsCrouched() => crouchButtonPressed && CanCrouchEver;

    public void Crouching()
    {
        // Decide the target height of the character
        float targetHeight = IsCrouched() ? CrouchedHeight : UnCrouchedHeight;
        // Move our root object to this height
        rootTarget = new Vector3(0.0f, targetHeight, 0.0f);
    }

    float _smoothingTime = 0f;

    public void SmoothCrouching()
    {
        // Smooth crouching
        if (rootPivot.localPosition != rootTarget)
        {
            // Calculate how far the camera has movement
            float distance = Mathf.Max(0.1f, Vector3.Distance(rootPivot.localPosition, rootTarget));
            // If distance is greater than teleport threshold
            if (distance > 10f)
            {
                // Teleport the model
                rootPivot.localPosition = rootTarget;
            }
            else
            {
                // Add deltaTime since we're called by framerate not tick rate
                _smoothingTime += Time.deltaTime;
                // Calculate the smoothing rate
                float smoothingPercent = _smoothingTime / _smoothedPositionalTime;
                float smoothingRate = Mathf.Lerp(_positionalSmoothingRateMax, _positionalSmoothingRateMin, smoothingPercent);
                // Move the transform
                rootPivot.localPosition = Vector3.MoveTowards(rootPivot.localPosition, rootTarget, smoothingRate * distance * Time.deltaTime);
            }
        }
        else
        {
            // Reset the moving Time
            _smoothingTime = 0f;
        }
    }

    public float GetRootHeight() => rootPivot.localPosition.y;

    public void SetRootHeight(float height) => rootPivot.localPosition = new Vector3(0.0f, height, 0.0f);

    #endregion

    #region JUMPING

    [Header("Jumping")]
    [SerializeField] private bool CanJumpEver = true;
    [SerializeField] private int maxJumpCount = 2;
    [SerializeField] private float jumpImpulse = 6.5f;
    [SerializeField] private int _currentJumpCount = 0;

    public int CurrentJumpCount() => _currentJumpCount;

    public bool CanJump() => CanJumpEver && _currentJumpCount < maxJumpCount;

    public void Jump(bool IsReplayed)
    {
        // Check if we can jump
        if (!CanJump()) return;
        // If we can jump, launch the character up
        characterMovement.PauseGroundConstraint();

        // Make sure landed velocity gets reset before the jump
        prevLandedVelocity = Vector3.zero;

        // Determine the axis to set the velocity of
        Vector3 axis = -CurrentGravity().normalized;
        // Project the current velocity onto the axis
        Vector3 projectedVelocity = Vector3.Project(characterMovement.velocity, axis);
        // Calculate the current magnitude along the axis
        float currentJumpDirectionMagnitude = Vector3.Dot(projectedVelocity, axis);
        // Determine the new magnitude (ensuring it is at least the jumpImpulse)
        float newJumpDirectionMagnitude = Mathf.Max(currentJumpDirectionMagnitude, jumpImpulse);
        // Update the velocity along the jump axis
        characterMovement.velocity += axis * (newJumpDirectionMagnitude - currentJumpDirectionMagnitude);

        // Increase jump count
        if (!IsReplayed)
            _currentJumpCount++;
    }

    public void Jumping()
    {
        // Check if we can reset the jump count
        if (characterMovement.isGrounded) SetJumpCount(0);
    }

    public void SetJumpCount(int value) => _currentJumpCount = value;

    #endregion

    #region SPRINTING

    [Header("Sprinting")]
    public bool CanSprintEver = true;
    public float maxSpeedSprinting = 8.0f;

    public bool IsSprinting() => sprintButtonPressed && CanSprintEver;

    public void SetSprint(bool sprint)
    {
        sprintButtonPressed = sprint;
    }

    #endregion

    #region GRAVITY / PLANETS 

    public Vector3 tempGravity = Vector3.down * 9.81f;

    public void SetGravity(Vector3 newGrav)
    {
        // Pause the ground constraint
        if (newGrav != tempGravity) characterMovement.PauseGroundConstraint();
        // Save the gravity value
        tempGravity = newGrav;
    }

    public bool IsGravityAltered() => tempGravity != gravity;

    public Vector3 CurrentGravity() => IsGravityAltered() ? tempGravity : gravity;

    #endregion

}
