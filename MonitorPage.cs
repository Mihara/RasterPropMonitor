using System;
using System.Reflection;
using System.IO;
using UnityEngine;

namespace JSI
{
	public class MonitorPage
	{
		// We still need a numeric ID cause it makes persistence easier.
		public int PageNumber { get; private set; }

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



		public bool IsDefault { get; private set; }

		public string Camera { get; private set; }

		private readonly Func<string> pageHandler;

		private readonly RasterPropMonitor ourMonitor;

		public MonitorPage(int idNum, ConfigNode node, RasterPropMonitor thatMonitor)
		{
			ourMonitor = thatMonitor;
			PageNumber = idNum;
			if (!node.HasValue("text") && !node.HasValue("camera"))
				throw new ArgumentException("A page needs either text or camera.");

			if (node.HasValue("default"))
				IsDefault = true;

			if (node.HasValue("text")) {
				string pageDefinition = node.GetValue("text");

				try {
					Text = String.Join(Environment.NewLine, File.ReadAllLines(KSPUtil.ApplicationRootPath + "GameData/" + pageDefinition, System.Text.Encoding.UTF8));
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
										break;
									}
								}
								break;
							}
						}
					}

					// But regardless of whether we found a page handler, it won't matter if we populate the page data or not.
					Text = pageDefinition.Replace("<=", "{").Replace("=>", "}").Replace("$$$", Environment.NewLine);
				}
			}

			if (node.HasValue("button")) {
				SmarterButton.CreateButton(thatMonitor.internalProp, node.GetValue("button"), ButtonClick);
			}
			if (node.HasValue("camera")) {
				Camera = node.GetValue("camera");
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

