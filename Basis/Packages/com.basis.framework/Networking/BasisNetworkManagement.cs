using Basis.Network.Core;
using Basis.Scripts.BasisSdk.Helpers;
using Basis.Scripts.Networking.Transmitters;
using Basis.Scripts.Profiler;
using LiteNetLib;
using LiteNetLib.Utils;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.ResourceManagement.ResourceProviders;
using static SerializableBasis;
namespace Basis.Scripts.Networking
{
    [DefaultExecutionOrder(15001)]
    public class BasisNetworkManagement : MonoBehaviour
    {
        [Header("Connection")]
        public string Ip = "170.64.184.249";
        public ushort Port = 4296;
        [HideInInspector] public string Password = "default_password";
        public bool IsHostMode = false;
        public static BasisNetworkManagement Instance;
        public static SynchronizationContext MainThreadContext;
        public static bool NetworkRunning;
        public static BasisNetworkTransmitter Transmitter;
        public static NetPeer LocalPlayerPeer => BasisNetworkConnection.LocalPlayerPeer;

        [SerializeField] public BasisNetworkTransmitter LocalAccessTransmitter;
        public static ServerMetaDataMessage ServerMetaDataMessage = new ServerMetaDataMessage();
        public static Action OnEnableInstanceCreate;
        public static InstantiationParameters instantiationParameters;
        public static int mainThreadId;

        private void OnEnable()
        {
            if (!BasisHelpers.CheckInstance(Instance))
            {
                enabled = false;
                return;
            }

            Instance = this;

            BasisNetworkLifeCycle.Initalize(this);
        }
        public async void OnDisable()
        {
          await  BasisNetworkLifeCycle.Destroy(this);
        }
        public static bool IsMainThread()
        {
            return Thread.CurrentThread.ManagedThreadId == mainThreadId;
        }
        // --- Public wrappers (nice, tiny face for UI buttons/inspector) ----
        public void Connect() => BasisNetworkConnection.Connect(Port, Ip, Password, IsHostMode);

        // Simulation ticks stay here; they call into players/driver
        public static void SimulateNetworkCompute()
        {
            if (!NetworkRunning) return;

            var snapshot = BasisNetworkPlayers.ReceiversSnapshot;
            for (int Index = 0; Index < snapshot.Length; Index++)
            {
                snapshot[Index]?.Compute();
            }

            BasisRemoteNetworkDriver.Compute();
            BasisNetworkProfiler.Update();
        }

        public static void SimulateNetworkApply()
        {
            if (!NetworkRunning) return;

            BasisRemoteNetworkDriver.Apply();

            var snapshot = BasisNetworkPlayers.ReceiversSnapshot;
            for (int Index = 0; Index < snapshot.Length; Index++)
            {
                snapshot[Index]?.Apply();
            }

            BasisAudioTransformDriver.BeginFrame();
            BasisAudioTransformDriver.EndFrame();
        }
        public static int GetServerTimeOffsetSeconds()
        {
            var serverTime = RemoteUtcTime();
            return DateTimeToSeconds(serverTime);
        }

        public static int GetServerTimeInMilliseconds()
        {
            DateTime serverTime = RemoteUtcTime();
            int secondsAhead = DateTimeToSeconds(serverTime);
            return secondsAhead;
        }

        /// <summary>Send a message intended for server-only handling.</summary>
        public static void SendServerSideMessage(byte[] payload, DeliveryMethod mode)
        {
            if (payload == null || payload.Length == 0)
            {
                BasisDebug.LogWarning("Attempted to send empty server-side message.");
                return;
            }

            var peer = LocalPlayerPeer;
            if (peer == null)
            {
                BasisDebug.LogError("Local NetPeer was null!", BasisDebug.LogTag.Networking);
                return;
            }

            peer.Send(payload, BasisNetworkCommons.ServerBoundChannel, mode);
            BasisNetworkProfiler.AddToCounter(BasisNetworkProfilerCounter.SceneData, payload.Length);
        }
        public static void SendServerSideDatabaseItem(string DatabaseID, ConcurrentDictionary<string, object> jsonPayload)
        {
            var peer = LocalPlayerPeer;
            if (peer == null)
            {
                BasisDebug.LogError("Local NetPeer was null!", BasisDebug.LogTag.Networking);
                return;
            }
            DatabasePrimativeMessage databasePrimativeMessage = new DatabasePrimativeMessage();
            databasePrimativeMessage.Name = DatabaseID;
            databasePrimativeMessage.jsonPayload = jsonPayload;
            NetDataWriter netDataWriter = new NetDataWriter();
            databasePrimativeMessage.Serialize(netDataWriter);
            BasisNetworkConnection.LocalPlayerPeer.Send(netDataWriter, BasisNetworkCommons.RequestStoreDatabaseChannel, DeliveryMethod.ReliableOrdered);
        }
        public static void RequestServerSideDatabaseItem(string DatabaseID)
        {
            var peer = LocalPlayerPeer;
            if (peer == null)
            {
                BasisDebug.LogError("Local NetPeer was null!", BasisDebug.LogTag.Networking);
                return;
            }
            DataBaseRequest DataBaseRequest = new DataBaseRequest();
            DataBaseRequest.DatabaseID = DatabaseID;
            NetDataWriter netDataWriter = new NetDataWriter();
            DataBaseRequest.Serialize(netDataWriter);
            BasisNetworkConnection.LocalPlayerPeer.Send(netDataWriter, BasisNetworkCommons.RequestStoreDatabaseChannel, DeliveryMethod.ReliableOrdered);
        }
        public static Action<DatabasePrimativeMessage> OnRequestServerSideDatabaseItem;

        public static long RemoteTimeDelta() => LocalPlayerPeer?.RemoteTimeDelta ?? 0;
        public static DateTime RemoteUtcTime() => LocalPlayerPeer?.RemoteUtcTime ?? DateTime.UtcNow;
        public static float TimeSinceLastPacket() => LocalPlayerPeer?.TimeSinceLastPacket ?? float.MaxValue;
        public static NetStatistics Statistics() => LocalPlayerPeer?.Statistics;

        public static int DateTimeToSeconds(DateTime dateTime)
        {
            var timeSpan = dateTime - DateTime.Now;
            return (int)timeSpan.TotalSeconds;
        }
    }
}
