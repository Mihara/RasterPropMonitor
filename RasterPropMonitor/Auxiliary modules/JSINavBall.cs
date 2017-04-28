/*****************************************************************************
 * RasterPropMonitor
 * =================
 * Plugin for Kerbal Space Program
 *
 *  by Mihara (Eugene Medvedev), MOARdV, and other contributors
 * 
 * RasterPropMonitor is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, revision
 * date 29 June 2007, or (at your option) any later version.
 * 
 * RasterPropMonitor is distributed in the hope that it will be useful, but
 * WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General Public License
 * for more details.
 * 
 * You should have received a copy of the GNU General Public License
 * along with RasterPropMonitor.  If not, see <http://www.gnu.org/licenses/>.
 ****************************************************************************/
using System;
using UnityEngine;

namespace JSI
{
    class JSINavBall : InternalNavBall
    {
        /// <summary>
        /// Name of the enabling variable.  Required.
        /// </summary>
        [KSPField]
        public string variableName = string.Empty;

        /// <summary>
        /// vec2 containing the 'enabled' range.  May be numeric or varibles.  Required.
        /// </summary>
        [KSPField]
        public string range = string.Empty;

        /// <summary>
        /// Maximum angle that the navball can change per second, in degrees.  Defaults to 180.
        /// </summary>
        [KSPField]
        public float maxAngleChange = 180.0f;

        private VariableOrNumberRange enablingVariable;
        private RasterPropMonitorComputer rpmComp;

        private Quaternion lastOrientation;

        public void Start()
        {
            if (HighLogic.LoadedSceneIsEditor)
            {
                return;
            }

            rpmComp = RasterPropMonitorComputer.Instantiate(internalProp, true);

            lastOrientation = navBall.rotation;

            if (string.IsNullOrEmpty(variableName) || string.IsNullOrEmpty(range))
            {
                JUtil.LogErrorMessage(this, "variableName or range was null!");
                return;
            }
            string[] tokens = range.Split(',');
            if (tokens.Length != 2)
            {
                JUtil.LogErrorMessage(this, "range '{0}' did not have exactly two values!", range);
                return;
            }

            enablingVariable = new VariableOrNumberRange(rpmComp, variableName, tokens[0], tokens[1]);
        }

        public override void OnUpdate()
        {
            if (enablingVariable == null)
            {
                return;
            }

            if (enablingVariable.IsInRange())
            {
                base.OnUpdate();
                Quaternion post = navBall.rotation;
                float deltaAngle = Quaternion.Angle(lastOrientation, post);
                float maxAngle = maxAngleChange * Time.deltaTime;

                // If the rotation angle exceeds what we can do, slow it down
                if (deltaAngle > maxAngle)
                {
                    Quaternion newRotation = Quaternion.Slerp(lastOrientation, post, maxAngle / deltaAngle);
                    lastOrientation = newRotation;
                    navBall.rotation = newRotation;
                }
                else
                {
                    lastOrientation = post;
                }
            }
        }
    }
}
