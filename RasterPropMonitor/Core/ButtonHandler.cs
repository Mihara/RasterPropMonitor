using UnityEngine;
using System;
using System.Collections.Generic;

namespace JSI
{
	public class SmarterButton: MonoBehaviour
	{
		private Action<int> handlerID;
		private Action<MonitorPage> pageSelectionHandlerFunction;
		private Action handler;
		private int id;
		private readonly List<MonitorPage> pageReferences = new List<MonitorPage>();
		private int listCounter;

		public void OnMouseDown()
		{
			if (pageReferences.Count > 0) {
				pageSelectionHandlerFunction(pageReferences[listCounter]);
				listCounter++;
				if (listCounter >= pageReferences.Count)
					listCounter = 0;
			}
			if (handlerID != null) {
				handlerID(id);
			}
			if (handler != null)
				handler();
		}

		private static SmarterButton AttachBehaviour(InternalProp thatProp, string buttonName)
		{
			GameObject buttonObject = thatProp.FindModelTransform(buttonName).gameObject;
			if (buttonObject == (UnityEngine.Object)null) {
				Debug.LogError("Button transform name not found, expect errors.");
				return null;
			} 
			SmarterButton thatComponent = buttonObject.GetComponent<SmarterButton>() ?? buttonObject.AddComponent<SmarterButton>();
			return thatComponent;
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, MonitorPage thatPage, Action<MonitorPage> handlerFunction)
		{
			SmarterButton buttonBehaviour = AttachBehaviour(thatProp, buttonName);
			buttonBehaviour.pageSelectionHandlerFunction = handlerFunction;
			buttonBehaviour.pageReferences.Add(thatPage);
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, int id, Action<int> handlerFunction)
		{
			SmarterButton buttonBehaviour = AttachBehaviour(thatProp, buttonName);
			buttonBehaviour.id = id;
			buttonBehaviour.handlerID = handlerFunction;
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, Action handlerFunction)
		{
			SmarterButton buttonBehaviour = AttachBehaviour(thatProp, buttonName);
			buttonBehaviour.handler = handlerFunction;
		}
	}
}

