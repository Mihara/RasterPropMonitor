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
		private readonly RasterPropMonitor ourMonitor;

		public MonitorPage(int idNum, ConfigNode node, RasterPropMonitor thatMonitor)
		{
			ourMonitor = thatMonitor;
			pageNumber = idNum;
			isMutable = false;
			if (!node.HasValue("text") && !node.HasValue("background") && !node.HasValue("button"))
				throw new ArgumentException("A page needs to have either text, a background or a button.");

			isDefault |= node.HasValue("default");

			if (node.HasValue("text")) {
				string pageDefinition = node.GetValue("text");

				try {
					Text = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageDefinition, System.Text.Encoding.UTF8));
					if (Text.IndexOf("$&$", StringComparison.Ordinal) != -1)
						isMutable = true;
				} catch (FileNotFoundException e) {
					// There's no file.
					Text = e.Message;
				}
				if (pageDefinition.IndexOf("$&$", StringComparison.Ordinal) != -1)
					isMutable = true;
				// But regardless of whether we found a page handler, it won't matter if we populate the page data or not.
				Text = pageDefinition.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
			}

			if (node.HasNode("PAGEHANDLER")) {
				ConfigNode pageHandlerNode = node.GetNode("PAGEHANDLER");
				if (pageHandlerNode.HasValue("name") && pageHandlerNode.HasValue("method")) {
					string moduleName = pageHandlerNode.GetValue("name");
					string methodName = pageHandlerNode.GetValue("method");

					ConfigNode handlerConfiguration = new ConfigNode("MODULE");
					pageHandlerNode.CopyTo(handlerConfiguration);
					InternalModule thatModule = thatMonitor.internalProp.AddModule(handlerConfiguration);

					foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
						if (m.Name == methodName) {
							// We'll assume whoever wrote it is not being an idiot today.
							pageHandler = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), thatModule, m);
							isMutable = true;
							break;
						}
					}
				}
			}

			if (node.HasValue("button")) {
				SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), ButtonClick);
			}

			background = BackgroundType.None;
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
					case "handler":
						break;
				}
			}
		}

		public void RenderBackground(RenderTexture screen)
		{
			switch (background) {
				case BackgroundType.Texture:
					Graphics.DrawTexture(
						new Rect(0, 0, screen.width, screen.height),
						backgroundTexture);
					return;
				case BackgroundType.Handler:
					return;
			}
			return;
		}

		public void ButtonClick()
		{
			// This method should do more, seriously.
			// Maybe I can do events?...
			ourMonitor.ButtonClick(this);
		}
	}
}

