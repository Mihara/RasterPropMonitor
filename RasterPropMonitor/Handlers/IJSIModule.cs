namespace JSI
{
    /// <summary>
    /// This class exists to provide a base class that RasterPropMonitorComputer
    /// manages for tracking various built-in plugin action handlers.
    /// </summary>
    public class IJSIModule
    {
        protected Vessel vessel;
        protected bool moduleInvalidated;

        protected IJSIModule(Vessel vessel)
        {
            this.vessel = vessel;
            moduleInvalidated = true;
        }

        /// <summary>
        /// Because the Vessel may change when the craft docks, we must
        /// refresh the local vessel ID.  Since we need to loop over all of the
        /// parts for Invalidate, we may as well update it then.
        /// </summary>
        /// <param name="vessel"></param>
        public void Invalidate(Vessel vessel)
        {
            this.vessel = vessel;
            moduleInvalidated = true;
        }
    }
}
