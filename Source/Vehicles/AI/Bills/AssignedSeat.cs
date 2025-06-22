using System;
using JetBrains.Annotations;
using UnityEngine.Assertions;
using Verse;

namespace Vehicles;

public class AssignedSeat : IExposable
{
  public Pawn pawn;
  public VehicleRoleHandler handler;

  /// <summary>
  /// Public constructor is required for ScribeLoader and instantiation via Scribe_Deep
  /// </summary>
  public AssignedSeat()
  {
  }

  public AssignedSeat([NotNull] Pawn pawn, [NotNull] VehicleRoleHandler handler)
  {
    this.pawn = pawn;
    this.handler = handler;
  }

  public VehiclePawn Vehicle => handler?.vehicle;

  public static implicit operator ValueTuple<Pawn, VehicleRoleHandler>(
    AssignedSeat assignedSeat)
  {
    return (assignedSeat.pawn, assignedSeat.handler);
  }

  public void ExposeData()
  {
    Scribe_References.Look(ref pawn, nameof(pawn));
    Scribe_References.Look(ref handler, nameof(handler));
  }
}