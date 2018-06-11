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
using System.Collections;
using UnityEngine;

namespace JSI
{
    /// <summary>
    /// JSIFlashModule is a very basic class for managing flashing behavior in
    /// multiple props without everyone needing their own counters.  It uses the
    /// Unity coroutine functionality to approximate the requested flashRate,
    /// accounting for timewarp (although if the warp is high enough or the rate
    /// is low enough, it'll use the FixedUpdate interval instead).
    /// 
    /// TODO: Is FixedUpdate the right interval, or should I use something like
    /// render update?
    /// </summary>
    public class JSIFlashModule : PartModule
    {
        /// <summary>
        /// Flash toggle rate in Hz (1/2 of the duty cycle)
        /// </summary>
        [KSPField]
        public float flashRate = 0.0f;

        /// <summary>
        /// Current state
        /// </summary>
        private bool flashToggle;

        /// <summary>
        /// Who cares about this?
        /// </summary>
        public event Action<bool> flashSubscribers;

        /// <summary>
        /// Start the coroutine
        /// </summary>
        public void Start()
        {
            if (!HighLogic.LoadedSceneIsEditor && flashRate > 0.0f)
            {
                StartCoroutine(FlashCoroutine());
            }
        }

        /// <summary>
        /// Clear out flashRate (probably not needed)
        /// </summary>
        public void OnDestroy()
        {
            flashRate = 0.0f;
        }

        /// <summary>
        /// Coroutine for toggling the flash boolean.
        /// </summary>
        /// <returns></returns>
        private IEnumerator FlashCoroutine()
        {
            while (flashRate > 0.0f)
            {
                float delay = 0.0f;
                try
                {
                    flashToggle = !flashToggle;

                    flashSubscribers(flashToggle);

                    delay = flashRate / TimeWarp.CurrentRate;
                }
                catch
                {
                }
                if (delay == 0.0f)
                {
                    yield return null;
                }
                else if (delay < TimeWarp.fixedDeltaTime)
                {
                    yield return new WaitForFixedUpdate();
                }
                else
                {
                    yield return new WaitForSeconds(delay);
                }

            }

            yield return null;

        }
    }
}
