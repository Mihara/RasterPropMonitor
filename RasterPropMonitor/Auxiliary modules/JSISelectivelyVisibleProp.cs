using System.Collections.Generic;

namespace JSI
{
	public class JSISelectivelyVisibleProp: InternalModule
	{
		[KSPField]
		public string visibleFromSeats = string.Empty;

		private readonly List<int> seatNumbers = new List<int>();

		public void Start()
		{
			foreach (string seatNumberString in visibleFromSeats.Split(',')) {
				int result;
				if (int.TryParse(seatNumberString.Trim(), out result) && result >= 0) {
					JUtil.LogMessage(this, "Running in prop '{2}' with ID {1}, will be visible from seat {0}", result, internalProp.propID, internalProp.name);
					seatNumbers.Add(result);
				}
				JUtil.HideShowProp(internalProp, false);
			}
		}

		public override void OnUpdate()
		{
			if (JUtil.UserIsInPod(part)) {
				JUtil.HideShowProp(internalProp,seatNumbers.Contains(part.CurrentActiveSeat()));
			}
		}
	}
}

