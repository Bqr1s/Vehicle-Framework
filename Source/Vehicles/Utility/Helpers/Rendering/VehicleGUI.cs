﻿using System;
using System.Collections.Generic;
using RimWorld;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using UnityEngine.Assertions;
using Verse;
using Verse.Sound;

namespace Vehicles.Rendering;

[StaticConstructorOnStartup]
public static class VehicleGui
{
  private const int ImageSizeTiny = 128;
  private const int ImageSizeSmall = 256;
  private const int ImageSizeMedium = 512;
  private const int ImageSizeLarge = 1024;

  private const float IdlerTimeExpiry = 5; // seconds

  private static readonly List<VehicleTurret> allTurrets = [];
  private static readonly List<GraphicOverlay> allOverlays = [];

  private static readonly RenderTextureIdler[] gizmoTextures;

  static VehicleGui()
  {
    gizmoTextures = new RenderTextureIdler[DefDatabase<VehicleDef>.DefCount];
  }

  public static RenderTexture GetTexture(ImageSize size)
  {
    return size switch
    {
      ImageSize.Tiny   => CreateRenderTexture(ImageSizeTiny),
      ImageSize.Small  => CreateRenderTexture(ImageSizeSmall),
      ImageSize.Medium => CreateRenderTexture(ImageSizeMedium),
      ImageSize.Large  => CreateRenderTexture(ImageSizeLarge),
      _                => throw new NotImplementedException(nameof(ImageSize))
    };
  }

  private static void AddRenderData(ref readonly RenderData renderData)
  {
    if (RenderTextureDrawer.InUse)
      RenderTextureDrawer.Add(renderData);
    else if (TextureDrawer.InUse)
      TextureDrawer.Add(renderData);
    else
      throw new InvalidOperationException();
  }

  private static RenderTexture CreateRenderTexture(int size)
  {
    RenderTexture rt = new(size, size, 0, RenderTextureFormat.ARGBFloat.OrNextSupportedFormat());
    rt.Create();
    return rt;
  }

  private static BlitData GetBlitData(Rect rect, VehicleDef vehicleDef,
    PatternData patternData = null,
    Rot8? rot = null)
  {
    Vector2 rectSize = vehicleDef.ScaleDrawRatio(rect.size);
    Rot8 rotDrawn = rot ?? vehicleDef.drawProperties.displayRotation;

    bool elongated = rotDrawn.IsHorizontal || rotDrawn.IsDiagonal;

    Vector2 displayOffset = vehicleDef.drawProperties.DisplayOffsetForRot(rotDrawn);
    float scaledWidth = rectSize.x;
    float scaledHeight = rectSize.y;
    if (elongated)
    {
      scaledWidth = rectSize.y;
      scaledHeight = rectSize.x;
    }
    float offsetX = (rect.width - scaledWidth) / 2 + (displayOffset.x * rect.width);
    float offsetY = (rect.height - scaledHeight) / 2 + (displayOffset.y * rect.height);

    Rect adjustedRect = new(rect.x + offsetX, rect.y + offsetY, scaledWidth, scaledHeight);

    Graphic_Vehicle graphic = vehicleDef.graphicData.Graphic as Graphic_Vehicle;
    Assert.IsNotNull(graphic);

    PatternData pattern = patternData ??
      VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicleDef.defName,
        vehicleDef.graphicData);
    if (!VehicleMod.settings.main.useCustomShaders)
    {
      pattern.patternDef = PatternDefOf.Default;
    }

    Texture2D mainTex = graphic.TexAt(rotDrawn);
    //Texture2D maskTex = graphic.MaskAt(rotDrawn);

    Material material = null;
    if (graphic.Shader.SupportsRGBMaskTex())
    {
      material = RGBMaterialPool.Get(vehicleDef, rotDrawn);
      RGBMaterialPool.SetProperties(vehicleDef, pattern, graphic.TexAt, graphic.MaskAt);
    }
    return new BlitData(adjustedRect, mainTex, material, rotDrawn, pattern);
  }

  public static void Blit(RenderTexture renderTexture, Rect rect, in BlitRequest request)
  {
    RenderTextureDrawer.Open(renderTexture);
    try
    {
      foreach (IBlitTarget blitTarget in request.blitTargets)
      {
        foreach (RenderData renderData in blitTarget.GetRenderData(rect, request))
        {
          RenderTextureDrawer.Add(renderData);
        }
      }
      RenderTextureDrawer.Draw(rect);
    }
    finally
    {
      RenderTextureDrawer.Close();
    }
  }

  public static void DrawVehicleDefOnGUI(Rect rect, VehicleDef vehicleDef,
    PatternData patternData = null, Rot8? rot = null)
  {
    BlitData blitData = GetBlitData(rect, vehicleDef, patternData: patternData, rot: rot);

    // Rendering
    TextureDrawer.Open();
    try
    {
      TextureDrawer.Add(new RenderData(blitData.rect, blitData.mainTex, blitData.material,
        vehicleDef.PropertyBlock, 0, 0));
      if (vehicleDef.GetSortedCompProperties<CompProperties_VehicleTurrets>() is { } props)
      {
        allTurrets.AddRange(props.turrets);
        AddAllTurretSettingsGUIProperties(rect,
          vehicleDef, blitData.rot, allTurrets,
          blitData.patternData);
      }
      allOverlays.AddRange(vehicleDef.drawProperties.overlays);
      AddAllOverlaySettingsGUIProperties(rect, vehicleDef, blitData.rot,
        allOverlays, blitData.patternData);
      TextureDrawer.Draw(rect);
    }
    finally
    {
      TextureDrawer.Close();
      allTurrets.Clear();
      allOverlays.Clear();
    }
  }

  /// <summary>
  /// Retrieve GUI data for rendering, adjusted by settings UI properties for <paramref name="vehicleDef"/>
  /// </summary>
  private static void AddAllOverlaySettingsGUIProperties(Rect rect,
    VehicleDef vehicleDef, Rot8 rot, IEnumerable<GraphicOverlay> graphicOverlays,
    PatternData patternData)
  {
    foreach (GraphicOverlay graphicOverlay in graphicOverlays)
    {
      if (graphicOverlay.data.renderUI)
      {
        AddOverlaySettingsGUIProperties(rect, vehicleDef, rot, graphicOverlay, patternData);
      }
    }
  }

  private static void AddOverlaySettingsGUIProperties(Rect rect, VehicleDef vehicleDef,
    Rot8 rot, GraphicOverlay graphicOverlay, PatternData patternData)
  {
    Rect overlayRect = VehicleGraphics.OverlayRect(rect, vehicleDef, graphicOverlay, rot);
    Graphic graphic = graphicOverlay.Graphic;
    bool canMask = graphic.Shader.SupportsMaskTex() || graphic.Shader.SupportsRGBMaskTex();

    Material material = canMask ? graphicOverlay.Graphic.MatAt(rot) : null;

    Texture2D texture = graphic.MatAt(rot).mainTexture as Texture2D;
    if (canMask)
    {
      if (graphic is Graphic_Rgb graphicRgb)
      {
        RGBMaterialPool.SetProperties(graphicOverlay, patternData, graphicRgb.TexAt,
          graphicRgb.MaskAt);
      }
      else
      {
        RGBMaterialPool.SetProperties(graphicOverlay, patternData,
          forRot => graphic.MatAt(forRot).mainTexture as Texture2D,
          forRot => graphic.MatAt(forRot).GetMaskTexture());
      }
    }
    // TODO - vehicleDef.PropertyBlock here would be incorrect for VehiclePawn instance rendering. Will
    // need a refactor later if and when I get to drawing all of this via material property blocks.
    RenderData overlayRenderData = new(overlayRect, texture, material, vehicleDef.PropertyBlock,
      graphicOverlay.data.graphicData.DrawOffsetFull(rot).y, graphicOverlay.data.rotation);
    AddRenderData(in overlayRenderData);
  }

  /// <summary>
  /// Retrieve <seealso cref="VehicleTurret"/> GUI data for rendering, adjusted by settings UI properties for <paramref name="vehicleDef"/>
  /// </summary>
  private static void AddAllTurretSettingsGUIProperties(Rect rect,
    VehicleDef vehicleDef, Rot8 rot, IEnumerable<VehicleTurret> turrets, PatternData patternData)
  {
    foreach (VehicleTurret turret in turrets)
    {
      if (!turret.NoGraphic)
      {
        AddTurretSettingsGUIProperties(rect, vehicleDef, turret, rot, patternData);
      }
      if (!turret.TurretGraphics.NullOrEmpty())
      {
        foreach (VehicleTurret.TurretDrawData turretDrawData in turret.TurretGraphics)
        {
          Rect turretRect = VehicleGraphics.TurretRect(rect, vehicleDef, turret, rot);
          Graphic_Turret graphic = turretDrawData.graphic;
          bool canMask = graphic.Shader.SupportsMaskTex() || graphic.Shader.SupportsRGBMaskTex();
          Material material = canMask ? graphic.MatAtFull(Rot8.North) : null;
          if (canMask && turret.turretDef.matchParentColor)
          {
            RGBMaterialPool.SetProperties(turretDrawData, patternData, graphic.TexAt,
              graphic.MaskAt);
          }
          RenderData turretRenderData = new(turretRect, graphic.TexAt(Rot8.North),
            material, turretDrawData.PropertyBlock, turretDrawData.graphicData.drawOffset.y,
            turret.defaultAngleRotated + rot.AsAngle);
          AddRenderData(in turretRenderData);
        }
      }
    }
  }

  private static void AddTurretSettingsGUIProperties(Rect rect, VehicleDef vehicleDef,
    VehicleTurret turret, Rot8 rot, PatternData patternData, float iconScale = 1)
  {
    if (turret.NoGraphic)
    {
      Log.Warning("Attempting to fetch GUI properties for VehicleTurret with no graphic.");
      return;
    }
    Rect turretRect =
      VehicleGraphics.TurretRect(rect, vehicleDef, turret, rot, iconScale: iconScale);
    Graphic_Turret graphic = turret.Graphic;
    bool canMask = turret.Graphic.Shader.SupportsMaskTex() ||
      turret.Graphic.Shader.SupportsRGBMaskTex();
    Material material = canMask ? turret.Material : null;
    if (canMask && turret.turretDef.matchParentColor)
    {
      RGBMaterialPool.SetProperties(turret, patternData, graphic.TexAt, graphic.MaskAt);
    }
    RenderData turretRenderData = new(turretRect, turret.Texture, material,
      turret.PropertyBlock, turret.GraphicData.drawOffset.y,
      turret.defaultAngleRotated + rot.AsAngle);
    AddRenderData(in turretRenderData);
  }

  /// <summary>
  /// Draw <paramref name="buildDef"/> with proper vehicle material
  /// </summary>
  public static GizmoResult GizmoOnGUIWithMaterial(Command command, Rect rect,
    GizmoRenderParms parms, VehicleBuildDef buildDef)
  {
    bool mouseOver = false;
    bool clicked = false;

    VehicleDef vehicleDef = buildDef.thingToSpawn;
    using TextBlock textFont = new(GameFont.Tiny, Color.white);

    if (Mouse.IsOver(rect))
    {
      mouseOver = true;
      if (!command.Disabled)
        GUI.color = GenUI.MouseoverColor;
    }

    MouseoverSounds.DoRegion(rect, SoundDefOf.Mouseover_Command);
    if (parms.highLight)
    {
      Widgets.DrawStrongHighlight(rect.ExpandedBy(12f));
    }

    if (parms.lowLight)
      GUI.color = Command.LowLightBgColor;

    Material material = command.Disabled ? TexUI.GrayscaleGUI : null;
    GenUI.DrawTextureWithMaterial(rect, command.BGTexture, material);
    GUI.color = Color.white;

    // BeginGroup
    Rect iconRect = rect.ContractedBy(1);
    Widgets.BeginGroup(iconRect);
    iconRect = iconRect.AtZero();

    Rect buttonRect = iconRect;
    PatternData defaultPatternData =
      new(
        VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicleDef.defName,
          vehicleDef.graphicData));
    if (command.Disabled)
    {
      defaultPatternData.color = vehicleDef.graphicData.color.SubtractNoAlpha(0.1f, 0.1f, 0.1f);
      defaultPatternData.colorTwo =
        vehicleDef.graphicData.colorTwo.SubtractNoAlpha(0.1f, 0.1f, 0.1f);
      defaultPatternData.colorThree =
        vehicleDef.graphicData.colorThree.SubtractNoAlpha(0.1f, 0.1f, 0.1f);
    }

    if (!command.Disabled || parms.lowLight)
    {
      GUI.color = command.IconDrawColor;
    }
    else
    {
      GUI.color = command.IconDrawColor.SaturationChanged(0f);
      defaultPatternData.color = vehicleDef.graphicData.color.SaturationChanged(0);
      defaultPatternData.colorTwo = vehicleDef.graphicData.colorTwo.SaturationChanged(0);
      defaultPatternData.colorThree = vehicleDef.graphicData.colorThree.SaturationChanged(0);
    }
    if (parms.lowLight)
    {
      GUI.color = GUI.color.ToTransparent(0.6f);
      defaultPatternData.color = defaultPatternData.color.ToTransparent(0.6f);
      defaultPatternData.colorTwo = defaultPatternData.colorTwo.ToTransparent(0.6f);
      defaultPatternData.colorThree = defaultPatternData.colorThree.ToTransparent(0.6f);
    }
    RenderTextureIdler rtIdler = gizmoTextures[vehicleDef.DefIndex];
    if (rtIdler == null || rtIdler.Disposed)
    {
      rtIdler = new RenderTextureIdler(CreateRenderTexture(ImageSizeMedium), IdlerTimeExpiry);
      gizmoTextures[vehicleDef.DefIndex] = rtIdler;
      Blit(rtIdler.GetWrite(), buttonRect,
        BlitRequest.For(vehicleDef) with { patternData = defaultPatternData });
    }
    GUI.DrawTexture(buttonRect, rtIdler.Read);
    GUI.color = Color.white;

    if (command.hotKey != null)
    {
      KeyCode keyCode = command.hotKey.MainKey;
      if (keyCode != KeyCode.None && !GizmoGridDrawer.drawnHotKeys.Contains(keyCode))
      {
        Vector2 vector = new(5f, 3f);
        Widgets.Label(
          new Rect(iconRect.x + vector.x, iconRect.y + vector.y, iconRect.width - 10f, 18f),
          keyCode.ToStringReadable());
        GizmoGridDrawer.drawnHotKeys.Add(keyCode);
        if (command.hotKey.KeyDownEvent)
        {
          clicked = true;
          Event.current.Use();
        }
      }
    }
    if (Widgets.ButtonInvisible(iconRect))
    {
      clicked = true;
    }
    Widgets.EndGroup();
    // EndGroup

    string topRightLabel = command.TopRightLabel;
    if (!topRightLabel.NullOrEmpty())
    {
      Vector2 vector2 = Text.CalcSize(topRightLabel);
      Rect position = new(rect.xMax - vector2.x - 2f, rect.y + 3f, vector2.x, vector2.y);
      Rect rectBase = position;
      position.x -= 2f;
      position.width += 3f;

      using TextBlock labelBlock = new(TextAnchor.UpperRight, Color.white);
      GUI.DrawTexture(position, TexUI.GrayTextBG);
      Widgets.Label(rectBase, topRightLabel);
    }
    string labelCap = buildDef.LabelCap;
    if (!labelCap.NullOrEmpty())
    {
      float num = Text.CalcHeight(labelCap, rect.width);
      Rect rect2 = new(rect.x, rect.yMax - num + 12f, rect.width, num);
      GUI.DrawTexture(rect2, TexUI.GrayTextBG);
      using TextBlock labelBlock = new(TextAnchor.UpperCenter, Color.white);
      Widgets.Label(rect2, labelCap);
    }
    GUI.color = Color.white;
    if (Mouse.IsOver(rect))
    {
      TipSignal tip = command.Desc;
      if (command.Disabled && !command.disabledReason.NullOrEmpty())
      {
        tip.text += "\n\n" + "DisabledCommand".Translate() + ": " + command.disabledReason;
      }
      TooltipHandler.TipRegion(rect, tip);
    }
    if (!command.HighlightTag.NullOrEmpty() && (Find.WindowStack.FloatMenu == null ||
      !Find.WindowStack.FloatMenu.windowRect.Overlaps(rect)))
    {
      UIHighlighter.HighlightOpportunity(rect, command.HighlightTag);
    }

    Text.Font = GameFont.Small;
    if (clicked)
    {
      if (command.Disabled)
      {
        if (!command.disabledReason.NullOrEmpty())
        {
          Messages.Message(command.disabledReason, MessageTypeDefOf.RejectInput, false);
        }
        return new GizmoResult(GizmoState.Mouseover, null);
      }
      GizmoResult result;
      if (Event.current.button == 1)
      {
        result = new GizmoResult(GizmoState.OpenedFloatMenu, Event.current);
      }
      else
      {
        if (!TutorSystem.AllowAction(command.TutorTagSelect))
        {
          return new GizmoResult(GizmoState.Mouseover, null);
        }
        result = new GizmoResult(GizmoState.Interacted, Event.current);
        TutorSystem.Notify_Event(command.TutorTagSelect);
      }
      return result;
    }
    return new GizmoResult(mouseOver ? GizmoState.Mouseover : GizmoState.Clear, null);
  }

  private readonly struct BlitData(
    Rect rect,
    Texture2D mainTex,
    Material material,
    Rot8 rot,
    PatternData patternData)
  {
    public readonly Rect rect = rect;
    public readonly Texture2D mainTex = mainTex;
    public readonly Material material = material;
    public readonly Rot8 rot = rot;
    public readonly PatternData patternData = patternData;
  }

  public enum ImageSize
  {
    Tiny,
    Small,
    Medium,
    Large
  }
}