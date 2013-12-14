using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace JSI
{
	public class TextMenu:List<TextMenuItem>
	{
		public int currentSelection;
		public string labelColor = JUtil.ColorToColorTag(Color.white);
		public string rightTextColor = JUtil.ColorToColorTag(Color.cyan);
		public string selectedColor = JUtil.ColorToColorTag(Color.green);
		public string disabledColor = JUtil.ColorToColorTag(Color.gray);
		public string menuTitle = string.Empty;
		public int rightColumnWidth;

		public string ShowMenu(int width, int height)
		{
			var strings = new List<string>(height);
			if (!string.IsNullOrEmpty(menuTitle)) {
				strings.Add(menuTitle);
				--height;
			}

			// figure out which entries are visible.
			int numEntries = Count;
			// Sanity check: clamp the current selection
			currentSelection = Math.Min(currentSelection, numEntries - 1);

			// Pick the half-way point of the list
			int midPoint = height >> 1;

			int firstPoint;
			if (midPoint > currentSelection) {
				// Menu entry is near the top of the list
				firstPoint = 0;
			} else if ((currentSelection + height - midPoint) >= numEntries) {
				// Menu entry is near the end of the list.  Account for short
				// lists by clamping to zero.
				firstPoint = Math.Max(0, numEntries - height);
			} else {
				// Long list, current selection is not near the middle
				firstPoint = currentSelection - midPoint;
			}
			int endPoint = Math.Min(firstPoint + height, numEntries);
			// -2 to account for the first column '  ' or '> ' characters
			int textWidth = width - rightColumnWidth - 2;

			for (int index = firstPoint; index < endPoint; ++index) {
				var textItem = new StringBuilder();

				// Add color strings
				textItem.Append(labelColor);
				if (index == currentSelection) {
					textItem.Append("> ");
				} else {
					textItem.Append("  ");
				}
				if (this[index].isDisabled) {
					textItem.Append(disabledColor);
				} else if (this[index].isSelected) {
					textItem.Append(selectedColor);
				}

				if (!string.IsNullOrEmpty(this[index].labelText)) {
					textItem.Append(this[index].labelText.PadRight(textWidth).Substring(0, textWidth));

					// Only allow a 'right text' to be added if we already have text.
					if (!string.IsNullOrEmpty(this[index].rightText) && rightColumnWidth > 0) {
						if (!this[index].isDisabled && !this[index].isSelected) {
							textItem.Append(rightTextColor);
						}

						textItem.Append(this[index].rightText.PadLeft(rightColumnWidth).Substring(0, rightColumnWidth));
					}
				}

				strings.Add(textItem.ToString());
			}

			var menuString = new StringBuilder();
			foreach (string item in strings) {
				menuString.AppendLine(item);
			}
			return menuString.ToString();
		}

		public void NextItem()
		{
			currentSelection = Math.Min(currentSelection + 1, Count - 1);
		}

		public void PreviousItem()
		{
			currentSelection = Math.Max(currentSelection - 1, 0);
		}

		public void SelectItem()
		{
			// Do callback
			if (!this[currentSelection].isDisabled && this[currentSelection].action != null) {
				this[currentSelection].action(currentSelection, this[currentSelection]);
			}
		}

		public TextMenuItem GetCurrentItem()
		{
			return this[currentSelection];
		}

		public int GetCurrentIndex()
		{
			return currentSelection;
		}
		// Set the isSelected flag for the index menu item.  If "exclusive"
		// is set, all other isSelected flags are cleared.
		public void SetSelected(int index, bool exclusive)
		{
			if (exclusive) {
				foreach (TextMenuItem item in this) {
					item.isSelected = false;
				}
			}

			this[index].isSelected |= index >= 0 && index < Count;

		}
	}

	public class TextMenuItem
	{
		public string labelText = string.Empty;
		public string rightText = string.Empty;
		public bool isDisabled;
		public bool isSelected;
		public Action<int, TextMenuItem> action;
		// Mihara: This can be much more terse to use if there is a constructor with optional parameters.
		// Even if it's finicky about "" rather than string.Empty.
		public TextMenuItem(string labelText = "", Action<int,TextMenuItem> action = null, bool isSelected = false, string rightText = "", bool isDisabled = false)
		{
			this.labelText = labelText;
			this.rightText = rightText;
			this.action = action;
			this.isDisabled = isDisabled;
			this.isSelected = isSelected;
		}
	}
}
