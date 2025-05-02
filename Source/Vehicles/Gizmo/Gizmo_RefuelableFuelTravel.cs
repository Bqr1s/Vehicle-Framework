using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using Verse.Steam;

namespace Vehicles.Rendering;

[StaticConstructorOnStartup]
public class Gizmo_RefuelableFuelTravel : Gizmo_Slider
{
  private const float FuelIconSize = 24;

  private readonly CompFueledTravel refuelable;
  private readonly bool showVehicleLabel;

  public Gizmo_RefuelableFuelTravel(CompFueledTravel refuelable, bool showVehicleLabel)
  {
    this.refuelable = refuelable;
    this.showVehicleLabel = showVehicleLabel;
    Order = -100f;
  }

  protected override float Target
  {
    get { return refuelable.TargetFuelPercent; }
    set { refuelable.TargetFuelPercent = value; }
  }

  protected override float ValuePercent => refuelable.FuelPercent;

  protected override string Title =>
    showVehicleLabel ? refuelable.Vehicle.LabelCap : refuelable.Props.GizmoLabel;

  protected override bool IsDraggable => true;

  protected override string BarLabel
  {
    get
    {
      return
        $"{refuelable.Fuel.ToStringDecimalIfSmall()} / {refuelable.FuelCapacity.ToStringDecimalIfSmall()}";
    }
  }

  protected override string GetTooltip()
  {
    return string.Empty;
  }

  public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
  {
    if (SteamDeck.IsSteamDeckInNonKeyboardMode)
      return base.GizmoOnGUI(topLeft, maxWidth, parms);

    KeyCode hotKeyCode = KeyBindingDefOf.Command_ItemForbid.MainKey;
    if (hotKeyCode != KeyCode.None && !GizmoGridDrawer.drawnHotKeys.Contains(hotKeyCode))
    {
      if (KeyBindingDefOf.Command_ItemForbid.KeyDownEvent)
      {
        ToggleAutoRefuel();
        Event.current.Use();
      }
    }
    return base.GizmoOnGUI(topLeft, maxWidth, parms);
  }

  protected override void DrawHeader(Rect headerRect, ref bool mouseOverElement)
  {
    headerRect.xMax -= FuelIconSize;
    Rect iconRect = new(headerRect.xMax, headerRect.y, FuelIconSize, FuelIconSize);
    GUI.DrawTexture(iconRect, refuelable.Props.FuelIcon);
    Rect subIconRect =
      new(iconRect.center.x, iconRect.y, iconRect.width / 2f, iconRect.height / 2f);
    GUI.DrawTexture(subIconRect,
      refuelable.allowAutoRefuel ? Widgets.CheckboxOnTex : Widgets.CheckboxOffTex);
    if (Widgets.ButtonInvisible(iconRect))
    {
      ToggleAutoRefuel();
    }

    if (Mouse.IsOver(iconRect))
    {
      Widgets.DrawHighlight(iconRect);
      TooltipHandler.TipRegion(iconRect, RefuelTip, "RefuelTip".GetHashCode());
      mouseOverElement = true;
    }

    base.DrawHeader(headerRect, ref mouseOverElement);
  }

  private void ToggleAutoRefuel()
  {
    refuelable.allowAutoRefuel = !refuelable.allowAutoRefuel;

    if (refuelable.allowAutoRefuel)
      SoundDefOf.Tick_High.PlayOneShotOnCamera();
    else
      SoundDefOf.Tick_Low.PlayOneShotOnCamera();
  }

  private string RefuelTip()
  {
    StringBuilder tooltip = UIHelper.tooltipBuilder;
    tooltip.Clear();
    tooltip.AppendLine("CommandToggleAllowAutoRefuel".Translate());
    tooltip.AppendLine();
    tooltip.AppendLine();
    tooltip.AppendLine("CommandToggleAllowAutoRefuelDesc".Translate(refuelable.TargetFuelLevel
         .ToString("F0")
         .Colorize(ColoredText.TipSectionTitleColor),
        (refuelable.allowAutoRefuel ? "On".TranslateSimple() : "Off".TranslateSimple())
       .UncapitalizeFirst()
       .Named("ONOFF")
      )
     .Resolve());
    tooltip.AppendLine();
    tooltip.AppendLine();
    tooltip.AppendLine(
      $"{"HotKeyTip".Translate()}: {KeyPrefs.KeyPrefsData.GetBoundKeyCode(KeyBindingDefOf.Command_ItemForbid, KeyPrefs.BindingSlot.A).ToStringReadable()}");
    string text = tooltip.ToString();
    tooltip.Clear();
    return text;
  }
}