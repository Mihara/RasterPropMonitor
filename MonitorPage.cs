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

		public BackgroundType background;
		public string camera;
		public float cameraFOV;
		private const float defaultFOV = 60f;
		private readonly Texture2D backgroundTexture;
		private readonly Func<string> pageHandler;
		private readonly Func<RenderTexture,bool> backgroundHandler;
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
				SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), ButtonClick);
			}


			if (node.HasNode("PAGEHANDLER")) {
				InternalModule handlerModule;
				MethodInfo handlerMethod = InstantiateHandler(node.GetNode("PAGEHANDLER"), out handlerModule);
				if (handlerMethod != null && handlerModule != null) {
					pageHandler = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), handlerModule, handlerMethod);
					isMutable = true;
				}
			} else {
				if (node.HasValue("text")) {
					string pageDefinition = node.GetValue("text");

					try {
						Text = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageDefinition, System.Text.Encoding.UTF8));
						if (Text.IndexOf("$&$", StringComparison.Ordinal) != -1)
							isMutable = true;
					} catch (FileNotFoundException e) {
						// There's no file.
						Debug.Log("There's no file named " + e.Message + ", assuming direct definition.");
						if (pageDefinition.IndexOf("$&$", StringComparison.Ordinal) != -1)
							isMutable = true;
						Text = pageDefinition.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
					}
				}
			}


			background = BackgroundType.None;
			if (node.HasNode("BACKGROUNDHANDLER")) {
				InternalModule handlerModule;
				MethodInfo handlerMethod = InstantiateHandler(node.GetNode("BACKGROUNDHANDLER"), out handlerModule);
				if (handlerMethod != null && handlerModule != null) {
					backgroundHandler = (Func<RenderTexture,bool>)Delegate.CreateDelegate(typeof(Func<RenderTexture,bool>), handlerModule, handlerMethod);
					isMutable = true;
					background = BackgroundType.Handler;
				}
			} else {
				if (node.HasValue("background")) {
					switch (node.GetValue("background")) {
						case "camera":
							if (node.HasValue("cameraTransform")) {
								isMutable = true;
								background = BackgroundType.Camera;
								camera = node.GetValue("cameraTransform");
								if (node.HasValue("fov")) {
									float fov;
									cameraFOV = float.TryParse(node.GetValue("fov"), out fov) ? fov : defaultFOV;
								} else
									cameraFOV = defaultFOV;
							
							}
							break;
						case "texture":
							if (node.HasValue("textureURL")) {
								string textureURL = node.GetValue("textureURL");
								if (GameDatabase.Instance.ExistsTexture(textureURL)) {
									backgroundTexture = GameDatabase.Instance.GetTexture(textureURL, false);
									background = BackgroundType.Texture;
								}
							}
							break;
					}
				}
			}
		}

		private MethodInfo InstantiateHandler(ConfigNode node, out InternalModule moduleInstance)
		{
			moduleInstance = null;
			if (node.HasValue("name") && node.HasValue("method")) {
				string moduleName = node.GetValue("name");
				string methodName = node.GetValue("method");

				ConfigNode handlerConfiguration = new ConfigNode("MODULE");
				node.CopyTo(handlerConfiguration);
				InternalModule thatModule = ourMonitor.internalProp.AddModule(handlerConfiguration);

				if (thatModule == null)
					return null;
				moduleInstance = thatModule;
				foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
					if (m.Name == methodName) {
						return m;
					}
				}

			}
			return null;
		}

		public bool RenderBackground(RenderTexture screen)
		{
			switch (background) {
				case BackgroundType.Texture:
					Graphics.DrawTexture(
						new Rect(0, 0, screen.width, screen.height),
						backgroundTexture);
					return true;
				case BackgroundType.Handler:
					return backgroundHandler(screen);
			}
			return false;
		}

		public void ButtonClick()
		{
			// This method should do more, seriously.
			// Maybe I can do events?...
			ourMonitor.ButtonClick(this);
		}
	}
}

