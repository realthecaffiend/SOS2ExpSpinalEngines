using System;
using System.Collections.Generic;
using SaveOurShip2;
using UnityEngine;
using Verse;

namespace TheCafFiend
{
    public class PlaceWorker_NeedsSpinalEngineMount : PlaceWorker_NeedsSpinalMountPort
    {
        public override void DrawGhost(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {
            Map currentMap = Find.CurrentMap;
            List<Building> allBuildingsColonist = currentMap.listerBuildings.allBuildingsColonist;
            for (int i = 0; i < allBuildingsColonist.Count; i++)
            {
                Building building = allBuildingsColonist[i];
                if (building == null) continue;
                CompSpinalMount localSpinalComp = building.TryGetComp<CompSpinalMount>();
                if (!Find.Selector.IsSelected(building) && localSpinalComp != null && localSpinalComp.Props.emits)
                {
                    PlaceWorker_SpinalMountPort.DrawFuelingPortCell(building.Position, building.Rotation, building.def);
                }
            } //Nothing with this worker at all needs to draw funny doom lines
        }
    }
}
