using UnityEngine;
using System;
using System.Collections.Generic;

namespace JSI
{
	public class SmarterButton: MonoBehaviour
	{
		private readonly List<handlerID> clickHandlersID = new List<handlerID>();
		private readonly List<handlerID> releaseHandlersID = new List<handlerID>();
		private readonly List<Action> clickHandlers = new List<Action>();
		private readonly List<Action> releaseHandlers = new List<Action>();
		private readonly List<pageTrigger> pageReferences = new List<pageTrigger>();
		private int listCounter;

		private struct handlerID
		{
			public Action<int> function;
			public int idValue;
		}

		private struct pageTrigger
		{
			public Action<MonitorPage> selector;
			public MonitorPage page;
		}

		public void OnMouseDown()
		{
			if (pageReferences.Count > 0) {
				pageReferences[listCounter].selector(pageReferences[listCounter].page);
				listCounter++;
				if (listCounter >= pageReferences.Count)
					listCounter = 0;
			}
			foreach (handlerID consumer in clickHandlersID) {
				consumer.function(consumer.idValue);
			}
			foreach (Action clickHandler in clickHandlers) {
				clickHandler();
			}
		}

		public void OnMouseUp()
		{
			foreach (handlerID consumer in releaseHandlersID) {
				consumer.function(consumer.idValue);
			}
			foreach (Action releaseHandler in releaseHandlers) {
				releaseHandler();
			}
		}

		private static SmarterButton AttachBehaviour(InternalProp thatProp, string buttonName)
		{

			string[] tokens = buttonName.Split('|');
			if (tokens.Length == 2) {
				// First token is the button name, second is the prop ID.
				int propID;
				if (int.TryParse(tokens[1], out propID)) {
					if (propID < thatProp.internalModel.props.Count) {
						thatProp = thatProp.internalModel.props[propID];
						buttonName = tokens[0].Trim();
					} else
						Debug.LogError(string.Format("Could not find a prop with ID {0}", propID));
				}
			} else
				buttonName = buttonName.Trim();
			try {
				GameObject buttonObject = thatProp.FindModelTransform(buttonName).gameObject;
				SmarterButton thatComponent = buttonObject.GetComponent<SmarterButton>() ?? buttonObject.AddComponent<SmarterButton>();
				return thatComponent;
			} catch {
				Debug.LogError(string.Format(
					"Could not register a button on transform named '{0}' in prop named '{1}'. Check your configuration.",
					buttonName, thatProp.propName));
			}
			return null;
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, MonitorPage thatPage, Action<MonitorPage> handlerFunction)
		{
			SmarterButton buttonBehaviour;
			if ((buttonBehaviour = AttachBehaviour(thatProp, buttonName)) == null)
				return;
			buttonBehaviour.pageReferences.Add(new pageTrigger {
				selector = handlerFunction,
				page = thatPage
			});
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, int numericID, Action<int> clickHandlerFunction, Action<int> releaseHandlerFunction)
		{
			SmarterButton buttonBehaviour;
			if ((buttonBehaviour = AttachBehaviour(thatProp, buttonName)) == null)
				return;

			buttonBehaviour.clickHandlersID.Add(new handlerID {
				function = clickHandlerFunction,
				idValue = numericID
			});
			buttonBehaviour.releaseHandlersID.Add(new handlerID {
				function = releaseHandlerFunction,
				idValue = numericID
			});
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, Action handlerFunction, Action releaseHandlerFunction = null)
		{
			SmarterButton buttonBehaviour;
			if ((buttonBehaviour = AttachBehaviour(thatProp, buttonName)) == null)
				return;
			buttonBehaviour.clickHandlers.Add(handlerFunction);
			if (releaseHandlerFunction != null)
				buttonBehaviour.releaseHandlers.Add(releaseHandlerFunction);
		}
	}
}

