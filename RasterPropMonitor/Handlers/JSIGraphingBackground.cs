using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
using UnityEngine;

namespace JSI
{
    // JSIGraphingBackground provides an editable / configurable way to render
    // one or more data in a graphical manner.
    class JSIGraphingBackground : InternalModule
    {
        [KSPField]
        public string layout;
        [KSPField]
        public string backgroundColor = "0,0,0,0";
        private Color32 backgroundColorValue;

        private bool startupComplete = false;

        public bool RenderBackground(RenderTexture screen, float cameraAspect)
        {
            GL.Clear(true, true, backgroundColorValue);

            return true;
        }

        public override void OnUpdate()
        {
            if (!JUtil.IsActiveVessel(vessel))
                return;

            if (!JUtil.VesselIsInIVA(vessel))
            {
                //foreach (VariableAnimationSet unit in variableSets)
                //{
                //    unit.MuteSoundWhileOutOfIVA();
                //}
            }

            //if ((!alwaysActive && !JUtil.VesselIsInIVA(vessel)) || !UpdateCheck())
            //    return;

            //foreach (VariableAnimationSet unit in variableSets)
            //{
            //    unit.Update();
            //}
        }

        public void LateUpdate()
        {
            if (vessel != null && JUtil.VesselIsInIVA(vessel) && !startupComplete)
            {
                JUtil.AnnoyUser(this);
                enabled = false;
            }
        }

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }
            try
            {
                if (string.IsNullOrEmpty(layout))
                {
                    throw new ArgumentNullException("layout");
                }

                backgroundColorValue = ConfigNode.ParseColor32(backgroundColor);

                foreach (ConfigNode node in GameDatabase.Instance.GetConfigNodes("JSI_GRAPHING_BACKGROUND"))
                {
                    if (node.GetValue("name") == layout)
                    {
                        ConfigNode[] dataNodes = node.GetNodes("DATA_SET");
                        JUtil.LogMessage(this, "Found my config with {0} DATA_SET nodes", dataNodes.Length);

                        for (int i = 0; i < dataNodes.Length; i++)
                        {
                            try
                            {
                                JUtil.LogMessage(this, "Found a DATA_SET");
                                //variableSets.Add(new VariableAnimationSet(variableNodes[i], internalProp));
                            }
                            catch (ArgumentException e)
                            {
                                JUtil.LogErrorMessage(this, "Error in building prop number {1} - {0}", e.Message, internalProp.propID);
                            }
                        }
                        break;
                    }
                }
                startupComplete = true;
            } 

            catch 
            {
				JUtil.AnnoyUser(this);
				throw;
			}
        }
    }
}
