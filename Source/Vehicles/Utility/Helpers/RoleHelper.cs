using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;
using SmashTools;

namespace Vehicles
{
  public static class RoleHelper
  {
    public static void Distribute(List<VehiclePawn> vehicles, List<Pawn> pawns)
    {
      DistributeOnPriority(vehicles, pawns, HandlingType.Movement);
      DistributeOnPriority(vehicles, pawns, HandlingType.Turret);
      DistributeOnPriority(vehicles, pawns);
    }

    private static void DistributeOnPriority(List<VehiclePawn> vehicles, List<Pawn> pawns,
      HandlingType? requiredHandling = null)
    {
      foreach (VehiclePawn vehicle in vehicles)
      {
        foreach (VehicleRoleHandler handler in vehicle.handlers)
        {
          if (requiredHandling != null && !handler.role.HandlingTypes.HasFlag(requiredHandling))
          {
            continue;
          }
          while (handler.AreSlotsAvailable && pawns.Count > 0)
          {
            Pawn pawn = pawns.Pop();
            handler.thingOwner.TryAddOrTransfer(pawn);
          }
        }
      }
    }
  }
}