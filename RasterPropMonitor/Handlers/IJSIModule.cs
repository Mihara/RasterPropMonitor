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

        public void Invalidate()
        {
            moduleInvalidated = true;
        }
    }
}
