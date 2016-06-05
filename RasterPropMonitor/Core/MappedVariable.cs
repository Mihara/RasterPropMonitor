/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;
using UnityEngine;

namespace JSI
{
    class MappedVariable : IComplexVariable
    {
        private readonly VariableOrNumberRange sourceVariable;
        public readonly string mappedVariable;
        private readonly VariableOrNumber mappedExtent1, mappedExtent2;

        public MappedVariable(ConfigNode node)
        {
            if (!node.HasValue("mappedVariable") || !node.HasValue("mappedRange") || !node.HasValue("sourceVariable") || !node.HasValue("sourceRange"))
            {
                throw new ArgumentException("MappedVariable missing required values");
            }

            string sourceVariableStr = node.GetValue("sourceVariable");
            string sourceRange = node.GetValue("sourceRange");
            string[] sources = sourceRange.Split(',');
            if (sources.Length != 2)
            {
                throw new ArgumentException("MappedVariable sourceRange does not have exactly two values");
            }

            sourceVariable = new VariableOrNumberRange(sourceVariableStr, sources[0], sources[1]);

            mappedVariable = node.GetValue("mappedVariable");
            string[] destinations = node.GetValue("mappedRange").Split(',');
            if (destinations.Length != 2)
            {
                throw new ArgumentException("MappedVariable mappedRange does not have exactly two values");
            }
            mappedExtent1 = VariableOrNumber.Instantiate(destinations[0]);
            mappedExtent2 = VariableOrNumber.Instantiate(destinations[1]);
        }

        public object Evaluate(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            float lerp, extent1, extent2;
            if (sourceVariable.InverseLerp(rpmComp, comp, out lerp) && mappedExtent1.Get(out extent1, rpmComp, comp) && mappedExtent2.Get(out extent2, rpmComp, comp))
            {
                return Mathf.Lerp(extent1, extent2, lerp);
            }
            else
            {
                return 0.0f;
            }
        }
    }
}
