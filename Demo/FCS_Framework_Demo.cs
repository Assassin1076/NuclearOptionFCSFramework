using BepInEx;
using FCSAPI;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
namespace FCSSets;

[BepInPlugin("FCS_Framework_Demo", "FCS_Framework_Demo", "0.0.1")]
[BepInDependency("NuclearOptionFCSFramework", BepInDependency.DependencyFlags.HardDependency)]
public class FCSSets : BaseUnityPlugin
{
    private static string ConfigPath => Path.Combine(Paths.PluginPath, "CustomFCS", "FlightControlConfig.json");
    private static Dictionary<string, FlightControlParam> LoadedParams = new();
    private bool done = false;
    void Update()
    {
        if (done) return;
        var api = FCSAPI.FCSPatch_API.Instance;
        Logger.LogInfo($"API assembly seen: {typeof(FCSAPI.FCSPatch_API).Assembly.FullName}");
        if (api != null)
        {
            if (!api.IsReady()) return;
            Logger.LogInfo($"FCSPatch API Ready");
            LoadConfig();

            api.SetFCS_Global(LoadedParams["CI22"], AircraftType.CI22);
            api.SetFCS_Global(LoadedParams["TA30"], AircraftType.TA30);
            api.SetFCS_Global(LoadedParams["FS12"], AircraftType.FS12);
            api.SetFCS_Global(LoadedParams["FS20"], AircraftType.FS20);
            api.SetFCS_Global(LoadedParams["KR67"], AircraftType.KR67);
            api.SetFCS_Global(LoadedParams["EW25"], AircraftType.EW25);
            api.SetFCS_Global(LoadedParams["SFB81"], AircraftType.SFB81);

            done = true;
        }
        else
        {
            Logger.LogInfo($"Waiting for FCPatch API");
        }

    }
    private static void LoadConfig()
    {
        var api = FCSAPI.FCSPatch_API.Instance;
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                var defaultConfig = new Dictionary<string, FlightControlParam>
                {
                    ["CI22"] = api.GetDefaultFCS(AircraftType.CI22),
                    ["TA30"] = api.GetDefaultFCS(AircraftType.TA30),
                    ["FS12"] = api.GetDefaultFCS(AircraftType.FS12),
                    ["FS20"] = api.GetDefaultFCS(AircraftType.FS20),
                    ["KR67"] = api.GetDefaultFCS(AircraftType.KR67),
                    ["EW25"] = api.GetDefaultFCS(AircraftType.EW25),
                    ["SFB81"] = api.GetDefaultFCS(AircraftType.SFB81),
                };

                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented));
                Debug.Log($"[FlightControlMod] Generated default profile: {ConfigPath}");
            }

            string json = File.ReadAllText(ConfigPath);
            LoadedParams = JsonConvert.DeserializeObject<Dictionary<string, FlightControlParam>>(json);
            Debug.Log($"[FlightControlMod] Loaded {LoadedParams.Count} set(s) of FCS Parameters。");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FlightControlMod] Failed to loaded profile: {ex}");
        }
    }

}

