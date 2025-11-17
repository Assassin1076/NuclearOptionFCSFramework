using BepInEx;
using FCSAPI;
using HarmonyLib;
using NuclearOption.DebugScripts;
using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
namespace FCPatch;

[BepInPlugin("NuclearOptionFCSFramework", "FCS_Framework", "0.0.1")]
public class FlightControlPatch : BaseUnityPlugin, FCSModifier, VectorEngineUnlocker
{
    private readonly string[] targetPrefabNames =
    {
        "cockpit_F",
        "cockpit",
        "Fighter1",
        "engine_R",
        "engine_L",
        "engine",
    };
    private ControlsFilter FCS_CI22;
    private ControlsFilter FCS_TA30;
    private ControlsFilter FCS_FS12;
    private ControlsFilter FCS_FS20;
    private ControlsFilter FCS_KR67;
    private ControlsFilter FCS_EW25;
    private ControlsFilter FCS_SFB81;

    private Turbojet Engine_FS12;
    private Turbojet Engine_KR67_L;
    private Turbojet Engine_KR67_R;

    private FlightControlParam Default_FCS_CI22;
    private FlightControlParam Default_FCS_TA30;
    private FlightControlParam Default_FCS_FS12;
    private FlightControlParam Default_FCS_FS20;
    private FlightControlParam Default_FCS_KR67;
    private FlightControlParam Default_FCS_EW25;
    private FlightControlParam Default_FCS_SFB81;

    private bool done = false;
    void Awake()
    {
        var harmony = new Harmony("NuclearOptionFCSFramework");
        harmony.PatchAll();
        Logger.LogInfo("FlyByWire yaw patch applied.");
        FCSPatch_API.Instance = this;
        Logger.LogInfo($"FCS API assigned: {this.GetType().Assembly.FullName}");
        FCSPatch_API.VEU_Instance = this;
        Logger.LogInfo($"VectorEngineUnlocker API assigned: {this.GetType().Assembly.FullName}");
    }
    void Update()
    {
        if (done) return;
        var allPrefabs = Resources.FindObjectsOfTypeAll<GameObject>();
        var matchedPrefabs = allPrefabs
            .Where(go => targetPrefabNames.Any(t => go.name.Equals(t, StringComparison.OrdinalIgnoreCase)))
            .ToList();
        if (matchedPrefabs.Count == 0)
        {
            Logger.LogInfo("Waiting for target prefabs to load...");
            return;
        }
        foreach (var prefab in matchedPrefabs)
        {
            ModifyPrefab(prefab);
        }

        Logger.LogInfo($"Searched {matchedPrefabs.Count} target prefabs.");
        done = true;
    }

    private void ModifyPrefab(GameObject prefab)
    {
        var flightcontrol = prefab.GetComponent<ControlsFilter>();
        var turbojet = prefab.GetComponent<Turbojet>();
        if (turbojet != null)
        {
            if (prefab.name == "Fighter1") //FS12的引擎
            {
                Logger.LogInfo($"Engine of FS-12 found.");
                Engine_FS12 = turbojet;
            }
            else if (prefab.name == "engine_L" || prefab.name == "engine_R")
            {
                //任何具有矢量推进功能的引擎都有一个独特的非零映射向量thrustVectoring，利用该值定位KR的引擎
                Type jetType = turbojet.GetType();
                FieldInfo maxvectorspeedField = jetType.GetField("thrustVectoring", BindingFlags.NonPublic | BindingFlags.Instance);
                if (maxvectorspeedField != null)
                {
                    var v = maxvectorspeedField.GetValue(turbojet);
                    Vector3 known1 = new Vector3(15, 0, 15);
                    Vector3 known2 = new Vector3(15, 0, -15);
                    if (v.ToString() == known1.ToString() || v.ToString() == known2.ToString())
                    {
                        Logger.LogInfo($"Engine of KR-67 found.");
                        if (prefab.name == "engine_L") Engine_KR67_L = turbojet;
                        else Engine_KR67_R = turbojet;
                    }
                }
            }
        }
        if (flightcontrol != null)
        {
            Type fctype = flightcontrol.GetType();

            /*
            考虑到游戏版本更迭时，FCS的名称，参数等都有可能变化，因此此处采用if-else嵌套以保证后续修改的方便，我知道这很蠢，但是先这样吧（逃
            */

            FieldInfo minSpeed = fctype.GetField("minSpeed", BindingFlags.NonPublic | BindingFlags.Instance);
            if (minSpeed != null)
            {
                string minSpeedStr = minSpeed.GetValue(flightcontrol).ToString();
                if (minSpeedStr == 5f.ToString())
                {
                    Logger.LogInfo($"FCS of FS-12 found");
                    FCS_FS12 = flightcontrol;
                    var custom = prefab.AddComponent<FCSPatchData>();
                    Default_FCS_FS12 = GetFlightControParam(prefab);
                    custom.Aircraft_Type = "FS12";

                }


                else if (minSpeedStr == 25f.ToString() && prefab.name == "cockpit")
                {
                    Logger.LogInfo($"FCS of SFB-81 found");
                    FCS_SFB81 = flightcontrol;
                    var custom = prefab.AddComponent<FCSPatchData>();
                    Default_FCS_SFB81 = GetFlightControParam(prefab);
                    custom.Aircraft_Type = "SFB81";

                }


                else if (minSpeedStr == 25f.ToString() && prefab.name == "cockpit_F")
                {
                    Logger.LogInfo($"FCS of TA-30 found");
                    FCS_TA30 = flightcontrol;
                    var custom = prefab.AddComponent<FCSPatchData>();
                    Default_FCS_TA30 = GetFlightControParam(prefab);
                    custom.Aircraft_Type = "TA30";

                }


                else if (minSpeedStr == 10f.ToString() && prefab.name == "cockpit_F")
                {
                    Logger.LogInfo($"FCS of CI-22 found");
                    FCS_CI22 = flightcontrol;
                    var custom = prefab.AddComponent<FCSPatchData>();
                    Default_FCS_CI22 = GetFlightControParam(prefab);
                    custom.Aircraft_Type = "CI22";
                }


                else if (minSpeedStr == 0f.ToString() && prefab.name == "cockpit_F")
                {
                    Logger.LogInfo($"FCS of EW-25 found");
                    FCS_EW25 = flightcontrol;
                    var custom = prefab.AddComponent<FCSPatchData>();
                    Default_FCS_EW25 = GetFlightControParam(prefab);
                    custom.Aircraft_Type = "EW-25";

                }


                else if (minSpeedStr == 0f.ToString() && prefab.name == "cockpit")
                {
                    FieldInfo flyByWireField = typeof(ControlsFilter)
                        .GetField("flyByWire", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

                    if (flyByWireField == null)
                    {
                        Debug.LogError("Cannot find field 'flyByWire' in ControlsFilter!");
                        return;
                    }
                    object flyByWireInstance = flyByWireField.GetValue(flightcontrol);
                    if (flyByWireInstance == null)
                    {
                        Debug.LogWarning("flyByWire is null!");
                        return;
                    }
                    Type flyByWireType = flyByWireInstance.GetType();
                    FieldInfo aoaField = flyByWireType.GetField("alphaLimiter", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (aoaField == null)
                    {
                        Debug.LogError("Cannot find field 'alphaLimiter' in FlyByWire!");
                        return;
                    }


                    if (aoaField.GetValue(flyByWireInstance).ToString() == 27f.ToString())
                    {
                        Logger.LogInfo($"FCS of KR-67 found");
                        FCS_KR67 = flightcontrol;
                        var custom = prefab.AddComponent<FCSPatchData>();
                        Default_FCS_KR67 = GetFlightControParam(prefab);
                        custom.Aircraft_Type = "KR67";

                    }


                    else if (aoaField.GetValue(flyByWireInstance).ToString() == 25f.ToString())
                    {
                        Logger.LogInfo($"FCS of FS-20 found");
                        FCS_FS20 = flightcontrol;
                        var custom = prefab.AddComponent<FCSPatchData>();
                        Default_FCS_FS20 = GetFlightControParam(prefab);
                        custom.Aircraft_Type = "FS20";

                    }
                }

            }
        }
    }

    private static void applyFlightControParam(FlightControlParam param, ControlsFilter fc)//用于修改飞机飞控参数
    {
        float[] singleArray1 = new float[15];
        singleArray1[0] = param.directControlFactor;
        singleArray1[1] = param.maxPitchAngularVel;
        singleArray1[2] = param.cornerSpeed;
        singleArray1[3] = param.postStallManeuverSpeed;
        singleArray1[4] = param.pidTransitionSpeed;
        singleArray1[5] = param.pitchAdjusterLimitSlow;
        singleArray1[6] = param.pFactorSlow;
        singleArray1[7] = param.dFactorSlow;
        singleArray1[8] = param.pitchAdjusterLimitFast;
        singleArray1[9] = param.pFactorFast;
        singleArray1[10] = param.dFactorFast;
        singleArray1[11] = param.rollTrimRate;
        singleArray1[12] = param.rollTrimLimit;
        singleArray1[13] = param.yawTightness;
        singleArray1[14] = param.rollTightness;
        fc.SetFlyByWireParameters(true, singleArray1);
        FCSPatchData data =  fc.gameObject.GetComponent<FCSPatchData>();
        if(data == null)
        {
            Debug.LogError("Warning: Error when trying to set additional params");
            data = fc.gameObject.AddComponent<FCSPatchData>();
        }
        data.yawDamperLimit = param.yawDamperLimit_Additional;
        FieldInfo flyByWireField = typeof(ControlsFilter)
            .GetField("flyByWire", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (flyByWireField == null)
        {
            Debug.LogError("Cannot find field 'flyByWire' in ControlsFilter!");
            return;
        }
        object flyByWireInstance = flyByWireField.GetValue(fc);
        if (flyByWireInstance == null)
        {
            Debug.LogWarning("flyByWire is null!");
            return;
        }
        Type flyByWireType = flyByWireInstance.GetType();
        FieldInfo aoaField = flyByWireType.GetField("alphaLimiter", BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo gField = flyByWireType.GetField("gLimitPositive", BindingFlags.NonPublic | BindingFlags.Instance);
        if (aoaField == null)
        {
            Debug.LogError("Cannot find field 'alphaLimiter' in FlyByWire!");
            return;
        }
        if (gField == null)
        {
            Debug.LogError("Cannot find field 'alphaLimiter' in FlyByWire!");
            return;
        }
        aoaField.SetValue(flyByWireInstance, param.alphaLimiter_S);
        gField.SetValue(flyByWireInstance, param.gLimitPositive_S);
    }

    private void SetVector(Turbojet target, float v)
    {
        Type jetType = target.GetType();
        FieldInfo maxvectorspeedField = jetType.GetField("thrustVectoringMaxAirspeed", BindingFlags.NonPublic | BindingFlags.Instance);
        if (maxvectorspeedField != null) maxvectorspeedField.SetValue(target, v);
    }


    private static FlightControlParam GetFlightControParam(GameObject target)
    {
        ControlsFilter fc = target.GetComponent<ControlsFilter>();
        FCSPatchData data = target.GetComponent<FCSPatchData>();
        FlightControlParam param = new FlightControlParam();
        if (fc == null || data == null)
        {
            Debug.LogError("Error while obtaining FCS data");
            return param;
        }
        

        FieldInfo flyByWireField = typeof(ControlsFilter)
            .GetField("flyByWire", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

        if (flyByWireField == null)
        {
            Debug.LogError("Cannot find field 'flyByWire' in ControlsFilter!");
            return param;
        }
        object flyByWireInstance = flyByWireField.GetValue(fc);
        if (flyByWireInstance == null)
        {
            Debug.LogWarning("flyByWire is null!");
            return param;
        }

        Type flyByWireType = flyByWireInstance.GetType();
        var fields = typeof(FlightControlParam).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var f in fields)
        {
            string sourceFieldName;
            if (f.Name.Contains("_Additional"))
            {

                continue;
            }
            if (f.Name == "alphaLimiter_S" || f.Name == "gLimitPositive_S")
            {
                sourceFieldName = f.Name.Replace("_S", "");
            }
            else
            {
                sourceFieldName = f.Name;
            }

            FieldInfo sourceField = flyByWireType.GetField(sourceFieldName, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
            if (sourceField == null)
            {
                UnityEngine.Debug.LogWarning($"[FlightControlParamExtractor] Field not found in FlyByWire: {sourceFieldName}");
                continue;
            }

            object value = sourceField.GetValue(flyByWireInstance);
            if (value is float fVal)
            {
                f.SetValueDirect(__makeref(param), fVal);
            }
            else
            {
                UnityEngine.Debug.LogWarning($"[FlightControlParamExtractor] Field {sourceFieldName} type mismatch.");
            }
        }
        param.yawDamperLimit_Additional = data.yawDamperLimit;
        return param;
    }

    public int SetFCS(FlightControlParam Param, GameObject Target)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        var FCS = Target.GetComponent<ControlsFilter>();
        if (FCS == null)
        {
            throw new NullReferenceException("Target do not have a FCS Component");
        }
        applyFlightControParam(Param, FCS);
        return 0;
    }

    public FlightControlParam GetFCS(GameObject Target)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        var FCS = Target.GetComponent<ControlsFilter>();
        if (FCS == null)
        {
            throw new NullReferenceException("Target do not have a FCS Component");
        }
        return GetFlightControParam(Target);
    }

    public int SetFCS_Global(FlightControlParam Param, AircraftType Target)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        switch (Target)
        {
            case AircraftType.CI22:
                applyFlightControParam(Param, FCS_CI22);
                break;
            case AircraftType.TA30:
                applyFlightControParam(Param, FCS_TA30);
                break;
            case AircraftType.FS12:
                applyFlightControParam(Param, FCS_FS12);
                break;
            case AircraftType.FS20:
                applyFlightControParam(Param, FCS_FS20);
                break;
            case AircraftType.KR67:
                applyFlightControParam(Param, FCS_KR67);
                break;
            case AircraftType.EW25:
                applyFlightControParam(Param, FCS_EW25);
                break;
            case AircraftType.SFB81:
                applyFlightControParam(Param, FCS_SFB81);
                break;
            default:
                break;
        }
        return 0;
    }

    public FlightControlParam GetDefaultFCS(AircraftType Aircraft)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        FlightControlParam fcsdata = new FlightControlParam();
        switch (Aircraft)
        {
            case AircraftType.CI22:
                fcsdata = Default_FCS_CI22;
                break;
            case AircraftType.TA30:
                fcsdata = Default_FCS_TA30;
                break;
            case AircraftType.FS12:
                fcsdata = Default_FCS_FS12;
                break;
            case AircraftType.FS20:
                fcsdata = Default_FCS_FS20;
                break;
            case AircraftType.KR67:
                fcsdata = Default_FCS_KR67;
                break;
            case AircraftType.EW25:
                fcsdata = Default_FCS_EW25;
                break;
            case AircraftType.SFB81:
                fcsdata = Default_FCS_SFB81;
                break;
            default:
                break;
        }
        return fcsdata;
    }

    public FlightControlParam GetDefaultFCS(GameObject Target)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        var FCS = Target.GetComponent<ControlsFilter>();
        if (FCS == null)
        {
            throw new NullReferenceException("Target do not have a FCS Component");
        }
        var AdditionalData = Target.GetComponent<FCSPatchData>();
        if (Enum.TryParse<AircraftType>(AdditionalData.Aircraft_Type, out var type))
        {
            return GetDefaultFCS(type);
        }
        else
        {
            throw new NullReferenceException("Target do not contain additional data from NuclearOptionFCSFramework");
        }
    }

    public int SetFCSToDefault(GameObject Target)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        SetFCS(GetDefaultFCS(Target), Target);
        return 0;
    }

    public bool IsReady()
    {
        return done;
    }

    public void SetVectoringMaxAirSpeed_Global(AircraftType Aircraft, float vel)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        if (Aircraft == AircraftType.FS12)
        {
            SetVector(Engine_FS12, vel);
        }
        else if (Aircraft == AircraftType.KR67)
        {
            SetVector(Engine_KR67_L, vel);
            SetVector(Engine_KR67_R, vel);
        }
        else
        {
            throw new InvalidOperationException("Target do not have a vector engine");
        }
    }

    public void SetVectoringMaxAirSpeed(GameObject Target, float vel)
    {
        if (!done) throw new InvalidOperationException("NuclearOptionFCSFramework API has not been initialized yet.");
        var engine = Target.GetComponent<Turbojet>();
        if (engine == null)
        {
            throw new NullReferenceException("Target do not have a Engine Component");
        }
        SetVector(engine, vel);
    }
}


public class FCSPatchData : MonoBehaviour //用于快速识别和额外参数储存的辅助类
{
    [SerializeField] public string Aircraft_Type;
    [SerializeField] public float yawDamperLimit = 0.1f;
}


[HarmonyPatch]
public static class Patch_FlyByWire_BypassYaw
{

    static System.Reflection.MethodBase TargetMethod()
    {
        var outer = typeof(ControlsFilter);
        var inner = AccessTools.Inner(outer, "FlyByWire");
        return AccessTools.Method(inner, "Filter");
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
    {
        var codes = new List<CodeInstruction>(instructions);

        var adjustMethod = AccessTools.Method(typeof(YawExternalHandler), nameof(YawExternalHandler.AdjustYaw));
        var yawField = AccessTools.Field(typeof(ControlInputs), "yaw");

        for (int i = 0; i < codes.Count - 3; i++)
        {
            /*
            匹配目标源序列：
            ldarg.2
            ldloc.s V_10
            neg
            stfld float32 ControlInputs::yaw
            */

            if (codes[i].opcode == OpCodes.Ldarg_2 &&
                codes[i + 1].opcode == OpCodes.Ldloc_S &&
                codes[i + 2].opcode == OpCodes.Neg &&
                codes[i + 3].opcode == OpCodes.Stfld &&
                codes[i + 3].operand is FieldInfo f &&
                f.Name == "yaw")
            {
                var loadNum10 = codes[i + 1].Clone();  // ldloc.s V_10（num10）

                var newInstr = new List<CodeInstruction>()
            {
                // 输入对象 inputs —— stfld 的目标
                new CodeInstruction(OpCodes.Ldarg_2),

                // -------- 参数1：num10 --------
                loadNum10,

                // -------- 参数2：oldYaw --------
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldfld, yawField),

                // -------- 参数3：stabilityAssist --------
                new CodeInstruction(OpCodes.Ldarg_S, 4),

                // -------- 参数4：aircraft --------
                new CodeInstruction(OpCodes.Ldarg_1),

                // 调用 AdjustYaw(num10, oldYaw, stabilityAssist, aircraft)
                new CodeInstruction(OpCodes.Call, adjustMethod),

                // 为 inputs.yaw 赋值
                new CodeInstruction(OpCodes.Stfld, yawField),
            };

                codes.RemoveRange(i, 4);
                codes.InsertRange(i, newInstr);

                break;
            }
        }

        return codes;
    }




}


public static class YawExternalHandler
{

    public static float AdjustYaw(float num10, float oldYaw, bool stabilityAssist, Aircraft aircraft)
    {
        float ret;
        
        if (stabilityAssist)
        {
            ret = -num10;
        }
        else
        {
            var data = aircraft.cockpit.gameObject.GetComponent<FCSPatchData>();
            if (data != null) 
            {
                float limit = Mathf.Clamp01(data.yawDamperLimit);
                ret = oldYaw - Mathf.Clamp(num10, -limit, limit);
            }
            else
            {
                ret = oldYaw - Mathf.Clamp(num10, -0.1f, 0.1f);
            }
        }
        return ret;
    }
}

