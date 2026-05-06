using System.Collections.Generic;
using UnityEngine;

public enum PlayerColorState
{
    White,
    Black
}

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class PlayerController2D : MonoBehaviour
{
    private const int GroundHitBufferSize = 8;
    private static readonly List<PlayerController2D> ActivePlayers = new List<PlayerController2D>();

    [Header("Run")]
    [SerializeField] private float runSpeed = 8.5f;
    [SerializeField] private float groundAcceleration = 95f;
    [SerializeField] private float groundDeceleration = 110f;
    [SerializeField] private float airAcceleration = 80f;
    [SerializeField] private float airDeceleration = 65f;

    [Header("Jump")]
    [SerializeField] private float jumpVelocity = 13.5f;
    [SerializeField] private float coyoteTime = 0.08f;
    [SerializeField] private float jumpBufferTime = 0.1f;
    [SerializeField] private float jumpCutMultiplier = 0.45f;
    [SerializeField] private float jumpGroundLockout = 0.08f;
    [SerializeField] private float jumpLiftOffset = 0.025f;

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
    [SerializeField] private bool updateCameraBackground = true;
    [SerializeField] private Color whiteGroundBackground = Color.black;
    [SerializeField] private Color blackGroundBackground = Color.white;
    [SerializeField] private Transform backgroundColorSamplePoint;
    [SerializeField] private bool invertBackgroundColor = true;

    [Header("Gray Door")]
    [SerializeField] private float grayDoorExitGraceTime = 0.35f;

    [Header("Respawn")]
    [SerializeField] private Transform spawnPoint;

    private Rigidbody2D body;
    private Collider2D mainCollider;
    private SpriteRenderer spriteRenderer;
    private Collider2D[] playerColliders;
    private readonly RaycastHit2D[] groundHits = new RaycastHit2D[GroundHitBufferSize];
    private Vector2 spawnPosition;
    private PlayerColorState spawnColor;
    private bool isGrounded;
    private bool controlEnabled = true;
    private int grayDoorTouchCount;
    private float moveInput;
    private float coyoteCounter;
    private float jumpBufferCounter;
    private float groundLockoutCounter;
    private float grayDoorExitGraceCounter;
    private bool jumpHeld;
    private bool jumpCutQueued;

    public PlayerColorState CurrentColor => currentColor;
    public Collider2D[] PlayerColliders => playerColliders;
    public bool IsTouchingGrayDoor => grayDoorTouchCount > 0;
    public bool IsInGrayDoorGroundTransition => grayDoorTouchCount > 0 || grayDoorExitGraceCounter > 0f;
    public bool IsGrounded => isGrounded;
    public float MoveInput => moveInput;
    public Vector2 Velocity => body != null ? body.velocity : Vector2.zero;
    public bool ControlsEnabled => controlEnabled;
    public static IReadOnlyList<PlayerController2D> Players => ActivePlayers;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        mainCollider = GetComponent<Collider2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

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
            UpdateVisualColor();
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
        if (jumpBufferCounter <= 0f || coyoteCounter <= 0f)
        {
            return;
        }

        body.velocity = new Vector2(body.velocity.x, jumpVelocity);
        body.position += Vector2.up * jumpLiftOffset;
        jumpBufferCounter = 0f;
        coyoteCounter = 0f;
        groundLockoutCounter = jumpGroundLockout;
        jumpCutQueued = false;
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

        if (body.velocity.y < -maxFallSpeed)
        {
            body.velocity = new Vector2(body.velocity.x, -maxFallSpeed);
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

            ColorPlatform platform = hit.collider.GetComponentInParent<ColorPlatform>();
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
        spriteRenderer.color = currentColor == PlayerColorState.White
            ? whiteStateColor
            : blackStateColor;

        if (!updateCameraBackground || Camera.main == null)
        {
            return;
        }

        Camera.main.backgroundColor = currentColor == PlayerColorState.White
            ? whiteGroundBackground
            : blackGroundBackground;
    }

    private void ApplyColorFromBackground()
    {
        Vector3 samplePosition = backgroundColorSamplePoint != null
            ? backgroundColorSamplePoint.position
            : mainCollider.bounds.center;

        if (!ColorPlatform.TryGetColorAtWorldPosition(samplePosition, out PlatformColorType backgroundColor))
        {
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

        SetColorState(targetColor);
    }
}
