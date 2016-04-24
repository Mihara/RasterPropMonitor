using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JSIAdvTransparentPods
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using KSP.UI.Screens.Flight;
    using UnityEngine;
    
    //This Class does not work very well. It was meant to attach to the Portrait PreFab to build our own list of Portraits.
    //But for some reason it is resulting in duplicated portraits and I have spent hours and hours on this trying to figure it out.
    //So I am doing a brute force approach to get the Portrait GameObjects. I am pretty sure this is breaking the EULA of KSP but pretty sure it is not
    //as I am still ONLY accessing the PUBLIC Portraits Class and fields.
    // I hope Squad open the list up to public as per my request in the next release: http://bugs.kerbalspaceprogram.com/issues/8993

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    class Portraits : MonoBehaviour
    {
        public static readonly List<KerbalPortrait> PortraitList = new List<KerbalPortrait>();

        class PortraitTracker : MonoBehaviour
        {
            private KerbalPortrait _portrait;

            private void Start() // Awake might be too early
            {
                if (transform.GetComponentCached(ref _portrait) == null)
                    Destroy(this);
                else AddPortrait(_portrait);
            }

            private void OnDestroy()
            {
                if (_portrait == null) return;

                RemovePortrait(_portrait);
            }
        }


        private void Awake()
        {
            //var kpg = KerbalPortraitGallery.Instance;

            //AddTracker(kpg.portraitPrefab);

            // uncertain whether KSPAddons created before KerbalPortraits initialized
            // pretty sure they are but too lazy to check
            //kpg.gameObject.GetComponentsInChildren<KerbalPortrait>()
            //    .ToList()
            //    .ForEach(AddTracker);

            //Destroy(gameObject);
        }

        // Might only need to edit the prefab once. This will make sure we don't add duplicates
        private static void AddTracker(KerbalPortrait portrait)
        {
            if (portrait.gameObject.GetComponent<PortraitTracker>() != null) return;

            portrait.gameObject.AddComponent<PortraitTracker>();
        }


        private static void AddPortrait(KerbalPortrait portrait)
        {
            if (portrait == null) return;

            PortraitList.AddUnique(portrait);
        }

        private static void RemovePortrait(KerbalPortrait portrait)
        {
            if (PortraitList.Contains(portrait)) PortraitList.RemoveAll(a => a.crewMember == portrait.crewMember);
        }

        /// <summary>
        /// Destroy Portraits for a kerbal and Unregisters them from the KerbalPortraitGallery
        /// </summary>
        /// <param name="kerbal">the Kerbal we want to delete portraits for</param>
        internal static void DestroyPortrait(Kerbal kerbal)
        {
            //set the kerbal InPart to null - this should stop their portrait from re-spawning.
            kerbal.InPart = null;
            //Find all the KerbalPortrait class instances in the KerbalPortraitGallery where CrewMemberName is the one we are after and delete the gameobject.
            KerbalPortraitGallery.Instance.gameObject.GetComponentsInChildren<KerbalPortrait>().Where(a => a.crewMemberName == kerbal.crewMemberName).ToList().ForEach(x => DestroyObject(x));
            //Unregister our kerbal.
            KerbalPortraitGallery.Instance.UnregisterActiveCrew(kerbal);
            
        }
    }
}
