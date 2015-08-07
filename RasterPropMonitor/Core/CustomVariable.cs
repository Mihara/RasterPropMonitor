using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSI
{
    // SourceVariable defines a single RPM variable plus a range of values
    // that are used for compound tests for custom variables.
    class SourceVariable
    {
        public readonly VariableOrNumberRange range;
        public readonly bool reverse;

        public SourceVariable(ConfigNode node)
        {
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
        }

        public bool Evaluate(RPMVesselComputer comp, PersistenceAccessor persistence)
        {
            return range.IsInRange(comp, persistence) ^ reverse;
        }
    }

    // A CustomVariable defines a user-defined variable that consists of one or
    // more RPM variables.  The CustomVariable applies a single logical operator
    // across all the variables.
    class CustomVariable
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

        private Operator op;
        private List<SourceVariable> sourceVariables = new List<SourceVariable>();

        public CustomVariable(ConfigNode node)
        {
            name = node.GetValue("name");

            foreach (ConfigNode sourceVarNode in node.GetNodes("SOURCE_VARIABLE"))
            {
                SourceVariable sourceVar = new SourceVariable(sourceVarNode);

                sourceVariables.Add(sourceVar);
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

        public object Evaluate(RPMVesselComputer comp, PersistenceAccessor persistence)
        {
            // MOARdV TODO: Reevaluate (SWIDT?) this method if math expressions are added
            bool evaluation = sourceVariables[0].Evaluate(comp, persistence);

            for (int i = 1; i < sourceVariables.Count; ++i)
            {
                bool nextValue = sourceVariables[i].Evaluate(comp, persistence);

                switch (op)
                {
                    case Operator.AND:
                    case Operator.NAND:
                        evaluation = (evaluation) && (nextValue);
                        break;
                    case Operator.OR:
                    case Operator.NOR:
                        evaluation = (evaluation) || (nextValue);
                        break;
                    case Operator.XOR:
                        evaluation = (evaluation) ^ (nextValue);
                        break;
                    default:
                        throw new ArgumentException("CustomVariable.Evaluate was called with an invalid operator?");
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
    }
}
