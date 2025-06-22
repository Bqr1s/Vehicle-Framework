using SmashTools.Patching;

namespace Vehicles
{
  public abstract class ConditionalVehiclePatch : ConditionalPatch
  {
    public override string SourceId => VehicleHarmony.VehiclesUniqueId;
  }
}