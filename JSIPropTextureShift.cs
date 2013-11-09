using System;
using UnityEngine;

namespace JSI
{
	public class JSIPropTextureShift: InternalModule
	{
		[KSPField]
		public string transformToShift = "";
		[KSPField]
		public string layerToShift = "_MainTex";
		[KSPField]
		public float x = 0f;
		[KSPField]
		public float y = 0f;

		public void Start ()
		{
			Vector2 shiftval = new Vector2 (x, y);
			Material shifted = base.internalProp.FindModelTransform (transformToShift).renderer.material;
			foreach (string layer in layerToShift.Split ())
				shifted.SetTextureOffset (layer, shiftval + shifted.GetTextureOffset (layer));
			Destroy (this);
		}
	}
}