using System;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ParseM3UNet.Helpers;

public class Env
{
    public static readonly string ConfigFile = GetEnv("SETTINGS_JSON");
    
    static string GetEnv(string name)
    {
        return Environment.GetEnvironmentVariable(name) ?? throw new Exception("Unset env variable : " + name); ;
    }
}
