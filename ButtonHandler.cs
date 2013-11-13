using UnityEngine;
using System;

namespace JSI
{
	public class ButtonHandler:MonoBehaviour
	{
		public delegate void HandlerFunction(int ID);

		public HandlerFunction handlerFunction;
		public int ID;

		public void OnMouseDown()
		{
			handlerFunction(ID);
		}
	}
	public class ButtonHandlerSingular:MonoBehaviour
	{
		public delegate void HandlerFunction();

		public HandlerFunction handlerFunction;

		public void OnMouseDown()
		{
			handlerFunction();
		}
	}

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

