using System;
using UnityEngine;

namespace JSI
{
    // Just a helper class to encapsulate this mess.
    public class PersistenceAccessor
    {
        private readonly RasterPropMonitorComputer comp;
        private readonly int propID;

        public PersistenceAccessor(MonoBehaviour referenceLocation)
        {
            if(referenceLocation is InternalProp)
            {
                comp = RasterPropMonitorComputer.Instantiate(referenceLocation);
                propID = (referenceLocation as InternalProp).propID;
            }
            else if(referenceLocation is Part)
            {
                comp = RasterPropMonitorComputer.Instantiate(referenceLocation);
                propID = -1;
            }
            else
            {
                throw new Exception("Instantiating PersistenceAccessor with indeterminate type");
            }
        }

        internal InternalProp prop
        {
            get
            {
                if (propID >= 0)
                {
                    return comp.part.internalModel.props[propID];
                }
                else
                {
                    return null;
                }
            }
        }


        public bool GetBool(string persistentVarName, bool defaultValue)
        {
            return comp.GetBool(persistentVarName, defaultValue);
        }

        public int GetVar(string persistentVarName, int defaultValue)
        {
            return comp.GetVar(persistentVarName, defaultValue);
        }

        public int GetVar(string persistentVarName)
        {
            return comp.GetVar(persistentVarName);
        }

        public bool HasVar(string persistentVarName)
        {
            return comp.HasVar(persistentVarName);
        }

        public void SetVar(string persistentVarName, int varvalue)
        {
            comp.SetVar(persistentVarName, varvalue);
        }

        public void SetVar(string persistentVarName, bool varvalue)
        {
            comp.SetVar(persistentVarName, varvalue);
        }


        public int GetPropVar(string persistentVarName)
        {
            string propVar = "%PROP%"+propID+persistentVarName;
            return comp.GetVar(propVar);
        }

        public bool HasPropVar(string persistentVarName)
        {
            string propVar = "%PROP%" + propID + persistentVarName;
            return comp.HasVar(propVar);
        }

        public void SetPropVar(string persistentVarName, int varvalue)
        {
            string propVar = "%PROP%" + propID + persistentVarName;
            comp.SetVar(propVar, varvalue);
        }

        
        public string GetStoredString(int index)
        {
            return comp.GetStoredString(index);
        }
    }
}
