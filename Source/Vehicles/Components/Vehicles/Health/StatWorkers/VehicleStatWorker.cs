﻿using System;
using System.Collections.Generic;
using System.Text;
using JetBrains.Annotations;
using SmashTools;
using UnityEngine;
using Verse;

namespace Vehicles;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
public class VehicleStatWorker
{
  public VehicleStatDef statDef;

  protected Dictionary<VehicleDef, float> baseValues;
  protected List<VehicleStatPart> statParts;

  private static readonly Gradient gradient = new()
  {
    colorKeys =
    [
      new GradientColorKey(Color.gray, 0),
      new GradientColorKey(TexData.RedReadable, 0.25f),
      new GradientColorKey(TexData.SevereDamage, 0.4f),
      new GradientColorKey(TexData.ModerateDamage, 0.7f),
      new GradientColorKey(TexData.MinorDamage, 0.75f),
      new GradientColorKey(TexData.WorkingCondition, 1),
      new GradientColorKey(TexData.Enhanced, 1.01f) //greater than 101% max efficiency
    ]
  };

  public virtual void InitStatWorker(VehicleStatDef statDef)
  {
    this.statDef = statDef;
    baseValues = new Dictionary<VehicleDef, float>();
  }

  public virtual float GetValue(VehiclePawn vehicle)
  {
    float value = GetBaseValue(vehicle.VehicleDef);
    value = TransformValue(vehicle, value);
    value *= StatEfficiency(vehicle);
    return value.Clamp(statDef.minValue, statDef.maxValue);
  }

  public virtual float GetValueAbstract(VehicleDef vehicleDef)
  {
    float value = GetBaseValue(vehicleDef);
    return value;
  }

  public virtual float StatEfficiency(VehiclePawn vehicle)
  {
    return vehicle.statHandler.StatEfficiency(statDef);
  }

  public float RecacheBaseValue(VehicleDef vehicleDef)
  {
    float value = statDef.defaultBaseValue;
    foreach (VehicleStatModifier statModifier in vehicleDef.vehicleStats)
    {
      if (statModifier.statDef == statDef)
      {
        value = statModifier.value;
        statParts = statModifier.parts;
        break;
      }
    }
    baseValues[vehicleDef] = value;
    return value;
  }

  public virtual string StatValueFormatted(VehiclePawn vehicle)
  {
    string output = GetValue(vehicle).ToStringByStyle(statDef.toStringStyle);
    if (!statDef.formatString.NullOrEmpty())
    {
      output = string.Format(statDef.formatString, output);
    }
    return output;
  }

  public virtual float GetBaseValue(VehiclePawn vehicle)
  {
    return GetBaseValue(vehicle.VehicleDef);
  }

  public virtual float GetBaseValue(VehicleDef vehicleDef)
  {
    if (baseValues.TryGetValue(vehicleDef, out float value))
    {
      return SettingsCache.TryGetValue(vehicleDef, statDef, value);
    }
    value = RecacheBaseValue(vehicleDef);
    return SettingsCache.TryGetValue(vehicleDef, statDef, value);
  }

  public virtual float TransformValue(VehiclePawn vehicle, float value)
  {
    if (!statDef.parts.NullOrEmpty())
    {
      foreach (VehicleStatPart statPart in statDef.parts)
      {
        value = statPart.TransformValue(vehicle, value);
      }
    }
    if (!statParts.NullOrEmpty())
    {
      foreach (VehicleStatPart statPart in statParts)
      {
        value = statPart.TransformValue(vehicle, value);
      }
    }
    float offset = vehicle.statHandler.GetStatOffset(statDef);
    return value + offset;
  }

  public virtual bool IsDisabledFor(VehiclePawn vehicle)
  {
    if (statDef.neverDisabled)
    {
      return false;
    }
    if (!statDef.parts.NullOrEmpty())
    {
      foreach (VehicleStatPart statPart in statDef.parts)
      {
        if (statPart.Disabled(vehicle))
        {
          return true;
        }
      }
    }
    return false;
  }

  public virtual bool ShouldShowFor(VehicleDef vehicleDef)
  {
    if (statDef.alwaysHide)
    {
      return false;
    }
    if (!statDef.showIfUndefined && !vehicleDef.vehicleStats.StatListContains(statDef))
    {
      return false;
    }
    if (!statDef.CanShowWithLoadedMods())
    {
      return false;
    }
    if (!statDef.CanShowWithVehicle(vehicleDef))
    {
      return false;
    }
    return true;
  }

  public virtual string TipSignal(VehiclePawn vehicle)
  {
    return statDef.description;
  }

  public virtual string GetStatDrawEntryLabel(VehicleStatDef stat, float value,
    ToStringNumberSense numberSense, bool finalized = true)
  {
    return stat.ValueToString(value, finalized, numberSense);
  }

  public string GetExplanationFull(VehicleDef vehicleDef, ToStringNumberSense numberSense,
    float value, VehiclePawn forVehicle = null)
  {
    if (IsDisabledFor(forVehicle))
    {
      return "StatsReport_PermanentlyDisabled".Translate();
    }
    string text = statDef.Worker.GetExplanationUnfinalized(vehicleDef, numberSense, forVehicle)
     .TrimEndNewlines();
    if (!text.NullOrEmpty())
    {
      text += Environment.NewLine + Environment.NewLine;
    }
    if (forVehicle != null)
    {
      text += statDef.Worker.GetExplanationFinalizePart(forVehicle, numberSense, value);
    }
    return text;
  }

  public virtual string GetExplanationUnfinalized(VehicleDef vehicleDef,
    ToStringNumberSense numberSense, VehiclePawn forVehicle = null)
  {
    StringBuilder stringBuilder = new();
    float baseValueFor = GetBaseValue(vehicleDef);
    if (baseValueFor != 0f || statDef.showZeroBaseValue)
    {
      stringBuilder.AppendLine(
        $"{"StatsReport_BaseValue".Translate()}: {statDef.ValueToString(baseValueFor, numberSense: numberSense)}");
    }
    // TODO - add stuffable support for vehicles
    if (forVehicle?.Stuff != null)
    {
      if (baseValueFor > 0f || statDef.applyFactorsIfNegative)
      {
        //float statFactorFromList3 = vehicle.Stuff.stuffProps.statFactors.GetStatFactorFromList(this.stat);
        //if (statFactorFromList3 != 1f)
        //{
        //	stringBuilder.AppendLine("StatsReport_Material".Translate() + " (" + req.StuffDef.LabelCap + "): " + statFactorFromList3.ToStringByStyle(ToStringStyle.PercentZero, ToStringNumberSense.Factor));
        //}
      }
      //float statOffsetFromList2 = vehicle.Stuff.stuffProps.statOffsets.GetStatOffsetFromList(this.stat);
      //if (statOffsetFromList2 != 0f)
      //{
      //	stringBuilder.AppendLine("StatsReport_Material".Translate() + " (" + req.StuffDef.LabelCap + "): " + statOffsetFromList2.ToStringByStyle(this.stat.toStringStyle, ToStringNumberSense.Offset));
      //}
    }
    if (!statDef.statFactors.NullOrEmpty())
    {
      stringBuilder.AppendLine("StatsReport_OtherStats".Translate());
      foreach (VehicleStatDef statFactor in statDef.statFactors)
      {
        string value = forVehicle != null ?
          statFactor.Worker.GetValue(forVehicle).ToStringPercent() :
          statFactor.Worker.GetValueAbstract(vehicleDef).ToStringPercent();
        stringBuilder.AppendLine($"    {statFactor.LabelCap}: {value}");
      }
    }
    return stringBuilder.ToString();
  }

  public virtual string GetExplanationFinalizePart(VehiclePawn vehicle,
    ToStringNumberSense numberSense, float finalValue)
  {
    StringBuilder stringBuilder = new();
    if (statDef.parts != null)
    {
      foreach (VehicleStatPart statPart in statDef.parts)
      {
        string text = statPart.ExplanationPart(vehicle);
        if (!text.NullOrEmpty())
        {
          stringBuilder.AppendLine(text);
        }
      }
    }
    if (vehicle.VehicleDef.vehicleStats?.FirstOrDefault(statMod => statMod.statDef == statDef) is
        { } statModifier && !statModifier.parts.NullOrEmpty())
    {
      foreach (VehicleStatPart statPart in statModifier.parts)
      {
        string text = statPart.ExplanationPart(vehicle);
        if (!text.NullOrEmpty())
        {
          stringBuilder.AppendLine(text);
        }
      }
    }
    if (statDef.postProcessCurve != null)
    {
      float value = GetValue(vehicle);
      float num = statDef.postProcessCurve.Evaluate(value);
      if (!Mathf.Approximately(value, num))
      {
        string valueString = ValueToString(value, false);
        string valueStringFormatted = statDef.ValueToString(num, true, numberSense);
        stringBuilder.AppendLine(
          $"{"StatsReport_PostProcessed".Translate()}: {valueString} => {valueStringFormatted}");
      }
    }
    if (statDef.postProcessStatFactors != null)
    {
      stringBuilder.AppendLine("StatsReport_OtherStats".Translate());
      foreach (VehicleStatDef statFactor in statDef.postProcessStatFactors)
      {
        stringBuilder.AppendLine(
          $"    {statFactor.LabelCap}: x{statFactor.Worker.GetValue(vehicle).ToStringPercent()}");
      }
    }
    stringBuilder.Append(
      $"{"StatsReport_FinalValue".Translate()}:  {statDef.ValueToString(finalValue, numberSense: statDef.toStringNumberSense)}");
    return stringBuilder.ToString();
  }

  public virtual float DrawVehicleStat(Rect leftRect, float curY, VehiclePawn vehicle)
  {
    Rect rect = new(0f, curY, leftRect.width, 20f);
    if (Mouse.IsOver(rect))
    {
      GUI.color = TexData.HighlightColor;
      GUI.DrawTexture(rect, TexUI.HighlightTex);
    }
    GUI.color = Color.white;
    Widgets.Label(new Rect(0f, curY, leftRect.width * 0.65f, 30f), statDef.LabelCap);
    float baseValue = GetBaseValue(vehicle.VehicleDef);
    if (statDef.operationType > EfficiencyOperationType.None)
    {
      Color effColor = baseValue == 0 ?
        TexData.WorkingCondition :
        gradient.Evaluate(GetValue(vehicle) / baseValue);
      Widgets.Label(new Rect(leftRect.width * 0.65f, curY, leftRect.width * 0.35f, 30f),
        StatValueFormatted(vehicle).Colorize(effColor));
      Rect rect2 = new(0f, curY, leftRect.width, 20f);
      if (Mouse.IsOver(rect2))
      {
        TooltipHandler.TipRegion(rect2,
          new TipSignal(TipSignal(vehicle), vehicle.thingIDNumber ^ statDef.index));
      }
    }
    curY += 20f;
    return curY;
  }

  public virtual string ValueToString(float val, bool finalized,
    ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
  {
    if (!finalized)
    {
      string text = val.ToStringByStyle(statDef.ToStringStyleUnfinalized, numberSense);
      if (numberSense != ToStringNumberSense.Factor &&
        !statDef.formatStringUnfinalized.NullOrEmpty())
      {
        text = string.Format(statDef.formatStringUnfinalized, text);
      }
      return text;
    }
    string text2 = val.ToStringByStyle(statDef.toStringStyle, numberSense);
    if (numberSense != ToStringNumberSense.Factor && !statDef.formatString.NullOrEmpty())
    {
      text2 = string.Format(statDef.formatString, text2);
    }
    return text2;
  }

  public virtual IEnumerable<Dialog_InfoCard.Hyperlink> GetInfoCardHyperlinks(VehiclePawn vehicle)
  {
    if (statDef.parts != null)
    {
      foreach (VehicleStatPart statPart in statDef.parts)
      {
        foreach (Dialog_InfoCard.Hyperlink hyperlink in statPart.GetInfoCardHyperlinks(vehicle))
        {
          yield return hyperlink;
        }
      }
    }
  }
}