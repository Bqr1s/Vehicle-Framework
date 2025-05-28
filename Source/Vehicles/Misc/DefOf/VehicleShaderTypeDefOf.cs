using RimWorld;
using Verse;

namespace Vehicles;

[DefOf]
public static class VehicleShaderTypeDefOf
{
  static VehicleShaderTypeDefOf()
  {
    DefOfHelper.EnsureInitializedInCtor(typeof(VehicleShaderTypeDefOf));
  }

  public static ShaderTypeDef CutoutComplexRGB;

  public static ShaderTypeDef CutoutComplexPattern;

  public static ShaderTypeDef CutoutComplexSkin;
}