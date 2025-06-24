﻿using HarmonyLib;
using JetBrains.Annotations;
using RimWorld;
using UnityEngine;
using Verse;

namespace Vehicles;

[UsedImplicitly]
public class GraphicDataRGB : GraphicDataLayered
{
  public Color colorThree = Color.white;

  public float tiles = 1;
  public Vector2 displacement = Vector2.zero;

  public PatternDef pattern;

  private Graphic_Rgb cachedRGBGraphic;

  public new Graphic_Rgb Graphic
  {
    get
    {
      if (cachedRGBGraphic == null && !shaderType.Shader.SupportsRGBMaskTex())
      {
        cachedRGBGraphic = base.Graphic as Graphic_Rgb; //Invoke vanilla Init method
      }
      return cachedRGBGraphic;
    }
  }

  public virtual void CopyDrawData(GraphicDataRGB graphicData)
  {
    color = graphicData.color;
    colorTwo = graphicData.colorTwo;
    colorThree = graphicData.colorThree;

    tiles = graphicData.tiles;
    displacement = graphicData.displacement;

    pattern = graphicData.pattern ?? PatternDefOf.Default;
  }

  public override void CopyFrom(GraphicDataLayered graphicData)
  {
    base.CopyFrom(graphicData);
    if (graphicData is GraphicDataRGB graphicDataRGB)
    {
      colorThree = graphicDataRGB.colorThree;
      pattern = graphicDataRGB.pattern ?? PatternDefOf.Default;
    }
  }

  public virtual void CopyFrom(GraphicDataLayered graphicData, PatternDef pattern,
    Color colorThree)
  {
    CopyFrom(graphicData);
    if (graphicData is GraphicDataRGB)
    {
      this.colorThree = colorThree;
      this.pattern = pattern ?? PatternDefOf.Default;
    }
  }

  public override void Init(IMaterialCacheTarget target)
  {
    base.Init(target);
    if (graphicClass is null)
    {
      cachedRGBGraphic = null;
      return;
    }
    // Failsafe to ensure pattern isn't null
    pattern ??= PatternDefOf.Default;
    ShaderTypeDef shaderTypeDef =
      pattern is SkinDef ? VehicleShaderTypeDefOf.CutoutComplexSkin : shaderType;
    if (shaderTypeDef == null)
    {
      color = Color.white;
      colorTwo = Color.white;
      colorThree = Color.white;
      shaderTypeDef = ShaderTypeDefOf.Cutout;
    }
    if (!VehicleMod.settings.main.useCustomShaders)
    {
      shaderTypeDef = shaderTypeDef.Shader.SupportsRGBMaskTex(ignoreSettings: true) ?
        ShaderTypeDefOf.CutoutComplex :
        ShaderTypeDefOf.Cutout;
    }
    Shader shader = shaderTypeDef.Shader;
    cachedRGBGraphic = GraphicDatabaseRGB.Get(target, graphicClass, texPath, shader, drawSize,
      color, colorTwo, colorThree, tiles, displacement.x, displacement.y, this, shaderParameters);
    AccessTools.Field(typeof(GraphicData), "cachedGraphic").SetValue(this, cachedRGBGraphic);
  }

  public override string ToString()
  {
    return $"({texPath}, {color}, {colorTwo}, {colorThree}, {pattern}, {tiles}, {displacement})";
  }
}