using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[ExecuteAlways]
public class SplinePath : MonoBehaviour
{
    [Serializable]
    public class SplinePoint
    {
        public Transform Position;
        public Transform TangentIn;
        public Transform TangentOut;
    }

    [Serializable]
    public struct SplineSample
    {
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Up;

        public SplineSample(Vector3 position, Vector3 tangent, Vector3 up)
        {
            this.Position = position;
            this.Tangent = tangent;
            this.Up = up;
        }
    }

    private struct ArcSample
    {
        public float Distance;
        public float NormalizedT;
        public Vector3 Position;
        public Vector3 Tangent;
        public Vector3 Up;
    }

    [Header("SPLINE")]
    [SerializeField]
    private List<SplinePoint> _points = new();
    public int PointCount { get { return _points.Count; } }
    public IReadOnlyList<SplinePoint> Points { get { return _points; } }

    private float _totalLength;
    public float Length 
    { 
        get 
        {
            EnsureCache();
            return _totalLength; 
        } 
    }

    [SerializeField, Min(2)]
    private int _samplesPerSegment = 20;

    [Header("DEBUG")]

    [SerializeField]
    private Color _splineColor = Color.yellow;

    [SerializeField]
    private Color _pointColor = Color.white;

    [SerializeField]
    private Color _tangentColor = Color.cyan;

    private readonly List<ArcSample> _arcSamples = new();

    private bool _cacheDirty = true;
    private Vector3[] lastTransformPositions;
    private Quaternion[] lastTransformRotations;



    private void Awake()
    {
        EnsureCache();
    }

    private void OnEnable()
    {
        _cacheDirty = true;
    }

    private void Update()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (HasTransformChanges())
            {
                _cacheDirty = true;

                if (TryGetComponent(out SplineMeshGenerator meshGenerator))
                    meshGenerator.MarkDirty();
            }
        }
#endif

        EnsureCache();
    }

    #region PRIVATE FUNCTIONS
    private void EnsureCache()
    {
        if (!_cacheDirty)
            return;

        RebuildCache();
    }

    public void RebuildCache()
    {
        _arcSamples.Clear();
        _totalLength = 0f;

        if (_points.Count < 2)
        {
            _cacheDirty = false;
            return;
        }

        Vector3 previousPosition = _points[0].Position.position;
        Vector3 previousTangent = transform.forward;
        Vector3 previousUp = transform.up;

        int sampleCount =
            Mathf.Max(2, _samplesPerSegment);

        for (int segment = 0; segment < _points.Count - 1; segment++)
        {
            for (int i = 0; i <= sampleCount; i++)
            {
                // Avoid duplicating the beginning of subsequent segments.
                if (segment > 0 && i == 0)
                    continue;

                float segmentT =
                    i / (float)sampleCount;

                SplineSample sample =
                    EvaluateSegment(segment, segmentT);

                Vector3 tangent = sample.Tangent;

                if (_arcSamples.Count == 0)
                {
                    previousUp =
                        Vector3.ProjectOnPlane(
                            transform.up,
                            tangent).normalized;

                    if (previousUp.sqrMagnitude < 0.001f)
                        previousUp = Vector3.up;
                }
                else
                {
                    // Parallel-transport the previous up vector.
                    previousUp =
                        Vector3.ProjectOnPlane(
                            previousUp,
                            tangent).normalized;

                    if (previousUp.sqrMagnitude < 0.001f)
                    {
                        previousUp =
                            Vector3.ProjectOnPlane(
                                Vector3.up,
                                tangent).normalized;
                    }
                }

                if (_arcSamples.Count > 0)
                {
                    _totalLength +=
                        Vector3.Distance(
                            previousPosition,
                            sample.Position);
                }

                ArcSample arcSample = new ArcSample
                {
                    Distance = _totalLength,
                    NormalizedT =
                        (segment + segmentT) /
                        (_points.Count - 1f),
                    Position = sample.Position,
                    Tangent = tangent,
                    Up = previousUp
                };

                _arcSamples.Add(arcSample);

                previousPosition = sample.Position;
                previousTangent = tangent;
            }
        }

        _cacheDirty = false;
        CacheTransformState();
    }

    private bool HasTransformChanges()
    {
        int transformCount = _points.Count * 3;

        if (lastTransformPositions == null ||
            lastTransformPositions.Length != transformCount)
        {
            CacheTransformState();
            return true;
        }

        int index = 0;

        foreach (SplinePoint point in _points)
        {
            Transform[] transforms =
            {
                point.Position,
                point.TangentIn,
                point.TangentOut
            };

            foreach (Transform t in transforms)
            {
                if (t == null)
                {
                    index++;
                    continue;
                }

                if (lastTransformPositions[index] != t.position ||
                    lastTransformRotations[index] != t.rotation)
                {
                    CacheTransformState();
                    return true;
                }

                index++;
            }
        }

        return false;
    }

    private void CacheTransformState()
    {
        int transformCount = _points.Count * 3;

        lastTransformPositions = new Vector3[transformCount];
        lastTransformRotations = new Quaternion[transformCount];

        int index = 0;

        foreach (SplinePoint point in _points)
        {
            Transform[] transforms =
            {
                point.Position,
                point.TangentIn,
                point.TangentOut
            };

            foreach (Transform t in transforms)
            {
                if (t != null)
                {
                    lastTransformPositions[index] = t.position;
                    lastTransformRotations[index] = t.rotation;
                }

                index++;
            }
        }
    }

    private static SplineSample ToSplineSample(ArcSample sample)
    {
        return new SplineSample(
            sample.Position,
            sample.Tangent,
            sample.Up);
    }

    private SplineSample EvaluateSegment(int segment, float t)
    {
        SplinePoint a = _points[segment];
        SplinePoint b = _points[segment + 1];

        Vector3 p0 = a.Position.position;
        Vector3 p1 = a.TangentOut.position;
        Vector3 p2 = b.TangentIn.position;
        Vector3 p3 = b.Position.position;

        Vector3 position = EvaluateBezier(p0, p1, p2, p3, t);

        Vector3 tangent = EvaluateBezierDerivative(p0, p1, p2, p3, t).normalized;

        if (tangent.sqrMagnitude < 0.001f)
            tangent = transform.forward;

        Vector3 up =
            Vector3.ProjectOnPlane(transform.up, tangent)
                .normalized;

        if (up.sqrMagnitude < 0.001f)
            up = Vector3.up;

        return new SplineSample(position, tangent, up);
    }

    private Vector3 EvaluateBezier(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;

        return
            u * u * u * p0 +
            3f * u * u * t * p1 +
            3f * u * t * t * p2 +
            t * t * t * p3;
    }

    private Vector3 EvaluateBezierDerivative(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1f - t;

        return
            3f * u * u * (p1 - p0) +
            6f * u * t * (p2 - p1) +
            3f * t * t * (p3 - p2);
    }

    public void MarkDirty()
    {
        _cacheDirty = true;
    }

    private void OnValidate()
    {
        _samplesPerSegment = Mathf.Max(2, _samplesPerSegment);

        _cacheDirty = true;
    }
    #endregion

    #region PUBLIC FUNCTIONS
    public SplineSample EvaluateAtDistance(float distance)
    {
        EnsureCache();

        if (_arcSamples.Count == 0)
            return default;

        distance = Mathf.Clamp(distance, 0f, _totalLength);

        if (distance <= 0f)
            return ToSplineSample(_arcSamples[0]);

        if (distance >= _totalLength)
            return ToSplineSample(_arcSamples[^1]);

        int low = 0;
        int high = _arcSamples.Count - 1;

        while (low <= high)
        {
            int middle = (low + high) / 2;

            if (_arcSamples[middle].Distance < distance)
                low = middle + 1;
            else
                high = middle - 1;
        }

        int upperIndex = Mathf.Clamp(low, 1, _arcSamples.Count - 1);
        int lowerIndex = upperIndex - 1;

        ArcSample a = _arcSamples[lowerIndex];
        ArcSample b = _arcSamples[upperIndex];

        float range = b.Distance - a.Distance;

        float t = range > Mathf.Epsilon
            ? (distance - a.Distance) / range
            : 0f;

        Vector3 position = Vector3.Lerp(a.Position, b.Position, t);

        Vector3 tangent =
            Vector3.Slerp(a.Tangent, b.Tangent, t).normalized;

        Vector3 up =
            Vector3.Slerp(a.Up, b.Up, t);

        up = Vector3.ProjectOnPlane(up, tangent).normalized;

        if (up.sqrMagnitude < 0.001f)
            up = Vector3.up;

        return new SplineSample(position, tangent, up);
    }

    public SplineSample Evaluate(float normalizedT)
    {
        EnsureCache();

        if (_arcSamples.Count == 0)
            return default;

        normalizedT = Mathf.Clamp01(normalizedT);

        if (_points.Count == 1)
        {
            return new SplineSample(
                _points[0].Position.position,
                transform.forward,
                transform.up);
        }

        float segmentFloat =
            normalizedT * (_points.Count - 1);

        int segment =
            Mathf.Min(
                Mathf.FloorToInt(segmentFloat),
                _points.Count - 2);

        float segmentT =
            segmentFloat - segment;

        return EvaluateSegment(segment, segmentT);
    }

    public float GetClosestDistance(Vector3 worldPosition)
    {
        EnsureCache();

        if (_arcSamples.Count == 0)
            return 0f;

        float bestDistance = 0f;
        float bestSqrDistance = float.PositiveInfinity;

        foreach (ArcSample sample in _arcSamples)
        {
            float sqrDistance =
                (sample.Position - worldPosition).sqrMagnitude;

            if (sqrDistance < bestSqrDistance)
            {
                bestSqrDistance = sqrDistance;
                bestDistance = sample.Distance;
            }
        }

        return bestDistance;
    }

    #endregion

    #region EDITOR POINTS
    [ContextMenu("Add Point")]
    public void AddPoint()
    {
        Vector3 position;

        if (_points.Count == 0)
        {
            position = transform.position;
        }
        else
        {
            SplinePoint last = _points[^1];

            position =
                last.Position.position +
                transform.forward * 2f;
        }

        AddPoint(position);
    }

    public void AddPoint(Vector3 position)
    {
        GameObject pointObject =
            new GameObject($"Point_{_points.Count:00}");

        pointObject.transform.SetParent(transform);
        pointObject.transform.position = position;

        GameObject tangentInObject =
            new GameObject("TangentIn");

        GameObject tangentOutObject =
            new GameObject("TangentOut");

        tangentInObject.transform.SetParent(pointObject.transform);
        tangentOutObject.transform.SetParent(pointObject.transform);

        tangentInObject.transform.position =
            position - transform.right;

        tangentOutObject.transform.position =
            position + transform.right;

        SplinePoint point = new SplinePoint
        {
            Position = pointObject.transform,
            TangentIn = tangentInObject.transform,
            TangentOut = tangentOutObject.transform
        };

        _points.Add(point);

        _cacheDirty = true;

        CacheTransformState();

        if (TryGetComponent(out SplineMeshGenerator meshGenerator))
            meshGenerator.MarkDirty();
    }

    [ContextMenu("Clear Points")]
    public void ClearPoints()
    {
        foreach (SplinePoint point in _points)
        {
            if (point.Position != null)
                SafeDestroy(point.Position.gameObject);
        }

        _points.Clear();

        _cacheDirty = true;
        CacheTransformState();

        if (TryGetComponent(out SplineMeshGenerator meshGenerator))
            meshGenerator.MarkDirty();
    }

    private void SafeDestroy(GameObject obj)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            DestroyImmediate(obj);
        else
            Destroy(obj);
#else
        Destroy(obj);
#endif
    }
    #endregion

    #region GIZMOS
    private void OnDrawGizmos()
    {
        if (_points.Count == 0)
            return;

        Gizmos.color = _pointColor;

        foreach (SplinePoint point in _points)
        {
            if (point.Position == null)
                continue;

            Gizmos.DrawSphere(
                point.Position.position,
                0.08f);
        }

        Gizmos.color = _tangentColor;

        foreach (SplinePoint point in _points)
        {
            if (point.Position == null)
                continue;

            if (point.TangentIn != null)
            {
                Gizmos.DrawLine(
                    point.Position.position,
                    point.TangentIn.position);

                Gizmos.DrawSphere(
                    point.TangentIn.position,
                    0.05f);
            }

            if (point.TangentOut != null)
            {
                Gizmos.DrawLine(
                    point.Position.position,
                    point.TangentOut.position);

                Gizmos.DrawSphere(
                    point.TangentOut.position,
                    0.05f);
            }
        }

        if (_points.Count < 2)
            return;

        Gizmos.color = _splineColor;

        const int visualizationSamples = 100;

        Vector3 previous =
            Evaluate(0f).Position;

        for (int i = 1; i <= visualizationSamples; i++)
        {
            float t =
                i / (float)visualizationSamples;

            Vector3 current =
                Evaluate(t).Position;

            Gizmos.DrawLine(previous, current);

            previous = current;
        }
    }
    #endregion
}
