﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

[System.Serializable]
public class Leg
{
    [SerializeField] LegTarget _legTarget;
    [SerializeField] InverseKinematics _inverseKinematics;
    //Used to get Legs that must be grounded
    [SerializeField] LegTarget[] _mustBeGroundedLegTargets;

    List<Leg> _mustBeGroundedLegs;

    Vector3 _startMovingJointPosition;
    Vector3 _virtualTargetPosition;
    float _currentMoveFraction;

    public Vector3 LeafJointPosition { get { return _inverseKinematics.LeafJointPosition; } }
    public Vector3 TargetPosition { get { return _legTarget.Position; } }
    public Vector3 VirtualTargetPosition { get { return _legTarget.VirtualPosition; } }
    public bool Moving { get; private set; }
    public bool Stretched { get { return _inverseKinematics.IsStretched; } }
    public Vector3 Normal { get { return _legTarget.Normal; } }

    public float SqrDistance { get { return (LeafJointPosition - VirtualTargetPosition).sqrMagnitude; } }

    public void SetupOppositeLegs(List<Leg> allLegs)
    {
        _mustBeGroundedLegs = new List<Leg>();
        for (int i = 0; i < allLegs.Count; i++)
        {
            for (int j = 0; j < _mustBeGroundedLegTargets.Length; j++)
            {
                if (allLegs[i]._legTarget == _mustBeGroundedLegTargets[j])
                    _mustBeGroundedLegs.Add(allLegs[i]);
            }
        }
    }

    //If any of the opposite legs is moving this leg can't move!
    //Usually only 1 leg but support exists for multiple
    public bool CanStartMoving
    {
        get
        {
            for (int i = 0; i < _mustBeGroundedLegs.Count; i++)
            {
                if (_mustBeGroundedLegs[i].Moving)
                    return false;
            }

            return true;
        } 
    }

    public void UpdateInverseKinematics()
    {
        _inverseKinematics.DoInverseKinematics();
    }

    public void StartMoving(float maxDistance)
    {
        _startMovingJointPosition = LeafJointPosition;

        if (SqrDistance < maxDistance * maxDistance)
        {
            Vector3 direction = (VirtualTargetPosition - _startMovingJointPosition).normalized;
            _virtualTargetPosition = _startMovingJointPosition + direction * maxDistance;
        }
        else
            _virtualTargetPosition = VirtualTargetPosition;

        Moving = true;
        _currentMoveFraction = 0;
    }

    public void Move(float moveSpeed)
    {
        if (_currentMoveFraction >= 1.0f)
        {
            Moving = false;
            return;
        }

        float startDistance = (_startMovingJointPosition - _virtualTargetPosition).sqrMagnitude;
        float actualDistance = (LeafJointPosition - _virtualTargetPosition).sqrMagnitude;
        float speedFraction = Mathf.Clamp(actualDistance / startDistance, 1.0f, actualDistance / startDistance);

        _currentMoveFraction += Time.deltaTime * moveSpeed * speedFraction;
        Vector3 newPosition = Vector3.Lerp(_startMovingJointPosition, _virtualTargetPosition, _currentMoveFraction);
        _inverseKinematics.TargetPosition = newPosition;
    }
}

public class MoveSpiderLegs : MonoBehaviour
{
    [SerializeField] Transform _body;
    [SerializeField, Range(0.01f, 10f)] float _bodyHeightOffset = 1.2f;
    [SerializeField, Range(0.01f, 100f)] float _minimumLegSpeed = 1f;
    [SerializeField, Range(0.01f, 100f)] float _maxDistance = 0.5f;
    [SerializeField, Range(0.01f, 10f)] float _virtualLegTargetRadius;
    [SerializeField] List<Leg> _legs;

    bool _isRunning = false;

    SpiderDebug _spiderDebugScript;
    public float VirtualLegTargetRadius { get { return _virtualLegTargetRadius; } }

    private void Awake()
    {
        _spiderDebugScript = GetComponent<SpiderDebug>();

        for (int i = 0; i < _legs.Count; i++)
            _legs[i].SetupOppositeLegs(_legs);

        _isRunning = true;
    }

    private void OnValidate()
    {
        _spiderDebugScript = GetComponent<SpiderDebug>();
    }

    public void DoFixedUpdate()
    {
        Vector3 addedLegPositions = Vector3.zero;
        Vector3 addedLegNormals = Vector3.zero;

        for (int i = 0; i < _legs.Count; i++)
        {
            addedLegPositions += _legs[i].LeafJointPosition;
            addedLegNormals += _legs[i].Normal;

            if ((_legs[i].SqrDistance > _maxDistance * _maxDistance || _legs[i].Stretched) && !_legs[i].Moving && _legs[i].CanStartMoving)
                _legs[i].StartMoving(_maxDistance);
            if (_legs[i].Moving)
                _legs[i].Move(_minimumLegSpeed);
        }
        Vector3 averageLegPosition = addedLegPositions / _legs.Count;
        Vector3 averageLegNormal = addedLegNormals / _legs.Count;

        //SetBodyHeight(averageLegPosition);
        //RotateBody(averageLegNormal);
    }

    void SetBodyHeight(Vector3 averageLegPosition)
    {
        //Vector3 newPosition = new Vector3(_body.position.x, averageLegPosition.y + _bodyHeightOffset, _body.position.z);
        //_body.position = newPosition;

        Vector3 newPosition = averageLegPosition + _body.up * _bodyHeightOffset;  
        _body.position = newPosition;
    }

    void RotateBody(Vector3 averageLegNormal)
    {
        _body.up = averageLegNormal;
    }

    private void OnDrawGizmosSelected()
    {
        if (_isRunning)
        {
            Gizmos.color = _spiderDebugScript.DistanceColor;
            for (int i = 0; i < _legs.Count; i++)
                Gizmos.DrawLine(_legs[i].LeafJointPosition, _legs[i].TargetPosition);
        }
    }
}
