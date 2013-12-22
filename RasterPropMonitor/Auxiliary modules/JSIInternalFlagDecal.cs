using UnityEngine;

namespace JSI
{
	public class JSIInternalFlagDecal: InternalModule
	{
		[KSPField]
		public string transformName = string.Empty;
		[KSPField]
		public string textureLayer = "_MainTex";

		public void Start()
		{
			Transform quad;
			quad = internalProp != null ? internalProp.FindModelTransform(transformName) : internalModel.FindModelTransform(transformName);
			Renderer mat = quad.GetComponent<Renderer>();
			mat.material.SetTexture(textureLayer, GameDatabase.Instance.GetTexture(part.flagURL, false));
			Destroy(this);
		}
	}
}

