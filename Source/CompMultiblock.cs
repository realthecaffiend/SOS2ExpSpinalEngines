using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Verse;

namespace TheCafFiend
{
    public class CompMultiblock : ThingComp
    {
        public new CompPropsMultiblock Props => (CompPropsMultiblock)props;
        public static IntVec3 MultiblockStep(Thing argBuilding, int argDirection)
        {
            return MultiblockStep(argBuilding.Position, argBuilding.Rotation, argBuilding.def.size, argDirection);
        }
        public static IntVec3 MultiblockStep(IntVec3 center, Rot4 rot, IntVec2 size, int argDirection) // GenAdj CellsAdjacentAlongEdge has similar function but I only want the "center" aligned block, not all of them
        {
            GenAdj.AdjustForRotation(ref center, ref size, rot);
            int edgeXNegative = center.x - (size.x - 1) / 2;
            int edgeXPositive = edgeXNegative + size.x;
            int edgeZNegative = center.z - (size.z - 1) / 2;
            int edgeZPositive = edgeZNegative + size.z;

            if (argDirection == 0)
            {
                if (size.z % 2 != 0 && size.z != 1) //Rimworld "centers" the position which is strange when the object is even
                {
                    return new IntVec3(center.x, center.y, edgeZPositive + 1);
                }
                else
                {
                    return new IntVec3(center.x, center.y, edgeZPositive);
                }
            }
            else if (argDirection == 1)
            {
                if (size.x % 2 != 0 && size.x != 1) //Rimworld "centers" the position which is strange when the object is even
                {
                    return new IntVec3(edgeXPositive + 1, center.y, center.z);
                }
                else
                {
                    return new IntVec3(edgeXPositive, center.y, center.z);
                }
            }
            else if (argDirection == 2)
            {
               return new IntVec3(center.x, center.y, edgeZNegative - 1);
            }
            else if (argDirection == 3)
            {
                return new IntVec3(edgeXNegative - 1, center.y, center.z);
            }
            else
            {
                Log.Error($"Multiblock MultiblockStep rotation invalid");
                return IntVec3.Zero;
            }
        }
        public static RecursiveReturn RecursiveFind(ref List<Building> workingBlockList, Thing argBuilding, MultiblockConnections argDirection, bool recursiveDebug = false)
        {
            RecursiveReturn returnedValues = new RecursiveReturn();
            if (argBuilding == null)
            {
                Log.Message($"MBLib: RecursiveFind argBuilding is null");
                return null;
            }
            if (workingBlockList == null)
            {
                Log.Message($"MBLib: RecursiveFind workingBlockList is null");
                return null;
            }
            IntVec3 nextPosition = MultiblockStep(argBuilding, argBuilding.Rotation.Rotated(argDirection.direction).AsInt);
            if (recursiveDebug) Log.Message($"MBLib: nextpos {nextPosition}");
            Building nextBlock = (Building)nextPosition.GetFirstThingWithComp<CompMultiblock>(argBuilding.Map);
            if (nextBlock == null) 
            {
                return returnedValues; //Might be still a valid multi, but this is the end fo this chain: Don't bail
            }
            if (recursiveDebug) Log.Message($"MBLib: nextblock defname {nextBlock.def.defName}");
            CompMultiblock nextMultiblockComp = nextBlock.TryGetComp<CompMultiblock>();
            //CompMultiblock currentMultiblock = argBuilding.TryGetComp<CompMultiblock>();
            if (recursiveDebug) Log.Message($"MBLib: currentMultiblock accepts from {argDirection.direction} types of {argDirection.connectType.ToCommaList()}");
            if (argDirection.connectType.Contains(nextMultiblockComp.Props.multiblockType)) //check for connection type rules
            { 
                if (recursiveDebug) Log.Message($"MBLib: connection type check succeeded");
                IntVec3 directionCheckPosition = MultiblockStep(nextBlock, nextBlock.Rotation.AsInt);
                if (directionCheckPosition == null)
                {
                    Log.Message($"MBLib: nextblock step(forward) directionCheckPosition returned null");
                    return null;
                }
                if (!argBuilding.OccupiedRect().Contains(directionCheckPosition)) // next block doesn't "point" into current
                {
                    // Log.Message($"MBLib: block {nextBlock.def.label} at {nextBlock.Position} does not connect into {argBuilding.def.label}");
                    returnedValues.incorrectBlockRotation = true;
                    returnedValues.errorBlockLabel = nextBlock.def.label;
                    returnedValues.errrorBlockPosition = nextBlock.Position;
                    returnedValues.recursiveErrorFound = true;
                    return returnedValues;
                }
                // This generates an intvec3 with 0's in the dimensions I don't care about, to multiply with a real position. Is this clever, or cursed? 
                IntVec3 removeExtraDims = nextBlock.Rotation.RighthandCell;
                IntVec3 compareDimNext = new IntVec3(nextBlock.Position.x * removeExtraDims.x, 0, nextBlock.Position.z * removeExtraDims.z);
                IntVec3 compareDimCurrent = new IntVec3(argBuilding.Position.x * removeExtraDims.x, 0, argBuilding.Position.z * removeExtraDims.z);
                if (recursiveDebug) Log.Message($"MBLib: clursed alignment check of {compareDimNext} to {compareDimCurrent}");
                if (compareDimCurrent != compareDimNext) //unaligned! 
                {
                    //Log.Message($"MBLib: {nextBlock.def.label} at {nextBlock.Position} is incorrectly aligned with {argBuilding.def.label}");
                    returnedValues.incorrectAlignment= true;
                    returnedValues.errorBlockLabel = nextBlock.def.label;
                    returnedValues.errrorBlockPosition = nextBlock.Position;
                    returnedValues.recursiveErrorFound = true;
                    return returnedValues;
                }
                workingBlockList.Add(nextBlock);
                if (recursiveDebug) Log.Message($"MBLib: added nextBlock to list");
                foreach (MultiblockConnections acceptsFrom in nextMultiblockComp.Props.acceptFrom)
                {
                    if (recursiveDebug) Log.Message($"MBLib: recursing in direction {acceptsFrom.direction}");
                    returnedValues = RecursiveFind(ref workingBlockList, nextBlock, acceptsFrom, recursiveDebug); //Probably fine to carry up the stack right?
                }
            }
            return returnedValues;
        }
        public static Building ControllerFromSubBlock(Building argBuilding, bool controllerFromDebug = false) //recursivefind builds out backwards and a list, this just beelines to controller
        {
            if (controllerFromDebug) Log.Message($"MBLib: ControllerFromSubBlock start");
            if (argBuilding == null)
            {
                Log.Message($"MBLib: null building in ControllerFromSubBlock");
                return null;
            }
            CompMultiblock multiComp = argBuilding.TryGetComp<CompMultiblock>();
            if (multiComp == null)
            {
                Log.Message($"MBLib: null CompMultiBlock in ControllerFromSubBlock");
                return null;
            }
            if (controllerFromDebug) Log.Message($"MBLib: ControllerFromSubBlock CompMultiBlock not null");
            if (multiComp.Props.multiblockController) // The item in question is the controller
            {
                return argBuilding;
            }
            bool foundController = false;
            Thing stepBuilding = argBuilding;
            while (foundController == false)
            {
                IntVec3 nextPosition = CompMultiblock.MultiblockStep(stepBuilding, argBuilding.Rotation.AsInt);
                if (nextPosition == null)
                {
                    Log.Message($"MBLib: null nextPosition in ControllerFromSubBlock");
                    return null;
                }
                if (controllerFromDebug) Log.Message($"MBLib: ControllerFromSubBlock nextPostion is {nextPosition}");
                Thing nextBuilding = nextPosition.GetFirstThingWithComp<CompMultiblock>(stepBuilding.Map);
                if (nextBuilding == null)
                {
                    Log.Message($"MBLib: null nextBuilding in ControllerFromSubBlock");
                    return null;
                }
                CompMultiblock nextMultiComp = nextBuilding.TryGetComp<CompMultiblock>();
                if (nextMultiComp == null)
                {
                    Log.Message($"MBLib: null nextMultiComp in ControllerFromSubBlock");
                    return null;
                }
                if (nextMultiComp.Props.multiblockController)
                {
                    foundController = true;
                    return (Building)nextBuilding;
                }
                else
                {
                    stepBuilding = nextBuilding;
                }
            }
            return argBuilding;
        }
    }
    public class MultiblockState //used to pass state of multiblock objects; Usually to the controller. Presumably extended
    {
        public string playerError;
        public bool fullyFormed;
    }
    public class RecursiveReturn
    {
        public bool incorrectAlignment = false;
        public bool incorrectBlockRotation = false;
        public bool recursiveErrorFound = false;
        public string errorBlockLabel = string.Empty;
        public IntVec3 errrorBlockPosition = new IntVec3();
    }
}
