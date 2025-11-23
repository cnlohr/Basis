using Basis.Network.Core;
using Basis.Scripts.Networking;
using Basis.Scripts.Networking.NetworkedAvatar;
using Basis.Scripts.Networking.Transmitters;
using Basis.Scripts.Profiler;
using Basis.Scripts.TransformBinders.BoneControl;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static SerializableBasis;
[System.Serializable]
public class BasisTransmissionResults : IDisposable
{
    public NativeArray<float3> targetPositions;
    public NativeArray<float> distances;
    public NativeArray<bool> PrevDistanceResults;
    public NativeArray<bool> PrevHearingResults;
    public NativeArray<bool> PrevAvatarResults;
    public NativeArray<bool> DistanceResults;
    public NativeArray<bool> HearingResults;
    public NativeArray<bool> AvatarResults;
    public NativeArray<float> smallestDistance;
    public BasisDistanceJob distanceJob;
    public JobHandle distanceJobHandle;

    public bool[] MicrophoneRangeIndex;
    public bool[] LastMicrophoneRangeIndex;
    public bool[] HearingIndex;
    public bool[] AvatarIndex;

    public ushort[] HearingIndexToId;
    public ushort[] LastHearingIndexToId;
    public float[] CalculatedDistances;

    public int IndexLength = -1;
    public bool requiresRebuild = false;
    public List<ushort> TalkingPoints = new List<ushort>(128);
    public float intervalSeconds = 0.5f;
    public float timer = 0f;
    public float SmallestDistanceToAnotherPlayer; // squared distance
    public float UnClampedInterval;
    public float DefaultInterval;

    [SerializeReference]
    public BasisLocalBoneControl MouthBone;
    [SerializeReference]
    public BasisNetworkTransmitter BasisNetworkTransmitter;

    public NetDataWriter AudioRecipientswriter = new NetDataWriter(true, 0);
    public bool hasScheduled = false;
    /// <summary>
    /// Called each frame; drives scheduling of distance job and network sync.
    /// </summary>
    public void Simulate()
    {
        timer += Time.deltaTime;

        if (timer <= intervalSeconds)
        {
            return;
        }

        // Use the actual accumulated interval (handles overshoot)
        float previousInterval = intervalSeconds;

        if (BasisNetworkTransmitter == null)
        {
            BasisDebug.LogError("BasisNetworkTransmitter is null; cannot send network update.", BasisDebug.LogTag.System);
            timer -= previousInterval;
            return;
        }

        var player = BasisNetworkTransmitter.Player;
        Basis.Scripts.BasisSdk.BasisAvatar avatar;
        if (player != null)
        {
            avatar = player.BasisAvatar;
        }
        else
        {
            avatar = null;
        }
        if (avatar == null)
        {
            BasisDebug.LogError("Missing Basis Avatar. Cannot send network update.", BasisDebug.LogTag.System);
            timer -= previousInterval;
            return;
        }

        // Schedule job to compute all distance info
        distanceJob.SquaredAvatarDistance = SMModuleDistanceBasedReductions.AvatarRange;
        distanceJob.SquaredHearingDistance = SMModuleDistanceBasedReductions.HearingRange;
        distanceJob.SquaredVoiceDistance = SMModuleDistanceBasedReductions.MicrophoneRange;
        distanceJob.HysteresisMargin = 0.05f; // clamped inside job
        distanceJob.referencePosition = MouthBone.OutgoingWorldData.position;

        int receiverCount = BasisNetworkPlayers.ReceiverCount;
        var snapshot = BasisNetworkPlayers.ReceiversSnapshot;
        if (IndexLength != receiverCount || requiresRebuild)
        {
            ResizeOrCreateArrayData(receiverCount);
            // managed mirrors
            LastMicrophoneRangeIndex = new bool[receiverCount];
            MicrophoneRangeIndex = new bool[receiverCount];
            HearingIndex = new bool[receiverCount];
            AvatarIndex = new bool[receiverCount];
            CalculatedDistances = new float[receiverCount];
            HearingIndexToId = new ushort[receiverCount];
            LastHearingIndexToId = new ushort[receiverCount];

            IndexLength = receiverCount;
            requiresRebuild = false;
        }
        if (receiverCount != 0)
        {
            // Fill target positions and ID map aligned to snapshot order
            for (int index = 0; index < receiverCount; index++)
            {
                var remote = snapshot[index];
                if (remote != null)
                {
                    bool hasMouth = RemoteBoneJobSystem.GetOutGoingMouth(remote.playerId, out float3 outgoing);

                    if (hasMouth)
                    {
                        targetPositions[index] = outgoing;
                    }
                    else
                    {
                        // Fallback: use reference position so distance is 0
                        targetPositions[index] = distanceJob.referencePosition;
                        BasisDebug.LogError($"Missing Mouth for {remote.playerId}");
                    }

                    // Always treat a valid remote as a valid receiver
                    HearingIndexToId[index] = remote.playerId;
                }
                else
                {
                    // Only truly invalid when we have no Remote at all.
                    targetPositions[index] = distanceJob.referencePosition;
                }
            }

            // reduction output
            distanceJob.outMin = smallestDistance;
            hasScheduled = true;
            distanceJobHandle = distanceJob.Schedule();
        }

        // Compress avatar state (doesn't touch mouth bone used as input)
        BasisNetworkAvatarCompressor.Compress(BasisNetworkTransmitter, avatar.Animator);

        if (hasScheduled)
        {
            // Complete job, consume results, update send interval, send recipients
            distanceJobHandle.Complete();
            hasScheduled = false;

            // Copy job outputs -> managed mirrors
            DistanceResults.CopyTo(MicrophoneRangeIndex);
            HearingResults.CopyTo(HearingIndex);
            AvatarResults.CopyTo(AvatarIndex);
            distances.CopyTo(CalculatedDistances);

            // Cache current as previous for next hysteresis step
            DistanceResults.CopyTo(PrevDistanceResults);
            HearingResults.CopyTo(PrevHearingResults);
            AvatarResults.CopyTo(PrevAvatarResults);

            if (HasMicrophoneStateChanged())
            {
                if (TalkingPoints.Capacity < IndexLength)
                {
                    TalkingPoints.Capacity = IndexLength;
                }
                TalkingPoints.Clear();
                for (int index = 0; index < IndexLength; index++)
                {
                    if (HearingIndex[index])
                    {
                        TalkingPoints.Add(HearingIndexToId[index]);
                    }
                }
                BasisNetworkTransmitter.HasReasonToSendAudio = TalkingPoints.Count != 0;

                // Copy current -> last for next tick (both mask and IDs)
                Array.Copy(MicrophoneRangeIndex, LastMicrophoneRangeIndex, IndexLength);
                Array.Copy(HearingIndexToId, LastHearingIndexToId, IndexLength);

                // Send to server
                VoiceReceiversMessage VoiceReceiversMessage = new VoiceReceiversMessage
                {
                    users = TalkingPoints.ToArray()
                };

                AudioRecipientswriter.Reset();
                VoiceReceiversMessage.Serialize(AudioRecipientswriter);
                //BasisDebug.Log($"Sending Microphone Check Data ({AudioRecipientswriter.Length})", BasisDebug.LogTag.Voice);
                BasisNetworkConnection.LocalPlayerPeer.Send(AudioRecipientswriter, BasisNetworkCommons.AudioRecipientsChannel, DeliveryMethod.ReliableOrdered);

                BasisNetworkProfiler.AddToCounter(BasisNetworkProfilerCounter.AudioRecipients, AudioRecipientswriter.Length);
            }
            if (IndexLength > 0)
            {
                float activeTime = Time.time;

                for (int index = 0; index < receiverCount; index++)
                {
                    try
                    {
                        var receiver = snapshot[index];
                        var remote = receiver.RemotePlayer;

                        // Avatar in-range toggling
                        if (remote.InAvatarRange != AvatarIndex[index])
                        {
                            remote.InAvatarRange = AvatarIndex[index];
                            remote.ReloadAvatar();
                        }

                        // Distance-based mesh LOD
                        remote.ChangeMeshLOD(CalculatedDistances[index], SMModuleDistanceBasedReductions.MeshLod);

                        // Voice start/stop (note: HearingIndex describes "we can hear them")
                        bool canHear = HearingIndex[index];
                        if (receiver.AudioReceiverModule.HasAudioSource != canHear)
                        {
                            if (canHear)
                            {
                                receiver.AudioReceiverModule.StartAudio();
                                remote.OutOfRangeFromLocal = false;
                            }
                            else
                            {
                                receiver.AudioReceiverModule.StopAudio();
                                remote.OutOfRangeFromLocal = true;
                            }
                        }

                        // Small anim drivers
                        remote.RemoteEyeDriver.Simulate();
                        remote.FacialBlinkDriver.Simulate(activeTime);
                    }
                    catch (Exception ex)
                    {
                        BasisDebug.LogError($"{ex} {ex.StackTrace}");
                    }
                }
            }
        }
        // Update rate control based on minimum distance
        SmallestDistanceToAnotherPlayer = smallestDistance[0]; // still squared

        if (!float.IsFinite(SmallestDistanceToAnotherPlayer))
        {
            // No receivers or all failed positions => treat as zero for rate calc
            SmallestDistanceToAnotherPlayer = 0f;
        }

        ServerMetaDataMessage ServerMetaDataMessage = BasisNetworkManagement.ServerMetaDataMessage;
        DefaultInterval = ServerMetaDataMessage.SyncInterval / 1000f;

        // Keep squared inside; apply sqrt at boundary where human-tuned params likely expect meters.
        float minLinear = math.sqrt(math.max(0f, SmallestDistanceToAnotherPlayer));

        float calculatedIntervalBase = ServerMetaDataMessage.BaseMultiplier + (minLinear * ServerMetaDataMessage.IncreaseRate);
        UnClampedInterval = DefaultInterval * calculatedIntervalBase;

        intervalSeconds = Mathf.Clamp(UnClampedInterval, DefaultInterval, ServerMetaDataMessage.SlowestSendRate);

        if (BasisAvatarRecorder.IsRecording)
        {
            BasisAvatarRecorder.StoreData(intervalSeconds, avatar.Animator.bodyRotation, avatar.Animator.bodyPosition, BasisNetworkTransmitter.HumanPose.muscles, avatar.Animator.transform.localScale.y);
        }
        // account for overshoot using the interval that actually accumulated
        timer -= previousInterval;
    }

    /// <summary>
    /// Call whenever the receiver list changes.
    /// </summary>
    public void OnPlayerJoinedOrleaved()
    {
        requiresRebuild = true;
    }
    private bool HasMicrophoneStateChanged()
    {
        for (int index = 0; index < IndexLength; index++)
        {
            if (MicrophoneRangeIndex[index] != LastMicrophoneRangeIndex[index])
            {
                return true;
            }
            if (HearingIndexToId[index] != LastHearingIndexToId[index])
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Allocate / reallocate all NativeArrays.
    /// </summary>
    public void ResizeOrCreateArrayData(int totalUserCount)
    {
        ReleaseResults();

        // (re)create
        smallestDistance = new NativeArray<float>(1, Allocator.Persistent);
        smallestDistance[0] = float.PositiveInfinity;

        targetPositions = new NativeArray<float3>(totalUserCount, Allocator.Persistent);
        distances = new NativeArray<float>(totalUserCount, Allocator.Persistent);

        // outputs
        DistanceResults = new NativeArray<bool>(totalUserCount, Allocator.Persistent);
        HearingResults = new NativeArray<bool>(totalUserCount, Allocator.Persistent);
        AvatarResults = new NativeArray<bool>(totalUserCount, Allocator.Persistent);

        // prevs (start false)
        PrevDistanceResults = new NativeArray<bool>(totalUserCount, Allocator.Persistent);
        PrevHearingResults = new NativeArray<bool>(totalUserCount, Allocator.Persistent);
        PrevAvatarResults = new NativeArray<bool>(totalUserCount, Allocator.Persistent);

        // wire job views
        distanceJob.distanceSq = distances;
        distanceJob.DistanceInside = DistanceResults;
        distanceJob.HearingInside = HearingResults;
        distanceJob.AvatarInside = AvatarResults;
        distanceJob.PrevDistanceInside = PrevDistanceResults;
        distanceJob.PrevHearingInside = PrevHearingResults;
        distanceJob.PrevAvatarInside = PrevAvatarResults;
        distanceJob.targetPositions = targetPositions;
    }

    /// <summary>
    /// Dispose NativeArrays and complete outstanding jobs.
    /// </summary>
    public void ReleaseResults()
    {
        // wait for in-flight jobs
        if (!distanceJobHandle.Equals(default(JobHandle)) && !distanceJobHandle.IsCompleted)
        {
            distanceJobHandle.Complete();
        }

        // dispose old
        if (targetPositions.IsCreated) targetPositions.Dispose();
        if (distances.IsCreated) distances.Dispose();
        if (smallestDistance.IsCreated) smallestDistance.Dispose();
        if (DistanceResults.IsCreated) DistanceResults.Dispose();
        if (HearingResults.IsCreated) HearingResults.Dispose();
        if (AvatarResults.IsCreated) AvatarResults.Dispose();
        if (PrevDistanceResults.IsCreated) PrevDistanceResults.Dispose();
        if (PrevHearingResults.IsCreated) PrevHearingResults.Dispose();
        if (PrevAvatarResults.IsCreated) PrevAvatarResults.Dispose();
    }
    public void Dispose()
    {
        ReleaseResults();
    }
    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    public struct BasisDistanceJob : IJob
    {
        public float SquaredVoiceDistance;
        public float SquaredHearingDistance;
        public float SquaredAvatarDistance;
        public float HysteresisMargin; // 0..0.49 (clamped)

        [ReadOnly] public float3 referencePosition;
        [ReadOnly] public NativeArray<float3> targetPositions;

        [ReadOnly] public NativeArray<bool> PrevDistanceInside;
        [ReadOnly] public NativeArray<bool> PrevHearingInside;
        [ReadOnly] public NativeArray<bool> PrevAvatarInside;

        [WriteOnly] public NativeArray<float> distanceSq;  // d^2 per target
        [WriteOnly] public NativeArray<bool> DistanceInside;
        [WriteOnly] public NativeArray<bool> HearingInside;
        [WriteOnly] public NativeArray<bool> AvatarInside;

        // length = 1
        public NativeArray<float> outMin;

        [BurstCompile]
        private static bool Hysteresis(bool wasInside, float d2, float thr2, float margin)
        {
            margin = math.clamp(margin, 0f, 0.49f);
            float insideFactor = 1f + margin;
            float outsideFactor = 1f - margin;
            float factor = math.select(outsideFactor, insideFactor, wasInside);
            return d2 < thr2 * factor;
        }

        public void Execute()
        {
            float3 refPos = referencePosition;
            float minD2 = float.PositiveInfinity;

            for (int i = 0; i < targetPositions.Length; i++)
            {
                float3 diff = targetPositions[i] - refPos;
                float d2 = math.lengthsq(diff);
                distanceSq[i] = d2;

                DistanceInside[i] = Hysteresis(PrevDistanceInside[i], d2, SquaredVoiceDistance, HysteresisMargin);
                HearingInside[i] = Hysteresis(PrevHearingInside[i], d2, SquaredHearingDistance, HysteresisMargin);
                AvatarInside[i] = Hysteresis(PrevAvatarInside[i], d2, SquaredAvatarDistance, HysteresisMargin);

                minD2 = math.min(minD2, d2);
            }

            if (outMin.IsCreated)
            {
                outMin[0] = minD2;
            }
        }
    }
}
