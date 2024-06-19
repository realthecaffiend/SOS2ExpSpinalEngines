using SaveOurShip2;
using UnityEngine;

namespace TheCafFiend
{
    public class CompProperties_SpinalEngineMount : CompPropsMultiblock
    {
        public int supportWeight = 0; // additional "engineMass" for the object
        public float thrustAmp = 0f;
        public float fuelUseAmp = 0f;
        public float fuelAllowAmp = 0f;
        public float powerUseAmp = 0f; 
        public Color color = Color.white;
        public CompProperties_SpinalEngineMount()
        {
            this.compClass = typeof(CompSpinalEngineMount);
        }
    }
}