using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;

namespace ResoniteMario64.Mario64;

public static class Mario64Manager
{
    // LibSM64 Native Library
    private const string LibName = "sm64.dll";
    private static readonly string LibPath = Path.Combine(Plugin.DllDirectory, LibName);

    // SM64 ROM
    private const string RomExpectedHash = "20b854b239203baf6c961b850a4a51a2"; // MD5 hash
    private const string RomFileName = "baserom.us.z64";
    private static readonly string RomPath = Path.Combine(Plugin.DllDirectory, RomFileName);
    internal static byte[] RomBytes;
    
    internal static bool Init()
    {
        if (!ExtractNativeLibrary())
        {
            return false;
        }

        if (!LoadRom())
        {
            return false;
        }

        return true;
    }

    private static bool ExtractNativeLibrary()
    {
        try
        {
            Logger.Info($"Loading {LibName} from embedded resources...");
            
            if (File.Exists(LibPath))
            {
                Logger.Msg($"{LibName} already exists, overwriting.");
            }

            Logger.Msg($"Copying {LibName} to {LibPath}");

            using Stream resourceStream = typeof(Mario64Manager).Assembly.GetManifestResourceStream(LibName);
            if (resourceStream == null)
            {
                throw new FileLoadException($"Embedded resource {LibName} not found in assembly.");
            }

            using FileStream fileStream = File.Open(LibPath, FileMode.Create, FileAccess.Write);
            resourceStream.CopyTo(fileStream);

            return true;
        }
        catch (IOException ex)
        {
            Logger.Fatal("Failed to copy native library.");
            Logger.Fatal(ex.Message);
            return false;
        }
    }

    private static bool LoadRom()
    {
        try
        {
            Logger.Info($"Loading \"Super Mario 64 [US].z64\" ROM from {RomPath}...");

            if (!File.Exists(RomPath))
            {
                throw new FileLoadException($"Missing ROM: Download a \"Super Mario 64 [US].z64\" ROM (MD5 {RomExpectedHash})");
            }

            using FileStream romFileStream = File.OpenRead(RomPath);
            string romFileHash = Convert.ToHexStringLower(MD5.Create().ComputeHash(romFileStream));

            if (romFileHash != RomExpectedHash)
            {
                throw new FileLoadException($"Invalid ROM Hash: Found {romFileHash}, expected {RomExpectedHash}.");
            }

            RomBytes = File.ReadAllBytes(RomPath);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Fatal("Failed to load the Super Mario 64 [US] z64 ROM.");
            Logger.Fatal(ex.Message);
            return false;
        }
    }
}