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
using UnityEngine;

namespace JSI
{
    class MathVariable : IComplexVariable
    {
        enum Operator
        {
            NONE,
            ADD,
            SUBTRACT,
            MULTIPLY,
            DIVIDE,
            MAX,
            MIN,
            POWER,
            ANGLEDELTA,
            ATAN2,
            MAXINDEX,
            MININDEX,
            MODULO,
        };

        public readonly string name;
        private List<VariableOrNumber> sourceVariables = new List<VariableOrNumber>();
        private readonly Operator op;
        private readonly bool indexOperator;

        internal MathVariable(ConfigNode node, RasterPropMonitorComputer rpmComp)
        {
            name = node.GetValue("name");

            int maxParameters = int.MaxValue;

            string oper = node.GetValue("operator");
            if (oper == Operator.NONE.ToString())
            {
                op = Operator.NONE;
                indexOperator = false;
            }
            else if (oper == Operator.ADD.ToString())
            {
                op = Operator.ADD;
                indexOperator = false;
            }
            else if (oper == Operator.SUBTRACT.ToString())
            {
                op = Operator.SUBTRACT;
                indexOperator = false;
            }
            else if (oper == Operator.MULTIPLY.ToString())
            {
                op = Operator.MULTIPLY;
                indexOperator = false;
            }
            else if (oper == Operator.DIVIDE.ToString())
            {
                op = Operator.DIVIDE;
                indexOperator = false;
            }
            else if (oper == Operator.MAX.ToString())
            {
                op = Operator.MAX;
                indexOperator = false;
            }
            else if (oper == Operator.MIN.ToString())
            {
                op = Operator.MIN;
                indexOperator = false;
            }
            else if (oper == Operator.POWER.ToString())
            {
                op = Operator.POWER;
                indexOperator = false;
            }
            else if (oper == Operator.ANGLEDELTA.ToString())
            {
                op = Operator.ANGLEDELTA;
                indexOperator = false;
                maxParameters = 2;
            }
            else if (oper == Operator.ATAN2.ToString())
            {
                op = Operator.ATAN2;
                indexOperator = false;
                maxParameters = 2;
            }
            else if (oper == Operator.MODULO.ToString())
            {
                op = Operator.MODULO;
                indexOperator = false;
                maxParameters = 2;
            }
            else if (oper == Operator.MAXINDEX.ToString())
            {
                op = Operator.MAXINDEX;
                indexOperator = true;
            }
            else if (oper == Operator.MININDEX.ToString())
            {
                op = Operator.MININDEX;
                indexOperator = true;
            }
            else
            {
                throw new ArgumentException("Found an invalid operator type in RPM_CUSTOM_VARIABLE", oper);
            }

            string[] sources = node.GetValues("sourceVariable");
            int numIndices = Math.Min(sources.Length, maxParameters);
            for (int i = 0; i < numIndices; ++i)
            {
                VariableOrNumber sv = rpmComp.InstantiateVariableOrNumber(sources[i]);
                sourceVariables.Add(sv);
            }

            if (sourceVariables.Count == 0)
            {
                throw new ArgumentException("Did not find any SOURCE_VARIABLE nodes in RPM_CUSTOM_VARIABLE", name);
            }
        }

        public object Evaluate()
        {
            if (indexOperator)
            {
                int index = 0;
                float value = sourceVariables[0].AsFloat();

                for (int i = 1; i < sourceVariables.Count; ++i)
                {
                    float operand = sourceVariables[i].AsFloat();

                    switch (op)
                    {
                        case Operator.MAXINDEX:
                            if (operand > value)
                            {
                                index = i;
                                value = operand;
                            }
                            break;
                        case Operator.MININDEX:
                            if (operand < value)
                            {
                                index = i;
                                value = operand;
                            }
                            break;
                    }
                }

                return index;
            }
            else
            {
                double value = sourceVariables[0].AsDouble();

                for (int i = 1; i < sourceVariables.Count; ++i)
                {
                    double operand = sourceVariables[i].AsDouble();

                    switch (op)
                    {
                        case Operator.NONE:
                            break;
                        case Operator.ADD:
                            value += operand;
                            break;
                        case Operator.SUBTRACT:
                            value -= operand;
                            break;
                        case Operator.MULTIPLY:
                            value *= operand;
                            break;
                        case Operator.DIVIDE:
                            value /= operand;
                            break;
                        case Operator.MAX:
                            value = Math.Max(value, operand);
                            break;
                        case Operator.MIN:
                            value = Math.Min(value, operand);
                            break;
                        case Operator.POWER:
                            value = Math.Pow(value, operand);
                            break;
                        case Operator.ANGLEDELTA:
                            value = Mathf.DeltaAngle((float)value, (float)operand);
                            break;
                        case Operator.ATAN2:
                            value = Math.Atan2(value, operand) * Mathf.Rad2Deg;
                            break;
                        case Operator.MODULO:
                            value = (operand == 0.0) ? 0.0 : (value - operand * Math.Floor(value / operand));
                            break;
                    }
                }

                return value;
            }
        }
    }
}
