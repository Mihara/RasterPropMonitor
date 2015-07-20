using UnityEngine;

namespace JSI
{
    public class JSIPropTextureShift : InternalModule
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

            Transform xform = internalProp.FindModelTransform(transformToShift);
            if (xform.renderer != null && xform.renderer.material != null)
            {
                // MOARdV TODO: Accessing and changing .material causes it to
                // become a copy, according to Unity.  Must destroy it.  Which
                // means this method can't self-destruct; it must use OnDestroy.
                Material shifted = xform.renderer.material;
                if (shifted != null)
                {
                    foreach (string layer in layerToShift.Split())
                    {
                        shifted.SetTextureOffset(layer, shiftval + shifted.GetTextureOffset(layer));
                    }
                }
                else
                {
                    JUtil.LogErrorMessage(this, "Unable to find transform {0} to shift in prop {1}", transformToShift, internalProp.propName);
                }
            }
            Destroy(this);
        }
    }
}
