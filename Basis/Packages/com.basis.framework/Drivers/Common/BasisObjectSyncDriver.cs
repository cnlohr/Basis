using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Jobs;

public static class BasisObjectSyncDriver
{
    public static readonly HashSet<BasisObjectSyncNetworking> OwnedObjectSyncs = new();
    public static readonly HashSet<BasisObjectSyncNetworking> RemoteOwnedObjectSyncs = new();
    public static float TargetMilliseconds = 0.25f;

    private static double _lastUpdateTime;

    private static TransformAccessArray _remoteTransforms;
    private static NativeList<float3> _targetPositions;
    private static NativeList<quaternion> _targetRotations;
    private static NativeList<float> _lerpMultipliers;

    private static Transform[] _cachedTransforms = Array.Empty<Transform>();
    private static JobHandle _remoteJobHandle;

    public static void Initalization()
    {
        _remoteTransforms = new TransformAccessArray(0);
        _targetPositions = new NativeList<float3>(128, Allocator.Persistent);
        _targetRotations = new NativeList<quaternion>(128, Allocator.Persistent);
        _lerpMultipliers = new NativeList<float>(128, Allocator.Persistent);
    }

    public static void OnDestroy()
    {
        _remoteJobHandle.Complete();

        if (_remoteTransforms.isCreated) _remoteTransforms.Dispose();
        if (_targetPositions.IsCreated) _targetPositions.Dispose();
        if (_targetRotations.IsCreated) _targetRotations.Dispose();
        if (_lerpMultipliers.IsCreated) _lerpMultipliers.Dispose();
    }

    public static void TransmitOwnedPickups(double currentTime)
    {
        if (currentTime - _lastUpdateTime < TargetMilliseconds)
        {
            return;
        }

        _lastUpdateTime = currentTime;

        foreach (BasisObjectSyncNetworking obj in OwnedObjectSyncs)
        {
            if (obj != null)
            {
                obj.SendNetworkSync();
            }
        }
    }
    public static void ScheduleRemoteLerp(float deltaTime)
    {
        _remoteJobHandle.Complete();

        int count = 0;

        foreach (var obj in RemoteOwnedObjectSyncs)
        {
            if (obj == null || obj.IsOwnedLocallyOnClient) continue;
            count++;
        }

        if (count == 0) return;

        if (_cachedTransforms.Length != count)
        {
            _cachedTransforms = new Transform[count];
        }

        int index = 0;
        bool State = _targetPositions.Length <= count;

        if (State)
        {
            _targetPositions.ResizeUninitialized(count);
            _targetRotations.ResizeUninitialized(count);
            _lerpMultipliers.ResizeUninitialized(count);
        }


        foreach (BasisObjectSyncNetworking obj in RemoteOwnedObjectSyncs)
        {
            if (obj == null || obj.IsOwnedLocallyOnClient) continue;

            _cachedTransforms[index] = obj.SelfTransform;

            _targetPositions[index] = obj.BTU.TargetPosition;
            _targetRotations[index] = obj.BTU.TargetRotation;
            _lerpMultipliers[index] = obj.BTU.LerpMultipliers * deltaTime;

            index++;
        }
        if (_remoteTransforms.isCreated)
        {
            if (_remoteTransforms.length != _cachedTransforms.Length)
            {
                _remoteTransforms.Dispose();
                _remoteTransforms = new TransformAccessArray(_cachedTransforms);
            }
            else
            {
                _remoteTransforms.SetTransforms(_cachedTransforms);
            }
        }
        else
        {
            _remoteTransforms = new TransformAccessArray(_cachedTransforms);
        }

        var job = new RemoteSyncJob
        {
            targetPositions = _targetPositions,
            targetRotations = _targetRotations,
            lerpMultipliers = _lerpMultipliers
        };

        _remoteJobHandle = job.Schedule(_remoteTransforms);
    }

    public static void CompleteScheduledRemoteLerp()
    {
        _remoteJobHandle.Complete();
    }

    [BurstCompile]
    public struct RemoteSyncJob : IJobParallelForTransform
    {
        [ReadOnly] public NativeList<float3> targetPositions;
        [ReadOnly] public NativeList<quaternion> targetRotations;
        [ReadOnly] public NativeList<float> lerpMultipliers;

        public void Execute(int index, TransformAccess transform)
        {
            float lerp = lerpMultipliers[index];
            if (lerp <= 0f) return;

            if (transform.isValid)
            {
                transform.GetLocalPositionAndRotation(out Vector3 currentPos, out Quaternion currentRot);
                float3 newPos = math.lerp(currentPos, targetPositions[index], lerp);
                quaternion newRot = math.slerp(currentRot, targetRotations[index], lerp);

                transform.SetLocalPositionAndRotation(newPos, newRot);
            }
        }
    }

    #region Static API

    public static void AddLocalOwner(BasisObjectSyncNetworking obj)
    {
        if (obj != null)
            OwnedObjectSyncs.Add(obj);
    }

    public static void RemoveLocalOwner(BasisObjectSyncNetworking obj)
    {
        if (obj != null)
            OwnedObjectSyncs.Remove(obj);
    }

    public static void AddRemoteOwner(BasisObjectSyncNetworking obj)
    {
        if (obj != null)
            RemoteOwnedObjectSyncs.Add(obj);
    }

    public static void RemoveRemoteOwner(BasisObjectSyncNetworking obj)
    {
        if (obj != null)
            RemoteOwnedObjectSyncs.Remove(obj);
    }

    #endregion
}

[Serializable]
public struct BasisTranslationUpdate
{
    public float3 TargetPosition;
    public quaternion TargetRotation;
    public float3 TargetScales;
    public float LerpMultipliers;
}
