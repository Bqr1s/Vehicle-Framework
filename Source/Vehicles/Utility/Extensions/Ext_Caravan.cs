﻿using System;
using System.Collections.Generic;
using System.Linq;
using Verse;
using RimWorld;
using RimWorld.Planet;
using SmashTools;

namespace Vehicles
{
  public static class Ext_Caravan
  {
    /// <summary>
    /// Caravan contains one or more Vehicles
    /// </summary>
    /// <param name="c"></param>
    public static bool HasVehicle(this Caravan caravan)
    {
      return (caravan is VehicleCaravan vehicleCaravan && vehicleCaravan.pawns.HasVehicle()) ||
        (Dialog_FormVehicleCaravan.CurrentFormingCaravan != null &&
          TransferableUtility
           .GetPawnsFromTransferables(Dialog_FormVehicleCaravan.CurrentFormingCaravan.transferables)
           .HasVehicle());
    }

    /// <summary>
    /// Caravan contains one or more Boats
    /// </summary>
    /// <param name="c"></param>
    public static bool HasBoat(this Caravan caravan)
    {
      return (caravan is VehicleCaravan vehicleCaravan && vehicleCaravan.pawns.HasBoat()) ||
        (Dialog_FormVehicleCaravan.CurrentFormingCaravan != null &&
          TransferableUtility
           .GetPawnsFromTransferables(Dialog_FormVehicleCaravan.CurrentFormingCaravan.transferables)
           .HasBoat());
    }

    /// <summary>
    /// Get all unique Vehicles in Caravan <paramref name="caravan"/>
    /// </summary>
    /// <param name="caravan"></param>
    public static HashSet<VehicleDef> UniqueVehicleDefsInCaravan(this Caravan caravan)
    {
      var vehicleSet = new HashSet<VehicleDef>();
      foreach (VehiclePawn p in caravan.PawnsListForReading.Where(v => v is VehiclePawn))
      {
        vehicleSet.Add(p.VehicleDef);
      }

      return vehicleSet;
    }

    /// <summary>
    /// Validate if <paramref name="vehicle"/> is able to join <paramref name="vehicleCaravan"/> without causing caravan to not be able to path on world map with current path settings
    /// </summary>
    /// <param name="vehicleCaravan"></param>
    /// <param name="vehicle"></param>
    public static bool ViableForCaravan(this VehicleCaravan vehicleCaravan, VehiclePawn vehicle)
    {
      foreach (VehiclePawn caravanVehicle in vehicleCaravan.VehiclesListForReading)
      {
        if (!GridOwners.World.MatchingReachability(caravanVehicle.VehicleDef, vehicle.VehicleDef))
        {
          return false;
        }
      }

      return true;
    }

    /// <summary>
    /// Get all pawns from Caravan inside vehicles
    /// </summary>
    /// <param name="caravan"></param>
    public static List<Pawn> GrabPawnsFromVehicleCaravanSilentFail(this Caravan caravan)
    {
      if (caravan is null || !caravan.HasVehicle())
      {
        return null;
      }

      List<Pawn> vehicles = new List<Pawn>();
      foreach (Pawn p in caravan.PawnsListForReading)
      {
        if (p is VehiclePawn vehicle)
        {
          vehicles.AddRange(vehicle.AllPawnsAboard);
        }
        else
        {
          vehicles.Add(p);
        }
      }

      return vehicles;
    }

    public static void TransferPawnOrItem(this Caravan caravan, ThingOwner owner, Thing thing)
    {
      if (thing is Pawn)
      {
        owner.TryTransferToContainer(thing, caravan.pawns, canMergeWithExistingStacks: false);
      }
      else
      {
        Pawn giveToPawn =
          CaravanInventoryUtility.FindPawnToMoveInventoryTo(thing, caravan.PawnsListForReading,
            null);
        if (giveToPawn == null)
        {
          Log.Error($"Failed to give item {thing} to caravan {caravan}; item was lost.");
          thing.Destroy(DestroyMode.Vanish);
          return;
        }

        if (!giveToPawn.inventory.innerContainer.TryAddOrTransfer(thing))
        {
          Log.Error($"Failed to give item {thing} to caravan {caravan}; item was lost");
          thing.Destroy(DestroyMode.Vanish);
          return;
        }
      }
    }
  }
}