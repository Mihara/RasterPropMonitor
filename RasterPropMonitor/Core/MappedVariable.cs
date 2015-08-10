using System;
using UnityEngine;

namespace JSI
{
    class MappedVariable
    {
        private readonly string sourceVariable;
        private readonly float sourceMin = 0.0f;
        private readonly float sourceMax = 0.0f;
        private readonly string sourceMinStr = null;
        private readonly string sourceMaxStr = null;
        public readonly string mappedVariable;
        private readonly Vector2 mappedRange;

        public MappedVariable(ConfigNode node)
        {
            if (!node.HasValue("mappedVariable") || !node.HasValue("mappedRange") || !node.HasValue("sourceVariable") || !node.HasValue("sourceRange"))
            {
                throw new ArgumentException("MappedVariable missing required values");
            }

            sourceVariable = node.GetValue("sourceVariable");
            string sourceRange = node.GetValue("sourceRange");
            string[] sources = sourceRange.Split(',');
            if (sources.Length != 2)
            {
                throw new ArgumentException("MappedVariable sourceRange does not have exactly two values");
            }

            if (!float.TryParse(sources[0].Trim(), out sourceMin))
            {
                sourceMinStr = sources[0].Trim();
            }
            if (!float.TryParse(sources[1].Trim(), out sourceMax))
            {
                sourceMaxStr = sources[1].Trim();
            }

            mappedVariable = node.GetValue("mappedVariable");
            mappedRange = ConfigNode.ParseVector2(node.GetValue("mappedRange"));
        }

        public double Evaluate(RPMVesselComputer comp, PersistenceAccessor persistence)
        {
            float result = comp.ProcessVariable(sourceVariable, persistence).MassageToFloat();

            Vector2 sourceRange;
            if (!string.IsNullOrEmpty(sourceMinStr))
            {
                sourceRange.x = comp.ProcessVariable(sourceMinStr, persistence).MassageToFloat();
            }
            else
            {
                sourceRange.x = sourceMin;
            }

            if (!string.IsNullOrEmpty(sourceMaxStr))
            {
                sourceRange.y = comp.ProcessVariable(sourceMaxStr, persistence).MassageToFloat();
            }
            else
            {
                sourceRange.y = sourceMax;
            }

            return JUtil.DualLerp(mappedRange, sourceRange, result);
        }
    }
}
