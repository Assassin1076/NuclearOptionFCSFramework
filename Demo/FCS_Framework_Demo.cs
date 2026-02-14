using BepInEx;
using BepInEx.Configuration;
using FCSAPI;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace FCSSets;

[BepInPlugin("fcs.assassin1076.FCSFrameworkDemo", "FCS_Framework_Demo", "0.0.5")]
[BepInDependency("NuclearOptionFCSFramework", BepInDependency.DependencyFlags.HardDependency)]
public class FCSSets : BaseUnityPlugin
{
    private static string ConfigPath => Path.Combine(Paths.PluginPath, "CustomFCS", "FlightControlConfig.json");
    private static Dictionary<string, FlightControlParam> LoadedParams = new();
    private bool done = false;

    private ConfigEntry<KeyboardShortcut> HotReload { get; set; }
    private ConfigEntry<KeyboardShortcut> ToggleUIKey;

    private bool showWindow = false;
    private Rect windowRect = new Rect(100, 100, 420, 600);

    private Aircraft currentAircraft;
    private Aircraft lastAircraft;

    private FlightControlParam currentParam;
    private bool paramInitialized = false;

    public static Dictionary<AircraftType, string> knownNamesDict = new Dictionary<AircraftType, string>{
        {AircraftType.CI22, "CI-22" },
        {AircraftType.TA30, "T/A-30" },
        {AircraftType.A19, "A-19" },
        {AircraftType.FS12, "FS-12" },
        {AircraftType.FS20, "FS-20" },
        {AircraftType.KR67, "KR-67" },
        {AircraftType.EW25, "EW-25" },
        {AircraftType.SFB81, "SFB-81" },
    };

    // 是否自动实时应用
    private bool liveApply = true;

    // UI 滚动
    private Vector2 scrollPos;

    void Awake()
    {
        ToggleUIKey = Config.Bind(
        "Hotkeys",
        "Toggle FCS UI",
        new KeyboardShortcut(KeyCode.F8));
        HotReload = Config.Bind("Hotkeys", "Hot Reload FCS Params", new KeyboardShortcut(KeyCode.L, KeyCode.LeftControl));
    }
    void Update()
    {
        GUIUpdater();
        if (done && !HotReload.Value.IsDown()) return;
        var api = FCSAPI.FCSPatch_API.Instance;
        var VEUapi = FCSAPI.FCSPatch_API.VEU_Instance;
        Logger.LogInfo($"API assembly seen: {typeof(FCSAPI.FCSPatch_API).Assembly.FullName}");
        if (api != null)
        {
            if (!api.IsReady()) return;
            Logger.LogInfo($"FCSPatch API Ready");
            LoadConfig();

            api.SetFCS_Global(LoadedParams["CI22"], AircraftType.CI22);
            api.SetFCS_Global(LoadedParams["TA30"], AircraftType.TA30);
            api.SetFCS_Global(LoadedParams["A19"], AircraftType.A19);
            api.SetFCS_Global(LoadedParams["FS12"], AircraftType.FS12);
            api.SetFCS_Global(LoadedParams["FS20"], AircraftType.FS20);
            api.SetFCS_Global(LoadedParams["KR67"], AircraftType.KR67);
            api.SetFCS_Global(LoadedParams["EW25"], AircraftType.EW25);
            api.SetFCS_Global(LoadedParams["SFB81"], AircraftType.SFB81);
            VEUapi.SetVectoringMaxAirSpeed_Global(AircraftType.FS12, 9999f);
            VEUapi.SetVectoringMaxAirSpeed_Global(AircraftType.KR67, 9999f);
            done = true;
        }
        else
        {
            Logger.LogInfo($"Waiting for FCPatch API");
        }

    }

    void GUIUpdater()
    {
        if (ToggleUIKey.Value.IsDown())
        {
            showWindow = !showWindow;
        }
            

        currentAircraft = FCS_param_helpers.GetCurrentAircraft();

        if (currentAircraft == null)
        {
            if (paramInitialized)
            {
                paramInitialized = false;
                lastAircraft = null;
            }
            return;
        }

        if (currentAircraft != lastAircraft)
        {
            InitializeParamForAircraft(currentAircraft);
            lastAircraft = currentAircraft;
        }
    }

    void OnGUI()
    {
        if (!showWindow) return;

        windowRect = GUILayout.Window(
            10761076,          // 唯一 ID
            windowRect,
            DrawWindow,
            "FCS Realtime Tuning"
        );
    }

    void DrawWindow(int id)
    {
        GUILayout.BeginVertical();

        //顶部信息区
        GUILayout.Label("Flight Control System");
        GUILayout.Space(5);

        liveApply = GUILayout.Toggle(liveApply, " Live Apply");

        GUILayout.Space(4);

        scrollPos = GUILayout.BeginScrollView(scrollPos);

        DrawFloatSlider("Alpha Limiter", ref currentParam.alphaLimiter_S, 0f, 180f);
        DrawFloatSlider("Positive G Limit", ref currentParam.gLimitPositive_S, 0f, 15f);
        DrawFloatSlider("Direct Control Factor", ref currentParam.directControlFactor, 0f, 1f);
        DrawFloatSlider("Max Pitch Angular Vel", ref currentParam.maxPitchAngularVel, 0f, 10f);
        DrawFloatSlider("Corner Speed", ref currentParam.cornerSpeed, 0f, 1000f);
        DrawFloatSlider("Yaw Damper Limit", ref currentParam.yawDamperLimit_Additional, 0f, 1f);
        DrawFloatSlider("postStallManeuverSpeed", ref currentParam.postStallManeuverSpeed, 0, 1500f);
        DrawFloatSlider("pidTransitionSpeed", ref currentParam.pidTransitionSpeed, 0, 200f);
        DrawFloatSlider("pitchAdjusterLimitSlow", ref currentParam.pitchAdjusterLimitSlow, 0, 1f);
        DrawFloatSlider("pFactorSlow", ref currentParam.pFactorSlow, 0, 10f);
        DrawFloatSlider("dFactorSlow", ref currentParam.dFactorSlow, 0, 10f);
        DrawFloatSlider("pitchAdjusterLimitFast", ref currentParam.pitchAdjusterLimitFast, 0, 1f);
        DrawFloatSlider("pFactorFast", ref currentParam.pFactorFast, 0, 10f);
        DrawFloatSlider("dFactorFast", ref currentParam.dFactorFast, 0, 10f);
        DrawFloatSlider("rollTrimRate", ref currentParam.rollTrimRate, 0, 1f);
        DrawFloatSlider("rollTrimLimit", ref currentParam.rollTrimLimit, 0, 1f);
        DrawFloatSlider("yawTightness", ref currentParam.yawTightness, 0, 10f);
        DrawFloatSlider("rollTightness", ref currentParam.rollTightness, 0, 10f);

        GUILayout.EndScrollView();

        GUILayout.Space(8);

        //底部按钮
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("Apply"))
        {
            FCS_param_helpers.ApplyToAircraft(currentParam, currentAircraft);
        }

        if (GUILayout.Button("Reset"))
        {
            FCS_param_helpers.ResetToDefault(currentAircraft);
        }

        if (GUILayout.Button("Save"))
            SaveCurrentToJson();
        if (GUILayout.Button("Load"))
            LoadCurrentFromJson();

        GUILayout.EndHorizontal();

        GUILayout.EndVertical();

        GUI.DragWindow();
    }

    void DrawFloatSlider(string label, ref float value, float min, float max)
    {
        GUILayout.Label($"{label}: {value:F2}");

        GUILayout.BeginHorizontal();

        value = GUILayout.HorizontalSlider(value, min, max);

        string text = GUILayout.TextField(value.ToString("F2"), GUILayout.Width(60));
        if (float.TryParse(text, out float parsed))
        {
            value = Mathf.Clamp(parsed, min, max);
        }

        GUILayout.EndHorizontal();

        if (liveApply)
        {
            FCS_param_helpers.ApplyToAircraft(currentParam, currentAircraft);
        }

        GUILayout.Space(6);
    }

    void InitializeParamForAircraft(Aircraft aircraft)
    {
        var api = FCSAPI.FCSPatch_API.Instance;
        if (api == null || !api.IsReady()) return;

        currentParam = api.GetFCS(aircraft.GetControlsFilter().gameObject);
        paramInitialized = true;

        Logger.LogInfo($"[FCS UI] Aircraft changed: {aircraft.name}");
    }

    void LoadCurrentFromJson()
    {
        if (!paramInitialized) return;

        LoadConfig();
        var type = FCS_param_helpers.GetAircraftType(currentAircraft);
        if (type == null) return;

        currentParam = LoadedParams[type.ToString()];

        var api = FCSAPI.FCSPatch_API.Instance;
        if (api == null || !api.IsReady()) return;
        if (currentAircraft == null) return;

        api.SetFCS(currentParam, currentAircraft.GetComponent<Aircraft>()?.GetControlsFilter().gameObject);
    }

    void SaveCurrentToJson()
    {
        if (!paramInitialized) return;

        var type = FCS_param_helpers.GetAircraftType(currentAircraft);
        if (type == null) return;

        if (!LoadedParams.ContainsKey(type.ToString()))
            LoadedParams[type.ToString()] = currentParam;
        else
            LoadedParams[type.ToString()] = currentParam;

        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);

        File.WriteAllText(
            ConfigPath,
            JsonConvert.SerializeObject(
                LoadedParams,
                Formatting.Indented
            )
        );

        Logger.LogInfo($"[FCS UI] Saved FCS for {type}");
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
                    ["A19"] = api.GetDefaultFCS(AircraftType.A19),
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

    public static class FCS_param_helpers
    {
        public static Aircraft GetCurrentAircraft()
        {
            var hud = SceneSingleton<CombatHUD>.i;
            if (hud == null) return null;
            if (hud.aircraft == null) return null;

            return hud.aircraft;
        }
        public static AircraftType? GetAircraftType(Aircraft aircraft)
        {
            string name = aircraft?.definition.code;
            foreach (AircraftType type in Enum.GetValues(typeof(AircraftType)))
            {
                if (name.Contains(knownNamesDict[type].ToString()))
                    return type;
            }

            return null;
        }
        public static void ApplyToAircraft(FlightControlParam currentParam, Aircraft currentAircraft)
        {

            var api = FCSAPI.FCSPatch_API.Instance;
            if (api == null || !api.IsReady()) return;

            api.SetFCS(currentParam, currentAircraft.GetControlsFilter().gameObject);
        }
        public static void ResetToDefault(Aircraft currentAircraft)
        {

            var api = FCSAPI.FCSPatch_API.Instance;
            if (api == null || !api.IsReady()) return;

            FlightControlParam currentParam = api.GetDefaultFCS(currentAircraft.GetControlsFilter().gameObject);
            api.SetFCS(currentParam, currentAircraft.GetControlsFilter().gameObject);
        }
    }
}

