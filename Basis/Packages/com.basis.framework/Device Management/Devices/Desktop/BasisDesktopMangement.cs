using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Drivers;
using System;
using UnityEngine;

namespace Basis.Scripts.Device_Management.Devices.Desktop
{
    [Serializable]
    public class BasisDesktopManagement : BasisBaseTypeManagement
    {
        public BasisAvatarEyeInput BasisAvatarEyeInput;
        public const string DesktopEye = "Desktop Eye";
        public override void StartSDK()
        {
            if (BasisAvatarEyeInput == null)
            {
                BasisLocalCameraDriver.AllowXRRenderering(false);
                GameObject gameObject = new GameObject(DesktopEye);
                if (BasisLocalPlayer.Instance != null)
                {
                    gameObject.transform.parent = BasisLocalPlayer.Instance.transform;
                }
                BasisAvatarEyeInput = gameObject.AddComponent<BasisAvatarEyeInput>();
                BasisAvatarEyeInput.Initialize(DesktopEye, nameof(BasisDesktopManagement));
                BasisDeviceManagement.Instance.TryAdd(BasisAvatarEyeInput);
            }
            BasisCursorManagement.LockCursor(nameof(BasisAvatarEyeInput));
        }
        public override void StopSDK()
        {
            BasisDeviceManagement.Instance.RemoveDevicesFrom(nameof(BasisDesktopManagement), DesktopEye);
            if(BasisAvatarEyeInput != null)
            {
                GameObject.Destroy(BasisAvatarEyeInput);
            }
            BasisAvatarEyeInput.Instance = null;
            BasisAvatarEyeInput = null;
        }
        public override bool IsDeviceBootable(string BootRequest)
        {
            if (BootRequest == BasisConstants.Desktop)
            {
                return true;
            }
            return false;
        }
    }
}
