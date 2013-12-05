using System;

namespace JSI
{
	public class JSITargetMenu: InternalModule
	{
		[KSPField]
		public string pageTitle;

		public string ShowMenu(int screenWidth, int screenHeight)
		{
			return "";
		}

		public void Start()
		{
			if (!string.IsNullOrEmpty(pageTitle))
				pageTitle = pageTitle.Replace("<=", "{").Replace("=>", "}");
		}
	}
}

