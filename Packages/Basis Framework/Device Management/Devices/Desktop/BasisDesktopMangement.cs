using Basis.Scripts.BasisSdk.Players;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace Basis.Scripts.Device_Management.Devices.Desktop
{
    [Serializable]
    public class BasisDesktopManagement : BasisBaseTypeManagement
    {
        public BasisAvatarEyeInput BasisAvatarEyeInput;
        public override async Task BeginLoadSDK()
        {
            if (BasisAvatarEyeInput == null)
            {
                BasisDeviceManagement.Instance.SetCameraRenderState(false);
                BasisDeviceManagement.Instance.CurrentMode = "Desktop";
                GameObject gameObject = new GameObject("Desktop Eye");
                if (BasisLocalPlayer.Instance != null)
                {
                    gameObject.transform.parent = BasisLocalPlayer.Instance.LocalBoneDriver.transform;
                }
                BasisAvatarEyeInput = gameObject.AddComponent<BasisAvatarEyeInput>();
              await  BasisAvatarEyeInput.Initalize("Desktop Eye", nameof(BasisDesktopManagement));
                BasisDeviceManagement.Instance.TryAdd(BasisAvatarEyeInput);
            }
        }

        public override async Task StartSDK()
        {
        }
        public override void StopSDK()
        {
            if (BasisDeviceManagement.Instance.TryFindBasisBaseTypeManagement("SimulateXR", out List<BasisBaseTypeManagement> Matched))
            {
                foreach (var m in Matched)
                {
                    m.StopSDK();
                }
            }
            BasisDeviceManagement.Instance.RemoveDevicesFrom(nameof(BasisDesktopManagement), "Desktop Eye");
        }

        public override string Type()
        {
            return "Desktop";
        }
    }
}