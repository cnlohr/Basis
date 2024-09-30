using System;
using System.Collections.Generic;
using System.Linq;
using Basis.Scripts.BasisSdk;
using Basis.Scripts.Networking;
using Basis.Scripts.Networking.NetworkedPlayer;
using DarkRift;
using UnityEngine;
using UnityEngine.Scripting;

namespace HVR.Basis.Comms
{
    [AddComponentMenu("HVR.Basis/Comms/Streamed Avatar Feature")]
    [Preserve]
    public class StreamedAvatarFeature : MonoBehaviour
    {
        private const int HeaderBytes = 2;
        private const int SubHeaderBytes = HeaderBytes - 1;
        // 1/60 makes for a maximum encoded delta time of 4.25 seconds.
        private const float DeltaLocalIntToSeconds = 1 / 60f;
        // We use 254, not 255 (leaving 1 value out), because 254 divided by 2 is a round number, 127.
        // This makes the value of 0 in range [-1:1] encodable as 127.
        private const float EncodingRange = 254f;

        private const DeliveryMethod DeliveryMethod = DarkRift.DeliveryMethod.Sequenced;
        private const float TransmissionDeltaSeconds = 0.1f;

        internal BasisAvatar avatar;
        [SerializeField] public byte valueArraySize = 8; // Must not change after first enabled.

        private readonly Queue<StreamedAvatarFeaturePayload> _queue = new();
        private float[] current;
        private float[] previous;
        private float[] target;
        private float _deltaTime;
        private float _timeLeft;
        private bool _isOutOfTape;
        private bool _writtenThisFrame;
        private bool _isWearer;
        private byte _scopedIndex;

        public event InterpolatedDataChanged OnInterpolatedDataChanged;
        public delegate void InterpolatedDataChanged(float[] current);
        
        private void Awake()
        {
            previous ??= new float[valueArraySize];
            target ??= new float[valueArraySize];
            current ??= new float[valueArraySize];
        }

        private void OnDisable()
        {
            _writtenThisFrame = false;
        }

        public void Store(int index, float value)
        {
            current[index] = value;
        }

        /// Exposed for testing purposes.
        public void QueueEvent(StreamedAvatarFeaturePayload message)
        {
            _queue.Enqueue(message);
        }

        private void Update()
        {
            if (_isWearer) OnSender();
            else OnReceiver();
        }

        private void OnSender()
        {
            _timeLeft += Time.deltaTime;

            if (_timeLeft > TransmissionDeltaSeconds)
            {
                var toSend = new StreamedAvatarFeaturePayload
                {
                    DeltaTime = _timeLeft,
                    FloatValues = current // Not copied: Process this message immediately
                };

                EncodeAndSubmit(toSend);
                
                _timeLeft = 0;
            }
        }

        private void OnReceiver()
        {
            var timePassed = Time.deltaTime;
            _timeLeft -= timePassed;

            float totalQueueSeconds = 0;
            foreach (var payload in _queue) totalQueueSeconds += payload.DeltaTime;
            // Debug.Log($"Queue time is {totalQueueSeconds} seconds, size is {_queue.Count}");
            
            while (_timeLeft <= 0 && _queue.TryDequeue(out var eval))
            {
                // Debug.Log($"Unpacking delta {eval.DeltaTime} as {string.Join(',', eval.FloatValues.Select(f => $"{f}"))}");
                var effectiveDeltaTime = _queue.Count <= 5 || totalQueueSeconds < 0.2f
                    ? eval.DeltaTime
                    : (eval.DeltaTime * Mathf.Lerp(0.66f, 0.05f, Mathf.InverseLerp(0.1f, totalQueueSeconds, 4f)));
                
                _timeLeft += effectiveDeltaTime;
                previous = target;
                target = eval.FloatValues;
                _deltaTime = effectiveDeltaTime;
            }

            if (_timeLeft <= 0)
            {
                if (!_isOutOfTape)
                {
                    _writtenThisFrame = true;
                    for (var i = 0; i < valueArraySize; i++)
                    {
                        current[i] = target[i];
                    }

                    _isOutOfTape = true;
                }
                else
                {
                    _writtenThisFrame = false;
                }
                _timeLeft = 0;
            }
            else
            {
                _writtenThisFrame = true;
                var progression01 = 1 - Mathf.Clamp01(_timeLeft / _deltaTime);
                for (var i = 0; i < valueArraySize; i++)
                {
                    current[i] = Mathf.Lerp(previous[i], target[i], progression01);
                }
                _isOutOfTape = false;
            }

            if (_writtenThisFrame)
            {
                OnInterpolatedDataChanged?.Invoke(current);
            }
        }

        public void SetEncodingInfo(bool isWearer, byte scopedIndex)
        {
            _isWearer = isWearer;
            _scopedIndex = scopedIndex;
        }

        #region Network Payload

        public void OnPacketReceived(ArraySegment<byte> subBuffer)
        {
            if (!isActiveAndEnabled) return;
            
            if (TryDecode(subBuffer, out var result))
            {
                _queue.Enqueue(result);
            }
        }

        // Header:
        // - Scoped Index (1 byte)
        // - Sub-header:
        //   - Delta Time (1 byte)
        //   - Float Values (valueArraySize bytes)

        private void EncodeAndSubmit(StreamedAvatarFeaturePayload message)
        {
            var buffer = new byte[HeaderBytes + valueArraySize];
            buffer[0] = _scopedIndex;
            buffer[1] = (byte)(message.DeltaTime / DeltaLocalIntToSeconds);
            
            for (var i = 0; i < current.Length; i++)
            {
                buffer[HeaderBytes + i] = (byte)(message.FloatValues[i] * EncodingRange);
            }
            
            avatar.NetworkMessageSend(HVRAvatarComms.OurMessageIndex, buffer, DeliveryMethod);
        }

        private bool TryDecode(ArraySegment<byte> subBuffer, out StreamedAvatarFeaturePayload result)
        {
            if (subBuffer.Count != SubHeaderBytes + valueArraySize)
            {
                result = default;
                return false;
            }
            var floatValues = new float[subBuffer.Count - SubHeaderBytes];
            for (var i = SubHeaderBytes; i < subBuffer.Count; i++)
            {
                floatValues[i - SubHeaderBytes] = subBuffer.get_Item(i) / EncodingRange;
            }
            
            result = new StreamedAvatarFeaturePayload
            {
                DeltaTime = subBuffer.get_Item(0) * DeltaLocalIntToSeconds,
                FloatValues = floatValues
            };
            
            return true;
        }
        
        #endregion
    }
    [Preserve]
    public class StreamedAvatarFeaturePayload
    {
        public float DeltaTime;
        public float[] FloatValues;
    }
}