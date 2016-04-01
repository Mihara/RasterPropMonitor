using System;
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

            try
            {
                Transform xform = internalProp.FindModelTransform(transformToShift);
                // MOARdV TODO: Accessing and changing .material causes it to
                // become a copy, according to Unity.  Must destroy it.  Which
                // means this method can't self-destruct; it must use OnDestroy.
                Material shifted = xform.GetComponent<Renderer>().material;
                foreach (string layer in layerToShift.Split())
                {
                    shifted.SetTextureOffset(layer.Trim(), shiftval + shifted.GetTextureOffset(layer.Trim()));
                }
            }
            catch (Exception)
            {
                JUtil.LogErrorMessage(this, "Exception configuring prop {1} (#{2}) with transform {0}.  Check its configuration.", transformToShift, internalProp.propName, internalProp.propID);
            }
            Destroy(this);
        }
    }
}
