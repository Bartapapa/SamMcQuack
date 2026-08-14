using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(SplinePath))]
public class BalancePath : MonoBehaviour
{
    [Header("OBJECT REFS")]
    private SplinePath _path;
    public SplinePath Path { get { return _path; } }

    [Header("BALANCING POSITION OFFSET")]
    [SerializeField] private float _balancingHeightOffset = 0f;
    public float BalancingOffset { get { return _balancingHeightOffset; } }

    private void Awake()
    {
        _path = GetComponent<SplinePath>();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawSphere(transform.position + (transform.up * _balancingHeightOffset), .15f);
    }
}
