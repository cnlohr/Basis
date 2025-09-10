using Basis.Scripts.BasisSdk.Players;
using Basis.Scripts.Device_Management;
using Basis.Scripts.Device_Management.Devices.Headless;
using Basis.Scripts.Drivers;
using Basis.Scripts.Networking;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
public class BasisHeadlessManagement : BasisBaseTypeManagement
{
    public BasisHeadlessInput BasisHeadlessInput;
    public static string Password = "default_password";
    public static string Ip = "localhost";
    public static int Port = 4296;
    private void OnSceneLoadeded(Scene arg0, Scene arg1)
    {
        RemoveAllMaterialTextures();
        RemoveAllReflectionProbes();
        RemoveAllText();
        Resources.UnloadUnusedAssets();
    }
    private void RemoveAllMaterialTextures()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        HashSet<Material> processedMats = new HashSet<Material>();

        foreach (Renderer renderer in renderers)
        {
            foreach (Material mat in renderer.materials)
            {
                if (mat == null || processedMats.Contains(mat))
                    continue;

                ShaderUtilSafe.ClearAllKnownTextures(mat);
                processedMats.Add(mat);
            }
        }

        Debug.Log("All textures cleared from all materials.");
    }

    public static class ShaderUtilSafe
    {
        // Commonly used texture property names across standard and URP shaders
        private static readonly string[] commonTextureProps = {
        "_MainTex", "_BaseMap", "_BumpMap", "_EmissionMap", "_MetallicGlossMap",
        "_ParallaxMap", "_OcclusionMap", "_DetailMask", "_DetailAlbedoMap", "_DetailNormalMap"
    };

        public static void ClearAllKnownTextures(Material material)
        {
            foreach (string prop in commonTextureProps)
            {
                if (material.HasProperty(prop))
                {
                    material.SetTexture(prop, null);
                }
            }
        }
    }
    private void RemoveAllReflectionProbes()
    {
        ReflectionProbe[] probes = FindObjectsByType<ReflectionProbe>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (ReflectionProbe probe in probes)
        {
            Destroy(probe.gameObject);
        }

        Debug.Log("All reflection probes removed from scene.");
    }
    private void RemoveAllText()
    {
        Canvas[] Canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas Canvas in Canvases)
        {
            Destroy(Canvas);
        }

        Debug.Log("All reflection probes removed from scene.");
    }
    public static void LoadOrCreateConfigXml()
    {
        string filePath = Path.Combine(Application.dataPath, "config.xml");
        if (!File.Exists(filePath))
        {
            var defaultConfig = new XElement("Configuration",
                new XElement("Password", Password),
                new XElement("Ip", Ip),
                new XElement("Port", Port)
            );
            new XDocument(defaultConfig).Save(filePath);
            return;
        }

        var doc = XDocument.Load(filePath);
        var root = doc.Element("Configuration");
        if (root == null) return;

        Password = root.Element("Password")?.Value ?? Password;
        Ip = root.Element("Ip")?.Value ?? Ip;
        Port = int.TryParse(root.Element("Port")?.Value, out var p) ? p : Port;
    }
    public async void ConnectToNetwork()
    {
        LoadOrCreateConfigXml();
        await CreateAssetBundle();
        BasisNetworkManagement.Instance.Ip = Ip;
        BasisNetworkManagement.Instance.Password = Password;
        BasisNetworkManagement.Instance.IsHostMode = false;
        BasisNetworkManagement.Instance.Port = (ushort)Port;
        BasisNetworkManagement.Instance.Connect();
        BasisDebug.Log("connecting to default");
    }
    public async Task CreateAssetBundle()
    {
        if (BundledContentHolder.Instance.UseSceneProvidedHere)
        {
            BasisDebug.Log("using Local Asset Bundle or Addressable", BasisDebug.LogTag.Networking);
            if (BundledContentHolder.Instance.UseAddressablesToLoadScene)
            {
                await BasisSceneLoad.LoadSceneAddressables(BundledContentHolder.Instance.DefaultScene.BasisRemoteBundleEncrypted.RemoteBeeFileLocation);
            }
            else
            {
                await BasisSceneLoad.LoadSceneAssetBundle(BundledContentHolder.Instance.DefaultScene);
            }
        }
    }
    public override void StartSDK()
    {
#if UNITY_SERVER
        if (BasisHeadlessInput == null)
        {
            GameObject gameObject = new GameObject("Headless Eye");
            if (BasisLocalPlayer.Instance != null)
            {
                gameObject.transform.parent = BasisLocalPlayer.Instance.transform;
            }
            BasisHeadlessInput = gameObject.AddComponent<BasisHeadlessInput>();
            BasisHeadlessInput.Initialize("Desktop Eye", nameof(Basis.Scripts.Device_Management.Devices.Headless.BasisHeadlessInput));
            BasisDeviceManagement.Instance.TryAdd(BasisHeadlessInput);
        }
        BasisDebug.Log(nameof(StartSDK), BasisDebug.LogTag.Device);
        BasisLocalPlayer.Instance.DisplayName = GenerateRandomPlayerName();
        BasisLocalPlayer.Instance.SetSafeDisplayname();
        if (BasisNetworkManagement.Instance != null)
        {
            ConnectToNetwork();
        }
        else
        {
            BasisNetworkManagement.OnEnableInstanceCreate += ConnectToNetwork;
        }
        SceneManager.activeSceneChanged += OnSceneLoadeded;
#endif
        BasisDebug.Log(nameof(StartSDK), BasisDebug.LogTag.Device);
    }
    public override void StopSDK()
    {
        BasisDebug.Log(nameof(StopSDK), BasisDebug.LogTag.Device);
    }
    public override bool IsDeviceBootable(string BootRequest)
    {
        if (BootRequest == "Headless")
        {
            return true;
        }
        return false;
    }
    public static string[] adjectives = { "Swift", "Brave", "Clever", "Fierce", "Nimble", "Silent", "Bold", "Lucky", "Strong", "Mighty", "Sneaky", "Fearless", "Wise", "Vicious", "Daring" };
    public static string[] nouns = { "Warrior", "Hunter", "Mage", "Rogue", "Paladin", "Shaman", "Knight", "Archer", "Monk", "Druid", "Assassin", "Sorcerer", "Ranger", "Guardian", "Berserker" };
    public static string[] titles = { "the Swift", "the Bold", "the Silent", "the Brave", "the Fierce", "the Wise", "the Protector", "the Shadow", "the Flame", "the Phantom" };
    // Thread-safe unique player name generation
    public static string[] animals = { "Wolf", "Tiger", "Eagle", "Dragon", "Lion", "Bear", "Hawk", "Panther", "Raven", "Serpent", "Fox", "Falcon" };

    // Colors with their corresponding names and hex codes for Unity's Rich Text
    public static (string Name, string Hex)[] colors =
    {
            ("Red", "#FF0000"),
            ("Blue", "#0000FF"),
            ("Green", "#008000"),
            ("Yellow", "#FFFF00"),
            ("Black", "#000000"),
            ("White", "#FFFFFF"),
            ("Silver", "#C0C0C0"),
            ("Golden", "#FFD700"),
            ("Crimson", "#DC143C"),
            ("Azure", "#007FFF"),
            ("Emerald", "#50C878"),
            ("Amber", "#FFBF00")
        };
    public static string GenerateRandomPlayerName()
    {
       System.Random random = new System.Random();

        // Randomly select one element from each array
        string adjective = adjectives[random.Next(adjectives.Length)];
        string noun = nouns[random.Next(nouns.Length)];
        string title = titles[random.Next(titles.Length)];
        (string Name, string Hex) color = colors[random.Next(colors.Length)];
        string animal = animals[random.Next(animals.Length)];

        // Combine elements with rich text for the color
        string colorText = $"<color={color.Hex}>{color.Name}</color>";
        string generatedName = $"{adjective}{noun} {title} of the {colorText} {animal}";

        // Ensure uniqueness by appending a counter
        return $"{generatedName}";
    }
}
