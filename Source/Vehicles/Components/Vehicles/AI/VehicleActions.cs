using JetBrains.Annotations;

namespace Vehicles;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public static class VehicleActions
{
  /// <summary>
  /// static hook for <see cref="VehiclePawn.DisembarkAll"/> used for event actions registered from xml
  /// </summary>
  public static void DisembarkAll(VehiclePawn vehicle)
  {
    vehicle.DisembarkAll();
  }
}