
namespace JSI
{
	public class JSIPropIDFinder: InternalModule
	{
		public void Start()
		{
			JUtil.LogMessage(this, "I am in prop named {0} and it has prop ID {1}", internalProp.name, internalProp.propID);
			// And just in case the user forgets.
			Destroy(this); 
		}
	}
}

