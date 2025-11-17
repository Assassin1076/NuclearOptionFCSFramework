using UnityEngine;
namespace FCSAPI
{
    public interface FCSModifier
    {
        FlightControlParam GetFCS(GameObject Target);
        int SetFCS(FlightControlParam Param, GameObject Target);
        int SetFCS_Global(FlightControlParam Param, AircraftType Target);
        FlightControlParam GetDefaultFCS(AircraftType Aircraft);
        FlightControlParam GetDefaultFCS(GameObject Target);
        int SetFCSToDefault(GameObject Target);
        bool IsReady();
    }
    public interface VectorEngineUnlocker
    {
        void SetVectoringMaxAirSpeed_Global(AircraftType Aircraft, float vel);
        void SetVectoringMaxAirSpeed(GameObject Target, float vel);
    }
    public struct FlightControlParam
    {
        public float alphaLimiter_S;
        public float gLimitPositive_S;
        public float directControlFactor;
        public float maxPitchAngularVel;
        public float cornerSpeed;
        public float postStallManeuverSpeed;
        public float pidTransitionSpeed;
        public float pitchAdjusterLimitSlow;
        public float pFactorSlow;
        public float dFactorSlow;
        public float pitchAdjusterLimitFast;
        public float pFactorFast;
        public float dFactorFast;
        public float rollTrimRate;
        public float rollTrimLimit;
        public float yawTightness;
        public float rollTightness;
        [Tooltip("Additional param from FCSFramwork, this provide a damper for yaw controls. " +
            "When stabilityAssist is disabled, this param indicate the yaw stablizer how much yaw control it can use, limited to [0,1] range." +
            "It is set to 0.1f by default")]
        public float yawDamperLimit_Additional;
    }

    public enum AircraftType
    {
        CI22,
        TA30,
        FS12,
        FS20,
        KR67,
        EW25,
        SFB81
    }

    public static class FCSPatch_API
    {
        public static FCSModifier Instance;
        public static VectorEngineUnlocker VEU_Instance;
    }
}
