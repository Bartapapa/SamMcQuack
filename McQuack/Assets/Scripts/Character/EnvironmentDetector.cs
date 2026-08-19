using JetBrains.Annotations;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CapsuleParameterDescriptor
{
    public float CapsuleHeight;
    public float CapsuleRadius;
    public Vector3 CapsuleCenter;
}

public class GroundDetectionDescriptor
{
    public Vector3 Point;
    public Vector3 Normal;
    public bool WalkableGroundDetected = false;
    public bool SteepSlopeDetected = false;

    public GroundDetectionDescriptor(RaycastHit hit, float maxGroundAngle, float maxSlopeAngle)
    {
        Point = hit.point;
        Normal = hit.normal;

        WalkableGroundDetected = Vector3.Angle(hit.normal, Vector3.up) <= maxGroundAngle;
        SteepSlopeDetected = Vector3.Angle(hit.normal, Vector3.up) > maxGroundAngle && Vector3.Angle(hit.normal, Vector3.up) <= maxSlopeAngle;         
    }
}

public class WallDetectionDescriptor
{
    public Vector3 Point;
    public Vector3 Normal;
    public float Distance;

    public WallDetectionDescriptor(RaycastHit hit)
    {
        Point = hit.point;
        Normal = hit.normal;
        Distance = hit.distance;
    }
}

public class LedgeDetectionDescriptor
{
    public Vector3 WallPoint;
    public Vector3 WallNormal;

    public Vector3 GroundPoint;
    public Vector3 GroundNormal;
    public float WallHitToGroundHitHeight;

    public Vector3 StandPoint;

    public LedgeDetectionDescriptor(WallDetectionDescriptor wallHit, RaycastHit groundHit, float wallHitToGroundHitHeight, float standPointClearance)
    {
        WallPoint = wallHit.Point;
        WallNormal = wallHit.Normal;

        GroundPoint = groundHit.point;
        GroundNormal = groundHit.normal;

        WallHitToGroundHitHeight = wallHitToGroundHitHeight;

        Vector3 ledgeHeightPoint = new Vector3(WallPoint.x, GroundPoint.y, WallPoint.z);
        StandPoint = ledgeHeightPoint + (-WallNormal * standPointClearance);
    }
}

public class EnvironmentDetector : MonoBehaviour
{
    [Header("GROUND DETECTION")]
    [Header("GROUNDCHECK")]
    [SerializeField] private float _groundCheckRadius = .4f;
    [SerializeField] private float _groundCheckDistance = .2f;
    [SerializeField] private float _groundCheckOffset = .5f;
    [SerializeField] private LayerMask _groundLayers;
    [SerializeField] private bool _showGroundCheckDebug = false;

    private RaycastHit _groundHit;
    public RaycastHit GroundHit { get { return _groundHit; } }

    [Header("WALL DETECTION")]
    [SerializeField] private float _wallCastRadius = .25f;
    [SerializeField] private float _wallCastDistance = 1f;
    [SerializeField] private LayerMask _wallMask;
    [SerializeField] private Vector3 _wallCastOffset = Vector3.zero;
    [SerializeField] private bool _showWallCheckDebug = true;
    public Vector3 WallCastOffset { get { return _wallCastOffset; } }

    [Header("LEDGE DETECTION")]
    [SerializeField] private float _groundCheckForwardOffset = .1f;
    [SerializeField] private bool _showLedgeCheckDebug = true;

    [Header("CAN FIT")]
    [SerializeField] private float _bottomOffset = .25f;
    [SerializeField] private bool _showCanFitDebug = true;
    private Vector3 _debugStandingPosition;
    private CapsuleParameterDescriptor _debugCapsuleParams;
    private LayerMask _debugLayermask;


    public bool GroundCheck(float maxGroundAngle, float maxSlopeAngle, out GroundDetectionDescriptor ground)
    {
        ground = default;

        Vector3 checkOrigin = transform.position + Vector3.up * _groundCheckOffset;
        bool groundDetected = Physics.SphereCast(checkOrigin, _groundCheckRadius, Vector3.down, out _groundHit, _groundCheckDistance, _groundLayers, QueryTriggerInteraction.Ignore);
        if (groundDetected)
        {
            ground = new GroundDetectionDescriptor(_groundHit, maxGroundAngle, maxSlopeAngle);
        }

        return groundDetected;
    }

    public float FacingSlope()
    {
        float slopeFacing = 0f;
        Vector3 forward = transform.forward;
        Vector3 slopeNormal = _groundHit.normal;

        Vector3 slopeForward = Vector3.ProjectOnPlane(transform.forward, slopeNormal).normalized;
        Vector3 downhill = Vector3.ProjectOnPlane(Vector3.down, slopeNormal).normalized;

        slopeFacing = Vector3.Dot(slopeForward, downhill);

        return slopeFacing;
    }

    public bool WallCheck(Vector3 origin, Vector3 direction, out WallDetectionDescriptor wall)
    {
        wall = default;

        direction = direction.normalized;

        if (Physics.SphereCast(origin, _wallCastRadius, direction, out RaycastHit hit, _wallCastDistance, _wallMask, QueryTriggerInteraction.Ignore))
        {
            wall = new WallDetectionDescriptor(hit);
            return true;
        }

        return false;
    }

    public bool LedgeCheck(Vector3 origin, Vector3 direction, float maxLedgeHeight, float minLedgeHeight, float standPointClearance, float maximumLedgeSlope, out LedgeDetectionDescriptor ledge)
    {
        ledge = default;
        bool check = false;

        if (!WallCheck(origin, direction, out WallDetectionDescriptor wallHit)) return false;

        Vector3 groundCheckOrigin = origin + Vector3.up * maxLedgeHeight - wallHit.Normal * _groundCheckForwardOffset;
        if (!Physics.Raycast(groundCheckOrigin, Vector3.down, out RaycastHit groundHit, maxLedgeHeight, _wallMask, QueryTriggerInteraction.Ignore)) return false;

        float height = groundHit.point.y - wallHit.Point.y;
        if (height < minLedgeHeight || height > maxLedgeHeight) return false;

        float slopeAngle = Vector3.Angle(groundHit.normal, Vector3.up);
        if (slopeAngle > maximumLedgeSlope) return false;

        ledge = new LedgeDetectionDescriptor(wallHit, groundHit, height, standPointClearance);

        if (_showLedgeCheckDebug)
        {
            Debug.DrawRay(groundCheckOrigin, Vector3.down * maxLedgeHeight, Color.cyan);
        }

        check = true;
        if (check && _showLedgeCheckDebug)
        {
            Debug.DrawRay(groundHit.point, groundHit.normal * .3f, Color.green);
        }

        return true;
    }

    public bool CanCharacterFit(Vector3 standPosition, CapsuleParameterDescriptor capsuleDescriptor, LayerMask environmentMask)
    {
        float capsuleHeight = capsuleDescriptor.CapsuleHeight;
        float capsuleRadius = capsuleDescriptor.CapsuleRadius;

        _debugStandingPosition = standPosition;
        _debugCapsuleParams = capsuleDescriptor;
        _debugLayermask = environmentMask;

        float cylinderHeight = capsuleHeight - 2f * capsuleRadius;
        Vector3 bottom = standPosition + (Vector3.up * (capsuleRadius + _bottomOffset));
        Vector3 top = standPosition + (Vector3.up * capsuleRadius) + (Vector3.up * cylinderHeight);

        return !Physics.CheckCapsule(bottom, top, capsuleRadius, environmentMask, QueryTriggerInteraction.Ignore);
    }

    private void OnDrawGizmosSelected()
    {
        if (_showWallCheckDebug)
        {
            Gizmos.color = Color.yellow;

            Vector3 origin = transform.position + _wallCastOffset;
            Vector3 direction = transform.forward.normalized;

            Gizmos.DrawWireSphere(origin, _wallCastRadius);

            Gizmos.DrawWireSphere(origin + direction * _wallCastDistance, _wallCastRadius);

            Gizmos.DrawLine(origin, origin + direction * _wallCastDistance);
        }

        if (_showGroundCheckDebug)
        {
            GroundDetectionDescriptor ground = default;

            if (GroundCheck(40f, 80f, out ground))
            {
                Gizmos.color = Color.green;
            }
            else
            {
                Gizmos.color = Color.red;
            }

            Vector3 origin = transform.position + transform.up * _groundCheckOffset;

            Gizmos.DrawWireSphere(origin, _groundCheckRadius);

            Gizmos.DrawWireSphere(origin + Vector3.down * _groundCheckDistance, _groundCheckRadius);

            Gizmos.DrawLine(origin, origin + Vector3.down * _groundCheckDistance);
        }

        if (_showCanFitDebug)
        {
            if (_debugStandingPosition != Vector3.zero)
            {
                if (CanCharacterFit(_debugStandingPosition, _debugCapsuleParams, _debugLayermask))
                {
                    Gizmos.color = Color.green;
                }
                else
                {
                    Gizmos.color = Color.red;
                }

                Vector3 bottomOrigin = _debugStandingPosition + (Vector3.up * _debugCapsuleParams.CapsuleRadius) + (Vector3.up * _bottomOffset);
                Vector3 topOrigin = _debugStandingPosition + (Vector3.up * _debugCapsuleParams.CapsuleHeight) - (Vector3.up * _debugCapsuleParams.CapsuleRadius);

                Gizmos.DrawWireSphere(bottomOrigin, _debugCapsuleParams.CapsuleRadius);
                Gizmos.DrawWireSphere(topOrigin, _debugCapsuleParams.CapsuleRadius);
                Gizmos.DrawLine(bottomOrigin, topOrigin);
            }
        }
    }
}
