using Basis.Scripts.Networking.Compression;
using Basis.Scripts.Networking.Receivers;
using Basis.Scripts.Profiler;
using LiteNetLib.Utils;
using System;
using static SerializableBasis;
using Vector3 = UnityEngine.Vector3;
namespace Basis.Scripts.Networking.NetworkedAvatar
{
    public static class BasisNetworkAvatarDecompressor
    {
        /// <summary>
        /// Single API to handle all avatar decompression tasks.
        /// </summary>
        public static void DecompressAndProcessAvatar(BasisNetworkReceiver baseReceiver, ServerSideSyncPlayerMessage syncMessage, ushort PlayerId)
        {
            if (syncMessage.avatarSerialization.array == null)
            {
                throw new ArgumentException("Cant Serialize Avatar Data");
            }
            int Length = syncMessage.avatarSerialization.array.Length;
            baseReceiver.Offset = 0;
            BasisAvatarBuffer avatarBuffer = new BasisAvatarBuffer
            {
                Position = BasisUnityBitPackerExtensions.ReadVectorFloatFromBytes(ref syncMessage.avatarSerialization.array, ref baseReceiver.Offset),//12
                rotation = BasisUnityBitPackerExtensions.ReadQuaternionFromBytes(ref syncMessage.avatarSerialization.array, BasisNetworkPlayer.RotationCompression, ref baseReceiver.Offset)//14
            };
            BasisUnityBitPackerExtensions.ReadMusclesFromBytes(ref syncMessage.avatarSerialization.array, ref baseReceiver.CopyData, ref baseReceiver.Offset);
            for (int Index = 0; Index < LocalAvatarSyncMessage.StoredBones; Index++)
            {
                avatarBuffer.Muscles[Index] = Decompress(baseReceiver.CopyData[Index], BasisNetworkPlayer.MinMuscle[Index], BasisNetworkPlayer.MaxMuscle[Index]);
            }
            avatarBuffer.Scale = Vector3.one;
            BasisNetworkProfiler.AddToCounter(BasisNetworkProfilerCounter.ServerSideSyncPlayer, Length);
            avatarBuffer.SecondsInterval = syncMessage.interval / 1000.0f;
            baseReceiver.EnQueueAvatarBuffer(ref avatarBuffer);
            int Count = syncMessage.avatarSerialization.AdditionalAvatarDataSize;
            //  BasisDebug.Log($"AdditionalAvatarDatas was {Count}");
            if (baseReceiver.Player != null && baseReceiver.Player.BasisAvatar != null)
            {
                for (int Index = 0; Index < Count; Index++)
                {
                    AdditionalAvatarData Data = syncMessage.avatarSerialization.AdditionalAvatarDatas[Index];
                    baseReceiver.Player.BasisAvatar.OnServerReductionSystemMessageReceived?.Invoke(Data.messageIndex, Data.array);
                }
            }
        }
        /// <summary>
        /// Initial Payload
        /// </summary>
        /// <param name="baseReceiver"></param>
        /// <param name="syncMessage"></param>
        /// <exception cref="ArgumentException"></exception>
        public static void DecompressAndProcessAvatar(BasisNetworkReceiver baseReceiver, LocalAvatarSyncMessage syncMessage, ushort PlayerId)
        {
            if (syncMessage.array == null)
            {
                throw new ArgumentException("Cant Serialize Avatar Data");
            }
            int Length = syncMessage.array.Length;
            baseReceiver.Offset = 0;
            BasisAvatarBuffer avatarBuffer = new BasisAvatarBuffer
            {
                Position = BasisUnityBitPackerExtensions.ReadVectorFloatFromBytes(ref syncMessage.array, ref baseReceiver.Offset),//12
                rotation = BasisUnityBitPackerExtensions.ReadQuaternionFromBytes(ref syncMessage.array, BasisNetworkPlayer.RotationCompression, ref baseReceiver.Offset)//14
            };
            BasisUnityBitPackerExtensions.ReadMusclesFromBytesAsUShort(ref syncMessage.array, ref baseReceiver.CopyData, ref baseReceiver.Offset);
            if (avatarBuffer.Muscles == null)
            {
                avatarBuffer.Muscles = new float[LocalAvatarSyncMessage.StoredBones];
            }
            for (int Index = 0; Index < LocalAvatarSyncMessage.StoredBones; Index++)//89 * 2 = 178
            {
                avatarBuffer.Muscles[Index] = Decompress(baseReceiver.CopyData[Index], BasisNetworkPlayer.MinMuscle[Index], BasisNetworkPlayer.MaxMuscle[Index]);
            }
            avatarBuffer.Scale = Vector3.one;
            BasisNetworkProfiler.AddToCounter(BasisNetworkProfilerCounter.ServerSideSyncPlayer, Length);
            avatarBuffer.SecondsInterval = 0.01f;
            baseReceiver.EnQueueAvatarBuffer(ref avatarBuffer);
            int Count = syncMessage.AdditionalAvatarDataSize;//1
          //  BasisDebug.Log($"AdditionalAvatarDatas was {Count}");
            if (baseReceiver.Player != null && baseReceiver.Player.BasisAvatar != null)
            {
                for (int Index = 0; Index < Count; Index++)
                {
                    AdditionalAvatarData Data = syncMessage.AdditionalAvatarDatas[Index];
                    baseReceiver.Player.BasisAvatar.OnServerReductionSystemMessageReceived?.Invoke(Data.messageIndex, Data.array);
                }
            }
        }
        public static float Decompress(ushort value, float MinValue, float MaxValue)
        {
            // Map the ushort value back to the float range
            float normalized = (float)value / FloatRangeDifference; // 0..1  - UShortMin
            return normalized * (float)(MaxValue - MinValue) + MinValue;
        }

        private const ushort UShortMin = ushort.MinValue; // 0
        private const ushort UShortMax = ushort.MaxValue; // 65535
        private const float FloatRangeDifference = UShortMax - UShortMin;
    }
}
