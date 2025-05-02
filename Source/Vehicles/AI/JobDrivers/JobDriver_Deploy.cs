using System.Collections.Generic;
using JetBrains.Annotations;
using Verse;
using Verse.AI;

namespace Vehicles;

public class JobDriver_Deploy : JobDriver
{
  [UsedImplicitly]
  protected VehiclePawn Vehicle => TargetA.Thing as VehiclePawn;

  public override bool TryMakePreToilReservations(bool errorOnFailed)
  {
    return Vehicle.CompVehicleTurrets is { CanDeploy: true };
  }

  protected override IEnumerable<Toil> MakeNewToils()
  {
    this.FailOnDestroyedOrNull(TargetIndex.A);
    this.FailOn(() => !Vehicle.Spawned);
    Toil deployToil = ToilMaker.MakeToil();
    deployToil.initAction = delegate
    {
      Map.pawnDestinationReservationManager.Reserve(Vehicle, job, Vehicle.Position);
      Vehicle.vehiclePather.StopDead();
      if (Vehicle.CompVehicleTurrets.Deployed)
      {
        Vehicle.CompVehicleTurrets
         .FlagAllTurretsForAlignment(); //Vehicle is deployed, re-orient turret back to default position
      }
      if (Vehicle.CompVehicleTurrets.Props.deployingSustainer != null)
      {
        deployToil.PlaySustainerOrSound(Vehicle.CompVehicleTurrets.Props.deployingSustainer);
      }
    };
    deployToil.tickAction = delegate
    {
      if (!Vehicle.CompVehicleTurrets.Deployed || Vehicle.CompVehicleTurrets.TurretsAligned)
      {
        Vehicle.CompVehicleTurrets.deployTicks--;
      }

      if (Vehicle.CompVehicleTurrets.deployTicks <= 0)
      {
        Vehicle.CompVehicleTurrets.ToggleDeployment();
        ReadyForNextToil();
      }
    };
    deployToil.WithProgressBar(TargetIndex.A,
      () => 1 - Vehicle.CompVehicleTurrets.deployTicks /
        (float)Vehicle.CompVehicleTurrets.DeployTicks);
    deployToil.defaultCompleteMode = ToilCompleteMode.Never;
    yield return deployToil;
  }
}