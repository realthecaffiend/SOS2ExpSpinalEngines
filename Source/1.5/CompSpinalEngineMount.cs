using SaveOurShip2;
using Verse;

namespace TheCafFiend
{
    [StaticConstructorOnStartup]
    public class CompSpinalEngineMount : CompSpinalMount
    {
        public new CompProperties_SpinalEngineMount Props => (CompProperties_SpinalEngineMount)props;
    }
}
