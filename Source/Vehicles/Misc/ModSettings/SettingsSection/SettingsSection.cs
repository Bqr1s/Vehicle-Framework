using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace Vehicles
{
  public abstract class SettingsSection : IExposable
  {
    protected static Listing_Standard listingStandard = new();
    protected static Listing_Settings listingSplit = new();

    public virtual IEnumerable<FloatMenuOption> ResetOptions
    {
      get
      {
        yield return new FloatMenuOption("VF_DevMode_ResetPage".Translate(), ResetSettings);
        yield return new FloatMenuOption("VF_DevMode_ResetAll".Translate(),
          VehicleMod.ResetAllSettings);
      }
    }

    public virtual Rect ButtonRect(Rect rect)
    {
      return new Rect(rect.x + 2.5f, rect.y - 2.5f, rect.width, rect.height);
    }

    public virtual void ResetSettings()
    {
      SoundDefOf.Click.PlayOneShotOnCamera();
    }

    public virtual void OnClose()
    {
    }

    public virtual void OnOpen()
    {
    }

    public virtual void Update()
    {
    }

    public abstract void OnGUI(Rect rect);

    public virtual void Initialize()
    {
    }

    public virtual void ExposeData()
    {
    }

    public virtual void PostDefDatabase()
    {
    }

    public virtual void VehicleSelected()
    {
    }
  }
}