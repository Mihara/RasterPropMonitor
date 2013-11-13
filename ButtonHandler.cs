using UnityEngine;
using System;

namespace JSI
{
	public class SmarterButton: MonoBehaviour
	{
		private Action<int> handlerID;
		private Action handler;

		private int id;

		public void OnMouseDown()
		{
			if (handlerID != null)
				handlerID(id);
			else
				handler();
		}

		private static SmarterButton AttachBehaviour(InternalProp thatProp, string buttonName){
			GameObject buttonObject = thatProp.FindModelTransform(buttonName).gameObject;
			return buttonObject.AddComponent<SmarterButton>();
		}

		public static void CreateButton(InternalProp thatProp, string buttonName, int id, Action<int> handlerFunction) {
			SmarterButton buttonBehaviour = AttachBehaviour(thatProp,buttonName);
			buttonBehaviour.id = id;
			buttonBehaviour.handlerID = handlerFunction;
		}
		public static void CreateButton(InternalProp thatProp, string buttonName, Action handlerFunction) {
			SmarterButton buttonBehaviour = AttachBehaviour(thatProp,buttonName);
			buttonBehaviour.handler = handlerFunction;
		}
	}
}

