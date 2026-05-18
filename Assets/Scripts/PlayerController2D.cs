using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerColorState
{
    White,
    Black
}

public enum PlayerJumpType
{
    Ground,
    Air,
    Wall
}

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    private const int GroundHitBufferSize = 8;
    private const int WallHitBufferSize = 6;
    private static readonly List<PlayerController2D> ActivePlayers = new List<PlayerController2D>();

    [Header("Move")]
    [SerializeField] private float runSpeed = 8.5f;
    [SerializeField] private float groundAcceleration = 95f;
    [SerializeField] private float groundDeceleration = 110f;
    [SerializeField] private float airAcceleration = 70f;
    [SerializeField] private float airDeceleration = 55f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 13.5f;
    [SerializeField] private int maxAirJumps = 1;
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float jumpCutMultiplier = 0.45f;

    [Header("Wall")]
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.12f, 0.9f);
    [SerializeField] private float wallCheckDistance = 0.08f;
    [SerializeField] private float wallJumpHorizontalVelocity = 6.4f;
    [SerializeField] private float wallJumpVerticalVelocity = 12.6f;
    [SerializeField] private float wallJumpLockTime = 0.1f;
    [SerializeField] private float wallSlideMaxFallSpeed = 4.5f;

    [Header("Gravity")]
    [SerializeField] private float upwardGravityScale = 3.2f;
    [SerializeField] private float fallGravityScale = 4.8f;
    [SerializeField] private float maxFallSpeed = 22f;

    [Header("Checks")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.72f, 0.12f);
    [SerializeField] private float groundCheckDistance = 0.06f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Physics")]
    [SerializeField] private PhysicsMaterial2D noFrictionMaterial;

    [Header("Color")]
    [SerializeField] private PlayerColorState currentColor = PlayerColorState.Black;
    [SerializeField] private Color whiteStateColor = Color.white;
    [SerializeField] private Color blackStateColor = Color.black;
    [SerializeField] private bool invertVisualColor = true;
    [SerializeField] private Transform backgroundColorSamplePoint;
    [SerializeField] private float backgroundColorSampleInterval = 0.04f;

    [Header("Gray Door")]
    [SerializeField] private float grayDoorExitGraceTime = 0.18f;

    [Header("Respawn")]
    [SerializeField] private Transform spawnPoint;

    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[GroundHitBufferSize];
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[WallHitBufferSize];
    private Rigidbody2D body;
    private Collider2D mainCollider;
    private SpriteRenderer spriteRenderer;
    private Collider2D[] playerColliders;
    private Vector2 spawnPosition;
    private PlayerColorState spawnColor;
    private bool controlsEnabled = true;
    private bool isGrounded;
    private bool isOnWall;
    private bool isWallSliding;
    private int wallSide;
    private int airJumpsRemaining;
    private int grayDoorTouchCount;
    private float grayDoorExitGraceCounter;
    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float wallJumpLockCounter;
    private float colorSampleTimer;
    private bool jumpHeld;
    private bool jumpCutQueued;
    private Color currentVisualColor;

    public PlayerColorState CurrentColor => currentColor;
    public Color VisualColor => currentVisualColor;
    public Collider2D[] PlayerColliders => playerColliders;
    public bool IsTouchingGrayDoor => grayDoorTouchCount > 0;
    public bool IsInGrayDoorGroundTransition => grayDoorTouchCount > 0 || grayDoorExitGraceCounter > 0f;
    public bool IsGrounded => isGrounded;
    public bool IsOnWall => isOnWall;
    public bool IsWallSliding => isWallSliding;
    public float MoveInput => moveInput;
    public Vector2 Velocity => body != null ? body.velocity : Vector2.zero;
    public Vector2 SpawnPosition => spawnPosition;
    public PlayerColorState SpawnColor => spawnColor;
    public bool ControlsEnabled => controlsEnabled;
    public static IReadOnlyList<PlayerController2D> Players => ActivePlayers;
    public event Action<PlayerJumpType, Vector2> Jumped;

    private int ExtraAirJumps => Mathf.Clamp(maxAirJumps, 0, 1);

    private void Awake()
    {
        CacheComponents();
        ConfigurePhysics();

        spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        spawnColor = currentColor;
        airJumpsRemaining = ExtraAirJumps;

        RefreshColorFromBackgroundNow();
        EnsureRuntimeComponents();
    }

    private void OnEnable()
    {
        if (!ActivePlayers.Contains(this))
        {
            ActivePlayers.Add(this);
        }

        if (body != null)
        {
            InitializeRuntimeSystems();
        }
    }

    private void OnDisable()
    {
        ActivePlayers.Remove(this);
    }

    private void Start()
    {
        InitializeRuntimeSystems();
        ColorPlatform.RefreshAllForPlayer(this);
    }

    private void OnValidate()
    {
        maxAirJumps = Mathf.Clamp(maxAirJumps, 0, 1);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToSpawn();
        }

        SampleColorTick();
        UpdateGrounded();
        UpdateWallState();
        UpdateTimers();
        ReadInput();
    }

    private void FixedUpdate()
    {
        ApplyHorizontalMovement();
        TryConsumeJump();
        ApplyJumpCut();
        ApplyGravity();
    }

    public void InitializeRuntimeSystems()
    {
        CacheComponents();
        ConfigurePhysics();
        UpdateVisualColor();
        EnsureRuntimeComponents();
    }

    public void ToggleColor()
    {
        SetColorState(currentColor == PlayerColorState.White ? PlayerColorState.Black : PlayerColorState.White);
    }

    public PlayerColorState GetColorState()
    {
        return currentColor;
    }

    public void SetColor(PlayerColorState newColor)
    {
        SetColorState(newColor);
    }

    public void SetColorState(PlayerColorState newState)
    {
        if (currentColor == newState)
        {
            UpdateVisualColor();
            return;
        }

        currentColor = newState;
        UpdateVisualColor();
        ColorPlatform.RefreshAllForPlayer(this);
    }

    public void RefreshColorFromBackgroundNow()
    {
        PlatformColorType background = SampleBackgroundColor(transform.position);
        SetColorState(background == PlatformColorType.White ? PlayerColorState.White : PlayerColorState.Black);
        colorSampleTimer = backgroundColorSampleInterval;
    }

    public Color GetVisualColorAtWorldPosition(Vector3 worldPosition)
    {
        return GetVisualColorForBackgroundColor(SampleBackgroundColor(worldPosition));
    }

    public Color GetVisualColorForBackgroundColor(PlatformColorType backgroundColor)
    {
        PlayerColorState logicalColor = backgroundColor == PlatformColorType.White
            ? PlayerColorState.White
            : PlayerColorState.Black;
        return GetVisualColor(logicalColor);
    }

    public void SetControlEnabled(bool enabled)
    {
        controlsEnabled = enabled;
        if (!enabled)
        {
            moveInput = 0f;
            jumpHeld = false;
            if (body != null)
            {
                body.velocity = new Vector2(0f, body.velocity.y);
            }
        }
    }

    public void ResetToSpawn()
    {
        CacheComponents();
        transform.position = spawnPosition;
        body.velocity = Vector2.zero;
        grayDoorTouchCount = 0;
        grayDoorExitGraceCounter = 0f;
        wallJumpLockCounter = 0f;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        airJumpsRemaining = ExtraAirJumps;
        isOnWall = false;
        isWallSliding = false;
        SetColorState(spawnColor);
        RefreshColorFromBackgroundNow();
    }

    public void RespawnToSpawnWithParticles()
    {
        PlayerDeathRespawnEffect deathEffect = GetComponent<PlayerDeathRespawnEffect>();
        if (deathEffect == null)
        {
            deathEffect = gameObject.AddComponent<PlayerDeathRespawnEffect>();
        }

        deathEffect.enabled = true;
        deathEffect.Warmup();
        if (!deathEffect.IsRespawning)
        {
            deathEffect.Play();
        }
    }

    public void SetSpawnPosition(Vector2 newSpawnPosition)
    {
        spawnPosition = newSpawnPosition;
    }

    public void SetSpawnPoint(Transform newSpawnPoint)
    {
        spawnPoint = newSpawnPoint;
        if (spawnPoint != null)
        {
            spawnPosition = spawnPoint.position;
        }
    }

    public void SetSpawn(Vector2 newSpawnPosition, PlayerColorState newSpawnColor)
    {
        spawnPosition = newSpawnPosition;
        spawnColor = newSpawnColor;
    }

    public void SetRespawnPoint(RespawnPoint respawnPoint)
    {
        if (respawnPoint != null)
        {
            SetSpawn(respawnPoint.transform.position, respawnPoint.RespawnColor);
        }
    }

    public void EnterGrayDoor()
    {
        grayDoorTouchCount++;
        grayDoorExitGraceCounter = 0f;
        ColorPlatform.RefreshAllForPlayer(this);
    }

    public void StayInGrayDoor()
    {
        if (grayDoorTouchCount <= 0)
        {
            grayDoorTouchCount = 1;
        }
        ColorPlatform.RefreshAllForPlayer(this);
    }

    public void ExitGrayDoor()
    {
        grayDoorTouchCount = Mathf.Max(0, grayDoorTouchCount - 1);
        if (grayDoorTouchCount == 0)
        {
            grayDoorExitGraceCounter = grayDoorExitGraceTime;
        }
        ColorPlatform.RefreshAllForPlayer(this);
    }

    private void ReadInput()
    {
        if (!controlsEnabled)
        {
            moveInput = 0f;
            return;
        }

        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
            jumpHeld = true;
        }

        if (Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.Space))
        {
            jumpHeld = false;
            jumpCutQueued = true;
        }
    }

    private void ApplyHorizontalMovement()
    {
        if (body == null)
        {
            return;
        }

        float targetSpeed = moveInput * runSpeed;
        float difference = targetSpeed - body.velocity.x;
        bool hasInput = Mathf.Abs(moveInput) > 0.01f;
        float acceleration = isGrounded
            ? (hasInput ? groundAcceleration : groundDeceleration)
            : (hasInput ? airAcceleration : airDeceleration);
        float change = Mathf.Clamp(difference, -acceleration * Time.fixedDeltaTime, acceleration * Time.fixedDeltaTime);
        body.velocity = new Vector2(body.velocity.x + change, body.velocity.y);
    }

    private void TryConsumeJump()
    {
        if (jumpBufferCounter <= 0f || !controlsEnabled)
        {
            return;
        }

        if (isOnWall && !isGrounded && wallJumpLockCounter <= 0f)
        {
            float direction = wallSide != 0 ? -wallSide : -Mathf.Sign(moveInput == 0f ? 1f : moveInput);
            body.velocity = new Vector2(direction * wallJumpHorizontalVelocity, wallJumpVerticalVelocity);
            wallJumpLockCounter = wallJumpLockTime;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            airJumpsRemaining = ExtraAirJumps;
            Jumped?.Invoke(PlayerJumpType.Wall, new Vector2(direction, 1f));
            return;
        }

        bool canGroundJump = coyoteCounter > 0f;
        bool canAirJump = !canGroundJump && airJumpsRemaining > 0;
        if (!canGroundJump && !canAirJump)
        {
            return;
        }

        body.velocity = new Vector2(body.velocity.x, jumpVelocity);
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        if (canAirJump)
        {
            airJumpsRemaining--;
        }

        Jumped?.Invoke(canAirJump ? PlayerJumpType.Air : PlayerJumpType.Ground, Vector2.up);
    }

    private void ApplyJumpCut()
    {
        if (!jumpCutQueued)
        {
            return;
        }

        if (!jumpHeld && body.velocity.y > 0f)
        {
            body.velocity = new Vector2(body.velocity.x, body.velocity.y * jumpCutMultiplier);
        }

        jumpCutQueued = false;
    }

    private void ApplyGravity()
    {
        if (body == null)
        {
            return;
        }

        body.gravityScale = body.velocity.y > 0f && jumpHeld ? upwardGravityScale : fallGravityScale;

        if (isWallSliding && body.velocity.y < -wallSlideMaxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -wallSlideMaxFallSpeed);
        }

        if (body.velocity.y < -maxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
        }
    }

    private void UpdateTimers()
    {
        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsRemaining = ExtraAirJumps;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        jumpBufferCounter -= Time.deltaTime;
        wallJumpLockCounter -= Time.deltaTime;

        if (grayDoorExitGraceCounter > 0f)
        {
            grayDoorExitGraceCounter -= Time.deltaTime;
            if (grayDoorExitGraceCounter <= 0f)
            {
                ColorPlatform.RefreshAllForPlayer(this);
            }
        }
    }

    private void UpdateGrounded()
    {
        isGrounded = CheckGrounded();
    }

    private bool CheckGrounded()
    {
        if (mainCollider == null)
        {
            return false;
        }

        Vector2 origin = groundCheck != null
            ? groundCheck.position
            : new Vector2(mainCollider.bounds.center.x, mainCollider.bounds.min.y);

        int count = Physics2D.BoxCastNonAlloc(origin, groundCheckSize, 0f, Vector2.down, groundHits, groundCheckDistance, groundMask);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (!IsValidCollisionHit(hit))
            {
                continue;
            }

            if (hit.normal.y >= 0.45f)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateWallState()
    {
        if (isGrounded || wallJumpLockCounter > 0f)
        {
            isOnWall = false;
            isWallSliding = false;
            wallSide = 0;
            return;
        }

        int left = CheckWall(-1);
        int right = CheckWall(1);
        wallSide = left != 0 ? -1 : (right != 0 ? 1 : 0);
        isOnWall = wallSide != 0;
        isWallSliding = isOnWall && body.velocity.y < 0f && Mathf.Sign(moveInput) == wallSide;
    }

    private int CheckWall(int direction)
    {
        Bounds bounds = mainCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x + direction * bounds.extents.x, bounds.center.y);
        int count = Physics2D.BoxCastNonAlloc(origin, wallCheckSize, 0f, Vector2.right * direction, wallHits, wallCheckDistance, groundMask);
        for (int i = 0; i < count; i++)
        {
            RaycastHit2D hit = wallHits[i];
            if (!IsValidCollisionHit(hit))
            {
                continue;
            }

            if (Mathf.Abs(hit.normal.x) >= 0.45f)
            {
                return direction;
            }
        }

        return 0;
    }

    private bool IsValidCollisionHit(RaycastHit2D hit)
    {
        if (hit.collider == null || hit.collider == mainCollider || hit.collider.isTrigger)
        {
            return false;
        }

        ColorPlatform.TryGetPlatformForCollider(hit.collider, out ColorPlatform platform);
        return platform == null || platform.CanPlayerCollide(this);
    }

    private void SampleColorTick()
    {
        colorSampleTimer -= Time.deltaTime;
        if (colorSampleTimer > 0f)
        {
            return;
        }

        colorSampleTimer = Mathf.Max(0.01f, backgroundColorSampleInterval);
        RefreshColorFromBackgroundNow();
    }

    private PlatformColorType SampleBackgroundColor(Vector3 fallbackPosition)
    {
        Vector3 samplePosition = backgroundColorSamplePoint != null
            ? backgroundColorSamplePoint.position
            : (mainCollider != null ? mainCollider.bounds.center : fallbackPosition);

        return ColorPlatform.TryGetColorAtWorldPosition(samplePosition, out PlatformColorType color)
            ? color
            : PlatformColorType.Black;
    }

    private void CacheComponents()
    {
        body = GetComponent<Rigidbody2D>();
        mainCollider = GetPrimaryCollider();
        spriteRenderer = GetComponent<SpriteRenderer>();
        playerColliders = GetComponentsInChildren<Collider2D>(true);
    }

    private Collider2D GetPrimaryCollider()
    {
        Collider2D[] colliders = GetComponents<Collider2D>();
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && colliders[i].enabled && !colliders[i].isTrigger)
            {
                return colliders[i];
            }
        }

        return GetComponent<Collider2D>();
    }

    private void ConfigurePhysics()
    {
        if (body != null)
        {
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.gravityScale = upwardGravityScale;
        }

        if (noFrictionMaterial == null)
        {
            noFrictionMaterial = new PhysicsMaterial2D("RuntimeNoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null && !colliders[i].isTrigger)
            {
                colliders[i].sharedMaterial = noFrictionMaterial;
            }
        }
    }

    private void EnsureRuntimeComponents()
    {
        PlayerMovementEffects movementEffects = GetComponent<PlayerMovementEffects>();
        if (movementEffects == null)
        {
            movementEffects = gameObject.AddComponent<PlayerMovementEffects>();
        }
        movementEffects.enabled = true;
        movementEffects.InitializeRuntimeEffects();

        PlayerDeathRespawnEffect deathEffect = GetComponent<PlayerDeathRespawnEffect>();
        if (deathEffect == null)
        {
            deathEffect = gameObject.AddComponent<PlayerDeathRespawnEffect>();
        }
        deathEffect.enabled = true;
        deathEffect.Warmup();

        PlayerSpawnAssembleEffect spawnAssembleEffect = GetComponent<PlayerSpawnAssembleEffect>();
        if (spawnAssembleEffect != null)
        {
            spawnAssembleEffect.InitializeRuntimeDefaults();
        }
    }

    private void UpdateVisualColor()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        currentVisualColor = GetVisualColor(currentColor);
        spriteRenderer.color = currentVisualColor;
    }

    private Color GetVisualColor(PlayerColorState logicalColor)
    {
        PlayerColorState visualColor = invertVisualColor
            ? (logicalColor == PlayerColorState.White ? PlayerColorState.Black : PlayerColorState.White)
            : logicalColor;

        return visualColor == PlayerColorState.White ? whiteStateColor : blackStateColor;
    }
}
