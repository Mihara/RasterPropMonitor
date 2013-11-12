using System;
using UnityEngine;

namespace JSI
{
	internal static class JUtil
	{
		public static RasterPropMonitorComputer GetComputer(InternalProp thatProp){
			// I hate copypaste, and this is what I'm going to do about it.
			if (thatProp.part != null) {
				foreach (InternalProp prop in thatProp.part.internalModel.props) {
					RasterPropMonitorComputer other = prop.FindModelComponent<RasterPropMonitorComputer>();
					if (other != null) {
						return other;
					}
				}
			}
			thatProp.AddModule(typeof(RasterPropMonitorComputer).Name);
			return thatProp.FindModelComponent<RasterPropMonitorComputer>();
		}

	}

}

