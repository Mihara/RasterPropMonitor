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
				if (pageHandler != null)
					return pageHandler();
				return text;
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

		private readonly Func<string> pageHandler;
		private readonly RasterPropMonitor ourMonitor;

		public MonitorPage(int idNum, ConfigNode node, RasterPropMonitor thatMonitor)
		{
			ourMonitor = thatMonitor;
			pageNumber = idNum;
			isMutable = false;
			if (!node.HasValue("text") && !node.HasValue("background") && !node.HasValue("button"))
				throw new ArgumentException("A page needs to have either text, a background or a button.");

			if (node.HasValue("default"))
				isDefault = true;

			if (node.HasValue("text")) {
				string pageDefinition = node.GetValue("text");

				try {
					Text = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageDefinition, System.Text.Encoding.UTF8));
					if (Text.IndexOf("$&$", StringComparison.Ordinal) != -1)
						isMutable = true;
				} catch (FileNotFoundException e) {
					// There's no file.
					Text = e.Message;

					// Now we check for a page handler.
					string[] tokens = pageDefinition.Split(',');
					if (tokens.Length == 2) {
						foreach (InternalModule thatModule in thatMonitor.internalProp.internalModules) {
							if (thatModule.ClassName == tokens[0].Trim()) {
								foreach (MethodInfo m in thatModule.GetType().GetMethods()) {
									if (m.Name == tokens[1].Trim()) {
										// We'll assume whoever wrote it is not being an idiot today.
										pageHandler = (Func<string>)Delegate.CreateDelegate(typeof(Func<string>), thatModule, m);

										isMutable = true;
										break;
									}
								}
								break;
							}
						}
					}
					if (pageDefinition.IndexOf("$&$", StringComparison.Ordinal) != -1)
						isMutable = true;
					// But regardless of whether we found a page handler, it won't matter if we populate the page data or not.
					Text = pageDefinition.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
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
								float.TryParse(node.GetValue("fov"), out fov);
								if (fov == 0)
									cameraFOV = defaultFOV;
								else
									cameraFOV = fov;
							} else
								cameraFOV = defaultFOV;
							
						}
						break;
				}
			}
		}

		public void ButtonClick()
		{
			// This method should do more, seriously.
			// Maybe I can do events?...
			ourMonitor.ButtonClick(this);
		}
	}
}

