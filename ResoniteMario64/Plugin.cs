using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using ResoniteMario64.Mario64;

namespace ResoniteMario64;

[ResonitePlugin(PluginMetadata.GUID, PluginMetadata.NAME, PluginMetadata.VERSION, PluginMetadata.AUTHORS, PluginMetadata.REPOSITORY_URL)]
[BepInDependency(BepInExResoniteShim.PluginMetadata.GUID, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BasePlugin
{
    internal new static ManualLogSource Log;
    
    public static readonly string DllDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    public override void Load()
    {
        Log = base.Log;

        try
        {
            if (!ResoniteMario64.Config.ConfigInit(Config))
            {
                throw new InvalidOperationException("Config initialization failed.");
            }

            if (!Mario64Manager.Init())
            {
                throw new InvalidOperationException("Mario64Manager initialization failed.");
            }

            HarmonyInstance.PatchAll();

            Logger.Info($"Plugin {PluginMetadata.GUID} loaded successfully.");
        }
        catch (Exception ex)
        {
            Logger.Fatal("Failed to load ResoniteMario64.");
            Logger.Fatal(ex);
        }
    }
}