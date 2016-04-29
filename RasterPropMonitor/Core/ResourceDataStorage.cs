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
using System.Collections.Generic;
using System;

namespace JSI
{
    public class ResourceDataStorage
    {
        private readonly ResourceData[] rs;
        private double lastcheck;
        private readonly Dictionary<string, ResourceData> nameResources = new Dictionary<string, ResourceData>();
        private readonly Dictionary<string, ResourceData> sysrResources = new Dictionary<string, ResourceData>();
        private readonly string[] sortedResourceNames;
        private int numValidResourceNames = 0;

        // A dictionary mapping resourceIDs to dictionaries that map 
        private readonly Dictionary<int, List<PartResource>> activeResources = new Dictionary<int, List<PartResource>>();

        private class ResourceComparer : IComparer<ResourceData>
        {
            public int Compare(ResourceData a, ResourceData b)
            {
                return string.Compare(a.name, b.name);
            }
        }

        private static bool IsFreeFlow(ResourceFlowMode flowMode)
        {
            return (flowMode == ResourceFlowMode.ALL_VESSEL || flowMode == ResourceFlowMode.ALL_VESSEL_BALANCE);
        }

        public ResourceDataStorage()
        {
            int resourceCount = 0;
            foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
            {
                ++resourceCount;
            }

            rs = new ResourceData[resourceCount];
            sortedResourceNames = new string[resourceCount];
            int index = 0;
            foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
            {
                string nameSysr = thatResource.name.ToUpperInvariant().Replace(' ', '-').Replace('_', '-');

                rs[index] = new ResourceData();
                rs[index].name = thatResource.name;
                rs[index].density = thatResource.density;
                rs[index].resourceId = thatResource.id;

                activeResources.Add(thatResource.id, new List<PartResource>());

                nameResources.Add(thatResource.name, rs[index]);
                sysrResources.Add(nameSysr, rs[index]);
                ++index;
            }

            // Alphabetize our list
            Array.Sort(rs, new ResourceComparer());
        }

        public void StartLoop()
        {
            for (int i = 0; i < rs.Length; ++i)
            {
                rs[i].current = 0.0f;
                rs[i].max = 0.0f;
                rs[i].stage = 0.0f;
                rs[i].stagemax = 0.0f;
                rs[i].ispropellant = false;

                activeResources[rs[i].resourceId].Clear();
            }
        }

        public void EndLoop(double time)
        {
            float invDeltaT = (float)(1.0 / (time - lastcheck));
            for (int i = 0; i < rs.Length; ++i)
            {
                rs[i].delta = (rs[i].previous - rs[i].current) * invDeltaT;
                rs[i].previous = rs[i].current;
            }
            lastcheck = time;

            numValidResourceNames = 0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    sortedResourceNames[numValidResourceNames] = rs[i].name;
                    ++numValidResourceNames;
                }

                // See if any engines marked these resources as propellants.
                // If so, we have stage and stageMax info available, so we can
                // sum them up.
                var list = activeResources[rs[i].resourceId];
                if (list.Count > 0)
                {
                    float stage = 0.0f, stageMax = 0.0f;
                    
                    for (int j = 0; j < list.Count; ++j)
                    {
                        stage += (float)list[j].amount;
                        stageMax += (float)list[j].maxAmount;
                    }

                    rs[i].stage = stage;
                    rs[i].stagemax = stageMax;
                }
            }
        }

        public string GetActiveResourceByIndex(int index)
        {
            return (index < numValidResourceNames) ? sortedResourceNames[index] : string.Empty;
        }
        //public void DumpData()
        //{
        //    JUtil.LogMessage(this, "Resource data update:");
        //    for (int i = 0; i < rs.Length; ++i)
        //    {
        //        JUtil.LogMessage(this, "{0}: C {1:0.0} / {2:0.0}; T {3:0.0} / {4:0.0}; R {5:0.00}",
        //            rs[i].name, rs[i].stage, rs[i].current, rs[i].stagemax, rs[i].max, rs[i].delta);
        //    }
        //}

        public void MarkPropellant(Propellant propel)
        {
            var connectedResources = propel.connectedResources;
            for (int resourceIdx = 0; resourceIdx < connectedResources.Count; ++resourceIdx)
            {
                try
                {
                    ResourceData r = nameResources[connectedResources[resourceIdx].info.name];
                    r.ispropellant = true;

                    // If the resoruce in question isn't a "free flow" -
                    // that is, an ALL_VESSEL_* resource - then add the
                    // PartResource to the list we will consider for stage
                    // resource availability.  But also don't add it if the
                    // particular PartResource is in the list (as based on
                    // checking GetHashCode()).
                    // MOARdV TODO: I *could* make a dictionary instead of list,
                    // but I don't know if it's worthwhile.
                    if (!IsFreeFlow(connectedResources[resourceIdx].info.resourceFlowMode))
                    {
                        var list = activeResources[r.resourceId];
                        bool needsAdded = true;
                        for (int listIndex = 0; listIndex < list.Count; ++listIndex)
                        {
                            if (list[listIndex].GetHashCode() == connectedResources[resourceIdx].GetHashCode())
                            {
                                needsAdded = false;
                                break;
                            }
                        }
                        if (needsAdded)
                        {
                            list.Add(connectedResources[resourceIdx]);
                        }
                    }
                }
                catch (Exception e)
                {
                    JUtil.LogErrorMessage(this, "Error in MarkPropellant({0}): {1}", connectedResources[resourceIdx].info.name, e);
                }
            }
        }

        public void GetAvailableResourceNames(ref string[] result)
        {
            int requiredLength = 0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    requiredLength++;
                }
            }

            if (result == null || result.Length != requiredLength)
            {
                Array.Resize(ref result, requiredLength);
            }

            int currentIndex = 0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    result[currentIndex] = rs[i].name;
                    ++currentIndex;
                }
            }
        }

        public double PropellantMass(bool stage)
        {
            double mass = 0.0;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].ispropellant)
                {
                    mass += rs[i].density * ((stage) ? rs[i].stage : rs[i].current);
                }
            }
            return mass;
        }

        private static readonly string[] keywords =
        {
            "VAL",
            "DENSITY",
            "DELTA",
            "DELTAINV",
            "MAXMASS",
            "MASS",
            "MAX",
            "PERCENT",
            "DEPLETED"
        };

        public object ListElement(string resourceQuery)
        {
            object v = 0.0;
            try
            {
                int i = 0;
                for (; i < keywords.Length; ++i)
                {
                    if (resourceQuery.EndsWith(keywords[i], StringComparison.Ordinal))
                    {
                        break;
                    }
                }
                int substringLength = resourceQuery.Length - "SYSR_".Length;
                string valueType;
                if (i == keywords.Length)
                {
                    valueType = "VAL";
                }
                else
                {
                    substringLength -= keywords[i].Length;
                    valueType = keywords[i];
                }

                string resourceName = resourceQuery.Substring("SYSR_".Length, substringLength);
                bool stage = false;
                if (resourceName.EndsWith("STAGE"))
                {
                    stage = true;
                    resourceName = resourceName.Substring(0, resourceName.Length - "STAGE".Length);
                }

                ResourceData resource = sysrResources[resourceName];
                switch (valueType)
                {
                    case "":
                    case "VAL":
                        v = stage ? resource.stage : resource.current;
                        break;
                    case "DENSITY":
                        v = resource.density;
                        break;
                    case "DELTA":
                        v = resource.delta;
                        break;
                    case "DELTAINV":
                        v = -resource.delta;
                        break;
                    case "MASS":
                        v = resource.density * (stage ? resource.stage : resource.current);
                        break;
                    case "MAXMASS":
                        v = resource.density * (stage ? resource.stagemax : resource.max);
                        break;
                    case "MAX":
                        v = stage ? resource.stagemax : resource.max;
                        break;
                    case "PERCENT":
                        if (stage)
                        {
                            v = resource.stagemax > 0 ? resource.stage / resource.stagemax : 0d;
                        }
                        else
                        {
                            v = resource.max > 0 ? resource.current / resource.max : 0d;
                        }
                        break;
                    case "DEPLETED":
                        if (stage)
                        {
                            bool available = (resource.stagemax > 0.0f && resource.stage < 0.01f);
                            v = available.GetHashCode();
                        }
                        else
                        {
                            bool available = (resource.max > 0.0f && resource.current < 0.01f);
                            v = available.GetHashCode();
                        }
                        break;
                }

            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "ListElement({1}) threw trapped exception {0}", e, resourceQuery);
            }
            return v;
        }

        public object ListElement(string resourceName, string valueType, bool stage)
        {
            double v = 0.0;

            try
            {
                ResourceData resource = nameResources[resourceName];

                switch (valueType)
                {
                    case "":
                    case "VAL":
                        v = stage ? resource.stage : resource.current;
                        break;
                    case "DENSITY":
                        v = resource.density;
                        break;
                    case "DELTA":
                        v = resource.delta;
                        break;
                    case "DELTAINV":
                        v = -resource.delta;
                        break;
                    case "MASS":
                        v = resource.density * (stage ? resource.stage : resource.current);
                        break;
                    case "MAXMASS":
                        v = resource.density * (stage ? resource.stagemax : resource.max);
                        break;
                    case "MAX":
                        v = stage ? resource.stagemax : resource.max;
                        break;
                    case "PERCENT":
                        if (stage)
                        {
                            v = resource.stagemax > 0 ? resource.stage / resource.stagemax : 0d;
                        }
                        else
                        {
                            v = resource.max > 0 ? resource.current / resource.max : 0d;
                        }
                        break;
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Error finding {0}-{2}: {1}", resourceName, e, valueType);
            }

            return v;
        }

        public void Add(PartResource resource)
        {
            try
            {
                ResourceData res = nameResources[resource.info.name];
                res.current += (float)resource.amount;
                res.max += (float)resource.maxAmount;

                var flowmode = resource.info.resourceFlowMode;
                if (IsFreeFlow(flowmode))
                {
                    res.stage += (float)resource.amount;
                    res.stagemax += (float)resource.maxAmount;
                }
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Error adding {0}: {1}", resource.info.name, e);
            }
        }

        public void SetActive(Vessel.ActiveResource resource)
        {
            try
            {
                ResourceData res = nameResources[resource.info.name];
                res.stage = (float)resource.amount;
                res.stagemax = (float)resource.maxAmount;
            }
            catch (Exception e)
            {
                JUtil.LogErrorMessage(this, "Error SetActive {0}: {1}", resource.info.name, e);
            }
        }

        private class ResourceData
        {
            public string name;

            public float current;
            public float max;
            public float previous;

            public float stage;
            public float stagemax;

            public float density;
            public float delta;

            public int resourceId;

            public bool ispropellant;
        }
    }
}
