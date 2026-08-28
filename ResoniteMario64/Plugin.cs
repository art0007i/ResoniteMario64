using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BepInEx.NET.Common;
using BepInExResoniteShim;
using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using ResoniteMario64.Mario64;
using ResoniteMario64.Mario64.Components.Context;

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
            BepisResoniteWrapper.ResoniteHooks.OnEngineReady += () =>
            {
                Task.Run(async () =>
                {
                    while (Userspace.UserspaceWorld == null) await Task.Delay(100);

                    World w = Userspace.UserspaceWorld;
                    w.RunSynchronously(() =>
                    {
                        Slot slot = w.RootSlot.LocalUserSpace.AddSlot("ResoniteMario64 Fatal", false);
                        UIBuilder uIBuilder = RadiantUI_Panel.SetupPanel(slot, "ResoniteMario64 - <color=Hero.Red>Fatal</color>", new float2(700f, 350f), pinButton: false);
                        slot.LocalScale *= 0.0008f;
                        RadiantUI_Constants.SetupEditorStyle(uIBuilder);
                        uIBuilder.VerticalLayout(4f);
                        uIBuilder.Style.MinHeight = 48f;

                        uIBuilder.Text($"ResoniteMario64 has encountered a\n<color=Hero.Red>Fatal Error</color> when loading!\nReason:\n<color=Hero.Red>{ex.GetType().GetNiceName()}</color>\n{ex.Message}", 32f);

                        Hyperlink hl = uIBuilder.Button("Go to Github", RadiantUI_Constants.Sub.GREEN).Slot.AttachComponent<Hyperlink>();
                        hl.URL.Value = new Uri("https://github.com/art0007i/ResoniteMario64");
                        hl.Reason.Value = "Opening ResoniteMario64 Github";

                        slot.PositionInFrontOfUser(float3.Backward, null, 3f);
                    });
                });
            };
        }
    }
}