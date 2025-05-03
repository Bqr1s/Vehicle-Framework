using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using SmashTools;
using SmashTools.Rendering;
using UnityEngine;
using Vehicles.Rendering;
using Verse;
using Verse.Sound;

namespace Vehicles;

[StaticConstructorOnStartup]
public class Dialog_VehiclePainter : Window
{
  private const float ButtonWidth = 90f;
  private const float ButtonHeight = 30f;
  private const float SliderHeight = 40;
  private const float IconButtonSize = 48;

  private const float SwitchSize = 60f;

  private const float FrameSpacing = 2;
  private const float FramePadding = 2;

  private const int GridDimensionColumns = 2;
  private const int GridDimensionRows = 2;
  private const int SampleCount = GridDimensionColumns * GridDimensionRows;

  // Widgets::WindowBGFillColor const
  private static readonly Color windowBGBorderColor = new ColorInt(97, 108, 122).ToColor;

  // Widgets::MenuSectionBGBorderColor
  private static readonly Color menuSectionBGBorderColor = new ColorInt(135, 135, 135).ToColor;

  private int pageNumber;
  private int pageCount;

  private static PatternDef selectedPattern;
  private Rot8 displayRotation;

  private RenderTextureBuffer previewBuffer;
  private bool previewDirty;

  private readonly List<RenderTextureBuffer> sampleBuffers = new(SampleCount);
  private readonly bool[] samplesDirty = new bool[SampleCount];


  // Color Picker
  private readonly ColorPicker colorPicker = new();

  private float hue;
  private float saturation;
  private float value;

  private ColorInt currentColorOne;
  private ColorInt currentColorTwo;
  private ColorInt currentColorThree;

  private string colorOneHex;
  private string colorTwoHex;
  private string colorThreeHex;

  private int colorSelected = 1;
  private float additionalTiling = 1;
  private float displacementX;
  private float displacementY;

  // Event handling
  private bool draggingDisplacement;

  private float initialDragDifferenceX;
  private float initialDragDifferenceY;

  private bool showPatterns = true;
  private bool mouseOver;

  public delegate void SaveColor(Color r, Color g, Color b, PatternDef pattern,
    Vector2 displacement, float tiles);

  private Dialog_VehiclePainter(VehicleDef vehicleDef)
  {
    VehicleDef = vehicleDef;
  }

  private Dialog_VehiclePainter(VehiclePawn vehicle) : this(vehicle.VehicleDef)
  {
    Vehicle = vehicle;
  }

  private VehiclePawn Vehicle { get; }

  private VehicleDef VehicleDef { get; }

  private PatternData PatternData { get; set; }

  private List<PatternDef> AvailablePatterns { get; set; }

  /// <summary>
  /// ColorOne, ColorTwo, ColorThree, PatternDef, Displacement, Tiles
  /// </summary>
  private SaveColor OnSave { get; set; }

  // OnGUI manages to get 1 more call in even after dialog is removed from WindowStack, so it's
  // easiest to just block any further gui events once the RenderTextures have been returned to pool.
  private bool IsClosing { get; set; }

  private int CurrentSelectedPalette { get; set; }

  public override Vector2 InitialSize => new(900f, 540f);

  private Rot8 DisplayRotation
  {
    get { return displayRotation; }
    set
    {
      displayRotation = value;
      SetRenderTexturesDirty();
    }
  }

  private static string ColorToHex(Color col)
  {
    return ColorUtility.ToHtmlStringRGB(col);
  }

  private static bool HexToColor(string hexColor, out Color color)
  {
    return ColorUtility.TryParseHtmlString("#" + hexColor, out color);
  }

  /// <summary>
  /// Open ColorPicker for <paramref name="vehicle"/> and apply changes via <paramref name="onSave"/>
  /// </summary>
  /// <param name="vehicle"></param>
  /// <param name="onSave"></param>
  public static void OpenColorPicker(VehiclePawn vehicle, SaveColor onSave)
  {
    Dialog_VehiclePainter colorPickerDialog = new(vehicle)
    {
      OnSave = onSave,
      PatternData = new PatternData(vehicle)
    };
    Open(colorPickerDialog);
  }

  /// <summary>
  /// Open ColorPicker for <paramref name="vehicleDef"/> and apply changes via <paramref name="onSave"/>
  /// </summary>
  public static void OpenColorPicker(VehicleDef vehicleDef, SaveColor onSave)
  {
    Dialog_VehiclePainter colorPickerDialog = new(vehicleDef)
    {
      OnSave = onSave,
      PatternData =
        new PatternData(
          VehicleMod.settings.vehicles.defaultGraphics.TryGetValue(vehicleDef.defName,
            vehicleDef.graphicData)),
    };
    Open(colorPickerDialog);
  }

  private static void Open(Dialog_VehiclePainter colorPickerDialog)
  {
    colorPickerDialog.Init();
    Find.WindowStack.Add(colorPickerDialog);
  }

  private void Init()
  {
    additionalTiling = PatternData.tiles;
    displacementX = PatternData.displacement.x;
    displacementY = PatternData.displacement.y;
    SetColors(PatternData.color, PatternData.colorTwo, PatternData.colorThree);

    CurrentSelectedPalette = -1;

    doCloseX = true;
    forcePause = true;
    absorbInputAroundWindow = true;

    // Setting initial rotation will also dirty the render textures
    DisplayRotation = VehicleDef.drawProperties.displayRotation;
    RecacheAvailablePatterns();
  }

  private void SetRenderTexturesDirty()
  {
    previewDirty = true;
    for (int i = 0; i < samplesDirty.Length; i++)
    {
      samplesDirty[i] = true;
    }
  }

  private void RecacheAvailablePatterns()
  {
    if (showPatterns)
    {
      AvailablePatterns = DefDatabase<PatternDef>.AllDefsListForReading.Where(patternDef =>
        patternDef is not SkinDef && patternDef.ValidFor(VehicleDef)).ToList();
    }
    else
    {
      AvailablePatterns = DefDatabase<SkinDef>.AllDefsListForReading
       .Where(patternDef => patternDef.ValidFor(VehicleDef)).Cast<PatternDef>().ToList();
    }

    float ratio = (float)AvailablePatterns.Count / (GridDimensionColumns * GridDimensionRows);
    pageCount = Mathf.CeilToInt(ratio);
    pageNumber = 1;

    selectedPattern = AvailablePatterns.Contains(PatternData.patternDef) ?
      PatternData.patternDef :
      AvailablePatterns.FirstOrDefault();
    selectedPattern ??= PatternDefOf.Default;
    SetRenderTexturesDirty();
  }

  public override void PostClose()
  {
    base.PostClose();

    // For some reason, OnGUI can still end up executing for 1 more frame after PostClose is called,
    // which results in null textures being accessed. We can just skip this last frame.
    IsClosing = true;
    CustomCursor.Deactivate();
    previewBuffer?.Dispose();
    foreach (RenderTextureBuffer buffer in sampleBuffers)
    {
      buffer.Dispose();
    }
    sampleBuffers.Clear();
  }

  public override void PostOpen()
  {
    base.PostOpen();
    colorOneHex = ColorToHex(currentColorOne.ToColor);
    colorTwoHex = ColorToHex(currentColorTwo.ToColor);
    colorThreeHex = ColorToHex(currentColorThree.ToColor);
  }

  public override void DoWindowContents(Rect inRect)
  {
    if (IsClosing)
      return;

    using TextBlock fontBlock = new(GameFont.Small);

    if (Widgets.ButtonText(new Rect(0f, 0f, ButtonWidth * 1.5f, ButtonHeight),
      "VF_ResetColorPalettes".Translate()))
    {
      Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
        "VF_ResetColorPalettesConfirmation".Translate(),
        VehicleMod.settings.colorStorage.ResetPalettes));
    }

    if (VehicleDef.graphicData.drawRotated &&
      VehicleDef.graphicData.Graphic is Graphic_Vehicle graphicVehicle)
    {
      Rect rotateVehicleRect = new(inRect.width / 3 - 10 - ButtonHeight, 0, ButtonHeight,
        ButtonHeight);
      Widgets.DrawHighlightIfMouseover(rotateVehicleRect);
      TooltipHandler.TipRegionByKey(rotateVehicleRect, "VF_RotateVehicleRendering");
      Widgets.DrawTextureFitted(rotateVehicleRect, VehicleTex.ReverseIcon, 1);
      if (Widgets.ButtonInvisible(rotateVehicleRect))
      {
        SoundDefOf.Click.PlayOneShotOnCamera();
        List<Rot8> validRotations = graphicVehicle.RotationsRenderableByUI.ToList();
        for (int i = 0; i < 4; i++)
        {
          DisplayRotation = DisplayRotation.Rotated(RotationDirection.Clockwise, false);
          if (validRotations.Contains(DisplayRotation))
            break;
        }
      }
    }
    float topBoxHeight = inRect.height / 2 + SwitchSize;
    Rect colorContainerRect = new(inRect.width / 1.5f, 15f, inRect.width / 3, topBoxHeight);
    DrawColorSelection(colorContainerRect);

    Rect paintRect = new(inRect.width / 3f - 5f, colorContainerRect.y, inRect.width / 3,
      topBoxHeight);
    DrawPaintSelection(paintRect);

    float panelWidth = inRect.width * 2 / 3 + 5f;
    float panelHeight = inRect.height - topBoxHeight - 20;
    Rect paletteRect = new(inRect.width / 3f - 5f, inRect.height - panelHeight, panelWidth,
      panelHeight);
    DrawColorPalette(paletteRect);

    Rect displayRect = new(0, paintRect.y + ((paintRect.height / 2) - ((paintRect.width - 15) / 2)),
      paintRect.width - 15, paintRect.width - 15);

    HandleDisplacementDrag(displayRect);

    PatternData patternData = new(currentColorOne.ToColor, currentColorTwo.ToColor,
      currentColorThree.ToColor, selectedPattern, new Vector2(displacementX, displacementY),
      additionalTiling);

    // Begin Group
    Widgets.BeginGroup(displayRect);
    Rect vehicleRect = displayRect.AtZero();
    Widgets.DrawBoxSolidWithOutline(vehicleRect, Widgets.WindowBGFillColor, windowBGBorderColor);
    vehicleRect = vehicleRect.ContractedBy(FramePadding);
    if (previewDirty)
    {
      BlitRequest request = Vehicle != null ?
        BlitRequest.For(Vehicle) with { patternData = patternData, rot = DisplayRotation } :
        BlitRequest.For(VehicleDef) with { patternData = patternData, rot = DisplayRotation };
      previewBuffer ??= VehicleGui.CreateRenderTextureBuffer(vehicleRect, request);
      VehicleGui.Blit(previewBuffer.GetWrite(), vehicleRect, request);
      previewDirty = false;
    }
    // NOTE - We draw after any dirtying occurs so if the buffer performed a swap, we'll be drawing
    // with the render texture not being written to.
    GUI.DrawTexture(vehicleRect, previewBuffer.Read);
    Widgets.EndGroup();
    // End Group


    // Disable displacement sliders
    if (!selectedPattern.properties.dynamicTiling)
      GUIState.Disable();

    Rect sliderRect =
      new(0f, inRect.height - SliderHeight * 3, ButtonWidth * 3, SliderHeight);
    if (UIElements.SliderLabeled(sliderRect, "VF_PatternZoom".Translate(),
      "VF_PatternZoomTooltip".Translate(), string.Empty, ref additionalTiling, 0.01f, 2))
    {
      SetRenderTexturesDirty();
    }
    Rect positionLeftBox = new(sliderRect)
    {
      y = sliderRect.y + sliderRect.height,
      width = (sliderRect.width / 2) * 0.95f
    };
    Rect positionRightBox = new(positionLeftBox)
    {
      x = positionLeftBox.x + (sliderRect.width / 2) * 1.05f
    };

    if (UIElements.SliderLabeled(positionLeftBox, "VF_PatternDisplacementX".Translate(),
      "VF_PatternDisplacementXTooltip".Translate(), string.Empty, ref displacementX, -1.5f, 1.5f))
    {
      SetRenderTexturesDirty();
    }
    if (UIElements.SliderLabeled(positionRightBox, "VF_PatternDisplacementY".Translate(),
      "VF_PatternDisplacementYTooltip".Translate(), string.Empty, ref displacementY, -1.5f, 1.5f))
    {
      SetRenderTexturesDirty();
    }

    // Re-enable after displacement sliders
    GUIState.Enable();

    Rect buttonRect = new(0f, inRect.height - SliderHeight, ButtonWidth, SliderHeight);
    DoBottomButtons(buttonRect);
  }

  private void HandleDisplacementDrag(Rect rect)
  {
    if (selectedPattern.properties.dynamicTiling && Mouse.IsOver(rect))
    {
      if (!mouseOver && AssetBundleDatabase.MouseHandOpen)
      {
        mouseOver = true;
        Cursor.SetCursor(AssetBundleDatabase.MouseHandOpen, new Vector2(3, 3), CursorMode.Auto);
      }
      if (Input.GetMouseButtonDown(0) && !draggingDisplacement &&
        AssetBundleDatabase.MouseHandClosed)
      {
        draggingDisplacement = true;
        initialDragDifferenceX =
          Mathf.InverseLerp(0f, rect.width, Event.current.mousePosition.x - rect.x) * 2 - 1 -
          displacementX;
        initialDragDifferenceY =
          Mathf.InverseLerp(rect.height, 0f, Event.current.mousePosition.y - rect.y) * 2 - 1 -
          displacementY;
        Cursor.SetCursor(AssetBundleDatabase.MouseHandClosed, new Vector2(3, 3), CursorMode.Auto);
      }
      if (draggingDisplacement && Event.current.isMouse)
      {
        displacementX =
          (Mathf.InverseLerp(0f, rect.width, Event.current.mousePosition.x - rect.x) * 2 - 1 -
            initialDragDifferenceX).Clamp(-1.5f, 1.5f);
        displacementY =
          (Mathf.InverseLerp(rect.height, 0f, Event.current.mousePosition.y - rect.y) * 2 - 1 -
            initialDragDifferenceY).Clamp(-1.5f, 1.5f);
      }
      if (Input.GetMouseButtonUp(0) && AssetBundleDatabase.MouseHandOpen)
      {
        draggingDisplacement = false;
        Cursor.SetCursor(AssetBundleDatabase.MouseHandOpen, new Vector2(3, 3), CursorMode.Auto);
      }
    }
    else
    {
      if (mouseOver)
      {
        mouseOver = false;
        draggingDisplacement = false;
        CustomCursor.Activate();
      }
    }
  }

  private void DrawPaintSelection(Rect paintRect)
  {
    const float PaginationBarHeight = ButtonHeight * 0.75f;

    Widgets.DrawMenuSection(paintRect);

    string patternsLabel = "VF_Patterns".Translate();
    string skinsLabel = "VF_Skins".Translate();
    float labelHeight = Text.CalcSize(patternsLabel).y;
    Rect switchRect = new(paintRect.x, paintRect.y, paintRect.width / 2 - IconButtonSize / 2,
      labelHeight);

    Color patternLabelColor = showPatterns ? Color.white : Color.gray;
    Color skinLabelColor = !showPatterns ? Color.white : Color.gray;

    UIElements.DrawLabel(switchRect, patternsLabel, Color.clear, patternLabelColor,
      GameFont.Small, TextAnchor.MiddleRight);

    Rect toggleRect = new(switchRect.xMax, switchRect.y - IconButtonSize / 4 - 2,
      IconButtonSize, IconButtonSize);
    bool mouseOverToggle = Mouse.IsOver(toggleRect);
    if (Widgets.ButtonImage(toggleRect,
      showPatterns ? VehicleTex.SwitchLeft : VehicleTex.SwitchRight))
    {
      showPatterns = !showPatterns;
      RecacheAvailablePatterns();
      SetRenderTexturesDirty();
      SoundDefOf.Click.PlayOneShotOnCamera();
    }

    switchRect.x = toggleRect.xMax;
    UIElements.DrawLabel(switchRect, skinsLabel, Color.clear, skinLabelColor, GameFont.Small);

    Rect outRect = paintRect with
    {
      yMin = switchRect.yMax, yMax = paintRect.yMax - PaginationBarHeight
    };
    outRect = outRect.ContractedBy(10f);
    float gridSize = Mathf.Min(outRect.width, outRect.height) / GridDimensionColumns;

    Rect displayRect = new(0, 0, gridSize, gridSize);
    Rect paginationRect = new(paintRect.x + 5, paintRect.y + paintRect.height - ButtonHeight,
      paintRect.width - 10, PaginationBarHeight);
    if (pageCount > 1)
    {
      if (UIHelper.DrawPagination(paginationRect, ref pageNumber, pageCount))
      {
        SetRenderTexturesDirty();
      }
    }

    int startingIndex = (pageNumber - 1) * (GridDimensionColumns * GridDimensionRows);
    int maxIndex =
      (pageNumber * GridDimensionColumns * GridDimensionRows).Clamp(0, AvailablePatterns.Count);
    int iteration = 0;
    for (int i = startingIndex; i < maxIndex; i++, iteration++)
    {
      PatternDef pattern = AvailablePatterns[i];
      displayRect.x = outRect.x + iteration % GridDimensionColumns * gridSize;
      displayRect.y = outRect.y + Mathf.FloorToInt(iteration / (float)GridDimensionRows) * gridSize;
      PatternData patternData = new(currentColorOne.ToColor, currentColorTwo.ToColor,
        currentColorThree.ToColor, pattern, new Vector2(displacementX, displacementY),
        additionalTiling);

      // Begin Group
      Widgets.BeginGroup(displayRect);
      Rect vehicleRect = displayRect.AtZero();
      int sampleIdx = i - startingIndex;
      //Widgets.DrawMenuSection(vehicleRect.ContractedBy(FrameSpacing));
      vehicleRect = vehicleRect.ContractedBy(FramePadding);
      if (samplesDirty[sampleIdx])
      {
        BlitRequest blitRequest = Vehicle != null ?
          BlitRequest.For(Vehicle) with { patternData = patternData, rot = DisplayRotation } :
          BlitRequest.For(VehicleDef) with
          {
            patternData = patternData,
            rot = DisplayRotation
          };
        if (sampleBuffers.OutOfBounds(sampleIdx))
          sampleBuffers.Add(VehicleGui.CreateRenderTextureBuffer(vehicleRect, blitRequest));
        VehicleGui.Blit(sampleBuffers[sampleIdx].GetWrite(), vehicleRect, blitRequest);
        samplesDirty[sampleIdx] = false;
      }
      GUI.DrawTexture(vehicleRect, sampleBuffers[sampleIdx].Read);
      Widgets.EndGroup();
      // End Group

      Rect imageRect = new(displayRect.x, displayRect.y, gridSize, gridSize);
      if (!mouseOverToggle)
      {
        TooltipHandler.TipRegion(imageRect, pattern.LabelCap);
        if (Widgets.ButtonInvisible(imageRect))
        {
          selectedPattern = pattern;
          SetRenderTexturesDirty();
          SoundDefOf.Click.PlayOneShotOnCamera();
        }
      }
    }
  }

  private void DrawColorSelection(Rect colorContainerRect)
  {
    Rect colorRect = new(colorContainerRect);
    colorRect.x += 5f;
    colorRect.y = SwitchSize / 2 + 5f;
    colorRect.height = colorContainerRect.height - SwitchSize;

    Widgets.DrawMenuSection(colorContainerRect);

    string c1Text = "VF_ColorOne".Translate().ToString();
    string c2Text = "VF_ColorTwo".Translate().ToString();
    string c3Text = "VF_ColorThree".Translate().ToString();
    float cHeight = Text.CalcSize(c1Text).y;

    Rect colorPickerRect =
      colorPicker.Draw(colorRect, ref hue, ref saturation, ref value, SetColor);
    Rect buttonRect = new(colorPickerRect.x, cHeight - 2, colorPickerRect.width, cHeight);

    Rect c1Rect = new(buttonRect)
    {
      width = buttonRect.width / 3
    };
    Rect c2Rect = new(buttonRect)
    {
      x = buttonRect.x + buttonRect.width / 3,
      width = buttonRect.width / 3
    };
    Rect c3Rect = new(buttonRect)
    {
      x = buttonRect.x + (buttonRect.width / 3) * 2,
      width = buttonRect.width / 3
    };

    Rect reverseRect = new(colorContainerRect.x + 11f, 20, SwitchSize / 2.75f, SwitchSize / 2.75f);
    if (Widgets.ButtonImage(reverseRect, VehicleTex.ReverseIcon))
    {
      SetColors(currentColorTwo.ToColor, currentColorThree.ToColor, currentColorOne.ToColor);
      SoundDefOf.Click.PlayOneShotOnCamera();
    }
    TooltipHandler.TipRegion(reverseRect, "VF_SwapColors".Translate());

    Color c1Color = colorSelected == 1 ? Color.white : Color.gray;
    Color c2Color = colorSelected == 2 ? Color.white : Color.gray;
    Color c3Color = colorSelected == 3 ? Color.white : Color.gray;

    if (Mouse.IsOver(c1Rect) && colorSelected != 1)
    {
      c1Color = GenUI.MouseoverColor;
    }
    else if (Mouse.IsOver(c2Rect) && colorSelected != 2)
    {
      c2Color = GenUI.MouseoverColor;
    }
    else if (Mouse.IsOver(c3Rect) && colorSelected != 3)
    {
      c3Color = GenUI.MouseoverColor;
    }
    // ReSharper disable once RedundantArgumentDefaultValue
    // NOTE - TextAnchor optional parameter is more readable left in, as the other 2
    // parameters related to other alignments for the top Color label buttons
    UIElements.DrawLabel(c1Rect, c1Text, Color.clear, c1Color, GameFont.Small,
      TextAnchor.MiddleLeft);
    UIElements.DrawLabel(c2Rect, c2Text, Color.clear, c2Color, GameFont.Small,
      TextAnchor.MiddleCenter);
    UIElements.DrawLabel(c3Rect, c3Text, Color.clear, c3Color, GameFont.Small,
      TextAnchor.MiddleRight);

    if (colorSelected != 1 && Widgets.ButtonInvisible(c1Rect))
    {
      colorSelected = 1;
      SetColor(currentColorOne.ToColor);
      SoundDefOf.Click.PlayOneShotOnCamera();
    }
    if (colorSelected != 2 && Widgets.ButtonInvisible(c2Rect))
    {
      colorSelected = 2;
      SetColor(currentColorTwo.ToColor);
      SoundDefOf.Click.PlayOneShotOnCamera();
    }
    if (colorSelected != 3 && Widgets.ButtonInvisible(c3Rect))
    {
      colorSelected = 3;
      SetColor(currentColorThree.ToColor);
      SoundDefOf.Click.PlayOneShotOnCamera();
    }

    Rect inputRect = new(colorRect.x, colorRect.y + colorRect.height + 5f,
      colorRect.width / 2, 20f);
    ApplyActionSwitch(delegate(ref ColorInt c, ref string hex)
    {
      hex = UIElements.HexField("VF_ColorPickerHex".Translate(), inputRect, hex);
      if (HexToColor(hex, out Color color) && Mathf.Approximately(color.a, 1))
      {
        c = new ColorInt(color);
      }
    });

    string saveText = "VF_SaveColorPalette".Translate();
    inputRect.width = Text.CalcSize(saveText).x + 20f;
    inputRect.x = colorContainerRect.x + colorContainerRect.width - inputRect.width - 5f;
    if (Widgets.ButtonText(inputRect, saveText))
    {
      if (CurrentSelectedPalette is >= 0 and < ColorStorage.PaletteCount)
      {
        VehicleMod.settings.colorStorage.AddPalette(currentColorOne.ToColor,
          currentColorTwo.ToColor, currentColorThree.ToColor, CurrentSelectedPalette);
        SoundDefOf.Click.PlayOneShotOnCamera();
      }
      else
      {
        Messages.Message("VF_MustSelectColorPalette".Translate(), MessageTypeDefOf.RejectInput);
      }
    }
  }

  private void DrawColorPalette(Rect rect)
  {
    List<(Color, Color, Color)> palettes = VehicleMod.settings.colorStorage.colorPalette;
    Widgets.DrawMenuSection(rect);

    rect = rect.ContractedBy(5);

    float rectSize =
      (rect.width - ((float)ColorStorage.PaletteCount / ColorStorage.PaletteRowCount - 1) * 5f) /
      (ColorStorage.PaletteCountPerRow * 3);
    Rect displayRect = new(rect.x, rect.y, rectSize, rectSize);

    for (int i = 0; i < ColorStorage.PaletteCount; i++)
    {
      if (i % (ColorStorage.PaletteCount / ColorStorage.PaletteRowCount) == 0 && i != 0)
      {
        displayRect.y += rectSize + 5f;
        displayRect.x = rect.x;
      }
      Rect buttonRect = new(displayRect.x, displayRect.y, displayRect.width * 3,
        displayRect.height);
      if (Widgets.ButtonInvisible(buttonRect))
      {
        if (CurrentSelectedPalette == i)
        {
          CurrentSelectedPalette = -1;
        }
        else
        {
          CurrentSelectedPalette = i;
          SetColors(palettes[i].Item1, palettes[i].Item2, palettes[i].Item3);
        }
      }
      if (CurrentSelectedPalette == i)
      {
        Rect selectRect = buttonRect.ExpandedBy(1.5f);
        Widgets.DrawBoxSolid(selectRect, Color.white);
      }
      Widgets.DrawBoxSolid(displayRect, palettes[i].Item1);
      displayRect.x += rectSize;
      Widgets.DrawBoxSolid(displayRect, palettes[i].Item2);
      displayRect.x += rectSize;
      Widgets.DrawBoxSolid(displayRect, palettes[i].Item3);
      displayRect.x += rectSize + 5f;
    }
  }

  private void DoBottomButtons(Rect buttonRect)
  {
    if (Widgets.ButtonText(buttonRect, "VF_ApplyButton".Translate()))
    {
      OnSave(currentColorOne.ToColor, currentColorTwo.ToColor, currentColorThree.ToColor,
        selectedPattern, new Vector2(displacementX, displacementY), additionalTiling);
      Close();
    }
    buttonRect.x += ButtonWidth;
    if (Widgets.ButtonText(buttonRect, "CancelButton".Translate()))
    {
      Close();
    }
    buttonRect.x += ButtonWidth;
    if (Widgets.ButtonText(buttonRect, "ResetButton".Translate()))
    {
      SoundDefOf.Click.PlayOneShotOnCamera();
      selectedPattern = PatternData.patternDef;
      additionalTiling = PatternData.tiles;
      displacementX = PatternData.displacement.x;
      displacementY = PatternData.displacement.y;
      if (CurrentSelectedPalette >= 0)
      {
        (Color one, Color two, Color three) palette =
          VehicleMod.settings.colorStorage.colorPalette[CurrentSelectedPalette];
        SetColors(palette.one, palette.two, palette.three);
      }
      else
      {
        SetColors(PatternData.color, PatternData.colorTwo, PatternData.colorThree);
      }
    }
  }

  // I don't know what I did it this way, it saves on code I guess? But my god does it seem
  // like a poor implementation.. but it works so leave it be.
  private ColorInt ApplyActionSwitch(Utilities.ActionRef<ColorInt, string> action)
  {
    switch (colorSelected)
    {
      case 1:
        action(ref currentColorOne, ref colorOneHex);
        return currentColorOne;
      case 2:
        action(ref currentColorTwo, ref colorTwoHex);
        return currentColorTwo;
      case 3:
        action(ref currentColorThree, ref colorThreeHex);
        return currentColorThree;
      default:
        throw new ArgumentOutOfRangeException(nameof(colorSelected));
    }
  }

  private void SetColors(Color col1, Color col2, Color col3)
  {
    currentColorOne = new ColorInt(col1);
    currentColorTwo = new ColorInt(col2);
    currentColorThree = new ColorInt(col3);
    colorOneHex = ColorToHex(currentColorOne.ToColor);
    colorTwoHex = ColorToHex(currentColorTwo.ToColor);
    colorThreeHex = ColorToHex(currentColorThree.ToColor);
    ApplyActionSwitch(delegate(ref ColorInt c, ref string hex)
    {
      Color.RGBToHSV(c.ToColor, out hue, out saturation, out value);
      hex = ColorToHex(c.ToColor);
    });
    SetRenderTexturesDirty();
  }

  private void SetColor(Color col)
  {
    // Yea.. very poor design but this is OLD code and not worth the effort to rework
    // since it already functions quite well.
    ColorInt curColor = ApplyActionSwitch(delegate(ref ColorInt c, ref string hex)
    {
      c = new ColorInt(col);
      hex = ColorToHex(c.ToColor);
    });
    Color.RGBToHSV(curColor.ToColor, out hue, out saturation, out value);
    SetRenderTexturesDirty();
  }

  private bool SetColor(string hex)
  {
    if (HexToColor(hex, out Color color))
    {
      currentColorOne = new ColorInt(color);
      Color.RGBToHSV(currentColorOne.ToColor, out hue, out saturation, out value);
      SetRenderTexturesDirty();
      return true;
    }
    return false;
  }

  private void SetColor(float h, float s, float v)
  {
    _ = ApplyActionSwitch(delegate(ref ColorInt c, ref string hex)
    {
      c = new ColorInt(Color.HSVToRGB(h, s, v));
      hex = ColorToHex(c.ToColor);
    });
    SetRenderTexturesDirty();
  }
}