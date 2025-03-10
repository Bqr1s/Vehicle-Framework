﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
using Verse.AI;
using SmashTools;
using UnityEngine;
using SmashTools.Animations;

namespace Vehicles
{
	public class VehicleComp : ThingComp
	{
		public VehiclePawn Vehicle => parent as VehiclePawn;

		/// <summary>
		/// If true, must request to start / stop ticking caller
		/// </summary>
		public virtual bool TickByRequest => false;

		public virtual IEnumerable<AnimationDriver> Animations { get; }

		public virtual IEnumerable<Gizmo> CompCaravanGizmos()
		{
			yield break;
		}

		public virtual IEnumerable<FloatMenuOption> CompFloatMenuOptions()
		{
			yield break;
		}

		public virtual void CompCaravanInspectString(StringBuilder stringBuilder)
		{
		}

		public virtual void PostLoad()
		{
		}

		public virtual void OnDestroy()
		{
		}

		[Obsolete]
		public virtual void PostDrawUnspawned(Vector3 drawLoc, Rot8 rot, float rotation)
		{
		}

		public virtual void PostDrawUnspawned(ref readonly TransformData transform)
		{
		}

		/// <summary>
		/// Called when newly generated, unlike PostSpawnSetup called every time it is spawned in-map
		/// </summary>
		public virtual void PostGeneration()
		{
		}

		public virtual void EventRegistration()
		{
		}

		public virtual void SpawnedInGodMode()
		{
		}

		public virtual void Notify_ColorChanged()
		{
		}

		public virtual bool CanDraft(out string failReason, out bool allowDevMode)
		{
			failReason = string.Empty;
			allowDevMode = true;
			return true;
		}

		public virtual bool IsThreat(IAttackTargetSearcher searcher)
		{
			return false;
		}

		public virtual void StartTicking()
		{
			if (TickByRequest)
			{
				Vehicle.RequestTickStart(this);
			}
		}

		public virtual void StopTicking()
		{
			if (TickByRequest)
			{
				Vehicle.RequestTickStop(this);
			}
		}
	}
}
