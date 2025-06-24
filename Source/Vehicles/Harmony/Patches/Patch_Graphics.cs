using HarmonyLib;
using SmashTools.Patching;
using Verse;

namespace Vehicles;

internal class Patch_Graphics : IPatchCategory
{
  PatchSequence IPatchCategory.PatchAt => PatchSequence.Mod;

  void IPatchCategory.PatchMethods()
  {
    HarmonyPatcher.Patch(original: AccessTools.Method(typeof(GraphicData), "Init"),
      postfix: new HarmonyMethod(typeof(Patch_Graphics),
        nameof(GraphicInit)));
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