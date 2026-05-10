using System.Collections.Generic;
using System;
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

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    private const int GroundHitBufferSize = 8;
    private const int WallHitBufferSize = 4;
    private static readonly List<PlayerController2D> ActivePlayers = new List<PlayerController2D>();

    [Header("Run")]
    [SerializeField] private float runSpeed = 8.5f;
    [SerializeField] private float groundAcceleration = 95f;
    [SerializeField] private float groundDeceleration = 110f;
    [SerializeField] private float airAcceleration = 80f;
    [SerializeField] private float airDeceleration = 65f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 13.5f;
    [SerializeField] private int maxAirJumps = 1;
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float jumpCutMultiplier = 0.45f;
    [SerializeField] private float jumpGroundLockout = 0.08f;
    [SerializeField] private float jumpLiftOffset = 0.025f;

    [Header("Wall Jump")]
    [SerializeField] private Vector2 wallCheckSize = new Vector2(0.12f, 1.05f);
    [SerializeField] private float wallCheckDistance = 0.12f;
    [SerializeField] private float wallJumpHorizontalVelocity = 7.2f;
    [SerializeField] private float wallJumpVerticalVelocity = 12.8f;
    [SerializeField] private float wallJumpPushOff = 0.08f;
    [SerializeField] private float wallSlideMaxFallSpeed = 4.5f;
    [SerializeField] private float wallJumpLockTime = 0.12f;

    [Header("Gravity")]
    [SerializeField] private float upwardGravityScale = 3.2f;
    [SerializeField] private float fallGravityScale = 4.8f;
    [SerializeField] private float maxFallSpeed = 22f;

    [Header("Ground Check")]
    [SerializeField] private Transform groundCheck;
    [SerializeField] private Vector2 groundCheckSize = new Vector2(0.72f, 0.12f);
    [SerializeField] private float groundCheckDistance = 0.05f;
    [SerializeField] private LayerMask groundMask = ~0;

    [Header("Physics")]
    [SerializeField] private bool useRoundedCollider = true;
    [SerializeField] private PhysicsMaterial2D noFrictionMaterial;

    [Header("Color State")]
    [SerializeField] private PlayerColorState currentColor = PlayerColorState.White;
    [SerializeField] private Color whiteStateColor = Color.white;
    [SerializeField] private Color blackStateColor = Color.black;
    [SerializeField] private bool invertVisualColor = true;
    [SerializeField] private bool updateCameraBackground = false;
    [SerializeField] private Color whiteGroundBackground = Color.black;
    [SerializeField] private Color blackGroundBackground = Color.white;
    [SerializeField] private Transform backgroundColorSamplePoint;
    [SerializeField] private bool invertBackgroundColor;
    [SerializeField] private float backgroundColorSampleInterval = 0.04f;

    [Header("Gray Door")]
    [SerializeField] private float grayDoorExitGraceTime = 0.35f;

    [Header("Respawn")]
    [SerializeField] private Transform spawnPoint;

    private Rigidbody2D body;
    private Collider2D mainCollider;
    private SpriteRenderer spriteRenderer;
    private Camera mainCamera;
    private Collider2D[] playerColliders;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[GroundHitBufferSize];
    private readonly RaycastHit2D[] wallHits = new RaycastHit2D[WallHitBufferSize];
    private Vector2 spawnPosition;
    private PlayerColorState spawnColor;
    private bool isGrounded;
    private bool isOnWall;
    private bool isWallSliding;
    private int wallSide;
    private int airJumpsRemaining;
    private bool controlEnabled = true;
    private int grayDoorTouchCount;
    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float groundLockoutCounter;
    private float grayDoorExitGraceCounter;
    private float wallJumpLockCounter;
    private bool jumpHeld;
    private bool jumpCutQueued;
    private bool hasLastBackgroundColor;
    private PlatformColorType lastBackgroundColor;
    private float backgroundColorSampleTimer;
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
    public bool ControlsEnabled => controlEnabled;
    public static IReadOnlyList<PlayerController2D> Players => ActivePlayers;
    public event Action<PlayerJumpType, Vector2> Jumped;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        mainCamera = Camera.main;

        spawnPosition = spawnPoint != null ? spawnPoint.position : transform.position;
        spawnColor = currentColor;

        ConfigureBody();
        ConfigureCollider();
        playerColliders = GetComponentsInChildren<Collider2D>();

        body.gravityScale = upwardGravityScale;
        UpdateVisualColor();
    }

    private void OnEnable()
    {
        if (!ActivePlayers.Contains(this))
        {
            ActivePlayers.Add(this);
        }
    }

    private void OnDisable()
    {
        ActivePlayers.Remove(this);
    }

    private void Start()
    {
        ColorPlatform.RefreshAllForPlayer(this);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.R))
        {
            ResetToSpawn();
        }

        if (groundLockoutCounter > 0f)
        {
            groundLockoutCounter -= Time.deltaTime;
            isGrounded = false;
        }
        else
        {
            ApplyColorFromBackground();
            isGrounded = CheckGrounded();
        }

        UpdateWallState();
        UpdateWallTimers();
        UpdateGroundTimers();

        if (!controlEnabled)
        {
            moveInput = 0f;
            jumpHeld = false;
            return;
        }

        moveInput = Input.GetAxisRaw("Horizontal");

        if (Input.GetButtonDown("Jump") || Input.GetKeyDown(KeyCode.Space))
        {
            jumpBufferCounter = jumpBufferTime;
            jumpHeld = true;
            TryJump();
        }

        if (Input.GetButtonUp("Jump") || Input.GetKeyUp(KeyCode.Space))
        {
            jumpHeld = false;
            jumpCutQueued = true;
        }
    }

    private void FixedUpdate()
    {
        ApplyHorizontalMovement();
        TryJump();
        ApplyJumpCut();
        ApplyGravity();
    }

    public void ToggleColor()
    {
        SetColorState(currentColor == PlayerColorState.White
            ? PlayerColorState.Black
            : PlayerColorState.White);
    }

    public PlayerColorState GetColorState()
    {
        return currentColor;
    }

    public void ResetToSpawn()
    {
        body.velocity = Vector2.zero;
        body.gravityScale = upwardGravityScale;
        transform.position = spawnPosition;
        grayDoorTouchCount = 0;
        grayDoorExitGraceCounter = 0f;
        hasLastBackgroundColor = false;
        backgroundColorSampleTimer = 0f;
        wallJumpLockCounter = 0f;
        isOnWall = false;
        isWallSliding = false;
        wallSide = 0;
        airJumpsRemaining = maxAirJumps;
        coyoteCounter = 0f;
        jumpBufferCounter = 0f;
        groundLockoutCounter = 0f;
        jumpCutQueued = false;
        SetColorState(spawnColor);
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

    public void SetColor(PlayerColorState newColor)
    {
        SetColorState(newColor);
    }

    public void SetColorState(PlayerColorState newState)
    {
        if (currentColor == newState)
        {
            return;
        }

        currentColor = newState;
        UpdateVisualColor();
        ColorPlatform.RefreshAllForPlayer(this);
    }

    public void SetControlEnabled(bool enabled)
    {
        controlEnabled = enabled;
        if (!controlEnabled && body != null)
        {
            body.velocity = new Vector2(0f, body.velocity.y);
        }
    }

    public void EnterGrayDoor()
    {
        bool wasOutsideGrayDoor = grayDoorTouchCount <= 0;
        grayDoorTouchCount++;
        grayDoorExitGraceCounter = 0f;
        if (wasOutsideGrayDoor)
        {
            ColorPlatform.RefreshAllForPlayer(this);
        }
    }

    public void StayInGrayDoor()
    {
        if (grayDoorTouchCount <= 0)
        {
            grayDoorTouchCount = 1;
            ColorPlatform.RefreshAllForPlayer(this);
        }
    }

    public void ExitGrayDoor()
    {
        grayDoorTouchCount = Mathf.Max(0, grayDoorTouchCount - 1);
        if (grayDoorTouchCount == 0)
        {
            grayDoorExitGraceCounter = grayDoorExitGraceTime;
            ColorPlatform.RefreshAllForPlayer(this);
        }
    }

    private void UpdateGroundTimers()
    {
        if (isGrounded)
        {
            coyoteCounter = coyoteTime;
            airJumpsRemaining = maxAirJumps;
        }
        else
        {
            coyoteCounter -= Time.deltaTime;
        }

        jumpBufferCounter -= Time.deltaTime;

        if (grayDoorExitGraceCounter > 0f)
        {
            grayDoorExitGraceCounter -= Time.deltaTime;
            if (grayDoorExitGraceCounter <= 0f)
            {
                ColorPlatform.RefreshAllForPlayer(this);
            }
        }
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

        int leftWall = CheckWall(-1);
        int rightWall = CheckWall(1);

        if (leftWall != 0)
        {
            isOnWall = true;
            wallSide = -1;
        }
        else if (rightWall != 0)
        {
            isOnWall = true;
            wallSide = 1;
        }
        else
        {
            isOnWall = false;
            wallSide = 0;
        }

        isWallSliding = isOnWall && body.velocity.y < 0f && Mathf.Abs(moveInput) > 0.1f && Mathf.Sign(moveInput) == wallSide;
    }

    private void ApplyHorizontalMovement()
    {
        float targetSpeed = moveInput * runSpeed;
        float speedDifference = targetSpeed - body.velocity.x;
        bool hasInput = Mathf.Abs(moveInput) > 0.01f;

        float acceleration = isGrounded
            ? (hasInput ? groundAcceleration : groundDeceleration)
            : (hasInput ? airAcceleration : airDeceleration);

        float speedChange = Mathf.Clamp(
            speedDifference,
            -acceleration * Time.fixedDeltaTime,
            acceleration * Time.fixedDeltaTime);

        body.velocity = new Vector2(body.velocity.x + speedChange, body.velocity.y);
    }

    private void TryJump()
    {
        if (jumpBufferCounter <= 0f)
        {
            return;
        }

        if (isOnWall && !isGrounded && wallJumpLockCounter <= 0f)
        {
            float pushDirection = wallSide != 0
                ? -wallSide
                : -Mathf.Sign(moveInput != 0f ? moveInput : body.velocity.x);

            if (Mathf.Approximately(pushDirection, 0f))
            {
                pushDirection = 1f;
            }

            body.velocity = new Vector2(pushDirection * wallJumpHorizontalVelocity, wallJumpVerticalVelocity);
            body.position += Vector2.right * pushDirection * wallJumpPushOff;
            jumpBufferCounter = 0f;
            coyoteCounter = 0f;
            airJumpsRemaining = maxAirJumps;
            groundLockoutCounter = jumpGroundLockout;
            wallJumpLockCounter = wallJumpLockTime;
            jumpCutQueued = false;
            Jumped?.Invoke(PlayerJumpType.Wall, new Vector2(pushDirection, 1f));
            return;
        }

        if (coyoteCounter <= 0f && airJumpsRemaining <= 0)
        {
            return;
        }

        bool usedAirJump = coyoteCounter <= 0f;

        body.velocity = new Vector2(body.velocity.x, jumpVelocity);
        body.position += Vector2.up * jumpLiftOffset;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        if (usedAirJump)
        {
            airJumpsRemaining--;
        }
        groundLockoutCounter = jumpGroundLockout;
        jumpCutQueued = false;
        Jumped?.Invoke(usedAirJump ? PlayerJumpType.Air : PlayerJumpType.Ground, Vector2.up);
    }

    private void ApplyJumpCut()
    {
        if (!jumpCutQueued)
        {
            return;
        }

        if (body.velocity.y > 0f && !jumpHeld)
        {
            body.velocity = new Vector2(body.velocity.x, body.velocity.y * jumpCutMultiplier);
        }

        jumpCutQueued = false;
    }

    private void ApplyGravity()
    {
        body.gravityScale = body.velocity.y > 0f && jumpHeld
            ? upwardGravityScale
            : fallGravityScale;

        if (isWallSliding && body.velocity.y < -wallSlideMaxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -wallSlideMaxFallSpeed);
        }

        if (body.velocity.y < -maxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
        }
    }

    private int CheckWall(int direction)
    {
        Bounds bounds = mainCollider.bounds;
        Vector2 origin = new Vector2(bounds.center.x, bounds.center.y);
        origin.x += direction * (bounds.extents.x - wallCheckDistance * 0.5f);

        int hitCount = Physics2D.BoxCastNonAlloc(
            origin,
            wallCheckSize,
            0f,
            new Vector2(direction, 0f),
            wallHits,
            wallCheckDistance,
            groundMask);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = wallHits[i];
            if (hit.collider == null || hit.collider == mainCollider || hit.collider.isTrigger)
            {
                continue;
            }

            ColorPlatform.TryGetPlatformForCollider(hit.collider, out ColorPlatform platform);
            if (platform == null || platform.CanPlayerCollide(this))
            {
                return direction;
            }
        }

        return 0;
    }

    private void UpdateWallTimers()
    {
        if (wallJumpLockCounter > 0f)
        {
            wallJumpLockCounter -= Time.deltaTime;
        }
    }

    private bool CheckGrounded()
    {
        Bounds bounds = mainCollider.bounds;
        Vector2 origin = groundCheck != null
            ? groundCheck.position
            : new Vector2(bounds.center.x, bounds.min.y);

        int hitCount = Physics2D.BoxCastNonAlloc(
            origin,
            groundCheckSize,
            0f,
            Vector2.down,
            groundHits,
            groundCheckDistance,
            groundMask);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundHits[i];
            if (hit.collider == null || hit.collider == mainCollider || hit.collider.isTrigger)
            {
                continue;
            }

            ColorPlatform.TryGetPlatformForCollider(hit.collider, out ColorPlatform platform);
            if (platform == null || platform.CanPlayerCollide(this))
            {
                return true;
            }
        }

        return false;
    }

    private void ConfigureBody()
    {
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    private void ConfigureCollider()
    {
        if (noFrictionMaterial == null)
        {
            noFrictionMaterial = new PhysicsMaterial2D("RuntimeNoFriction")
            {
                friction = 0f,
                bounciness = 0f
            };
        }

        if (useRoundedCollider && mainCollider is BoxCollider2D boxCollider)
        {
            CapsuleCollider2D capsuleCollider = GetComponent<CapsuleCollider2D>();
            if (capsuleCollider == null)
            {
                capsuleCollider = gameObject.AddComponent<CapsuleCollider2D>();
            }

            capsuleCollider.direction = CapsuleDirection2D.Vertical;
            capsuleCollider.size = boxCollider.size;
            capsuleCollider.offset = boxCollider.offset;
            capsuleCollider.sharedMaterial = noFrictionMaterial;
            capsuleCollider.enabled = true;

            boxCollider.enabled = false;
            mainCollider = capsuleCollider;
        }

        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        foreach (Collider2D playerCollider in colliders)
        {
            if (playerCollider != null && !playerCollider.isTrigger)
            {
                playerCollider.sharedMaterial = noFrictionMaterial;
            }
        }
    }

    private void UpdateVisualColor()
    {
        PlayerColorState visualColor = currentColor;
        if (invertVisualColor)
        {
            visualColor = currentColor == PlayerColorState.White
                ? PlayerColorState.Black
                : PlayerColorState.White;
        }

        currentVisualColor = visualColor == PlayerColorState.White
            ? whiteStateColor
            : blackStateColor;
        spriteRenderer.color = currentVisualColor;

        if (!updateCameraBackground)
        {
            return;
        }

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            return;
        }

        mainCamera.backgroundColor = currentColor == PlayerColorState.White
            ? whiteGroundBackground
            : blackGroundBackground;
    }

    private void ApplyColorFromBackground()
    {
        if (backgroundColorSampleInterval > 0f)
        {
            backgroundColorSampleTimer -= Time.deltaTime;
            if (backgroundColorSampleTimer > 0f)
            {
                return;
            }

            backgroundColorSampleTimer = backgroundColorSampleInterval;
        }

        Vector3 samplePosition = backgroundColorSamplePoint != null
            ? backgroundColorSamplePoint.position
            : mainCollider.bounds.center;

        if (!ColorPlatform.TryGetColorAtWorldPosition(samplePosition, out PlatformColorType backgroundColor))
        {
            hasLastBackgroundColor = false;
            return;
        }

        PlayerColorState targetColor = backgroundColor == PlatformColorType.White
            ? PlayerColorState.White
            : PlayerColorState.Black;

        if (invertBackgroundColor)
        {
            targetColor = targetColor == PlayerColorState.White
                ? PlayerColorState.Black
                : PlayerColorState.White;
        }

        if (hasLastBackgroundColor && lastBackgroundColor == backgroundColor && currentColor == targetColor)
        {
            return;
        }

        hasLastBackgroundColor = true;
        lastBackgroundColor = backgroundColor;

        SetColorState(targetColor);
    }
}
