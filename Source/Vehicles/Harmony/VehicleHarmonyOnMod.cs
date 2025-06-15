using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEngine;
using HarmonyLib;
using Verse;
using RimWorld;
using SmashTools;

namespace Vehicles
{
  [LoadedEarly]
  [StaticConstructorOnModInit]
  public static class VehicleHarmonyOnMod
  {
    static VehicleHarmonyOnMod()
    {
      Harmony harmony = new($"{VehicleHarmony.VehiclesUniqueId}_preload");

      harmony.Patch(
        original: AccessTools.PropertyGetter(typeof(ShaderTypeDef), nameof(ShaderTypeDef.Shader)),
        prefix: new HarmonyMethod(typeof(VehicleHarmonyOnMod),
          nameof(ShaderFromAssetBundle)));
      harmony.Patch(
        original: AccessTools.Method(typeof(DefGenerator),
          nameof(DefGenerator.GenerateImpliedDefs_PreResolve)),
        prefix: new HarmonyMethod(typeof(VehicleHarmonyOnMod),
          nameof(ImpliedDefGeneratorVehicles)));
      harmony.Patch(original: AccessTools.Method(typeof(GraphicData), "Init"),
        postfix: new HarmonyMethod(typeof(VehicleHarmonyOnMod),
          nameof(GraphicInit)));
      /* Debugging Only */
      //harmony.Patch(original: AccessTools.Method(typeof(XmlInheritance), nameof(XmlInheritance.TryRegister)),
      //	postfix: new HarmonyMethod(typeof(VehicleHarmonyOnMod),
      //	nameof(TestDebug)));

      GameEvent.onNewGame += GizmoHelper.ResetDesignatorStatuses;
      GameEvent.onLoadGame += GizmoHelper.ResetDesignatorStatuses;
    }

    /// <summary>
    /// Generic patch method for testing
    /// </summary>
    public static void TestDebug(XmlNode node, ModContentPack mod)
    {
      try
      {
        XmlAttribute xmlAttribute = node.Attributes["Name"];
        if (xmlAttribute != null)
        {
          Log.Message($"Registering {xmlAttribute.Name} = {xmlAttribute.Value}");
        }
      }
      catch (Exception ex)
      {
        Log.Error(
          $"[Test Postfix] Exception Thrown.\n{ex.Message}\n{ex.InnerException}\n{ex.StackTrace}");
      }
    }

    /// <summary>
    /// Load shader asset for RGB shader types
    /// </summary>
    /// <param name="__instance"></param>
    /// <param name="___shaderInt"></param>
    private static void ShaderFromAssetBundle(ShaderTypeDef __instance, ref Shader ___shaderInt)
    {
      if (__instance is RGBShaderTypeDef && VehicleMod.settings.debug.debugLoadAssetBundles)
      {
        ___shaderInt = AssetBundleDatabase.LoadAsset<Shader>(__instance.shaderPath);
        if (___shaderInt is null)
        {
          SmashLog.Error(
            $"Failed to load Shader from path <text>\"{__instance.shaderPath}\"</text>");
        }
      }
    }

    /// <summary>
    /// Autogenerate implied PawnKindDefs for VehicleDefs
    /// </summary>
    private static void ImpliedDefGeneratorVehicles(bool hotReload)
    {
      foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
      {
        if (PawnKindDefGenerator_Vehicles.GenerateImpliedPawnKindDef(vehicleDef,
          out PawnKindDef kindDef, hotReload))
        {
          DefGenerator.AddImpliedDef(kindDef, hotReload);
        }

        if (ThingDefGenerator_Skyfallers.GenerateImpliedSkyfallerDef(vehicleDef,
          out ThingDef skyfallerLeaving, out ThingDef skyfallerIncoming,
          out ThingDef skyfallerCrashing, hotReload))
        {
          if (skyfallerLeaving != null)
          {
            DefGenerator.AddImpliedDef(skyfallerLeaving, hotReload);
          }

          if (skyfallerIncoming != null)
          {
            DefGenerator.AddImpliedDef(skyfallerIncoming, hotReload);
          }

          if (skyfallerCrashing != null)
          {
            DefGenerator.AddImpliedDef(skyfallerCrashing, hotReload);
          }
        }

        if (ThingDefGenerator_Buildables.GenerateImpliedBuildDef(vehicleDef,
          out VehicleBuildDef buildDef, hotReload))
        {
          DefGenerator.AddImpliedDef(buildDef, hotReload);
        }
      }
    }

    /// <summary>
    /// Check to make sure GraphicData.Init calls are not being triggered for RGBShader-supporting graphics
    /// </summary>
    /// <param name="__instance"></param>
    private static void GraphicInit(GraphicData __instance)
    {
      if (__instance is GraphicDataLayered graphicDataLayered &&
        graphicDataLayered.shaderType.Shader.SupportsRGBMaskTex())
      {
        graphicDataLayered.Init(null);
        Log.Error($"Calling Init for {__instance.GetType()} with path: {__instance.texPath} " +
          $"from GraphicData which means it's being cached in vanilla when it should be using " +
          $"RGBMaterialPool.");
      }
    }
  }
}