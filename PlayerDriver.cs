using UnityEngine;
using FishNet.Object;
using FishNet.Object.Prediction;
using FishNet.Transporting;
using ECM2;

public class PlayerDriver : NetworkBehaviour
{
    #region STRUCTS

    /// <summary>
    /// Input movement data.
    /// </summary>

    private struct MoveData : IReplicateData
    {
        public readonly float horizontal;
        public readonly float vertical;
        public readonly bool jump;
        public readonly bool sprint;
        public readonly bool crouch;
        public readonly Vector2 look;

        private uint _tick;

        public MoveData(float horizontal, float vertical, bool jump, bool sprint, bool crouch, Vector2 look)
        {
            this.horizontal = horizontal;
            this.vertical = vertical;
            this.jump = jump;
            this.sprint = sprint;
            this.crouch = crouch;
            this.look = look;
            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    /// <summary>
    /// Reconciliation data.
    /// </summary>

    private struct ReconcileData : IReconcileData
    {
        public readonly Vector3 position;
        public readonly Quaternion rotation;

        public readonly Vector3 velocity;

        public readonly bool constrainedToGround;
        public readonly float unconstrainedTime;

        public readonly bool hitGround;
        public readonly bool isWalkable;

        public readonly Vector2 lookRotation;
        public readonly float rootHeight;

        public readonly int jumpCount;

        public readonly Vector3 tempGravity;

        private uint _tick;

        public ReconcileData(Vector3 position, Quaternion rotation, Vector3 velocity, bool constrainedToGround,
            float unconstrainedTime, bool hitGround, bool isWalkable, Vector2 lookRotation, float rootHeight, int jumpCount, Vector3 tempGrav)
        {
            this.position = position;
            this.rotation = rotation;

            this.velocity = velocity;

            this.constrainedToGround = constrainedToGround;
            this.unconstrainedTime = unconstrainedTime;

            this.hitGround = hitGround;
            this.isWalkable = isWalkable;

            this.lookRotation = lookRotation;
            this.rootHeight = rootHeight;

            this.jumpCount = jumpCount;

            this.tempGravity = tempGrav;

            _tick = 0;
        }

        public void Dispose() { }
        public uint GetTick() => _tick;
        public void SetTick(uint value) => _tick = value;
    }

    #endregion

    #region FIELDS

    private bool _jump = false;
    private bool _sprint = false;
    private bool _crouch = false;
    private Vector2 _look;

    #endregion

    #region PROPERTIES

    private CharacterMovement characterMovement { get; set; }
    private PlayerPawn playerPawn { get; set; }
    private PlayerModel playerModel { get; set; }

    #endregion

    #region METHODS

    private MoveData BuildMoveData()
    {
        if (!IsOwner)
            return default;

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        // Capture an Input Frame
        MoveData moveData = new MoveData(horizontal, vertical, _jump, _sprint, _crouch, _look);

        // Reset "containers"
        _jump = false;
        _sprint = false;
        _crouch = false;
        _look = Vector2.zero;

        return moveData;
    }

    [Replicate]
    private void Simulate(MoveData md, ReplicateState state = ReplicateState.Invalid, Channel channel = Channel.Unreliable)
    {
        // Handle Look first, since movement direction is going to need to be calculated later
        if (!state.IsReplayed()) playerPawn.Look(md.look, IsOwner);
        // Jump
        if (md.jump) playerPawn.Jump(state.IsReplayed());
        // Handle Jumping (check for reset)
        playerPawn.Jumping();
        // Sprint
        playerPawn.SetSprint(md.sprint);
        //Crouch
        if (!IsOwner && state == ReplicateState.ReplayedCreated) Debug.Log($"State:{state}, Crouch {md.crouch}");
        if (IsOwner || IsServerStarted || (!IsOwner && state == ReplicateState.ReplayedCreated)) playerPawn.SetCrouch(md.crouch);
        // Handle crouching (camera height)
        playerPawn.Crouching();
        // Movement
        playerPawn.Movement(md.horizontal, md.vertical, (float)TimeManager.TickDelta);

        // Run events (like JustLanded)
        playerPawn.HandleEvents(IsServerStarted, state.IsReplayed());
    }

    [Reconcile]
    private void Reconciliation(ReconcileData rd, Channel channel = Channel.Unreliable)
    {
        // Set character motor values
        characterMovement.SetState(
            rd.position,
            rd.rotation,
            rd.velocity,
            rd.constrainedToGround,
            rd.unconstrainedTime,
            rd.hitGround,
            rd.isWalkable);

        // Correct the jump count
        playerPawn.SetJumpCount(rd.jumpCount);
        // Correct the gravity
        playerPawn.SetGravity(rd.tempGravity);

        // Reconcile other player's look rotations to prevent desyncing
        if (!IsOwner) playerPawn.SetLookRotation(rd.lookRotation);
        // Update root heights to show spectated players crouching
        if (!IsOwner) playerPawn.SetRootHeight(rd.rootHeight);
    }

    public override void CreateReconcile()
    {
        // Call Base method
        base.CreateReconcile();
        // Create reconcile struct
        ReconcileData reconcileData = new ReconcileData(
                characterMovement.position,
                characterMovement.rotation,
                characterMovement.velocity,
                characterMovement.constrainToGround,
                characterMovement.unconstrainedTimer,
                characterMovement.currentGround.hitGround,
                characterMovement.currentGround.isWalkable,
                playerPawn.GetLookRotation(),
                playerPawn.GetRootHeight(),
                playerPawn.CurrentJumpCount(),
                playerPawn.tempGravity
            );
        // Call reconcile
        Reconciliation(reconcileData);
    }

    private void OnTick()
    {
        Simulate(BuildMoveData());

        if (IsServerStarted)
        {
            CreateReconcile();
        }
    }

    public override void OnStartNetwork()
    {
        TimeManager.OnTick += OnTick;
    }

    public override void OnStopNetwork()
    {
        if (TimeManager != null)
            TimeManager.OnTick -= OnTick;
    }

    #endregion

    #region MONOBEHAVIOUR

    private void Awake()
    {
        // Grab our associated components
        playerPawn = GetComponent<PlayerPawn>();
        playerModel = GetComponent<PlayerModel>();
        characterMovement = GetComponent<CharacterMovement>();
    }

    private void Update()
    {
        if (!IsOwner)
            return;

        // Check all movement inputs 
        HandleMovementInput();

        // Check all look inputs
        HandleLookInput();
    }

    #endregion

    #region MOVEMENT 

    private void HandleMovementInput()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            _jump = true;

        if (Input.GetKey(KeyCode.LeftShift))
            _sprint = true;

        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C))
            _crouch = true;
    }

    #endregion

    #region CAMERA LOOK INPUT

    [Header("Camera Look Input")]
    public bool lockCursor = false;
    public bool invertLook = false;
    public float maxPitch = 80.0f;
    public float minPitch = -80.0f;
    public Vector2 lookSensitivity = new Vector2(1.5f, 1.25f);

    private void HandleLookInput()
    {
        // Handle cursor locking
        if (lockCursor)
        {
            // If the escape key is pressed
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                    Cursor.lockState = CursorLockMode.None;
                else
                    Cursor.lockState = CursorLockMode.Locked;
            }
        }

        Vector2 frameDelta = new Vector2
        {
            x = Input.GetAxisRaw("Mouse X"),
            y = Input.GetAxisRaw("Mouse Y")
        };

        // Apply look sensitivity
        frameDelta.x *= lookSensitivity.x;
        frameDelta.y *= lookSensitivity.y;

        // Apply Invert look
        if (!invertLook) frameDelta.y = -frameDelta.y;

        // Add current delta to the _look delta (which will get sent to server)
        _look += frameDelta;
        // Update the local look
        playerModel.UpdateLocalLook(frameDelta, minPitch, maxPitch);
    }

    #endregion
}
