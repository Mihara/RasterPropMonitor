using System;
using UnityEngine;
using System.Reflection;
using System.Collections.Generic;

namespace JSI
{
	public class ExternalVariableHandlers
	{
		private readonly List<VariableHandler> handlers = new List<VariableHandler>();

		public ExternalVariableHandlers(RasterPropMonitorComputer ourComp)
		{
			foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes ("RPMCVARIABLEHANDLER")) {
				if (!node.HasValue("name") || !node.HasValue("method") || !node.HasValue("activeVariable")) {
					JUtil.LogMessage(ourComp, "A variable handler configuration block is missing key items and was ignored.");
				} else {
					foreach (ConfigNode handlerBlock in node.GetNodes("RPMCVARIABLEHANDLER")) {
						handlers.Add(new VariableHandler(handlerBlock, ourComp));
					}
				}
			}

		}

		public bool ProcessVariable(string variable, out object result, out bool cacheable)
		{
			result = null;
			cacheable = true;
			foreach (VariableHandler handler in handlers) {
				if (handler.ProcessVariable(variable, out result, out cacheable))
					return true;
			}
			return false;
		}

		private class VariableHandler
		{
			private readonly bool active;
			private readonly Func<string,object> handlerFunction;

			private struct VariableRecord
			{
				public double defaultValue;
				public bool cacheable;
			};

			private readonly Dictionary<string,VariableRecord> handledVariables = new Dictionary<string,VariableRecord>();

			public bool ProcessVariable(string variable, out object result, out bool cacheable)
			{
				cacheable = true;
				result = null;
				if (handledVariables.ContainsKey(variable)) {
					if (active) {
						result = handlerFunction(variable);
						cacheable = handledVariables[variable].cacheable;
					} else {
						result = handledVariables[variable].defaultValue;
						cacheable = true;
					}
					return true;
				}
				return false;
			}

			public VariableHandler(ConfigNode node, RasterPropMonitorComputer ourComp)
			{
				string handlerName = node.GetValue("name");
				foreach (string variableRecord in node.GetValues("variable")) {
					var record = new VariableRecord();
					string[] tokens = variableRecord.Split(',');
					if (tokens.Length >= 2) {
						record.defaultValue = double.Parse(tokens[1]);
					}
					if (tokens.Length >= 3) {
						record.cacheable = bool.Parse(tokens[2]);
					}
					handledVariables.Add(tokens[0], record);
				}

				active = InstantiateHandler(node, ourComp, out handlerFunction);
			}

			private static bool InstantiateHandler(ConfigNode node, PartModule ourComp, out Func<string,object> handlerFunction)
			{
				handlerFunction = null;
				var handlerConfiguration = new ConfigNode("MODULE");
				node.CopyTo(handlerConfiguration);
				string moduleName = node.GetValue("name");
				string methodName = node.GetValue("method");

				// Since we're working with part modules here, and starting in a pod config,
				// we'll keep one instance per pod, which will let them instantiate with their own config if needed.
				MonoBehaviour thatModule = null;
				foreach (PartModule potentialModule in ourComp.part.Modules) {
					if (potentialModule.ClassName == moduleName) {
						thatModule = potentialModule;
						break;
					}
				}
				if (thatModule == null) {
					thatModule = ourComp.part.AddModule(handlerConfiguration);
				}
				if (thatModule == null) {
					JUtil.LogMessage(ourComp, "Warning, variable handler module \"{0}\" could not be loaded. This could be perfectly normal.", moduleName);
					return false;
				}
				foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
					if (m.Name == node.GetValue("method")) {
						try {
							handlerFunction = (Func<string,object>)Delegate.CreateDelegate(typeof(Func<string,object>), thatModule, m);
						} catch {
							JUtil.LogErrorMessage(ourComp, "Error, incorrect variable handler configuration for module {0}", moduleName);
							return false;
						}
						break;
					}
				}
				return true;
			}
		}
	}
}