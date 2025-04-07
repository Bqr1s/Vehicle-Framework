using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using RimWorld;
using SmashTools;
using SmashTools.Xml;
using UnityEngine;
using Verse;

namespace Vehicles
{
  /// <summary>
  /// AssetBundle loader
  /// </summary>
  /// <remarks>
  /// Q: Why don't you just use RimWorld's content loader to load the asset bundle? It's supported right? <br/>
  /// A: Yes, but it does not support versioning.. meaning if a later version of Unity is used in a future update that requires a rebuild of all asset bundles,
  /// I may not be able to support that previous version. AssetBundles on older versions of Unity might not load properly and vice verse. Vanilla also doesn't support
  /// AssetBundles loading on other platforms, which requires different builds.
  /// </remarks>
  [StaticConstructorOnStartup]
  public static class AssetBundleDatabase
  {
    private const string VehicleAssetFolder = "Assets";

    private static readonly string CutoutComplexRGBPath =
      Path.Combine("Assets", "Shaders", "ShaderRGB.shader");

    private static readonly string CutoutComplexPatternPath =
      Path.Combine("Assets", "Shaders", "ShaderRGBPattern.shader");

    private static readonly string CutoutComplexSkinPath =
      Path.Combine("Assets", "Shaders", "ShaderRGBSkin.shader");

    private static readonly string MouseHandOpenPath =
      Path.Combine("Assets", "Textures", "MouseHandOpen.png");

    private static readonly string MouseHandClosedPath =
      Path.Combine("Assets", "Textures", "MouseHandClosed.png");

    /// <summary>
    /// AssetBundle version loader
    /// </summary>
    private static readonly Dictionary<string, string> bundleBuildVersions = new()
    {
      { "1.3", "2019.4.30f1" },
      { "1.4", "2019.4.30f1" },
      { "1.5", "2019.4.30f1" }
    };

    private static readonly Dictionary<string, UnityEngine.Object> assetLookup = [];

    private static readonly List<string> loadFoldersChecked = [];

    private static readonly List<AssetBundle> vehicleAssets = [];

    private static Shader CutoutComplexRGB { get; }
    private static Shader CutoutComplexPattern { get; }
    private static Shader CutoutComplexSkin { get; }

    public static Texture2D MouseHandOpen { get; private set; }
    public static Texture2D MouseHandClosed { get; private set; }

    private static bool IsLoaded { get; }

    static AssetBundleDatabase()
    {
      if (!UnityData.IsInMainThread)
      {
        Trace.Fail("Attempting to load AssetBundles outside of MainThread.");
        return;
      }
      // Don't load on StaticConstructorOnStartup, this is only to suppress RimWorld warnings
      // because for some reason they assume we're all incapable of ensuring assets are loaded
      // from the main thread. This violates the idea of constructors only being called once but
      // there's no alternative since Shader assets must be loaded earlier for defs.
      if (IsLoaded)
        return;

      if (!VehicleMod.settings.debug.debugLoadAssetBundles)
      {
        Log.Warning($"{VehicleHarmony.LogLabel} Skipping asset bundle loading");
        return;
      }
      if (bundleBuildVersions.TryGetValue(VersionControl.CurrentVersionStringWithoutBuild,
        out string currentVersion))
      {
        if (currentVersion != Application.unityVersion)
        {
          Log.Warning(
            $"{VehicleHarmony.LogLabel} Unity Version {Application.unityVersion} does not match registered version for AssetBundles being loaded.");
        }
      }
      else
      {
        Log.Warning(
          $"{VehicleHarmony.LogLabel} Unable to locate cached Unity version {Application.unityVersion} for {VersionControl.CurrentVersionString}.");
      }

      List<string> loadFolders = FilePaths.ModFoldersForVersion(VehicleMod.settings.Mod.Content);
      try
      {
        loadFoldersChecked.Clear();
        foreach (string folder in loadFolders)
        {
          loadFoldersChecked.Add(folder);
          string assetDirectory = Path.Combine(VehicleMod.settings.Mod.Content.RootDir, folder,
            VehicleAssetFolder, PlatformFolder);
          DirectoryInfo directoryInfo = new(assetDirectory);
          if (directoryInfo.Exists)
          {
            foreach (FileInfo fileInfo in directoryInfo.GetFiles("*", SearchOption.AllDirectories))
            {
              if (fileInfo.Extension.NullOrEmpty())
              {
                AssetBundle assetBundle = AssetBundle.LoadFromFile(fileInfo.FullName);
                if (assetBundle is null)
                {
                  SmashLog.Error($"Unable to load <type>AssetBundle</type> at {assetDirectory}");
                  throw new IOException();
                }
                vehicleAssets.Add(assetBundle);
              }
            }
          }
        }
      }
      catch (Exception ex)
      {
        Log.Error(
          $"Unable to load AssetBundle.\nException = {ex}\nFoldersSearched={loadFoldersChecked.ToCommaList()}");
      }
      finally
      {
        if (Prefs.DevMode)
        {
          foreach (AssetBundle assetBundle in vehicleAssets)
          {
            SmashLog.Message(
              $"<color=orange>{VehicleHarmony.LogLabel}</color> Importing additional assets from {assetBundle.name}. UnityVersion={Application.unityVersion} Status: {AssetBundleLoadMessage(assetBundle)}");
          }
        }
      }

      CutoutComplexRGB = LoadAsset<Shader>(CutoutComplexRGBPath);
      CutoutComplexPattern = LoadAsset<Shader>(CutoutComplexPatternPath);
      CutoutComplexSkin = LoadAsset<Shader>(CutoutComplexSkinPath);
      MouseHandOpen = LoadAsset<Texture2D>(MouseHandOpenPath);
      MouseHandClosed = LoadAsset<Texture2D>(MouseHandClosedPath);

      IsLoaded = true;
    }

    private static string PlatformFolder
    {
      get
      {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
          return "StandaloneWindows64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
          return "StandaloneLinux64";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
          return "StandaloneOSX";
        }

        Log.Warning(
          $"{RuntimeInformation.OSDescription} is not currently supported for RGBShaders. Disabling custom shaders.");
        VehicleMod.settings.main.useCustomShaders = false;
        return null;
      }
    }

    /// <summary>
    /// Status message
    /// </summary>
    /// <param name="assetBundle"></param>
    private static string AssetBundleLoadMessage(AssetBundle assetBundle) => assetBundle != null ?
      "<success>successfully loaded.</success>" :
      "<error>failed to load.</error>";

    /// <summary>
    /// Shader load from AssetBundle
    /// </summary>
    /// <param name="path"></param>
    public static T LoadAsset<T>(string path) where T : UnityEngine.Object
    {
      if (assetLookup.TryGetValue(path, out UnityEngine.Object asset))
      {
        return (T)asset;
      }
      foreach (AssetBundle assetBundle in vehicleAssets)
      {
        UnityEngine.Object unityObject = assetBundle.LoadAsset(path);
        if (unityObject != null)
        {
          if (unityObject is not T obj)
          {
            SmashLog.Error(
              $"Asset has loaded successfully from path=<text>\"{path}\"</text> but is not of type <type>{typeof(T)}</type>. Actual type is <type>{unityObject.GetType()}</type>.");
            return null;
          }
          assetLookup.Add(path, unityObject);
          return obj;
        }
      }
      SmashLog.Error($"Unable to locate asset at path=\"{path}\".");
      return null;
    }

    /// <summary>
    /// <paramref name="shader"/> supports AssetBundle shaders implementing RGB or RGB Pattern masks
    /// </summary>
    public static bool SupportsRGBMaskTex(this Shader shader, bool ignoreSettings = false)
    {
      if (!VehicleMod.settings.main.useCustomShaders && !ignoreSettings)
      {
        return false;
      }
      return shader == CutoutComplexPattern || shader == CutoutComplexSkin ||
        shader == CutoutComplexRGB;
    }
  }
}