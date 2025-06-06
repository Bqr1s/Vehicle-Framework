using System.Collections.Generic;
using SmashTools;
using Verse;

namespace Vehicles;

public class SoundUpgrade : Upgrade
{
  public List<VehicleSoundEventEntry<VehicleEventDef>> addOneShots;

  public List<VehicleSoundEventEntry<VehicleEventDef>> removeOneShots;

  public List<VehicleSustainerEventEntry<VehicleEventDef>> addSustainers;

  public List<VehicleSustainerEventEntry<VehicleEventDef>> removeSustainers;

  public override bool UnlockOnLoad => true;

  public override void Unlock(VehiclePawn vehicle, bool unlockingPostLoad)
  {
    if (!removeOneShots.NullOrEmpty())
    {
      foreach (VehicleSoundEventEntry<VehicleEventDef> soundEventEntry in removeOneShots)
      {
        vehicle.RemoveEvent(soundEventEntry.key, soundEventEntry.removalKey);
      }
    }
    if (!addOneShots.NullOrEmpty())
    {
      foreach (VehicleSoundEventEntry<VehicleEventDef> soundEventEntry in addOneShots)
      {
        vehicle.AddEvent(soundEventEntry.key, () => vehicle.PlayOneShotOnVehicle(soundEventEntry),
          soundEventEntry.removalKey);
      }
    }
    if (!removeSustainers.NullOrEmpty())
    {
      foreach (VehicleSustainerEventEntry<VehicleEventDef> soundEventEntry in removeSustainers)
      {
        vehicle.sustainers.EndAll(soundEventEntry.value);

        vehicle.RemoveEvent(soundEventEntry.start, soundEventEntry.removalKey);
        vehicle.RemoveEvent(soundEventEntry.stop, soundEventEntry.removalKey);
      }
    }
    if (!addSustainers.NullOrEmpty())
    {
      foreach (VehicleSustainerEventEntry<VehicleEventDef> soundEventEntry in addSustainers)
      {
        vehicle.AddEvent(soundEventEntry.start,
          () => vehicle.StartSustainerOnVehicle(soundEventEntry), soundEventEntry.removalKey);
        vehicle.AddEvent(soundEventEntry.stop,
          () => vehicle.StopSustainerOnVehicle(soundEventEntry), soundEventEntry.removalKey);
      }
    }
  }

  public override void Refund(VehiclePawn vehicle)
  {
    if (!addOneShots.NullOrEmpty())
    {
      foreach (VehicleSoundEventEntry<VehicleEventDef> soundEventEntry in addOneShots)
      {
        vehicle.RemoveEvent(soundEventEntry.key, soundEventEntry.removalKey);
      }
    }
    if (!removeOneShots.NullOrEmpty())
    {
      foreach (VehicleSoundEventEntry<VehicleEventDef> soundEventEntry in removeOneShots)
      {
        vehicle.AddEvent(soundEventEntry.key,
          () => vehicle.PlayOneShotOnVehicle(soundEventEntry), soundEventEntry.removalKey);
      }
    }
    if (!addSustainers.NullOrEmpty())
    {
      foreach (VehicleSustainerEventEntry<VehicleEventDef> soundEventEntry in addSustainers)
      {
        vehicle.sustainers.EndAll(soundEventEntry.value);

        vehicle.RemoveEvent(soundEventEntry.start, soundEventEntry.removalKey);
        vehicle.RemoveEvent(soundEventEntry.stop, soundEventEntry.removalKey);
      }
    }
    if (!removeSustainers.NullOrEmpty())
    {
      foreach (VehicleSustainerEventEntry<VehicleEventDef> soundEventEntry in removeSustainers)
      {
        vehicle.AddEvent(soundEventEntry.start,
          () => vehicle.StartSustainerOnVehicle(soundEventEntry),
          soundEventEntry.removalKey);
        vehicle.AddEvent(soundEventEntry.stop,
          () => vehicle.StopSustainerOnVehicle(soundEventEntry), soundEventEntry.removalKey);
      }
    }
  }
}