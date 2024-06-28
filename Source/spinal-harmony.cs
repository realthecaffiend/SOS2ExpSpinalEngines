using HarmonyLib;
using RimWorld;
using SaveOurShip2;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace TheCafFiend
{
    [StaticConstructorOnStartup]
    public static class SOS2SpinalEngines
    {
        static SOS2SpinalEngines()
        {
            SOS2ExpSpinal_HarmonyPatch.DoPatching();
            // add any other startup shenanigins
        }
    }
    public class SOS2ExpSpinal_HarmonyPatch
    {
        public System.Type pointerToDrawGhost;
        public static void DoPatching()
        {
            var harmony = new Harmony("TheCafFiend.SOS2ExpSpinal"); 
            harmony.PatchAll();
        }
    }
    [HarmonyPatch(typeof(SaveOurShip2.SpaceShipCache), nameof(SaveOurShip2.SpaceShipCache.FuelNeeded), MethodType.Normal)]
    public class PatchFuelNeeded
    {
        public static void Postfix(ref List<CompEngineTrail> ___Engines, ref float __result) // foglayer bullshit on land?!
        {
            foreach (CompSpinalEngineTrail iterateEngine in  ___Engines.OfType<CompSpinalEngineTrail>()) 
            {
                //Log.Message($"Find spinalenginetrail and check is {iterateEngine.cachedBuildingTypes.Count(stringEvaluate => stringEvaluate == "engineGravitationalCompensator")}");
                if (iterateEngine.cachedBuildingTypes.Count(stringEvaluate => stringEvaluate == "engineGravitationalCompensator") != 2)
                {
                    return;
                }
                __result += iterateEngine.refuelComp.Fuel;
                if (iterateEngine.refuelComp.Props.fuelFilter.AllowedThingDefs.Contains(ResourceBank.ThingDefOf.ShuttleFuelPods))
                {
                    __result += iterateEngine.refuelComp.Fuel;
                }
            }
        }
    }

    [HarmonyPatch]
    public class PatchSpinalEngineGhost //If PlaceWorker_ShipEngine was public I think I could extend this instead but, for now, here we are. 
    {
        static MethodBase TargetMethod() => AccessTools.Method("SaveOurShip2.PlaceWorker_ShipEngine:DrawGhost");
        public static bool Prefix(ThingDef def, IntVec3 center, Rot4 rot, Color ghostCol, Thing thing = null)
        {   // There is probably a better way? TODO
            if (def.defName != "Ship_Engine_Spinal_Chem" && def.defName != "Ship_Engine_Spinal_Nuke")
            {
                return true; 
            }
            List<IntVec3> rect = GenAdj.CellsOccupiedBy(center, rot, def.size).ToList();
            int minx = 99999;
            int minz = 99999;
            foreach (IntVec3 vec in rect)
            {
                if (vec.x < minx)
                    minx = vec.x;
                if (vec.z < minz)
                    minz = vec.z;
            }
            CellRect engineDestroyRect;
            engineDestroyRect = new CellRect(minx, minz, rot.IsHorizontal ? def.size.z : def.size.x, rot.IsHorizontal ? def.size.x : def.size.z).MovedBy(CompSpinalEngineTrail.killOffsetSpinal[rot.AsInt]).ExpandedBy(2);
            if (rot.IsHorizontal)
            {
                engineDestroyRect.Width = engineDestroyRect.Width * 2 - 3;
            }
            else
            {
                engineDestroyRect.Height = engineDestroyRect.Height * 2 - 3;
            }
            GenDraw.DrawFieldEdges(engineDestroyRect.Cells.ToList(), Color.red);
            return false; //bails on original drawghost! Some danger
        }
    }

    [HarmonyPatch(typeof(SaveOurShip2.SpaceShipCache), nameof(SaveOurShip2.SpaceShipCache.AddToCache), MethodType.Normal)] 
    public class PatchSpinalSpawn // Must be a prefix to check shipcache *current* state, not post-building-add 
    {
        public static bool Prefix(Building b, ref int ___EngineMass, ref float ___ThrustRaw, HashSet<Building> ___Buildings, ref List<CompEngineTrail> ___Engines) //prefix due to DoSpawn running addcache on every single cell :/
        {
            Building argBuilding = b; 
            //Log.Message($"SOS2 Spinal Engines: name of added is: {argBuilding.def.defName}");
            if (___Buildings.Contains(argBuilding))
            {
                return true; //building is already in cache, presumably re-running on other cells
            }
            if (argBuilding.TryGetComp<CompSpinalEngineMount>() == null) 
            {
                //Log.Message("SOS2ExpSpinalEngines SpinalComp null");
                return true; //Not spinal-related, bail ASAP
            }
            Building foundEngine = CompMultiblock.ControllerFromSubBlock(argBuilding);
            if (foundEngine == null)
            {
                Log.Message("SOS2 SpinalEngines EngineFromSpinal null");
                return true; 
            }
            CompSpinalEngineTrail foundEngineComp = foundEngine.TryGetComp<CompSpinalEngineTrail>();
            if (foundEngineComp == null)
            {
                Log.Error($"SOS2Spinal engines foundEngineComp is null! foundengine {foundEngine.def.defName}");
                return true; // I guess? true? 
            }
            if (argBuilding.TryGetComp<CompSpinalEngineTrail>() != null) // Built building is engine itself
            {
                ___EngineMass += foundEngineComp.SupportWeight; //regular add will soon pull thrust: Just completed spinal, so add mass
                return true;
            }
            if (!foundEngineComp.cachedBuildings.Contains(argBuilding))
            {
                foundEngineComp.Calc(); //new, uncached support building which points into controller added: re-validate 
            }
            //only reached with a spinal component that isn't an engine but found an engine in EngineFromSpinal (aka amp/capacitor)
            if (___Engines.Contains(foundEngineComp) && ___Buildings.Contains(foundEngine)) // Building is support, engine already added
            {
                ___EngineMass += foundEngineComp.SupportWeight; //calling triggers the spinal recalc and if successfull, sets fullyFormed
                ___ThrustRaw += foundEngineComp.Thrust; //Direct manipulation of the cache: Best I can think of but is there better?
                return true;
            }
            if (foundEngineComp.Thrust != 0) // If every check above bails this will actually *cause* the recalc
            {
                if (!___Engines.Contains(foundEngineComp) && ___Buildings.Contains(foundEngine)) // is engine purged from list BUT still in shipcache? Broken spinal support
                {
                    //Log.Message($"SOS2spinal engines: re-adding engine to internal engine list after spinal supports were removed earlier");
                    ___Engines.Add(foundEngineComp);
                    ___EngineMass += foundEngineComp.SupportWeight;
                    ___ThrustRaw += foundEngineComp.Thrust;
                }
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SaveOurShip2.SpaceShipCache), nameof(SaveOurShip2.SpaceShipCache.RemoveFromCache), MethodType.Normal)]
    public class PatchSpinalDeSpawn
    {
        public static void Postfix(Building b, ref int ___EngineMass, ref float ___ThrustRaw, ref List<CompEngineTrail> ___Engines)
        {
            Building argBuilding = b;

            if (argBuilding.TryGetComp<CompSpinalEngineMount>() == null)
            {
                //Log.Message("SOS2ExpSpinalEngines SpinalComp null");
                return; //Not spinal-related, bail ASAP
            }
            Building foundEngine = CompMultiblock.ControllerFromSubBlock(argBuilding);
            if (foundEngine == null)
            {
                //Log.Message("SOS2ExpSpinalEngines EngineFromSpinal null");
                return;
            }
            CompSpinalEngineTrail foundEngineComp = foundEngine.TryGetComp<CompSpinalEngineTrail>();
            if (foundEngineComp == null)
            {
                Log.Error("SOS2Spinal engines foundEngineComp is null!");
                return;
            }
            if (foundEngineComp.fullyFormed) // *was* a complete spinal engine, now time to cleanup
            {
                if (argBuilding.TryGetComp<CompSpinalEngineTrail>() != null) // Built building is engine itself
                {
                    ___EngineMass -= foundEngineComp.SupportWeight; //regular remove would have already pulled thrust: Just broke spinal, so remove mass
                    return;
                }
                foundEngineComp.cachedBuildings.Remove(argBuilding);
                // Log.Message($"SOS2 Spinal Engines: fully formed spinal with a component busted, remove thrust {foundEngineComp.Thrust}");
                //only reached removing a spinal component that isn't an engine but found an engine in EngineFromSpinal (aka amp/capacitor)
                ___EngineMass -= foundEngineComp.SupportWeight;
                ___ThrustRaw -= foundEngineComp.Thrust;
                foundEngineComp.Off();
                ___Engines.Remove(foundEngineComp); // Otherwise broken spinals in combat let engine still (visually) fire to no effect (also used to track for re-added parts)
                foundEngineComp.Reset(); //something was removed, start from scratch!
                foundEngineComp.SetError("SoS.SpinalEngines.ComponentRemoved".Translate(argBuilding.def.label));
                foundEngineComp.fullyFormed = false;
                foundEngineComp.Calc(); //could re-set fullyFormed depending on removed block!
            }
        }
    }
}