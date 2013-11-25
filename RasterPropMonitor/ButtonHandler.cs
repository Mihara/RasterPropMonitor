using UnityEngine;
using System;

namespace JSI
{
	public class SmarterButton: MonoBehaviour
	{
		private Action<int> handlerID;
		private Action<MonitorPage> handlerPageReference;
		private Action handler;
		private int id;
		private MonitorPage pageReference;

		public void OnMouseDown()
		{
			if (handlerPageReference != null) {
				handlerPageReference(pageReference);
				return;
			}
			if (handlerID != null)
				handlerID(id);
			else
				handler();
		}

		private static SmarterButton AttachBehaviour(InternalProp thatProp, string buttonName)
		{
			GameObject buttonObject = thatProp.FindModelTransform(buttonName).gameObject;
			if (buttonObject == (UnityEngine.Object)null) {
				Debug.LogError("Button transform name not found, expect errors.");
				return null;
			} 
			return buttonObject.AddComponent<SmarterButton>();
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, MonitorPage thatPage, Action<MonitorPage> handlerFunction)
		{
			SmarterButton buttonBehaviour = AttachBehaviour(thatProp, buttonName);
			buttonBehaviour.pageReference = thatPage;
			buttonBehaviour.handlerPageReference = handlerFunction;
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

