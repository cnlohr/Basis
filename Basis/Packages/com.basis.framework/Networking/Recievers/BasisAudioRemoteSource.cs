using Basis.Scripts.Device_Management;
using UnityEngine;
using UnityEngine.AddressableAssets;
namespace Basis.Scripts.Networking.Receivers
{
    public static class BasisAudioRemoteSource
    {
        const string AudioSource = "Packages/com.basis.sdk/Prefabs/Players/AudioSource.prefab";
        private static GameObject LoadableAudioSource;
        public static UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<GameObject> Loadable;
        public static void Initalize()
        {
            Loadable = Addressables.LoadAssetAsync<GameObject>(AudioSource);
            LoadableAudioSource = Loadable.WaitForCompletion();
        }
        public static void DeInitalize()
        {
            Loadable.Release();
        }
        public static GameObject RequestAudio()
        {
            GameObject Object = GameObject.Instantiate(LoadableAudioSource, BasisDeviceManagement.Instance.transform);
            return Object;
        }
        public static void Return(GameObject obj)
        {
            GameObject.Destroy(obj);
        }
    }
}
