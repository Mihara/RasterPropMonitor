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
        private const double secondsBetweenSamples = 0.5d;

        private class ResourceComparer : IComparer<ResourceData>
        {
            public int Compare(ResourceData a, ResourceData b)
            {
                return string.Compare(a.name, b.name);
            }
        }

        public ResourceDataStorage()
        {
            int resourceCount = 0;
            foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
            {
                ++resourceCount;
            }

            rs = new ResourceData[resourceCount];
            int index = 0;
            foreach (PartResourceDefinition thatResource in PartResourceLibrary.Instance.resourceDefinitions)
            {
                rs[index] = new ResourceData();
                rs[index].name = thatResource.name;
                rs[index].nameSysr = thatResource.name.ToUpperInvariant().Replace(' ', '-').Replace('_', '-');
                rs[index].density = thatResource.density;
                ++index;
            }

            // Alphabetize our list
            Array.Sort(rs, new ResourceComparer());
        }

        public void StartLoop(double time)
        {
            bool updateDeltas = false;
            float invDeltaT = 1.0f;
            if (time - lastcheck > secondsBetweenSamples)
            {
                updateDeltas = true;
                invDeltaT = (float)(1.0 / (time - lastcheck));
            }

            for (int i = 0; i < rs.Length; ++i)
            {
                if (updateDeltas)
                {
                    rs[i].delta = (rs[i].previous - rs[i].current) * invDeltaT;
                    rs[i].previous = rs[i].current;
                }
                rs[i].current = 0.0f;
                rs[i].max = 0.0f;
                rs[i].stage = 0.0f;
                rs[i].stagemax = 0.0f;
                rs[i].ispropellant = false;

            }

            if (updateDeltas)
            {
                lastcheck = time;
            }
        }

        public void MarkPropellant(Propellant propel)
        {
            foreach (PartResource resource in propel.connectedResources)
            {
                try
                {
                    ResourceData r = Array.Find(rs, t => t.name == resource.info.name);
                    r.ispropellant = true;
                }
                catch(Exception e)
                {
                    JUtil.LogErrorMessage(this, "Error in MarkPropellant({0}): {1}", resource.info.name,e);
                }
            }
        }

        public void GetActiveResourceNames(ref string[] result)
        {
            int currentIndex = 0;
            int currentLength = (result == null) ? 0 : result.Length;
            for (int i = 0; i < rs.Length; ++i)
            {
                if (rs[i].max > 0.0)
                {
                    if (currentIndex == currentLength)
                    {
                        ++currentLength;
                        Array.Resize(ref result, currentLength);
                    }
                    if (result[currentIndex] != rs[i].name)
                    {
                        result[currentIndex] = rs[i].name;
                    }
                    ++currentIndex;
                }
            }

            if(currentIndex > 0 && result.Length > currentIndex)
            {
                Array.Resize(ref result, currentIndex);
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
            "MASS",
            "MAXMASS",
            "MAX",
            "PERCENT"
        };

        public object ListElement(string resourceQuery)
        {
            try
            {
                int i = 0;
                for (; i < keywords.Length; ++i)
                {
                    if (resourceQuery.EndsWith(keywords[i], StringComparison.Ordinal))
                    {
                        //JUtil.LogMessage(this, "matched {0} to {1}", resourceQuery, keywords[i]);
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
                if(resourceName.EndsWith("STAGE"))
                {
                    stage = true;
                    resourceName = resourceName.Substring(0, resourceName.Length - "STAGE".Length);
                }
                //JUtil.LogMessage(this, "I think I should chop {0} down to {1}, with valueType {2} and stage {3}",
                //    resourceQuery, resourceName, valueType, stage);

                ResourceData resource = Array.Find(rs, t => t.nameSysr == resourceName);
                object v = null;
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

                return v;
            }
            catch(Exception e)
            {
                JUtil.LogErrorMessage(this, "ListElement horked on {0}", e);
            }
            return null;
        }

        public object ListElement(string resourceName, string valueType, bool stage)
        {
            double v = 0.0;

            try
            {
                ResourceData resource = Array.Find(rs, t => t.name == resourceName);

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
                ResourceData res = Array.Find(rs, t => t.name == resource.info.name);
                res.current += (float)resource.amount;
                res.max += (float)resource.maxAmount;
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
                ResourceData res = Array.Find(rs, t => t.name == resource.info.name);
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
            public string nameSysr;

            public float current;
            public float max;
            public float previous;
            public float stage;
            public float stagemax;
            public float density;
            public float delta;

            public bool ispropellant;
        }
    }
}
