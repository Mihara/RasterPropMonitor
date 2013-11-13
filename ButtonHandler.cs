using UnityEngine;
using System;

namespace JSI
{
	public class SmarterButton: MonoBehaviour
	{
		public Action<int> handlerID;
		public Action handler;

		public int id;

		public void OnMouseDown()
		{
			if (handlerID != null)
				handlerID(id);
			else
				handler();
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, int id, Action<int> handlerFunction) {
			GameObject buttonObject = thatProp.FindModelTransform(buttonName).gameObject;
			SmarterButton buttonBehaviour = buttonObject.AddComponent<SmarterButton>();
			buttonBehaviour.id = id;
			buttonBehaviour.handlerID = handlerFunction;
		}
		public static void CreateButton(InternalProp thatProp, string buttonName, Action handlerFunction) {
			GameObject buttonObject = thatProp.FindModelTransform(buttonName).gameObject;
			SmarterButton buttonBehaviour = buttonObject.AddComponent<SmarterButton>();
			buttonBehaviour.handler = handlerFunction;
		}
	}
}

