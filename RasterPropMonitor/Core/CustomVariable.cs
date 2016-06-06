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
using System.Collections.Generic;
using System.Text;

namespace JSI
{
    /// <summary>
    /// IComplexVariable defines the evaluation interface for a category of
    /// user-defined variables.
    /// </summary>
    interface IComplexVariable
    {
        object Evaluate(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp);
    }

    // A CustomVariable defines a user-defined variable that consists of one or
    // more RPM variables.  The CustomVariable applies a single logical operator
    // across all the variables.
    class CustomVariable : IComplexVariable
    {
        enum Operator
        {
            // No evaluation - returns the results of the first variable, but
            // evaluates the others.
            NONE,
            // Evaluate each source variable as a boolean, return the logical AND of the result
            AND,
            // Evaluate each source variable as a boolean, return the logical OR of the result
            OR,
            // Evaluate each source variable as a boolean, return the logical XOR of the result
            XOR,
            // Evaluate each source variable as a boolean, return the logical NAND of the result
            NAND,
            // Evaluate each source variable as a boolean, return the logical NOR of the result
            NOR,
        };

        public readonly string name;

        private List<VariableOrNumberRange> sourceVariables = new List<VariableOrNumberRange>();
        private List<bool> reverse = new List<bool>();
        private Operator op;

        internal CustomVariable(ConfigNode node)
        {
            name = node.GetValue("name");

            foreach (ConfigNode sourceVarNode in node.GetNodes("SOURCE_VARIABLE"))
            {
                bool reverseVal;
                VariableOrNumberRange vonr = ProcessSourceNode(sourceVarNode, out reverseVal);

                sourceVariables.Add(vonr);
                reverse.Add(reverseVal);
            }

            if (sourceVariables.Count == 0)
            {
                throw new ArgumentException("Did not find any SOURCE_VARIABLE nodes in RPM_CUSTOM_VARIABLE", name);
            }

            string oper = node.GetValue("operator");
            if (oper == Operator.NONE.ToString())
            {
                op = Operator.NONE;
            }
            else if (oper == Operator.AND.ToString())
            {
                op = Operator.AND;
            }
            else if (oper == Operator.OR.ToString())
            {
                op = Operator.OR;
            }
            else if (oper == Operator.NAND.ToString())
            {
                op = Operator.NAND;
            }
            else if (oper == Operator.NOR.ToString())
            {
                op = Operator.NOR;
            }
            else if (oper == Operator.XOR.ToString())
            {
                op = Operator.XOR;
            }
            else
            {
                throw new ArgumentException("Found an invalid operator type in RPM_CUSTOM_VARIABLE", oper);
            }
        }

        public object Evaluate(RasterPropMonitorComputer rpmComp, RPMVesselComputer comp)
        {
            // MOARdV TODO: Reevaluate (SWIDT?) this method if math expressions are added
            bool evaluation = sourceVariables[0].IsInRange(rpmComp, comp) ^ reverse[0];

            // Use an optimization on evaluation to speed things up
            bool earlyExit;
            switch (op)
            {
                case Operator.AND:
                case Operator.NAND:
                    earlyExit = !evaluation;
                    break;
                case Operator.OR:
                case Operator.NOR:
                    earlyExit = evaluation;
                    break;
                case Operator.XOR:
                    earlyExit = false;
                    break;
                case Operator.NONE:
                    earlyExit = true;
                    break;
                default:
                    throw new ArgumentException("CustomVariable.Evaluate was called with an invalid operator?");
            }

            for (int i = 1; i < sourceVariables.Count && (earlyExit == false); ++i)
            {
                bool nextValue = sourceVariables[i].IsInRange(rpmComp, comp) ^ reverse[i];

                switch (op)
                {
                    case Operator.AND:
                    case Operator.NAND:
                        evaluation = (evaluation) && (nextValue);
                        earlyExit = !evaluation;
                        break;
                    case Operator.OR:
                    case Operator.NOR:
                        evaluation = (evaluation) || (nextValue);
                        earlyExit = evaluation;
                        break;
                    case Operator.XOR:
                        evaluation = (evaluation) ^ (nextValue);
                        break;
                    case Operator.NONE:
                        break;
                }
            }

            if (op == Operator.NAND || op == Operator.NOR)
            {
                evaluation = !evaluation;
            }

            return evaluation.GetHashCode();
        }

        private static VariableOrNumberRange ProcessSourceNode(ConfigNode node, out bool reverse)
        {
            VariableOrNumberRange range;
            if (node.HasValue("range"))
            {
                string[] tokens = { };
                tokens = node.GetValue("range").Split(',');
                if (tokens.Length != 2)
                {
                    throw new ArgumentException("Found an unparseable value reading custom SOURCE_VARIABLE range");
                }
                range = new VariableOrNumberRange(node.GetValue("name").Trim(), tokens[0].Trim(), tokens[1].Trim());
            }
            else
            {
                range = new VariableOrNumberRange(node.GetValue("name").Trim(), float.MinValue.ToString(), float.MaxValue.ToString());
            }

            if (node.HasValue("reverse"))
            {
                if (!bool.TryParse(node.GetValue("reverse"), out reverse))
                {
                    throw new ArgumentException("So is 'reverse' true or false?");
                }
            }
            else
            {
                reverse = false;
            }

            return range;
        }
    }
}
