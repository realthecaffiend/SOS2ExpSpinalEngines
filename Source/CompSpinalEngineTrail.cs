using RimWorld;
using SaveOurShip2;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;
// Future thoughts: ramjets, antimatter? end, better accelerators (Bump exotics?), liftoff (fuel tanks). Update descrip of compensators and overdrives to include location!
namespace TheCafFiend
{
    [StaticConstructorOnStartup]
    public class CompSpinalEngineTrail : CompEngineTrail
    {
        public override string CompInspectStringExtra()
        {
            StringBuilder stringBuilder = new StringBuilder();
            
            if (fullyFormed)
            {
                stringBuilder.Append("SoS.SpinalEngines.FuelRate".Translate(cachedFuelUse, cachedFuelAllowed)); // "Fuel burn rate per second: {cachedFuelUse} of {cachedFuelAllowed} supported"
                stringBuilder.AppendInNewLine("SoS.SpinalEngines.Thrust".Translate(Thrust * 500)); // "Thrust: {cachedThrust * 500}"
                stringBuilder.AppendWithSeparator("SoS.SpinalEngines.SupportMass".Translate(cachedMass * 60)," ");
            }
            else
            {
                stringBuilder.Append(cachedError.Colorize(UnityEngine.Color.red));
            }
            return stringBuilder.ToString();
        }
        public virtual CompProperties_SpinalEngineTrail Properties
        {
            get { return props as CompProperties_SpinalEngineTrail; }
        }
        private bool overdriveSetting = false;
        private int overdriveStartTick = 0;
        private bool testBurnSetting = false;
        private int testBurnStartTick = 0;
        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            foreach (Gizmo item in base.CompGetGizmosExtra())
            {
                yield return item;
            }
            if (parent.Faction != Faction.OfPlayer)
            {
                yield break;
            }
            Command_Toggle overdriveCommandToggle = new Command_Toggle();
            overdriveCommandToggle.defaultLabel = "SoS.SpinalEngines.EngineOverdrive".Translate(parent);
            overdriveCommandToggle.defaultDesc = "SoS.SpinalEngines.EngineOverdriveDesc".Translate(parent);
            overdriveCommandToggle.isActive = (() => overdriveSetting == true);
            overdriveCommandToggle.Disabled = !cachedBuildingTypes.Contains("EngineEmergencyOverdrive");
            overdriveCommandToggle.disabledReason = "SoS.SpinalEngines.EngineOverrideDisabled".Translate();
            overdriveCommandToggle.toggleAction = delegate
            {
                FlickOverdrive();
            };
            yield return overdriveCommandToggle;

            Command_Toggle testBurnCommandToggle = new Command_Toggle();
            testBurnCommandToggle.defaultLabel = "SoS.SpinalEngines.EngineTestBurn".Translate(parent);
            testBurnCommandToggle.defaultDesc = "SoS.SpinalEngines.EngineTestBurnDesc".Translate(parent);
            testBurnCommandToggle.isActive = (() => testBurnSetting == true);
            if (!this.CanFire() || this.active || this.mapComp.ShipMapState != ShipMapState.nominal)
            {
                testBurnCommandToggle.Disabled = true; // engine can't fire, or, already is, or, in combat (or other oddities)
            }
            testBurnCommandToggle.disabledReason = "SoS.SpinalEngines.EngineTestBurnDisabled".Translate();
            testBurnCommandToggle.toggleAction = delegate
            {
                FlickTestBurn();
            };
            yield return testBurnCommandToggle;
        }

        public void FlickOverdrive()
        {
            SpaceShipCache shipAttached = parent.Map.GetComponent<ShipMapComp>().ShipsOnMap[parent.Map.GetComponent<ShipMapComp>().ShipIndexOnVec(parent.Position)];
            if (overdriveSetting == true)
            {
                overdriveSetting = false;
                shipAttached.ThrustRaw -= cachedThrust *2;
                shipAttached.ThrustRaw += cachedThrust; // I don't love this but if I don't update ThrustRaw, the player ship info/debug hover are wrong
            }
            else 
            {
                Building injectorToDestroy = new Building();
                List<Building> replacementWrecks = new List<Building>();
                foreach (Building iterateBuilding in cachedBuildings)
                {
                    CompSpinalEngineMount iterateComp = iterateBuilding.TryGetComp<CompSpinalEngineMount>();
                    if (iterateComp == null)
                    {
                        continue;
                    }
                    if (iterateComp.Props.multiblockType != "EngineEmergencyOverdrive")
                    {
                        continue;
                    }
                    // found an injector 
                    injectorToDestroy = iterateBuilding;
                    foreach (IntVec3 iteratePosition in iterateBuilding.OccupiedRect())
                    {
                        Building replacementBlock = (Building)ThingMaker.MakeThing(ThingDef.Named("Ship_Beam_Wrecked"));
                        replacementBlock.Position = iteratePosition;
                        replacementBlock.SetFaction(Faction.OfPlayer);
                        replacementWrecks.Add(replacementBlock); 
                    }
                    break; // Only smash a single injector
                }
                injectorToDestroy.Destroy();
                foreach (Building iterateWreck in replacementWrecks)
                {
                    iterateWreck.SpawnSetup(parent.Map, false);
                    FleckMaker.ThrowSmoke(iterateWreck.DrawPos, parent.Map, 1);
                }
                overdriveStartTick = Find.TickManager.TicksGame;
                shipAttached.ThrustRaw -= cachedThrust;
                shipAttached.ThrustRaw += cachedThrust * 2;
                overdriveSetting = true;
            }
        }
        public void FlickTestBurn()
        {
            if (testBurnSetting)
            {
                testBurnSetting = false;
                this.Off();
            }
            else
            {
                testBurnStartTick = Find.TickManager.TicksGame;
                this.On();
                testBurnSetting = true;
            }
        }
        public override void CompTick()
        {
            base.CompTick();
            if (Find.TickManager.TicksGame  % 600 != 0)
            {
                return;
            }
            if (overdriveSetting != true && testBurnSetting != true)
            {
                return;
            }
            else
            {
                if (Find.TickManager.TicksGame >= (overdriveStartTick + 3600) && overdriveStartTick != 0) //60s, but modulo only runs every ~10, might get a little extra shhhhh
                {
                    //Log.Message($"sos2 spinal engines gametick less than 3600+ overdrivestarttick is {overdriveStartTick + 3600} gametick is {Find.TickManager.TicksGame}");
                    overdriveSetting = false;
                }
                else if (Find.TickManager.TicksGame >= (testBurnStartTick + 600) && testBurnStartTick != 0)
                {
                    testBurnSetting = false;
                    this.Off();
                }
                else
                {
                    return;
                }
            }

        }
        public class SpinalEngineState : MultiblockState
        {
            public float thrustAmp;
            public float fuelAmp;
            public float fuelAllowAmp;
            public float powerUseAmp;
            public int supportWeight;
        }
        private int cachedThrust = 0;
        private int cachedMass = 0; // add/remove on the *engine* does this itself, this is only amps+caps
        public bool fullyFormed = false;
        private int cachedFuelUse = 0;
        private int cachedPowerUse = 0;
        private int cachedFuelAllowed = 0; // Error below probably never surfaces to a player but I want a fallback
        private string cachedError = "SoS.SpinalEngines.DefaultError".Translate();
        public HashSet<Building> cachedBuildings = new HashSet<Building>(); //getter setter nonsense instead? 
        public List<string> cachedBuildingTypes = new List<string>();
        public override int Thrust
        {
            get
            {
                if (parent.TryGetComp<CompSpinalEngineTrail>() == null) //should never? happen but reflex
                {
                    Log.Message("SOS2 spinal engines: somehow hit thrust from a non-compspinalenginetrail?");
                    return base.Thrust;
                }
                if (!fullyFormed)
                {
                    //Log.Message($"sos2 spinal engines thrust called about a spinal engine, returning cached thrust {thrustCached}");
                    Calc();
                }
                if (overdriveSetting)
                {
                    //Log.Message($"sos2 spinal engines thrust called about a spinal engine while overdriving, returning cached thrust {cachedThrust * 3} overdrivestarttick is {overdriveStartTick} gametick is {Find.TickManager.TicksGame}");
                    return cachedThrust * 2;
                }
                else
                {
                    return cachedThrust;
                } 
            }
        }
        public bool SetError(string passedError) // trivial atm but stubbed for any later handling: Consolidate translation?
        {
            if (string.IsNullOrEmpty(passedError))
            { 
                return false; 
            }
            else
            {
                cachedError = passedError;
                return true;
            }
        }
        public int PowerUse
        {
            get
            {
                if (parent.TryGetComp<CompSpinalEngineTrail>() == null)
                {
                    Log.Message("SOS2 spinal engines: somehow hit powerUse from a non-compspinalenginetrail?");
                    return (int)(parent.TryGetComp<CompPowerTrader>().Props.PowerConsumption); // pretty? Sure this cannot happen but in case
                }
                if (fullyFormed)
                {
                    if (overdriveSetting)
                    {
                        return cachedPowerUse * 10;
                    }
                    else
                    {
                        return -cachedPowerUse;
                    }
                    
                }
                else
                {
                    return (int)(parent.TryGetComp<CompPowerTrader>().Props.PowerConsumption);
                }
            }
        }
        public override int FuelUse
        {
            get
            {
                if (parent.TryGetComp<CompSpinalEngineTrail>() == null) //should never? happen but reflex
                {
                    Log.Message("SOS2 spinal engines: somehow hit FuelUse from a non-compspinalenginetrail?");
                    return base.FuelUse;
                }
                if (!fullyFormed)
                {
                    //Log.Message($"sos2 spinal engines thrust called about a spinal engine, returning cached fuelUse {cachedFuelUse}");
                    Calc();
                }
                if (overdriveSetting)
                { 
                    return cachedFuelUse * 10; 
                }
                return cachedFuelUse;
            }
        }
        public int SupportWeight
        {
            get
            {
                if (parent.TryGetComp<CompSpinalEngineTrail>() == null) //should never? happen but reflex
                {
                    Log.Message("SOS2 spinal engines: somehow hit supportWeight from a non-compspinalenginetrail?!!!!??");
                    return 0;// is this a log.error? Something went squirelly 
                }
                if (!fullyFormed)
                {
                    Calc();
                }
                return cachedMass;
            }
        }

        public void Reset()
        {
            //Log.Message($"SOS2spinal engines: resetting: thrust {cachedThrust} mass {cachedMass} fueluse {cachedFuelUse} fuelallowed {cachedFuelAllowed} power {cachedPowerUse}");
            cachedThrust = 0;
            cachedMass = 0;
            cachedFuelUse = 0;
            cachedFuelAllowed = 0;
            cachedPowerUse = 0;
            cachedError = "SoS.SpinalEngines.DefaultError".Translate();
        }
        public void Calc()
        {
            SpinalEngineState wasReturned = SpinalEngineRecalc(this);
            SpinalEngineRecalc(this);
            if (wasReturned == null) // null basically means something went wrong: Multiblock broken, re-set to zeros
            {
                Reset();
                return;
            }
            else if(wasReturned.fullyFormed == false)
            {
                cachedError = wasReturned.playerError; //cleanest way I could think of to surface errors? 
            }
            else
            {
                Reset(); //I can't see this being needed but eh (it already saved me from one error!)
                cachedThrust = (int)(base.Props.thrust * (1 + wasReturned.thrustAmp)); 
                cachedFuelUse = (int)(base.Props.fuelUse * (1 + wasReturned.fuelAmp));
                CompSpinalEnginePowerTrader powerComp = parent.TryGetComp<CompSpinalEnginePowerTrader>(); 
                cachedPowerUse = (int)(powerComp.Props.PowerConsumption * (1 + wasReturned.powerUseAmp));
                //Log.Message($"sos2spinal engines base.powercomp.poweroutput from Calc returned poweruse is {wasReturned.powerUseAmp} cached is {cachedPowerUse}");
                powerComp.PowerOutput = 0 - cachedPowerUse; 
                //Log.Message($"sos2spinal engines modified comppowertrader PowerOutput on {parent.GetUniqueLoadID()} to {powerComp.PowerOutput}");
                cachedFuelAllowed = (int)(this.Properties.fuelAllowed * (1 + wasReturned.fuelAllowAmp));
                cachedMass = (int)(wasReturned.supportWeight);
                fullyFormed = true;
            }
        }
        public static IntVec2[] killOffsetSpinal = { new IntVec2(0, -11), new IntVec2(-11, 0), new IntVec2(0, 6), new IntVec2(6, 0) };
        public override void PostSpawnSetup(bool respawningAfterLoad) // fixes offset of plume area
        {
            base.PostSpawnSetup(respawningAfterLoad); //regular SOS2 postSpawn
            CompSpinalEngineTrail engineCompToSetup = parent.TryGetComp<CompSpinalEngineTrail>();
            if (engineCompToSetup == null) 
            {
                return; //Not a spinal engine
            }
            ExhaustArea.Clear();
            CellRect engineDestroyRect;
            engineDestroyRect = parent.OccupiedRect().MovedBy(killOffsetSpinal[parent.Rotation.AsInt]).ExpandedBy(2);
            if (parent.Rotation.IsHorizontal)
            {
                engineDestroyRect.Width = engineDestroyRect.Width * 2 - 3;
            }
            else
            {
                engineDestroyRect.Height = engineDestroyRect.Height * 2 - 3;
            }
            foreach (IntVec3 v in engineDestroyRect.Where(v => v.InBounds(parent.Map)))
            {
                ExhaustArea.Add(v);
            }
        }
        //cannot be added till SOS2 updates to flag virtual
        public override bool CanFire(Rot4 rot) // fixes a visual glitch where engines in some cases can visually "fire" 
        {
            if (fullyFormed == false)
            {
                return false;
            }
            return base.CanFire(rot);
        }
        // SpinalRecalc, but using multiblock code and less gnarly
        public static SpinalEngineState SpinalEngineRecalc(CompSpinalEngineTrail argEngineTrail)
        {
            RecursiveReturn returnHandler = new RecursiveReturn();
            SpinalEngineState returnedSpinalEngineState = new SpinalEngineState
            {
                fullyFormed = false
            };
            if (argEngineTrail == null)
            {
                Log.Message("SOS2 spinal engines: argEngineTrail in SpinalEngineRecalc null");
                return null;
            }
            Thing buildingPointer = argEngineTrail.parent;
            if (buildingPointer == null)
            {
                Log.Message("SOS2 spinal engines: buildingPointer in SpinalEngineRecalc null");
                return null;
            }
            CompSpinalEngineMount spinalComp = buildingPointer.TryGetComp<CompSpinalEngineMount>();
            if (spinalComp == null)
            {
                Log.Message("SOS2 spinal engines: spinalcomp in SpinalEngineRecalc null");
                return null;
            }
            //Log.Message($"MultiblockStep (forward) from {buildingPointer.def.defName} is: {CompMultiblock.MultiblockStep(buildingPointer,buildingPointer.Rotation.AsInt)}");
            List<Building> blockList = new List<Building>();
            foreach (MultiblockConnections connectObject in spinalComp.Props.acceptFrom)
            {
                returnHandler = CompMultiblock.RecursiveFind(ref blockList, buildingPointer, connectObject, false);
                if (returnHandler == null || returnHandler.recursiveErrorFound)
                {
                    continue;
                }
            }
            if (returnHandler == null)
            {
                Log.Message($"SOS2SE: RecursiveFind returned a null");
                return null;
            }
            if (returnHandler.recursiveErrorFound)
            {
                if (returnHandler.incorrectBlockRotation)
                {
                    returnedSpinalEngineState.playerError = "SoS.SpinalEngines.BackwardsSpinal".Translate(returnHandler.errorBlockLabel, returnHandler.errrorBlockPosition);
                    return returnedSpinalEngineState;
                }
                if (returnHandler.incorrectAlignment)
                {
                    returnedSpinalEngineState.playerError = "SoS.SpinalEngines.MissaligedMultiblock".Translate(returnHandler.errorBlockLabel, returnHandler.errrorBlockPosition);
                    return returnedSpinalEngineState;
                }
            }
            List<string> foundTypes = new List<string>();
            argEngineTrail.cachedBuildings.Clear(); // entirely re-build on calc? Is this the best? 
            foreach (Building iterateBuilding in blockList) //consolidate all the information
            {
                CompSpinalEngineMount iterateSpinalComp = iterateBuilding.TryGetComp<CompSpinalEngineMount>();
                if (iterateSpinalComp == null)
                {
                    Log.Error($"SOS2SE: No spinal comp found at {iterateBuilding.Position}!"); //this errors/breaks because something went Real Wrong (TM)
                    break;
                }
                foundTypes.Add(iterateSpinalComp.Props.multiblockType);
                returnedSpinalEngineState.thrustAmp += iterateSpinalComp.Props.thrustAmp;
                returnedSpinalEngineState.fuelAmp += iterateSpinalComp.Props.fuelUseAmp;
                returnedSpinalEngineState.powerUseAmp += iterateSpinalComp.Props.powerUseAmp;
                returnedSpinalEngineState.supportWeight += iterateSpinalComp.Props.supportWeight; 
                returnedSpinalEngineState.fuelAllowAmp += iterateSpinalComp.Props.fuelAllowAmp;
                argEngineTrail.cachedBuildings.Add(iterateBuilding);
            }
            if (returnedSpinalEngineState.thrustAmp == 0) //no point in iterating strings here
            {
                returnedSpinalEngineState.playerError = "SoS.SpinalEngines.NoAccelerator".Translate();
                return returnedSpinalEngineState;
            }
            if ((foundTypes.Count(stringEvaluate => stringEvaluate == "engineTankEnd")) < 1)
            {
                returnedSpinalEngineState.playerError = "SoS.SpinalEngines.NoFuelSupport".Translate();
                return returnedSpinalEngineState;
            }
            if ((foundTypes.Count(stringEvaluate => stringEvaluate == "engineTankStart")) < 1) // Is there any way to get two wedged in?
            {
                returnedSpinalEngineState.playerError = "SoS.SpinalEngines.NoFuelSupportEnd".Translate();
                return returnedSpinalEngineState;
            }
            if ((int)(returnedSpinalEngineState.fuelAllowAmp * argEngineTrail.Properties.fuelAllowed) < (int)(returnedSpinalEngineState.fuelAmp * argEngineTrail.Properties.fuelUse))
            {
                returnedSpinalEngineState.playerError = "SoS.SpinalEngines.InSufficientFuelSupport".Translate((int)(returnedSpinalEngineState.fuelAmp * argEngineTrail.Properties.fuelUse), (int)(returnedSpinalEngineState.fuelAllowAmp * argEngineTrail.Properties.fuelAllowed));
                return returnedSpinalEngineState;
            }
            // Found an entire multiblock
            argEngineTrail.cachedBuildingTypes = foundTypes;
            returnedSpinalEngineState.fullyFormed = true;
            return returnedSpinalEngineState;
        }
        // The below historical site is left in case I need something/want to stare into the abyss  
        /* public static MultiblockState SpinalRecalc(CompSpinalEngineTrail instance)
        {
            bool foundNonAmp = false;
            bool foundFuelStart = false;
            bool foundFuelEnd = false;
            bool foundNonFuelAmp = false;
            Thing buildingPointer = instance.parent;
            IntVec3 previousThingPos;
            IntVec3 vecMoveCheck;
            if (instance.parent.TryGetComp<CompSpinalEngineTrail>() == null)
            {
                return null;
            }
            if (instance.parent.Map == null)
            {
                Log.Message("SOS2 spinal engines: parent map in SpinalRecalc was null!");
                return null;
            }
            CompSpinalEngineMount spinalComp = instance.parent.TryGetComp<CompSpinalEngineMount>();
            if (spinalComp == null)
            {
                Log.Message("SOS2 spinal engines: spinalcomp in SpinalRecalc null");
                return null;
            }
            MultiblockState toReturn = new MultiblockState
            {
                fullyFormed = false
            };
            vecMoveCheck = -1 * buildingPointer.Rotation.FacingCell; // OG SOS2 basically rebuilt FacingCell, someone pointed that out to me
            //Log.Message(string.Format("found rotation in SpincalRecalc, iterating, vec is: {0}", vecMoveCheck));
            previousThingPos = buildingPointer.Position - (2 * vecMoveCheck); //Engines are inverse side from turrets, and engines are lorge
            while (!foundNonAmp)
            {
                previousThingPos -= vecMoveCheck;
                buildingPointer = previousThingPos.GetFirstThingWithComp<CompSpinalEngineMount>(instance.parent.Map);
                if (buildingPointer == null)
                {
                    //Log.Message($"SOS2 spinal engines:  new amp at {previousThingPos} in SpinalRecalc was null!");
                    foundNonAmp = true; //Currently unused in this case but might need it for a later plan
                    break;
                }
                CompSpinalEngineMount ampComp = buildingPointer.TryGetComp<CompSpinalEngineMount>();
                //Log.Message($"SOS2 Spinal Engines: vecs are: pointer, {buildingPointer.Position} Previous {previousThingPos}");
                if (buildingPointer.Rotation.Opposite != instance.parent.Rotation) // Remember, engines vs amps are inverted
                {
                    //Log.Message("SOS2 spinal engines: amps rotation in SpinalRecalc did not match parent rotation");
                    toReturn.playerError = "SoS.SpinalEngines.BackwardsSpinal".Translate();
                    return toReturn;
                }
                //This is... A way, to check I guess. (buildingPointer = prevthingppos.getfirstcomp<spinal> above returns thing, if that is *also* the center, 1-wide, must? be amp) 
                if (buildingPointer.Position == previousThingPos)
                {
                    //Log.Message($"amp found, toreturn.thrustAmp is {toReturn.thrustAmp}");
                    toReturn.thrustAmp += ampComp.Props.thrustAmp;
                    toReturn.fuelAmp += ampComp.Props.fuelUseAmp;
                    toReturn.powerUseAmp += ampComp.Props.powerUseAmp;
                    toReturn.supportWeight += ampComp.Props.supportWeight; //Currently none have a weight set but maybe I want one that does! 
                    toReturn.fuelAllowAmp += ampComp.Props.fuelAllowAmp; // ok for sure none have this and... Will... They? Sounds illegal
                    //ampComp.SetColor(spinalComp.Props.color);
                }
                //found emitter
                else if (buildingPointer.Position == previousThingPos - vecMoveCheck && ampComp.Props.stackEnd)
                {
                    foundNonAmp = true;
                    foundFuelStart = true;
                    toReturn.supportWeight += ampComp.Props.supportWeight; // likely first *real* supportWeight
                }
                //found unaligned, something rather odd
                else
                {
                    //Log.Message($"SOS2 spinal engines: Unaligned in SpinalRecalc: buildingPointer.position: {buildingPointer.Position}, previouspos minus vec: {previousThingPos - vecMoveCheck}");
                    return null;
                }
            }
            //Log.Message($"Found amps on spinal engine: ThrustBoost: {toReturn.thrustAmp}, fuelstart is {foundFuelStart}");
            if (toReturn.thrustAmp == 0)
            {
                toReturn.playerError = "SoS.SpinalEngines.NoAccelerator".Translate();
                // "No engine accelerators found attached to engine!"
                return toReturn;
            }

            if (foundFuelStart == false)
            {
                toReturn.playerError = "SoS.SpinalEngines.NoFuelSupport".Translate();
                // "No fuel support infrastructure found; Ensure accelerators are connected directly into the fuel support end!"
                return toReturn;
            }
            // to get here, foundNonAmp is true, foundFuelStart is true, and it didn't bail earlier on a broken rotation/nonamp
            previousThingPos -= vecMoveCheck; // one extra for the size of the fuelEnd! 
            while (!foundNonFuelAmp)
            {
                //Log.Message($"SOS2 spinal engines: SpinalRecalc: buildingPointer.position: {buildingPointer.Position}, previouspos: {previousThingPos}");
                previousThingPos -= vecMoveCheck;
                buildingPointer = previousThingPos.GetFirstThingWithComp<CompSpinalEngineMount>(instance.parent.Map);
                if (buildingPointer == null)
                {
                    //Log.Message($"SOS2 spinal engines:  new *fuel*amp at previousthingspos in SpinalRecalc was null");
                    break;
                }
                CompSpinalEngineMount ampComp = buildingPointer.TryGetComp<CompSpinalEngineMount>();
                if (buildingPointer.Rotation.Opposite != instance.parent.Rotation) // Remember, engines vs amps are inverted
                {
                    toReturn.playerError = "SoS.SpinalEngines.BackwardsSpinal".Translate();
                    return toReturn;
                }
                if (buildingPointer.Position == previousThingPos && ampComp.Props.fuelStackEnd == false) // tank ends are also 1 wide!
                {
                    //Log.Message($"*fuel*amp found, toreturn.supportweight is {toReturn.supportWeight} pointerpos {buildingPointer.Position} prevpos {previousThingPos}");
                    toReturn.thrustAmp += ampComp.Props.thrustAmp; // This would be weird but just in case I want it later
                    toReturn.fuelAmp += ampComp.Props.fuelUseAmp; // ditto
                    toReturn.powerUseAmp += ampComp.Props.powerUseAmp;
                    toReturn.supportWeight += ampComp.Props.supportWeight;
                    toReturn.fuelAllowAmp += ampComp.Props.fuelAllowAmp;
                }
                else if (buildingPointer.Position == previousThingPos && ampComp.Props.fuelStackEnd == true)
                {
                    //Log.Message($"Found fuel end");
                    foundNonFuelAmp = true;
                    foundFuelEnd = true;
                }
            }
            // Didn't bail trying to build the fuel infra!
            if (foundFuelEnd == false) // Incomplete fuel
            {
                toReturn.playerError = "SoS.SpinalEngines.NoFuelSupportEnd".Translate();
                // "Fuel infrastructure found, but not a complete set: Is the end missing?"
                return toReturn;
            }
            //Also bails if someone tries to build a cheeky 0-middle engine fuel support. (int) because floating point is the devil
            if ((int)(toReturn.fuelAllowAmp * instance.Properties.fuelAllowed) < (int)(toReturn.fuelAmp * instance.Properties.fuelUse))
            {
                toReturn.playerError = "SoS.SpinalEngines.InSufficientFuelSupport".Translate((int)(toReturn.fuelAmp * instance.Properties.fuelUse), (int)(toReturn.fuelAllowAmp * instance.Properties.fuelAllowed));
                // "Insufficent fuel structures to support accelerators: Requires {(int)(toReturn.fuelAmp * instance.Properties.fuelUse)} fuel/second, can supply {(int)(toReturn.fuelAllowAmp * instance.Properties.fuelAllowed)} per second"
                return toReturn;
            }
            // After all of the previous this has? to be a real spinal, right?
            toReturn.fullyFormed = true;
            return toReturn;
        }*/

        public static Building EngineFromSpinal(Building SpinalObject) // derived from spinalrecalc but too different to combine
        {
            IntVec3 previousThingPos;
            IntVec3 vecMoveCheck;
            bool foundNonAmp = false;
            Thing buildingPointer = SpinalObject;
            vecMoveCheck = -1 * buildingPointer.Rotation.FacingCell;
            if (buildingPointer.Map == null)
            {
                Log.Message("SOS2 spinal engines: parent map on trying to find EngineFromSpinal was null!"); // Something went rather sideways, is this log.error? 
                return null;
            }
            // return the engine if this building is, the engine
            if (SpinalObject.TryGetComp<CompSpinalEngineTrail>() != null)
            {
                return SpinalObject;
            }
            //Log.Message($"found rotation on a spinal component, iterating, vecMoveCheck is: {vecMoveCheck}");
            previousThingPos = buildingPointer.Position;
            //Log.Message($"SOS2 Spinal Engines: previousThingPos is {previousThingPos} and BuildingPointer.Position is {buildingPointer.Position}");
            do
            {
                /*if (buildingPointer.TryGetComp<CompSpinalEngineMount>().Props.stackEnd)
                {
                    previousThingPos -= vecMoveCheck; // Jump one more if it's a tankend, to reach the amp
                } */
                previousThingPos -= vecMoveCheck;
                //Log.Message($"SOS2 Spinal Engines: Climbing spinal to find engine, looking at: {previousThingPos}");
                //Log.Message($"SOS2 Spinal Engines: previousThingPos is {previousThingPos} and BuildingPointer.Position is {buildingPointer.Position}");
                buildingPointer = previousThingPos.GetFirstThingWithComp<CompSpinalEngineMount>(buildingPointer.Map);
                if (buildingPointer == null) //usually just first disconnected builds
                {
                    return null;
                }

                //Log.Message($"SOS2 Spinal Engine: BuildingPointer.position is {buildingPointer.Position} and previousthingpos - vec - vec is {previousThingPos - vecMoveCheck - vecMoveCheck} pointerdefname is {buildingPointer.def.defName}");
                //Log.Message($"SOS2 Spinal Engines: pointer rot {buildingPointer.Rotation}, original spinalobject is {SpinalObject.def.defName}, rot {SpinalObject.Rotation}");
                if (buildingPointer.Rotation != SpinalObject.Rotation && buildingPointer.TryGetComp<CompSpinalEngineTrail>() == null) // Engines are backwards! 
                {
                    //Log.Message("SOS2 spinal engines: amps rotation in EngineFromSpinal did not match parent rotation");
                    break;
                }
                else if (buildingPointer.Position == previousThingPos)
                {
                    // Log.Message(string.Format("amp found, ampcount is {0}", AmplifierCount));
                    // Left in case I need this block later, but seems unlikely: Don't care how many are found here, don't setcolour till checking from an engine
                }
                //found engine (Note the double -vecMoveCheck!)
                else if (previousThingPos - vecMoveCheck - vecMoveCheck == buildingPointer.Position && (buildingPointer.TryGetComp<CompSpinalEngineTrail>() != null))
                {
                    Log.Message("SOS2 spinal engines: EngineFromSpinal found an engine and is about to return it");
                    return (Building)buildingPointer;
                }
                //found unaligned
                else
                {
                    Log.Message($"SOS2 spinal engines: Found unaligned in EngineFromSpinal amp.position: {buildingPointer.Position}, previouspos minus vec: {previousThingPos - vecMoveCheck}");
                    foundNonAmp = true;
                }

            } while (!foundNonAmp);
            return null; // Only? reachable if no additional spinal comps found?
        }
    }
}