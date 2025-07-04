﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using SmashTools;
using Verse;
using Verse.AI;

namespace Vehicles;

#nullable enable annotations

[PublicAPI]
public class VehicleReservationManager : MapComponent
{
  private const int ReservationVerificationInterval = 120;

  private Dictionary<VehiclePawn, VehicleReservationCollection> reservations = [];
  private Dictionary<VehiclePawn, VehicleRequestCollection> vehicleListers = [];

  // Serialization
  private List<VehiclePawn> vehiclesReserving_tmp = [];
  private List<VehicleReservationCollection> vehicleReservations_tmp = [];
  private List<VehiclePawn> vehicleListerPawns_tmp = [];
  private List<VehicleRequestCollection> vehicleListerRequests_tmp = [];

  public VehicleReservationManager(Map map) : base(map)
  {
  }

  public bool Reserve<T1, T2>(VehiclePawn vehicle, Pawn pawn, Job job, T1 target)
    where T2 : Reservation<T1>
  {
    try
    {
      ReleaseAllClaimedBy(pawn);

      if (GetReservation<T2>(vehicle) is { } reversionSubType)
      {
        return reversionSubType.CanReserve(pawn, target) &&
          reversionSubType.AddClaimant(pawn, target);
      }
      int maxClaimaints = vehicle.TotalAllowedFor(job.def);

      if (!reservations.TryGetValue(vehicle,
        out VehicleReservationCollection reservationCollection))
      {
        reservationCollection = new VehicleReservationCollection();
        reservations[vehicle] = reservationCollection;
      }
      reservationCollection.Add(
        (ReservationBase)Activator.CreateInstance(typeof(T2), vehicle, job, maxClaimaints));

      reversionSubType = GetReservation<T2>(vehicle);
      if (reversionSubType == null)
      {
        Log.Error(
          $"Unable to retrieve reservation for {pawn} performing Job={job} from new reservation.");
        return false;
      }
      reversionSubType.AddClaimant(pawn, target);
    }
    catch (Exception ex)
    {
      Log.Error($"Exception thrown while attempting to reserve Vehicle based job. {ex}");
      return false;
    }
    return true;
  }

  public T? GetReservation<T>(VehiclePawn vehicle) where T : ReservationBase
  {
    if (!reservations.TryGetValue(vehicle, out VehicleReservationCollection vehicleReservations))
    {
      return null;
    }
    foreach (ReservationBase reservation in vehicleReservations.List)
    {
      if (reservation is T matchingReservation)
        return matchingReservation;
    }
    return null;
  }

  public void ReleaseAllClaims()
  {
    foreach (VehiclePawn vehicle in
      reservations.Keys.ToList()) //Repack in list to avoid modifying enumerator
    {
      ClearReservedFor(vehicle);
    }
  }

  public void ReleaseAllClaimedBy(Pawn pawn)
  {
    //Only 1 vehicle will have reservations by this pawn, but no way to know which without checking all
    foreach (VehicleReservationCollection vehicleReservations in reservations.Values)
    {
      foreach (ReservationBase reservation in vehicleReservations.List)
      {
        reservation.ReleaseReservationBy(pawn);
      }
    }
  }

  public void ClearReservedFor(VehiclePawn vehicle)
  {
    if (reservations.ContainsKey(vehicle))
    {
      reservations[vehicle].List.ForEach(reservation => reservation.ReleaseAllReservations());
      reservations.Remove(vehicle);
    }
  }

  public bool CanReserve(VehiclePawn vehicle, Pawn pawn, JobDef jobDef,
    StringBuilder? stringBuilder = null)
  {
    stringBuilder?.AppendLine("Starting Reservation check.");
    if (reservations.TryGetValue(vehicle, out VehicleReservationCollection vehicleReservations))
    {
      foreach (ReservationBase reservation in vehicleReservations.List)
      {
        if (reservation.JobDef == jobDef)
        {
          stringBuilder?.AppendLine(
            $"Reservation cached. Claimants = {vehicle.TotalAllowedFor(jobDef)}/{reservation.TotalClaimants}.");
          return vehicle.TotalAllowedFor(jobDef) > reservation.TotalClaimants;
        }
      }
    }
    stringBuilder?.AppendLine("Reservation not cached. Can automatically reserve");
    return true;
  }

  public bool CanReserve<T1, T2>(VehiclePawn vehicle, Pawn pawn, T1 target,
    StringBuilder? stringBuilder = null) where T2 : Reservation<T1>
  {
    if (reservations.TryGetValue(vehicle, out VehicleReservationCollection vehicleReservations))
    {
      foreach (ReservationBase reservation in vehicleReservations.List)
      {
        if (reservation is T2 reversionSubType)
        {
          bool canReserve = reversionSubType.CanReserve(pawn, target, stringBuilder);
          stringBuilder?.AppendLine(
            $"Reservation cached. Type={typeof(T2)} Summary={stringBuilder}");
          return canReserve;
        }
      }
    }
    stringBuilder?.AppendLine("Reservation not cached. Can automatically reserve");
    return true;
  }

  public bool ReservedBy<T1, T2>(VehiclePawn vehicle, Pawn pawn, T1 target,
    StringBuilder? stringBuilder = null) where T2 : Reservation<T1>
  {
    if (reservations.TryGetValue(vehicle, out VehicleReservationCollection vehicleReservations))
    {
      foreach (ReservationBase reservation in vehicleReservations.List)
      {
        if (reservation is T2 reservationSubType)
        {
          bool reserved = reservationSubType.ReservedBy(pawn, target);
          stringBuilder?.AppendLine($"Reserved={reserved}");
          return reserved;
        }
      }
    }
    stringBuilder?.AppendLine("Reservation not cached.");
    return true;
  }

  public int TotalReserving(VehiclePawn vehicle)
  {
    if (reservations.TryGetValue(vehicle, out VehicleReservationCollection vehicleReservations))
    {
      int total = 0;
      foreach (ReservationBase reservation in vehicleReservations.List)
      {
        total += reservation.TotalClaimants;
      }
      return total;
    }
    return 0;
  }

  public override void MapComponentTick()
  {
    if (Find.TickManager.TicksGame % ReservationVerificationInterval == 0)
    {
      List<KeyValuePair<VehiclePawn, VehicleReservationCollection>> vehicleRegistry =
        reservations.ToList();

      for (int i = vehicleRegistry.Count - 1; i >= 0; i--)
      {
        (VehiclePawn vehicle, VehicleReservationCollection vehicleReservations) =
          vehicleRegistry[i];
        for (int j = vehicleReservations.Count - 1; j >= 0; j--)
        {
          ReservationBase reservation = vehicleReservations[j];
          reservation.VerifyAndValidateClaimants();
          if (reservation.RemoveNow)
            reservations[vehicle].Remove(reservation);
        }
        if (reservations[vehicle].NullOrEmpty())
          reservations.Remove(vehicle);
      }
    }
  }

  public override void FinalizeInit()
  {
    base.FinalizeInit();

    VerifyCollection(ref vehiclesReserving_tmp);
    VerifyCollection(ref vehicleReservations_tmp);
    VerifyCollection(ref vehicleListerPawns_tmp);
    VerifyCollection(ref vehicleListerRequests_tmp);
  }

  private static void VerifyCollection<T>(ref List<T?> list)
  {
    list ??= [];
    for (int i = list.Count - 1; i >= 0; i++)
    {
      if (list[i] is null)
        list.RemoveAt(i);
    }
  }

  public bool VehicleListed(VehiclePawn vehicle, string request)
  {
    return vehicleListers.TryGetValue(vehicle, out VehicleRequestCollection collection) &&
      collection.requests.Contains(request);
  }

  public IEnumerable<VehiclePawn> VehicleListers(string request)
  {
    foreach ((VehiclePawn vehicle, VehicleRequestCollection reqCollection) in vehicleListers)
    {
      if (reqCollection.requests.Contains(request))
        yield return vehicle;
    }
  }

  public bool RegisterLister(VehiclePawn vehicle, string request)
  {
    if (vehicleListers.TryGetValue(vehicle, out VehicleRequestCollection collection))
    {
      if (!collection.requests.NotNullAndAny())
      {
        vehicleListers[vehicle] = new VehicleRequestCollection(request);
        return true;
      }
      return collection.requests.Add(request);
    }
    vehicleListers.Add(vehicle, new VehicleRequestCollection(request));
    return true;
  }

  public bool RemoveLister(VehiclePawn vehicle, string request)
  {
    if (vehicleListers.TryGetValue(vehicle, out VehicleRequestCollection collection))
    {
      return collection.requests.NullOrEmpty() ?
        vehicleListers.Remove(vehicle) :
        collection.requests.Remove(request);
    }
    return false;
  }

  public bool RemoveAllListerFor(VehiclePawn vehicle)
  {
    return vehicleListers.Remove(vehicle);
  }

  public static VehiclePawn? VehicleInhabitingCells(CellRect cellRect, Map map)
  {
    foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
    {
      if (pawn is not VehiclePawn vehicle)
        continue;

      if (vehicle.OccupiedRect().Overlaps(cellRect))
        return vehicle;
    }
    return null;
  }

  public static bool AnyVehicleInhabitingCells(CellRect cellRect, Map map)
  {
    return VehicleInhabitingCells(cellRect, map) != null;
  }

  public override void ExposeData()
  {
    base.ExposeData();
    Scribe_Collections.Look(ref reservations, nameof(reservations), LookMode.Reference,
      LookMode.Deep, ref vehiclesReserving_tmp, ref vehicleReservations_tmp);
    Scribe_Collections.Look(ref vehicleListers, nameof(vehicleListers), LookMode.Reference,
      LookMode.Deep, ref vehicleListerPawns_tmp, ref vehicleListerRequests_tmp);

    reservations ??= new Dictionary<VehiclePawn, VehicleReservationCollection>();
    vehicleListers ??= new Dictionary<VehiclePawn, VehicleRequestCollection>();
  }

  /// <summary>
  /// Serves as a wrapper class for reservation list.  Easier to save than a nested collection
  /// </summary>
  public class VehicleReservationCollection : IExposable
  {
    public List<ReservationBase> reservations = new List<ReservationBase>();

    public bool NullOrEmpty() => reservations.NullOrEmpty();

    public void Add<T>(T reservation) where T : ReservationBase
    {
      reservations.Add(reservation);
    }

    public bool Remove<T>(T reservation) where T : ReservationBase
    {
      return reservations.Remove(reservation);
    }

    public List<ReservationBase> List =>
      reservations; //avoid enumerator and just pass back list, no need for extra garbage creation

    public int Count => reservations.Count;

    public ReservationBase this[int index] => reservations[index];

    public void ExposeData()
    {
      Scribe_Collections.Look(ref reservations, nameof(reservations), LookMode.Deep);
    }
  }

  /// <summary>
  /// Serves as a wrapper class for vehicle requests.  Easier to save than a nested collection
  /// </summary>
  public class VehicleRequestCollection : IExposable
  {
    public HashSet<string> requests = new HashSet<string>();

    public VehicleRequestCollection()
    {
      requests = new HashSet<string>();
    }

    public VehicleRequestCollection(string req)
    {
      requests = new HashSet<string>() { req };
    }

    public void ExposeData()
    {
      Scribe_Collections.Look(ref requests, nameof(requests), LookMode.Value);
    }
  }
}