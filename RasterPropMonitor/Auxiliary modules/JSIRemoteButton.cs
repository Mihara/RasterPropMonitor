// Analysis disable once RedundantUsingDirective
using System;
using UnityEngine;

namespace JSI
{
	public class JSIRemoteButton: InternalModule
	{
		[KSPField]
		public string localButtonTransform;
		[KSPField]
		public string remoteButtonTransform;
		[KSPField]
		public int remoteButtonPropID;
		private SmarterButton remoteButtonBehaviour;

		public void Start()
		{

			if (internalModel.props.Count < remoteButtonPropID) {
				JUtil.LogErrorMessage(this, "There is no prop ID {0}, aborting.", remoteButtonPropID);
				return;
			}


			GameObject remoteButtonObject = internalModel.props[remoteButtonPropID].FindModelTransform(remoteButtonTransform).gameObject;
			if (remoteButtonObject == (UnityEngine.Object)null) {
				JUtil.LogErrorMessage(this, "Could not find a remote button transform named '{0}' in prop ID {1}, aborting.", remoteButtonTransform, remoteButtonPropID);
				return;
			}

			if ((remoteButtonBehaviour = remoteButtonObject.GetComponent<SmarterButton>()) == null) {
				JUtil.LogErrorMessage(this, "Transform named '{0}' in prop ID {1} is not an RPM button, aborting.", remoteButtonTransform, remoteButtonPropID);
				return;
			}

			SmarterButton.CreateButton(internalProp, localButtonTransform, RedirectButtonDown, RedirectButtonUp);
		}

		public void RedirectButtonDown()
		{
			remoteButtonBehaviour.OnMouseDown();
		}

		public void RedirectButtonUp()
		{
			remoteButtonBehaviour.OnMouseUp();
		}
	}
}

