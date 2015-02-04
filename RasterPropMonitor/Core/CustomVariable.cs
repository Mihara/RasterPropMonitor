using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSI
{
	// SourceVariable defines a single RPM variable plus a range of digits
	// that are used for compound tests for custom variables.
	class SourceVariable
	{
		public readonly string name;
		public readonly double minValue, maxValue;
		public readonly bool reverse;

		public SourceVariable(ConfigNode node)
		{
			name = node.GetValue("name");

			if (node.HasValue("range")) {
				string[] tokens = { };
				tokens = node.GetValue("range").Split(',');
				if(tokens.Length != 2) {
					throw new ArgumentException("Found an unparseable value reading custom SOURCE_VARIABLE range");
				}
				if (!double.TryParse(tokens[0], out minValue)) {
					throw new ArgumentException("Found an unparseable value reading custom SOURCE_VARIABLE range");
				}
				if (!double.TryParse(tokens[1], out maxValue)) {
					throw new ArgumentException("Found an unparseable value reading custom SOURCE_VARIABLE range");
				}

				if(minValue > maxValue) {
					double mv = minValue;
					minValue = maxValue;
					maxValue = mv;
				}
			} else {
				minValue = double.MaxValue;
				maxValue = double.MinValue;
			}

			if (node.HasValue("reverse")) {
				if (!bool.TryParse(node.GetValue("reverse"), out reverse)) {
					throw new ArgumentException("So is 'reverse' true or false?");
				}
			} else {
				reverse = false;
			}
		}

		public object Evaluate(RasterPropMonitorComputer comp)
		{
			double result = comp.ProcessVariable(name).MassageToDouble();
			bool returnResult = (result >= minValue && result <= maxValue);
			//JUtil.LogMessage(this, "Evaluate {0} ({3}, {4}) = {1}, returning {2}", name, result, returnResult, minValue, maxValue);

			return (reverse) ? !returnResult : returnResult;
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

			foreach (ConfigNode sourceVarNode in node.GetNodes("SOURCE_VARIABLE")) {
				SourceVariable sourceVar = new SourceVariable(sourceVarNode);

				sourceVariables.Add(sourceVar);
			}

			if(sourceVariables.Count == 0) {
				throw new ArgumentException("Did not find any SOURCE_VARIABLE nodes in RPM_CUSTOM_VARIABLE", name);
			}

			string oper = node.GetValue("operator");
			if (oper == Operator.NONE.ToString()) {
				op = Operator.NONE;
			} else if (oper == Operator.AND.ToString()) {
				op = Operator.AND;
			} else if (oper == Operator.OR.ToString()) {
				op = Operator.OR;
			} else if (oper == Operator.XOR.ToString()) {
				op = Operator.XOR;
			} else {
				throw new ArgumentException("Found an invalid operator type in RPM_CUSTOM_VARIABLE", oper);
			}
		}

		public object Evaluate(RasterPropMonitorComputer comp)
		{
			// MOARdV TODO: Reevaluate (SWIDT?) this method if math expressions are added
			object evaluation = sourceVariables[0].Evaluate(comp);

			for (int i = 1; i < sourceVariables.Count; ++i) {
				object nextValue = sourceVariables[i].Evaluate(comp);
				if(!(nextValue is bool)) {
					throw new ArgumentException("CustomVariable.Evaluate - source variable did not return a bool?");
				}

				switch(op)
				{
					case Operator.AND:
					case Operator.NAND:
						if (nextValue is bool) {
							evaluation = ((bool)evaluation) && ((bool)nextValue);
						}
						break;
					case Operator.OR:
					case Operator.NOR:
						if (nextValue is bool) {
							evaluation = ((bool)evaluation) || ((bool)nextValue);
						}
						break;
					case Operator.XOR:
						if (nextValue is bool) {
							evaluation = ((bool)evaluation) ^ ((bool)nextValue);
						}
						break;
					default:
						throw new ArgumentException("CustomVariable.Evaluate was called with an invalid operator?");
					case Operator.NONE:
						break;
				}
			}

			if (op == Operator.NAND || op == Operator.NOR) {
				evaluation = !((bool)evaluation);
			}

			return evaluation.GetHashCode();
		}
	}
}
