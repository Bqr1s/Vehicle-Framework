using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;
using Verse.AI;
using RimWorld;
using SmashTools;

namespace Vehicles;

public class WorkGiver_RefuelVehicle : WorkGiver_Scanner
{
  public override PathEndMode PathEndMode => PathEndMode.Touch;

  public virtual JobDef JobStandard => JobDefOf_Vehicles.RefuelVehicle;

  public virtual JobDef JobAtomic => JobDefOf_Vehicles.RefuelVehicleAtomic;

  public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
  {
    VehicleReservationManager resManager = pawn.Map
     .GetCachedMapComponent<VehicleReservationManager>();
    return resManager.VehicleListers(ReservationType.Refuel);
  }

  public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
  {
    return t is VehiclePawn { CompFueledTravel: not null, vehiclePather.Moving: false } vehicle &&
      CanRefuel(pawn, vehicle, forced);
  }

  public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
  {
    if (t is VehiclePawn { CompFueledTravel: not null } vehicle)
    {
      Thing closestFuel = vehicle.CompFueledTravel.ClosestFuelAvailable(pawn);
      if (closestFuel is null)
        return null;
      return JobMaker.MakeJob(JobDefOf_Vehicles.RefuelVehicle, vehicle, closestFuel);
    }
    return null;
  }

  [UsedImplicitly]
  public static bool CanRefuel(Pawn pawn, VehiclePawn vehicle, bool forced = false)
  {
    CompFueledTravel compFueler = vehicle.CompFueledTravel;
    if (compFueler is null)
      return false;

    if (vehicle.Faction != pawn.Faction)
      return false;

    // Disallowed
    if (compFueler.FuelLeaking)
      return false;

    // Unneeded
    if (compFueler.FullTank || (!forced && !compFueler.ShouldAutoRefuelNow))
      return false;

    // Forbidden
    if (vehicle.IsForbidden(pawn) || !pawn.CanReserve(vehicle, ignoreOtherReservations: forced))
      return false;

    if (compFueler.ClosestFuelAvailable(pawn) is null)
    {
      JobFailReason.Is("NoFuelToRefuel".Translate(compFueler.Props.fuelType));
      return false;
    }
    return true;
  }
}