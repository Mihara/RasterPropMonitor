using System;
using System.Reflection;
using UnityEngine;

namespace JSI
{
	public class MonitorPage
	{
		// We still need a numeric ID cause it makes persistence easier.
		public int pageNumber;
		private string text;

		public string Text {
			get {
				return pageHandler != null ? pageHandler(screenWidth, screenHeight) : string.IsNullOrEmpty(text) ? string.Empty : text;
			}
			private set {
				text = value;

			}
		}

		public bool isDefault;
		// A page is immutable if and only if it has only unchanging text and unchanging background and no handlers.
		public bool isMutable;

		private enum BackgroundType
		{
			None,
			Camera,
			Texture,
			Handler}
		;

		private readonly BackgroundType background = BackgroundType.None;
		private readonly float cameraFOV;
		private readonly string camera;
		private readonly FlyingCamera cameraObject;
		private const float defaultFOV = 60f;
		private readonly Texture2D backgroundTexture;
		private readonly Func<int,int,string> pageHandler;
		private readonly Func<RenderTexture,float,bool> backgroundHandler;
		private readonly Action<bool,int> pageHandlerActivate;
		private readonly Action<bool,int> backgroundHandlerActivate;
		private readonly Action<int> pageHandlerButtonClick;
		private readonly Action<int> backgroundHandlerButtonClick;
		private readonly RasterPropMonitor ourMonitor;
		private int screenWidth, screenHeight;
		private readonly float cameraAspect;
		private readonly int zoomUpButton, zoomDownButton;
		private readonly float maxFOV, minFOV;
		private readonly int zoomSteps;
		private readonly float zoomSkip;
		private int currentZoom;

		public MonitorPage(int idNum, ConfigNode node, RasterPropMonitor thatMonitor)
		{
			ourMonitor = thatMonitor;
			screenWidth = ourMonitor.screenWidth;
			screenHeight = ourMonitor.screenHeight;
			cameraAspect = ourMonitor.cameraAspect;
			cameraObject = thatMonitor.CameraStructure;

			pageNumber = idNum;
			isMutable = false;
			if (!node.HasData)
				throw new ArgumentException("Empty page?");

			isDefault |= node.HasValue("default");

			if (node.HasValue("button")) {
				SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), this, thatMonitor.PageButtonClick);
			}


			foreach (ConfigNode handlerNode in node.GetNodes("PAGEHANDLER")) {
				InternalModule handlerModule;
				MethodInfo handlerMethod = InstantiateHandler(handlerNode, ourMonitor, out handlerModule, out pageHandlerActivate, out pageHandlerButtonClick);
				if (handlerMethod != null && handlerModule != null) {
					pageHandler = (Func<int,int,string>)Delegate.CreateDelegate(typeof(Func<int,int,string>), handlerModule, handlerMethod);
					isMutable = true;
					break;
				}
			} 
			if (pageHandler == null) {
				if (node.HasValue("text")) {
					Text = JUtil.LoadPageDefinition(node.GetValue("text"));
					isMutable |= Text.IndexOf("$&$", StringComparison.Ordinal) != -1;
				}
			}


			foreach (ConfigNode handlerNode in node.GetNodes("BACKGROUNDHANDLER")) {
				InternalModule handlerModule;
				MethodInfo handlerMethod = InstantiateHandler(handlerNode, ourMonitor, out handlerModule, out backgroundHandlerActivate, out backgroundHandlerButtonClick);
				if (handlerMethod != null && handlerModule != null) {
					backgroundHandler = (Func<RenderTexture,float,bool>)Delegate.CreateDelegate(typeof(Func<RenderTexture,float,bool>), handlerModule, handlerMethod);
					isMutable = true;
					background = BackgroundType.Handler;
					break;
				}
			}

			if (background == BackgroundType.None) {
				if (node.HasValue("cameraTransform")) {
					isMutable = true;
					background = BackgroundType.Camera;
					camera = node.GetValue("cameraTransform");
					cameraFOV = defaultFOV;
					if (node.HasValue("fov")) {
						float fov;
						cameraFOV = float.TryParse(node.GetValue("fov"), out fov) ? fov : defaultFOV;
					} else if (node.HasValue("zoomFov") && node.HasValue("zoomButtons")) {
						Vector3 zoomFov = ConfigNode.ParseVector3(node.GetValue("zoomFov"));
						Vector2 zoomButtons = ConfigNode.ParseVector2(node.GetValue("zoomButtons"));
						if ((int)zoomFov.z != 0 && ((int)zoomButtons.x != (int)zoomButtons.y)) {
							maxFOV = Math.Max(zoomFov.x, zoomFov.y);
							minFOV = Math.Min(zoomFov.x, zoomFov.y);
							zoomSteps = (int)zoomFov.z;
							zoomUpButton = (int)zoomButtons.x;
							zoomDownButton = (int)zoomButtons.y;
							zoomSkip = (maxFOV - minFOV) / zoomSteps;
							currentZoom = 0;
							cameraFOV = maxFOV;
						} else {
							JUtil.LogMessage(ourMonitor, "Ignored invalid camera zoom settings on page {0}.", pageNumber);
						}
					}
				} 
			}
			if (background == BackgroundType.None) {
				if (node.HasValue("textureURL")) {
					string textureURL = node.GetValue("textureURL").EnforceSlashes();
					if (GameDatabase.Instance.ExistsTexture(textureURL)) {
						backgroundTexture = GameDatabase.Instance.GetTexture(textureURL, false);
						background = BackgroundType.Texture;
					}
				}
			}
		}

		private static MethodInfo InstantiateHandler(ConfigNode node, RasterPropMonitor ourMonitor, out InternalModule moduleInstance, out Action<bool,int> activationMethod, out Action<int> buttonClickMethod)
		{
			moduleInstance = null;
			activationMethod = null;
			buttonClickMethod = null;
			if (node.HasValue("name") && node.HasValue("method")) {
				string moduleName = node.GetValue("name");
				string methodName = node.GetValue("method");

				var handlerConfiguration = new ConfigNode("MODULE");
				node.CopyTo(handlerConfiguration);

				InternalModule thatModule = null;
				if (node.HasValue("multiHandler")) {
					foreach (InternalModule potentialModule in ourMonitor.internalProp.internalModules) {
						if (potentialModule.ClassName == moduleName) {
							thatModule = potentialModule;
							break;
						}
					}
				}
				if (thatModule == null)
					thatModule = ourMonitor.internalProp.AddModule(handlerConfiguration);

				if (thatModule == null) {
					JUtil.LogMessage(ourMonitor, "Warning, handler module \"{0}\" did not load. This could be perfectly normal.", moduleName);
					return null;
				}
					

				if (node.HasValue("pageActiveMethod")) {
					foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
						if (m.Name == node.GetValue("pageActiveMethod")) {
							activationMethod = (Action<bool,int>)Delegate.CreateDelegate(typeof(Action<bool,int>), thatModule, m);
						}
					}
				}

				if (node.HasValue("buttonClickMethod")) {
					foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
						if (m.Name == node.GetValue("buttonClickMethod")) {
							buttonClickMethod = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), thatModule, m);
						}
					}
				}

				moduleInstance = thatModule;
				foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
					if (m.Name == methodName) {
						return m;
					}
				}

			}
			return null;
		}

		private float ComputeFOV()
		{
			if (zoomSteps == 0)
				return cameraFOV;
			return maxFOV - zoomSkip * currentZoom;
		}

		public void Active(bool state)
		{
			if (state)
				cameraObject.PointCamera(camera, ComputeFOV());
			if (pageHandlerActivate != null)
				pageHandlerActivate(state, pageNumber);
			if (backgroundHandlerActivate != null && backgroundHandlerActivate != pageHandlerActivate)
				backgroundHandlerActivate(state, pageNumber);
		}

		public void GlobalButtonClick(int buttonID)
		{
			if (pageHandlerButtonClick != null)
				pageHandlerButtonClick(buttonID);
			if (backgroundHandlerButtonClick != null && backgroundHandlerButtonClick != pageHandlerButtonClick)
				backgroundHandlerButtonClick(buttonID);
			else if (zoomSteps > 0) {
				if (buttonID == zoomUpButton) {
					currentZoom--;
				}
				if (buttonID == zoomDownButton) {
					currentZoom++;
				}
				if (currentZoom < 0)
					currentZoom = 0;
				if (currentZoom > zoomSteps)
					currentZoom = zoomSteps;
				cameraObject.FOV = ComputeFOV();
			}
		}

		public void RenderBackground(RenderTexture screen)
		{
			switch (background) {
				case BackgroundType.None:
					GL.Clear(true, true, ourMonitor.emptyColor);
					break;
				case BackgroundType.Camera:
					if (!cameraObject.Render()) {
						if (ourMonitor.noSignalTexture != null)
							Graphics.Blit(ourMonitor.noSignalTexture, screen);
						else
							GL.Clear(true, true, ourMonitor.emptyColor);
					}
					break;
				case BackgroundType.Texture:
					Graphics.Blit(backgroundTexture, screen);
					break;
				case BackgroundType.Handler:
					if (!backgroundHandler(screen, cameraAspect))
						GL.Clear(true, true, ourMonitor.emptyColor);
					break;
			}
		}
	}
}

