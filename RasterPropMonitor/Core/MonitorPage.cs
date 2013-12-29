using System;
using System.Reflection;
using UnityEngine;
using System.Collections.Generic;

namespace JSI
{
	public class MonitorPage
	{
		// We still need a numeric ID cause it makes persistence easier.
		public readonly int pageNumber;
		public readonly string name = string.Empty;
		public readonly bool Unlocker;
		private readonly string text;

		public string Text {
			get {
				return pageHandler != null ? pageHandler(screenWidth, screenHeight) : string.IsNullOrEmpty(text) ? string.Empty : text;
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
			Handler,
		};

		private readonly BackgroundType background = BackgroundType.None;
		private readonly float cameraFOV;
		private readonly string camera;
		private readonly FlyingCamera cameraObject;
		private const float defaultFOV = 60f;
		private readonly Texture2D backgroundTexture;
		public readonly Texture2D overlayTexture, interlayTexture;
		private readonly Func<int,int,string> pageHandler;
		private readonly Func<RenderTexture,float,bool> backgroundHandler;
		private readonly HandlerSupportMethods pageHandlerS, backgroundHandlerS;
		private readonly RasterPropMonitor ourMonitor;
		private readonly int screenWidth, screenHeight;
		private readonly float cameraAspect;
		private readonly int zoomUpButton, zoomDownButton;
		private readonly float maxFOV, minFOV;
		private readonly int zoomSteps;
		private readonly float zoomSkip;
		private int currentZoom;
		private readonly bool showNoSignal;
		private readonly bool simpleLockingPage;
		private readonly List<string> disableSwitchingTo = new List<string>();
		private readonly DefaultableDictionary<string,string> redirectPages = new DefaultableDictionary<string,string>(string.Empty);
		private readonly DefaultableDictionary<int,int?> redirectGlobals = new DefaultableDictionary<int, int?>(null);

		private struct HandlerSupportMethods
		{
			public Action <bool,int> activate;
			public Action <int> buttonClick;
			public Action <int> buttonRelease;
		}

		public bool SwitchingPermitted(string destination)
		{
			return !simpleLockingPage && !disableSwitchingTo.Contains(destination);
		}

		public string ContextRedirect(string destination)
		{
			return redirectPages[destination];
		}

		private static bool IsValidPageName(string thatName)
		{
			char[] illegalChars = { ' ', ',', '#', '=' };
			foreach (char thatChar in illegalChars) {
				if (thatName.IndexOf(thatChar) != -1)
					return false;
			}
			return true;
		}

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

			if (node.HasValue("name")) {
				string value = node.GetValue("name").Trim();
				if (!IsValidPageName(value)) {
					JUtil.LogMessage(ourMonitor, "Warning, name given for page #{0} is invalid, ignoring.", pageNumber);
				} else
					name = value;
			} else
				JUtil.LogMessage(ourMonitor, "Warning, page #{0} has no name. It's much better if it does.", pageNumber);

			isDefault |= node.HasValue("default");

			if (node.HasValue("button"))
				SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), this, thatMonitor.PageButtonClick);

			// Page locking system -- simple locking:
			simpleLockingPage |= node.HasValue("lockingPage");
			// and name-based locking.
			if (node.HasValue("disableSwitchingTo")) {
				string[] tokens = node.GetValue("disableSwitchingTo").Split(',');
				foreach (string token in tokens) {
					disableSwitchingTo.Add(token.Trim());
				}
			}

			Unlocker |= node.HasValue("unlockerPage");

			if (node.HasNode("CONTEXTREDIRECT")) {
				foreach (string content in node.GetNode("CONTEXTREDIRECT").GetValues("redirect")) {
					string[] tokens = content.Split(',');
					if (tokens.Length > 2 || !IsValidPageName(tokens[0].Trim()) || !IsValidPageName(tokens[1].Trim())) {
						JUtil.LogMessage(ourMonitor, "Warning, invalid page redirect statement on page #{0}.", pageNumber);
						continue;
					}
					redirectPages[tokens[0].Trim()] = tokens[1].Trim();
				}
				const string valueError = "Warning, invalid global button redirect statement on page #{0}: {1}";
				foreach (string content in node.GetNode("CONTEXTREDIRECT").GetValues("renumber")) {
					string[] tokens = content.Split(',');
					if (tokens.Length > 2) {
						JUtil.LogMessage(ourMonitor, valueError, pageNumber, "requires two arguments.");
						continue;
					}
					int from, to;
					if (!int.TryParse(tokens[0], out from) || !int.TryParse(tokens[1], out to)) {
						JUtil.LogMessage(ourMonitor, valueError, pageNumber, "something isn't a number.");
						continue;
					}
					redirectGlobals[from] = to;
				}
				JUtil.LogMessage(this, "Page '{2}' (#{0}) registers {1} page redirects and {3} global button redirects.", idNum, redirectPages.Count, name, redirectGlobals.Count);
			}

			foreach (ConfigNode handlerNode in node.GetNodes("PAGEHANDLER")) {
				MonoBehaviour handlerModule;
				HandlerSupportMethods supportMethods;
				MethodInfo handlerMethod = InstantiateHandler(handlerNode, ourMonitor, out handlerModule, out supportMethods);
				if (handlerMethod != null && handlerModule != null) {
					try {
						pageHandler = (Func<int,int,string>)Delegate.CreateDelegate(typeof(Func<int,int,string>), handlerModule, handlerMethod);
					} catch {
						JUtil.LogErrorMessage(ourMonitor, "Incorrect signature for the page handler method {0}", handlerModule.name);
						break;
					}
					pageHandlerS = supportMethods;
					isMutable = true;
					break;
				}
			} 

			if (pageHandler == null)
			if (node.HasValue("text")) {
				text = JUtil.LoadPageDefinition(node.GetValue("text"));
				isMutable |= text.IndexOf("$&$", StringComparison.Ordinal) != -1;
			}

			foreach (ConfigNode handlerNode in node.GetNodes("BACKGROUNDHANDLER")) {
				MonoBehaviour handlerModule;
				HandlerSupportMethods supportMethods;
				MethodInfo handlerMethod = InstantiateHandler(handlerNode, ourMonitor, out handlerModule, out supportMethods);
				if (handlerMethod != null && handlerModule != null) {
					try {
						backgroundHandler = (Func<RenderTexture,float,bool>)Delegate.CreateDelegate(typeof(Func<RenderTexture,float,bool>), handlerModule, handlerMethod);
					} catch {
						JUtil.LogErrorMessage(ourMonitor, "Incorrect signature for the background handler method {0}", handlerModule.name);
						break;
					}
					backgroundHandlerS = supportMethods;
					isMutable = true;
					showNoSignal = node.HasValue("showNoSignal");
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
						} else
							JUtil.LogMessage(ourMonitor, "Ignored invalid camera zoom settings on page {0}.", pageNumber);
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
			if (node.HasValue("textureInterlayURL")) {
				string textureURL = node.GetValue("textureInterlayURL").EnforceSlashes();
				if (GameDatabase.Instance.ExistsTexture(textureURL)) {
					interlayTexture = GameDatabase.Instance.GetTexture(textureURL, false);
				} else
					JUtil.LogErrorMessage(ourMonitor, "Interlay texture could not be loaded.");
			}
			if (node.HasValue("textureOverlayURL")) {
				string textureURL = node.GetValue("textureOverlayURL").EnforceSlashes();
				if (GameDatabase.Instance.ExistsTexture(textureURL)) {
					overlayTexture = GameDatabase.Instance.GetTexture(textureURL, false);
				} else
					JUtil.LogErrorMessage(ourMonitor, "Overlay texture could not be loaded.");
			}

		}

		private static MethodInfo InstantiateHandler(ConfigNode node, RasterPropMonitor ourMonitor, out MonoBehaviour moduleInstance, out HandlerSupportMethods support)
		{
			moduleInstance = null;
			support.activate = null;
			support.buttonClick = null;
			support.buttonRelease = null;
			if (node.HasValue("name") && node.HasValue("method")) {
				string moduleName = node.GetValue("name");
				string methodName = node.GetValue("method");

				var handlerConfiguration = new ConfigNode("MODULE");
				node.CopyTo(handlerConfiguration);

				MonoBehaviour thatModule = null;
				// Part modules are different in that they remain instantiated when you switch vessels, while the IVA doesn't.
				// Because of this RPM can't instantiate partmodule-based handlers itself -- there's no way to tell if this was done already or not.
				// Which means there can only be one instance of such a handler per pod, and it can't receive configuration values from RPM.
				if (node.HasValue("isPartModule")) {
					foreach (PartModule potentialModule in ourMonitor.part.Modules) {
						if (potentialModule.ClassName == moduleName) {
							thatModule = potentialModule;
							break;
						}
					}
				} else if (node.HasValue("multiHandler")) {

					foreach (InternalModule potentialModule in ourMonitor.internalProp.internalModules)
						if (potentialModule.ClassName == moduleName) {
							thatModule = potentialModule;
							break;
						}

				}

				if (thatModule == null && !node.HasValue("isPartModule"))
					thatModule = ourMonitor.internalProp.AddModule(handlerConfiguration);

				if (thatModule == null) {
					JUtil.LogMessage(ourMonitor, "Warning, handler module \"{0}\" could not be loaded. This could be perfectly normal.", moduleName);
					return null;
				}

				const string sigError = "Incorrect signature of the {0} method in {1}, ignoring option. If it doesn't work later, that's why.";
					
				if (node.HasValue("pageActiveMethod"))
					foreach (MethodInfo m in thatModule.GetType().GetMethods())
						if (m.Name == node.GetValue("pageActiveMethod")) {
							try {
								support.activate = (Action<bool,int>)Delegate.CreateDelegate(typeof(Action<bool,int>), thatModule, m);
							} catch {
								JUtil.LogMessage(ourMonitor, sigError, "page activation", moduleName);
							}
							break;
						}

				if (node.HasValue("buttonClickMethod"))
					foreach (MethodInfo m in thatModule.GetType().GetMethods())
						if (m.Name == node.GetValue("buttonClickMethod")) {
							try {
								support.buttonClick = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), thatModule, m);
							} catch {
								JUtil.LogMessage(ourMonitor, sigError, "button click", moduleName);
							}
							break;
						}

				if (node.HasValue("buttonReleaseMethod"))
					foreach (MethodInfo m in thatModule.GetType().GetMethods())
						if (m.Name == node.GetValue("buttonReleaseMethod")) {
							try {
								support.buttonRelease = (Action<int>)Delegate.CreateDelegate(typeof(Action<int>), thatModule, m);
							} catch {
								JUtil.LogMessage(ourMonitor, sigError, "button release", moduleName);
							}
							break;
						}

				moduleInstance = thatModule;
				foreach (MethodInfo m in thatModule.GetType().GetMethods())
					if (m.Name == methodName)
						return m;

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
			if (pageHandlerS.activate != null)
				pageHandlerS.activate(state, pageNumber);
			if (backgroundHandlerS.activate != null && backgroundHandlerS.activate != pageHandlerS.activate)
				backgroundHandlerS.activate(state, pageNumber);
		}

		public bool GlobalButtonClick(int buttonID)
		{
			buttonID = redirectGlobals[buttonID] ?? buttonID;
			if (buttonID == -1)
				return false;
			bool actionTaken = false;
			if (pageHandlerS.buttonClick != null) {
				pageHandlerS.buttonClick(buttonID);
				actionTaken = true;
			}
			if (backgroundHandlerS.buttonClick != null && pageHandlerS.buttonClick != backgroundHandlerS.buttonClick) {
				backgroundHandlerS.buttonClick(buttonID);
				actionTaken = true;
			} else if (zoomSteps > 0) {
				actionTaken = true;
				if (buttonID == zoomUpButton)
					currentZoom--;
				if (buttonID == zoomDownButton)
					currentZoom++;
				if (currentZoom < 0)
					currentZoom = 0;
				if (currentZoom > zoomSteps)
					currentZoom = zoomSteps;
				cameraObject.FOV = ComputeFOV();
			}
			return actionTaken;
		}

		public bool GlobalButtonRelease(int buttonID)
		{
			buttonID = redirectGlobals[buttonID] ?? buttonID;
			if (buttonID == -1)
				return false;

			bool actionTaken = false;
			if (pageHandlerS.buttonRelease != null) {
				pageHandlerS.buttonRelease(buttonID);
				actionTaken = true;
			}
			if (backgroundHandlerS.buttonRelease != null && backgroundHandlerS.buttonRelease != pageHandlerS.buttonRelease) {
				actionTaken = true;
				backgroundHandlerS.buttonRelease(buttonID);
			}
			return actionTaken;
		}

		public void RenderBackground(RenderTexture screen)
		{
			switch (background) {
				case BackgroundType.None:
					GL.Clear(true, true, ourMonitor.emptyColorValue);
					break;
				case BackgroundType.Camera:
					if (!cameraObject.Render()) {
						if (ourMonitor.noSignalTexture != null)
							Graphics.Blit(ourMonitor.noSignalTexture, screen);
						else
							GL.Clear(true, true, ourMonitor.emptyColorValue);
					}
					break;
				case BackgroundType.Texture:
					Graphics.Blit(backgroundTexture, screen);
					break;
				case BackgroundType.Handler:
					if (!backgroundHandler(screen, cameraAspect)) {
						if (ourMonitor.noSignalTexture != null && showNoSignal)
							Graphics.Blit(ourMonitor.noSignalTexture, screen);
						else
							GL.Clear(true, true, ourMonitor.emptyColorValue);
					}
					break;
			}
		}
	}
}

