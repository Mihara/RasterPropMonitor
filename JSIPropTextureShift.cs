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
		public float x;
		[KSPField]
		public float y;

		public void Start()
		{
			var shiftval = new Vector2(x, y);
			Material shifted = internalProp.FindModelTransform(transformToShift).renderer.material;
			foreach (string layer in layerToShift.Split ())
				shifted.SetTextureOffset(layer, shiftval + shifted.GetTextureOffset(layer));
			Destroy(this);
		}
	}
}