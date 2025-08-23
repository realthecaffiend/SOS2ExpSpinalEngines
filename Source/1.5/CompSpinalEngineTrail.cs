using RimWorld;
using SaveOurShip2;
using System.Text;
using Verse;

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
                stringBuilder.AppendInNewLine("SoS.SpinalEngines.Thrust".Translate(cachedThrust * 500)); // "Thrust: {cachedThrust * 500}"
            }
            else
            {
                stringBuilder.Append(cachedError.Colorize(UnityEngine.Color.red));
            }
            return stringBuilder.ToString();
        }
        public virtual CompProperties_SpinalEngineTrail Properties //Wait is this dirty I just want to be clear it's not hiding hrm
        {
            get { return props as CompProperties_SpinalEngineTrail; }
        }
        public class ReturnValue
        {
            public float thrustAmp;
            public float fuelAmp;
            public float fuelAllowAmp;
            public float powerUseAmp;
            public int supportWeight;
            public string playerError;
            public bool fullyFormed;
        }
        private int cachedThrust = 0;
        private int cachedMass = 0; // add/remove on the *engine* does this itself, this is only amps+caps
        public bool fullyFormed = false;
        private int cachedFuelUse = 0;
        private int cachedPowerUse = 0;
        private int cachedFuelAllowed = 0; // Error below probably never surfaces to a player but I want a fallback
        private string cachedError = "SoS.SpinalEngines.DefaultError".Translate();
        public override int Thrust
        {
            get
            {
                if (parent.TryGetComp<CompSpinalEngineTrail>() == null) //should never? happen but reflex
                {
                    Log.Message("SOS2 spinal engines: somehow hit thrust from a non-compspinalenginetrail?");
                    return base.Thrust;
                }
                if (fullyFormed)
                {
                    //Log.Message($"sos2 spinal engines thrust called about a spinal engine, returning cached thrust {thrustCached}");
                    return cachedThrust;
                }
                else
                {
                    //Log.Message($"sos2 spinal engines thrust called about a spinal engine, not fully formed");
                    calc();
                    return cachedThrust;
                }
            }
        }
        public string CurrentError // Feels a little silly to have a setter just for this, maybe cachedError should just be public? 
        {
            set
            {
                cachedError = value;
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
                    return -cachedPowerUse; 
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
                if (fullyFormed)
                {
                    //Log.Message($"sos2 spinal engines thrust called about a spinal engine, returning cached fuelUse {cachedFuelUse}");
                    return cachedFuelUse;
                }
                else
                {
                    //Log.Message($"sos2 spinal engines fuel called about a spinal engine, not fully formed");
                    calc();
                    return cachedFuelUse;
                }
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
                if (fullyFormed)
                {
                    return cachedMass;
                }
                else
                {
                    calc();
                    return cachedMass;
                }
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
        private void calc()
        {
            ReturnValue wasReturned = SpinalRecalc(this);
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
                Reset(); //I can't see this being needed but eh 
                cachedThrust = (int)(base.Props.thrust * (1 + wasReturned.thrustAmp)); 
                cachedFuelUse = (int)(base.Props.fuelUse * (1 + wasReturned.fuelAmp));
                CompSpinalEnginePowerTrader powerComp = parent.TryGetComp<CompSpinalEnginePowerTrader>(); // removing this makes the game NRE?
                cachedPowerUse = (int)(powerComp.Props.PowerConsumption * (1 + wasReturned.powerUseAmp));// Ok I guess I keep powerComp?!
                //Log.Message($"sos2spinal engines base.powercomp.poweroutput from calc returned poweruse is {wasReturned.powerUseAmp} cached is {cachedPowerUse}");
                powerComp.PowerOutput = 0 - cachedPowerUse; 
                //Log.Message($"sos2spinal engines modified comppowertrader PowerOutput on {parent.GetUniqueLoadID()} to {powerComp.PowerOutput}");
                cachedFuelAllowed = (int)(this.Properties.fuelAllowed * (1 + wasReturned.fuelAllowAmp));
                cachedMass = (int)(wasReturned.supportWeight);
                fullyFormed = true;
            }
        }
        public static ReturnValue SpinalRecalc(CompSpinalEngineTrail instance)
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
            ReturnValue toReturn = new ReturnValue
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
        }

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
            do
            {
                if (buildingPointer.TryGetComp<CompSpinalMount>().Props.stackEnd)
                {
                    previousThingPos -= vecMoveCheck; // Jump one more if it's a tankend, to reach the amp
                }
                previousThingPos -= vecMoveCheck;
                //Log.Message($"SOS2 Spinal Engines: Climbing spinal to find engine, looking at: {previousThingPos}");
                //Log.Message($"SOS2 Spinal Engines: previousThingPos is {previousThingPos} and BuildingPointer.Position is {buildingPointer.Position}");
                buildingPointer = previousThingPos.GetFirstThingWithComp<CompSpinalMount>(buildingPointer.Map);
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
                    //Log.Message("SOS2 spinal engines: EngineFromSpinal found an engine and is about to return it");
                    return (Building)buildingPointer;
                }
                //found unaligned
                else
                {
                    //Log.Message($"SOS2 spinal engines: Found unaligned in EngineFromSpinal amp.position: {buildingPointer.Position}, previouspos minus vec: {previousThingPos - vecMoveCheck}");
                    foundNonAmp = true;
                }

            } while (!foundNonAmp);
            return null; // Only? reachable if no additional spinal comps found?
        }
    }
}