using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Vehicles;

[PublicAPI]
[StaticConstructorOnStartup]
public static class TexData
{
  public const int CloseRange = 5;
  public const int MidRange = 15;
  public const int FarRange = 25;

  public static readonly Texture2D YellowTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(255, 210, 45).ToColor);

  public static readonly Texture2D YellowOrangeTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(255, 175, 45).ToColor);

  public static readonly Texture2D OrangeTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(255, 110, 15).ToColor);

  public static readonly Texture2D OrangeRedTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(255, 75, 15).ToColor);

  public static readonly Texture2D RedTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(155, 30, 30).ToColor);

  public static readonly Texture2D MaroonTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(60, 30, 30).ToColor);

  public static readonly Texture2D BlueTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(35, 50, 185).ToColor);

  public static readonly Texture2D GreenTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(0, 115, 40).ToColor);

  public static readonly Texture2D BlueAddedStatBarTexture =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(35, 50, 185, 120).ToColor);

  public static readonly Texture2D GreenAddedStatBarTexture =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(0, 115, 40, 120).ToColor);

  public static readonly Texture2D RedAddedStatBarTexture =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(155, 30, 30, 120).ToColor);

  public static readonly Texture2D OrangeAddedStatBarTexture =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(185, 110, 15, 120).ToColor);

  public static readonly Texture2D RedBrownAddedStatBarTexture =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(60, 30, 30, 120).ToColor);

  public static readonly Texture2D SearchLightTex =
    SolidColorMaterials.NewSolidColorTexture(new ColorInt(155, 30, 30, 40).ToColor);

  public static readonly Texture2D FillableBarTexture =
    SolidColorMaterials.NewSolidColorTexture(0.5f, 0.5f, 0.5f, 0.5f);

  public static readonly Texture2D FullBarTex =
    SolidColorMaterials.NewSolidColorTexture(new Color(0.35f, 0.35f, 0.2f));

  public static readonly Texture2D EmptyBarTex =
    SolidColorMaterials.NewSolidColorTexture(Color.black);

  public static readonly Texture2D ClearBarTexture = BaseContent.ClearTex;

  /// <summary>
  /// World Materials with color
  /// </summary>
  public static readonly Material WorldLineMatWhite = MaterialPool.MatFrom(GenDraw.LineTexPath,
    ShaderDatabase.WorldOverlayTransparent, Color.white, WorldMaterials.WorldLineRenderQueue);

  public static readonly Material WorldLineMatYellow = MaterialPool.MatFrom(GenDraw.LineTexPath,
    ShaderDatabase.WorldOverlayTransparent, Color.yellow, WorldMaterials.WorldLineRenderQueue);

  public static readonly Material WorldLineMatRed = MaterialPool.MatFrom(GenDraw.LineTexPath,
    ShaderDatabase.WorldOverlayTransparent, Color.red, WorldMaterials.WorldLineRenderQueue);

  public static readonly Material OneSidedWorldLineMatWhite =
    MaterialPool.MatFrom(GenDraw.OneSidedLineTexPath, ShaderDatabase.WorldOverlayTransparent,
      Color.white, WorldMaterials.WorldLineRenderQueue);

  public static readonly Material OneSidedWorldLineMatRed =
    MaterialPool.MatFrom(GenDraw.OneSidedLineTexPath, ShaderDatabase.WorldOverlayTransparent,
      Color.red, WorldMaterials.WorldLineRenderQueue);

  public static readonly Material WorldFullMatRed = MaterialPool.MatFrom(SearchLightTex,
    ShaderDatabase.WorldOverlayTransparent, Color.white, WorldMaterials.WorldLineRenderQueue);

  public static readonly Texture2D TutorArrowRight =
    ContentFinder<Texture2D>.Get("UI/Overlays/TutorArrowRight");

  public static readonly Texture2D Rename = ContentFinder<Texture2D>.Get("UI/Buttons/Rename");

  public static readonly Texture2D Drop = ContentFinder<Texture2D>.Get("UI/Buttons/Drop");

  public static readonly List<Texture2D> FireIcons =
    ContentFinder<Texture2D>.GetAllInFolder("Things/Special/Fire").ToList();

  public static readonly Texture2D TargeterMouseAttachment =
    ContentFinder<Texture2D>.Get("UI/Overlays/LaunchableMouseAttachment");

  public static readonly Texture2D CaravanIcon =
    ContentFinder<Texture2D>.Get("UI/Commands/FormCaravan");

  public static readonly Texture2D FlickerIcon =
    ContentFinder<Texture2D>.Get("UI/Commands/DesirePower");

  public static readonly Texture2D LaunchCommandTex =
    ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip");

  public static readonly Texture2D TradeCommandTex =
    ContentFinder<Texture2D>.Get("UI/Commands/Trade");

  public static readonly Texture2D OfferGiftsCommandTex =
    ContentFinder<Texture2D>.Get("UI/Commands/OfferGifts");

  public static readonly Texture2D TradeArrow =
    ContentFinder<Texture2D>.Get("UI/Widgets/TradeArrow");

  /// <summary>
  /// Preset UI colors
  /// </summary>
  public static readonly Color IconColor = new(0.84f, 0.84f, 0.84f);

  public static readonly Color RedReadable = new(1f, 0.2f, 0.2f);
  public static readonly Color YellowReadable = new(1f, 1f, 0.2f);

  public static readonly Color HighlightColor = new(0.5f, 0.5f, 0.5f, 1f);
  public static readonly Color StaticHighlightColor = new(0.75f, 0.75f, 0.85f, 1f);

  public static readonly Color SevereDamage = new(0.75f, 0.45f, 0.45f);
  public static readonly Color ModerateDamage = new(0.55f, 0.55f, 0.55f);
  public static readonly Color MinorDamage = new(0.7f, 0.7f, 0.7f);
  public static readonly Color WorkingCondition = new(0.6f, 0.8f, 0.65f);
  public static readonly Color Enhanced = new(0.5f, 0.5f, 0.9f);

  public static Material RangeMat(int radius)
  {
    if (radius <= CloseRange)
    {
      return VehicleTex.RangeCircle_Close;
    }
    else if (radius <= MidRange)
    {
      return VehicleTex.RangeCircle_Mid;
    }
    else if (radius <= FarRange)
    {
      return VehicleTex.RangeCircle_Wide;
    }
    else
    {
      return VehicleTex.RangeCircle_ExtraWide;
    }
  }

  public static Texture2D HeatColorPercent(float percent)
  {
    if (percent <= 0.25)
    {
      return YellowTex;
    }
    else if (percent <= 0.5f)
    {
      return YellowOrangeTex;
    }
    else if (percent <= 0.75f)
    {
      return OrangeTex;
    }
    else if (percent < 1f)
    {
      return OrangeRedTex;
    }
    return RedTex;
  }
}