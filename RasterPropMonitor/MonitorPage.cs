using System;
using System.Reflection;
using System.IO;
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
				return pageHandler != null ? pageHandler() : text;
			}
			private set {
				text = value;

			}
		}

		public bool isDefault;
		// A page is immutable if and only if it has only unchanging text and unchanging background and no handlers.
		public bool isMutable;

		public enum BackgroundType
		{
			None,
			Camera,
			Texture,
			Handler}
		;

		public BackgroundType background = BackgroundType.None;
		public string camera;
		public float cameraFOV;
		private const float defaultFOV = 60f;
		private readonly Texture2D backgroundTexture;
		private readonly Func<string> pageHandler;
		private readonly Func<RenderTexture,bool> backgroundHandler;
		private readonly Action<bool> pageHandlerActivate;
		private readonly Action<bool> backgroundHandlerActivate;
		private readonly Action<int> pageHandlerButtonClick;
		private readonly Action<int> backgroundHandlerButtonClick;
		private readonly RasterPropMonitor ourMonitor;

		public MonitorPage(int idNum, ConfigNode node, RasterPropMonitor thatMonitor)
		{
			ourMonitor = thatMonitor;
			pageNumber = idNum;
			isMutable = false;
			if (!node.HasData)
				throw new ArgumentException("Empty page?");

			isDefault |= node.HasValue("default");

			if (node.HasValue("button")) {
				SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), PageButtonClick);
			}


			if (node.HasNode("PAGEHANDLER")) {
				InternalModule handlerModule;
				MethodInfo handlerMethod = InstantiateHandler(node.GetNode("PAGEHANDLER"), ourMonitor, out handlerModule, out pageHandlerActivate, out pageHandlerButtonClick);
				if (handlerMethod != null && handlerModule != null) {
					pageHandler = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), handlerModule, handlerMethod);
					isMutable = true;
				}
			} else {
				if (node.HasValue("text")) {
					string pageDefinition = node.GetValue("text");

					try {
						Text = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageDefinition, System.Text.Encoding.UTF8));
						isMutable |= Text.IndexOf("$&$", StringComparison.Ordinal) != -1;
					} catch {
						// There's no file. Probably.
						isMutable |= pageDefinition.IndexOf("$&$", StringComparison.Ordinal) != -1;
						Text = pageDefinition.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
					}
				}
			}


			if (node.HasNode("BACKGROUNDHANDLER")) {
				InternalModule handlerModule;
				MethodInfo handlerMethod = InstantiateHandler(node.GetNode("BACKGROUNDHANDLER"), ourMonitor, out handlerModule, out backgroundHandlerActivate, out backgroundHandlerButtonClick);
				if (handlerMethod != null && handlerModule != null) {
					backgroundHandler = (Func<RenderTexture,bool>)Delegate.CreateDelegate(typeof(Func<RenderTexture,bool>), handlerModule, handlerMethod);
					isMutable = true;
					background = BackgroundType.Handler;
				}
			} else {
				if (node.HasValue("cameraTransform")) {
					isMutable = true;
					background = BackgroundType.Camera;
					camera = node.GetValue("cameraTransform");
					cameraFOV = defaultFOV;
					if (node.HasValue("fov")) {
						float fov;
						cameraFOV = float.TryParse(node.GetValue("fov"), out fov) ? fov : defaultFOV;
					}
				} else {
					if (node.HasValue("textureURL")) {
						string textureURL = node.GetValue("textureURL");
						if (GameDatabase.Instance.ExistsTexture(textureURL)) {
							backgroundTexture = GameDatabase.Instance.GetTexture(textureURL, false);
							background = BackgroundType.Texture;
						}
					}
				}
			}

		}

		private static MethodInfo InstantiateHandler(ConfigNode node, InternalModule ourMonitor, out InternalModule moduleInstance, out Action<bool> activationMethod, out Action<int> buttonClickMethod)
		{
			moduleInstance = null;
			activationMethod = null;
			buttonClickMethod = null;
			if (node.HasValue("name") && node.HasValue("method")) {
				string moduleName = node.GetValue("name");
				string methodName = node.GetValue("method");

				var handlerConfiguration = new ConfigNode("MODULE");
				node.CopyTo(handlerConfiguration);
				InternalModule thatModule = ourMonitor.internalProp.AddModule(handlerConfiguration);

				if (thatModule == null)
					return null;

				if (node.HasValue("pageActiveMethod")) {
					foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
						if (m.Name == node.GetValue("pageActiveMethod")) {
							activationMethod = (Action<bool>)Delegate.CreateDelegate(typeof(Action<bool>), thatModule, m);
							;
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

		public void Active(bool state)
		{
			if (pageHandlerActivate != null)
				pageHandlerActivate(state);
			if (backgroundHandlerActivate != null)
				backgroundHandlerActivate(state);
		}

		public void GlobalButtonClick(int buttonID)
		{
			if (pageHandlerButtonClick != null)
				pageHandlerButtonClick(buttonID);
			if (backgroundHandlerButtonClick != null)
				backgroundHandlerButtonClick(buttonID);
		}

		public bool RenderBackground(RenderTexture screen)
		{
			switch (background) {
				case BackgroundType.Texture:
					Graphics.Blit(backgroundTexture, screen);
					return true;
				case BackgroundType.Handler:
					return backgroundHandler(screen);
			}
			return false;
		}

		public void PageButtonClick()
		{
			// This method should do more, seriously.
			// Maybe I can do events?...
			ourMonitor.PageButtonClick(this);
		}
	}
}

