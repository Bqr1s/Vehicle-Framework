using System;
using System.Xml;
using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using SmashTools;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles;

[StaticConstructorOnModInit]
public static class VehicleHarmonyOnMod
{
  static VehicleHarmonyOnMod()
  {
    Harmony harmony = new($"{VehicleHarmony.VehiclesUniqueId}_preload");

    harmony.Patch(original: AccessTools.Method(typeof(GraphicData), "Init"),
      postfix: new HarmonyMethod(typeof(VehicleHarmonyOnMod),
        nameof(GraphicInit)));

    // Debugging Only 
    //harmony.Patch(original: AccessTools.Method(typeof(XmlInheritance), nameof(XmlInheritance.TryRegister)),
    //	postfix: new HarmonyMethod(typeof(VehicleHarmonyOnMod),
    //	nameof(TestDebug)));

    GameEvent.OnNewGame += GizmoHelper.ResetDesignatorStatuses;
    GameEvent.OnLoadGame += GizmoHelper.ResetDesignatorStatuses;
    GameEvent.OnGenerateImpliedDefs += ImpliedDefGeneratorVehicles;
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

  /// <summary>
  /// Generic patch method for testing
  /// </summary>
  public static void TestDebug(XmlNode node)
  {
    try
    {
      Assert.IsNotNull(node.Attributes);
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

  [PublicAPI]
  public static void GenerateImpliedDefs<T, D>(bool hotReload)
    where T : IVehicleDefGenerator<D>, new()
    where D : Def, new()
  {
    T generator = new();
    foreach (VehicleDef vehicleDef in DefDatabase<VehicleDef>.AllDefsListForReading)
    {
      if (generator.TryGenerateImpliedDef(vehicleDef, out D impliedDef, hotReload))
        DefGenerator.AddImpliedDef(impliedDef, hotReload);
    }
  }

  /// <summary>
  /// Autogenerate implied PawnKindDefs for VehicleDefs
  /// </summary>
  private static void ImpliedDefGeneratorVehicles(bool hotReload)
  {
    GenerateImpliedDefs<GeneratorVehiclePawnKindDef, PawnKindDef>(hotReload);
    GenerateImpliedDefs<GeneratorVehicleBuildDef, VehicleBuildDef>(hotReload);
    GenerateImpliedDefs<GeneratorVehicleSkyfallerLeaving, ThingDef>(hotReload);
    GenerateImpliedDefs<GeneratorVehicleSkyfallerIncoming, ThingDef>(hotReload);
    GenerateImpliedDefs<GeneratorVehicleSkyfallerCrashing, ThingDef>(hotReload);
  }
}