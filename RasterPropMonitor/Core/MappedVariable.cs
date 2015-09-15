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
    class MappedVariable
    {
        private readonly VariableOrNumberRange sourceVariable;
        public readonly string mappedVariable;
        private readonly Vector2 mappedRange;

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
            mappedRange = ConfigNode.ParseVector2(node.GetValue("mappedRange"));
        }

        public double Evaluate(RPMVesselComputer comp)
        {
            float lerp;
            if (sourceVariable.InverseLerp(comp, out lerp))
            {
                return Mathf.Lerp(mappedRange.x, mappedRange.y, lerp);
            }
            else
            {
                return 0.0f;
            }
        }
    }
}
