namespace SwiftlyAC.Services.ConVars;

public class ConVarsConfiguration
{
    public bool Enabled { get; set; } = true;
    public string CheckInterval { get; set; } = "1m";
    public string DelayInterval { get; set; } = "15s";
    public Dictionary<string, string> ConVarQueryExpressions = new()
    {
        {"sensitivity", "float(queryValue) < 0.0001 || float(queryValue) > 20.0"},
        {"cl_yawspeed", "float(queryValue) != 210.0"},
        {"cl_pitchup", "float(queryValue) != 89.0"},
        {"cl_pitchdown", "float(queryValue) != 89.0"},
        {"fov_cs_debug", "shouldEnforceSvCheats && float(queryValue) != 0.0"},
        {"cl_drawhud", "shouldEnforceSvCheats && bool(queryValue) != true"},
        {"cam_showangles", "shouldEnforceSvCheats && bool(queryValue) != false"},
        {"cl_showpos", "shouldEnforceSvCheats && int(queryValue) != 0"},
        {"sv_cheats", "shouldEnforceSvCheats && bool(queryValue) != false"},
    };
}