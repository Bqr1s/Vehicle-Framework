﻿using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using SmashTools;
using System.Threading;
using SmashTools.Performance;

namespace Vehicles
{
	/// <summary>
	/// Vehicle specific room handler
	/// </summary>
	public sealed class VehicleRoom
	{
		private static int nextRoomID;

		public sbyte mapIndex = -1;
		public int id = -1;

		private readonly VehicleDef vehicleDef;

		public int lastChangeTick = -1;
		private int numRegionsTouchingMapEdge;

		public VehicleRoom(VehicleDef vehicleDef)
		{
			this.vehicleDef = vehicleDef;
			ObjectCounter.Increment<VehicleRoom>();
		}

		/// <summary>
		/// Map getter with fallback
		/// </summary>
		public Map Map => (mapIndex >= 0) ? Find.Maps[mapIndex] : null;

		/// <summary>
		/// Region type with fallback
		/// </summary>
		public RegionType RegionType => Regions.NullOrEmpty() ? RegionType.None : Regions.FirstOrDefault().Key.type;

		/// <summary>
		/// Region getter for regions contained within room
		/// </summary>
		public ConcurrentSet<VehicleRegion> Regions { get; } = new ConcurrentSet<VehicleRegion>();

		/// <summary>
		/// Region count
		/// </summary>
		public int RegionCount => Regions.Count;

		/// <summary>
		/// Room touches map edge
		/// </summary>
		public bool TouchesMapEdge => numRegionsTouchingMapEdge > 0;

		private IEnumerable<IntVec3> Cells
		{
			get
			{
				foreach (VehicleRegion region in Regions.Keys)
				{
					foreach (IntVec3 cell in region.Cells)
					{
						yield return cell;
					}
				}
			}
		}

		/// <summary>
		/// Create new room for <paramref name="vehicleDef"/>
		/// </summary>
		/// <param name="map"></param>
		/// <param name="vehicleDef"></param>
		public static VehicleRoom MakeNew(Map map, VehicleDef vehicleDef)
		{
			int id = Interlocked.CompareExchange(ref nextRoomID, 0, 0);
			VehicleRoom room = new VehicleRoom(vehicleDef)
			{
				mapIndex = (sbyte)map.Index,
				id = id
			};
			Interlocked.Increment(ref nextRoomID);
			return room;
		}

		/// <summary>
		/// Add region to room
		/// </summary>
		/// <param name="region"></param>
		public void AddRegion(VehicleRegion region)
		{
			if (Regions.ContainsKey(region))
			{
				Log.Error($"Tried to add the same region twice to Room. region={region} room={this}");
				return;
			}
			Regions.Add(region);
			if (region.touchesMapEdge)
			{
				numRegionsTouchingMapEdge++;
			}
			if (Regions.Count == 1)
			{
				Map.GetCachedMapComponent<VehiclePathingSystem>()[vehicleDef].VehicleRegionGrid.allRooms.Add(this);
			}
		}

		/// <summary>
		/// Remove region from room
		/// </summary>
		/// <param name="r"></param>
		public void RemoveRegion(VehicleRegion region)
		{
			if (!Regions.ContainsKey(region))
			{
				Log.Warning($"Tried to remove region from Room but this region is not here. region={region} room={this}"); //TODO - resolve race condition where region is already destroyed before room can clear it
				return;
			}
			Regions.Remove(region);
			if (region.touchesMapEdge)
			{
				numRegionsTouchingMapEdge--;
			}
			if (Regions.Count == 0)
			{
				VehiclePathingSystem mapping = MapComponentCache<VehiclePathingSystem>.GetComponent(Map);
				if (mapping != null)
				{
					mapping[vehicleDef].VehicleRegionGrid?.allRooms.Remove(this);
				}
			}
		}

		internal void DebugDraw(DebugRegionType debugRegionType)
		{
			if (debugRegionType.HasFlag(DebugRegionType.Rooms))
			{
				float color = Rand.ValueSeeded(GetHashCode());
				foreach (IntVec3 cell in Cells)
				{
					CellRenderer.RenderCell(cell, color);
				}
			}
		}

		/// <summary>
		/// ID based hashcode
		/// </summary>
		/// <returns></returns>
		public override int GetHashCode()
		{
			return Gen.HashCombineInt(id, vehicleDef.GetHashCode());
		}
	}
}
