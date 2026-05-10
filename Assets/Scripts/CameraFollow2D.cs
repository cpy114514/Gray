using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollow2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    [SerializeField] private bool autoFindPlayer = true;

    [Header("Follow")]
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField] private float smoothTime = 0.18f;
    [SerializeField] private float lookAheadDistance = 0f;
    [SerializeField] private float lookAheadSpeed = 3f;

    [Header("Dead Zone")]
    [SerializeField] private Vector2 deadZone = new Vector2(0.05f, 0.05f);

    [Header("Bounds")]
    [SerializeField] private bool useBounds;
    [SerializeField] private Vector2 minBounds = new Vector2(-20f, -10f);
    [SerializeField] private Vector2 maxBounds = new Vector2(40f, 10f);

    private Camera followCamera;
    private Vector3 velocity;
    private Vector2 currentLookAhead;
    private Vector3 lastTargetPosition;
    private float shakeTimer;
    private float shakeStrength;

    private void Awake()
    {
        followCamera = GetComponent<Camera>();
        followCamera.orthographic = true;
        FindTargetIfNeeded();

        if (target != null)
        {
            lastTargetPosition = target.position;
            SnapToTarget();
        }
    }

    private void LateUpdate()
    {
        FindTargetIfNeeded();
        if (target == null)
        {
            return;
        }

        Vector3 targetDelta = target.position - lastTargetPosition;
        float directionX = Mathf.Abs(targetDelta.x) > 0.001f ? Mathf.Sign(targetDelta.x) : 0f;
        Vector2 wantedLookAhead = new Vector2(directionX * lookAheadDistance, 0f);
        currentLookAhead = Vector2.Lerp(currentLookAhead, wantedLookAhead, Time.deltaTime * lookAheadSpeed);

        Vector3 desiredPosition = CalculateDesiredPosition();
        desiredPosition = ApplyDeadZone(desiredPosition);
        desiredPosition = ApplyBounds(desiredPosition);

        Vector3 shakeOffset = CalculateShakeOffset();
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref velocity, smoothTime) + shakeOffset;
        lastTargetPosition = target.position;
    }

    public void SetTarget(Transform newTarget, bool snap = true)
    {
        target = newTarget;
        if (target == null)
        {
            return;
        }

        lastTargetPosition = target.position;
        if (snap)
        {
            SnapToTarget();
        }
    }

    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        currentLookAhead = Vector2.zero;
        transform.position = ApplyBounds(CalculateDesiredPosition());
        velocity = Vector3.zero;
    }

    public void Shake(float duration, float strength)
    {
        shakeTimer = Mathf.Max(shakeTimer, duration);
        shakeStrength = Mathf.Max(shakeStrength, strength);
    }

    private void FindTargetIfNeeded()
    {
        if (target != null || !autoFindPlayer)
        {
            return;
        }

        PlayerController2D player = FindObjectOfType<PlayerController2D>();
        if (player != null)
        {
            target = player.transform;
            lastTargetPosition = target.position;
        }
    }

    private Vector3 CalculateDesiredPosition()
    {
        Vector3 desiredPosition = target.position;
        desiredPosition.x += offset.x + currentLookAhead.x;
        desiredPosition.y += offset.y + currentLookAhead.y;
        desiredPosition.z = transform.position.z;
        return desiredPosition;
    }

    private Vector3 ApplyDeadZone(Vector3 desiredPosition)
    {
        Vector3 currentPosition = transform.position;

        if (Mathf.Abs(desiredPosition.x - currentPosition.x) < deadZone.x)
        {
            desiredPosition.x = currentPosition.x;
        }

        if (Mathf.Abs(desiredPosition.y - currentPosition.y) < deadZone.y)
        {
            desiredPosition.y = currentPosition.y;
        }

        return desiredPosition;
    }

    private Vector3 ApplyBounds(Vector3 position)
    {
        if (!useBounds)
        {
            return position;
        }

        float halfHeight = followCamera.orthographicSize;
        float halfWidth = halfHeight * followCamera.aspect;

        position.x = Mathf.Clamp(position.x, minBounds.x + halfWidth, maxBounds.x - halfWidth);
        position.y = Mathf.Clamp(position.y, minBounds.y + halfHeight, maxBounds.y - halfHeight);
        return position;
    }

    private Vector3 CalculateShakeOffset()
    {
        if (shakeTimer <= 0f)
        {
            shakeStrength = 0f;
            return Vector3.zero;
        }

        shakeTimer -= Time.deltaTime;
        float fade = Mathf.Clamp01(shakeTimer / 0.12f);
        return new Vector3(
            Random.Range(-shakeStrength, shakeStrength) * fade,
            Random.Range(-shakeStrength, shakeStrength) * fade,
            0f);
    }
}
