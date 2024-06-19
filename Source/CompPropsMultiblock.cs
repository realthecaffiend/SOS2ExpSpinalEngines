using System.Collections.Generic;
using Verse;

namespace TheCafFiend
{
    public class CompPropsMultiblock : CompProperties
    {
        public bool emits = false;
        public string multiblockType = null; // mod-defined useage, with the below
        public List<MultiblockConnections> acceptFrom = new List<MultiblockConnections>();
        /*public List<string> connectToFront = null; //list (<li>) of types (above!) to connect to in front
        public List<string> connectToClockwise = null; //list (<li>) of types (above!) to connect to in front
        public List<string> connectToOpposite = null; //list (<li>) of types (above!) to connect to in front
        public List<string> connectToCounterclockwise = null; //list (<li>) of types (above!) to connect to in front */
        public bool multiblockController = false; // Only flagged on the one, singular, "smart" block in the structure
        public CompPropsMultiblock()
        {
            this.compClass = typeof(CompPropsMultiblock);
        }
    }
    public class MultiblockConnections
    {
        public List<string> connectType = null;
        public RotationDirection direction = RotationDirection.None;
    }
}
