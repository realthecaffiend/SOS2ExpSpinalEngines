using System.Collections.Generic;
using HarmonyLib;
using SaveOurShip2;
using Verse;

namespace TheCafFiend
{
    [StaticConstructorOnStartup]
    public static class SOS2SpinalEngines
    {
        static SOS2SpinalEngines()
        {
            SOS2ExpSpinal_HarmonyPatch.DoPatching();
            Log.Message("SOS2 SpinalEngines 1.6");
            // add any other startup shenanigans
        }
    }

    public class SOS2ExpSpinal_HarmonyPatch
    {
        public static void DoPatching()
        {
            var harmony = new Harmony("TheCafFiend.SOS2ExpSpinal"); 
            harmony.PatchAll();
        }
    }

    [HarmonyPatch(typeof(SaveOurShip2.SpaceShipCache), nameof(SaveOurShip2.SpaceShipCache.AddToCache), MethodType.Normal)] 
    public class PatchSpinalSpawn // Must be a prefix to check shipcache *current* state, not post-building-add 
    {
        public static bool Prefix(Building b, ref int ___EngineMass, ref float ___ThrustRaw, HashSet<Building> ___Buildings, ref List<CompEngineTrail> ___Engines) //prefix due to DoSpawn running addcache on every single cell :/
        {
            Building argBuilding = b; 
            // Log.Message($"SOS2 Spinal Engines: name of added is: {argBuilding.def.defName}");
            if (___Buildings.Contains(argBuilding))
            {
                return true; //building is already in cache, presumably re-running on other cells
            }
            if (argBuilding.TryGetComp<CompSpinalEngineMount>() == null) 
            {
                //Log.Message("SOS2ExpSpinalEngines SpinalComp null");
                return true; //Not spinal-related, bail ASAP
            }
            Building foundEngine = TheCafFiend.CompSpinalEngineTrail.EngineFromSpinal(argBuilding);
            if (foundEngine == null)
            {
                //Log.Message("SOS2 SpinalEngines EngineFromSpinal null");
                return true; //amp/cap but no engine
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
            if (!foundEngineComp.fullyFormed)
            {
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
            Building foundEngine = CompSpinalEngineTrail.EngineFromSpinal(argBuilding);
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
                // Log.Message($"SOS2 Spinal Engines: fully formed spinal with a component busted, remove thrust {foundEngineComp.Thrust}");
                //only reached removing a spinal component that isn't an engine but found an engine in EngineFromSpinal (aka amp/capacitor)
                ___EngineMass -= foundEngineComp.SupportWeight;
                ___ThrustRaw -= foundEngineComp.Thrust;
                foundEngineComp.Off();
                ___Engines.Remove(foundEngineComp); // Otherwise broken spinals in combat let engine still (visually) fire to no effect (also used to track for re-added parts)
                foundEngineComp.Reset(); //something was removed, start from scratch!
                foundEngineComp.CurrentError = "SoS.SpinalEngines.ComponentRemoved".Translate(argBuilding.def.label); //Only use of this setter hrm
                foundEngineComp.fullyFormed = false;
            }
        }
    }
}