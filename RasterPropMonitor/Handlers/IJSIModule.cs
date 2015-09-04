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
using System.Reflection;
using System.Reflection.Emit;

namespace JSI
{
    /// <summary>
    /// This class exists to provide a base class that RasterPropMonitorComputer
    /// manages for tracking various built-in plugin action handlers.
    /// </summary>
    public class IJSIModule
    {
        internal static Vessel vessel;

        public delegate object DynamicMethodDelegate(object instance, object[] args);

        // Takes a "this" parameter and returns nothing
        // TODO: Make this really a return void.
        public delegate object DynamicAction(object instance);

        // Takes a "this" parameter and returns a bool.
        public delegate bool DynamicFuncBool(object instance);

        // Takes a "this" parameter and returns a int.
        public delegate int DynamicFuncInt(object instance);

        // Takes a "this" parameter and returns a double.
        public delegate double DynamicFuncDouble(object instance);

        // Takes a "this" parameter and returns an object.
        public delegate object DynamicFuncObject(object instance);

        // This class comes from http://www.codeproject.com/Articles/10951/Fast-late-bound-invocation-through-DynamicMethod-d
        // covered by The Code Project Open License  http://www.codeproject.com/info/cpol10.aspx
        static public class DynamicMethodDelegateFactory
        {
            public static DynamicMethodDelegate Create(MethodInfo method)
            {
                //JUtil.LogMessage(method, "Create delegate for {0}: IsStatic = {1}, IsFinal = {2}", method.Name, method.IsStatic, method.IsFinal);
                ParameterInfo[] parms = method.GetParameters();
                int numparams = parms.Length;

                Type[] _argTypes = { typeof(object), typeof(object[]) };

                // Create dynamic method and obtain its IL generator to
                // inject code.
                DynamicMethod dynam =
                    new DynamicMethod(
                    "",
                    typeof(object),
                    _argTypes,
                    typeof(DynamicMethodDelegateFactory));
                ILGenerator il = dynam.GetILGenerator();

                /* [...IL GENERATION...] */
                // Define a label for succesfull argument count checking.
                Label argsOK = il.DefineLabel();

                // Check input argument count.
                il.Emit(OpCodes.Ldarg_1);
                il.Emit(OpCodes.Ldlen);
                il.Emit(OpCodes.Ldc_I4, numparams);
                il.Emit(OpCodes.Beq, argsOK);

                // Argument count was wrong, throw TargetParameterCountException.
                il.Emit(OpCodes.Newobj,
                   typeof(TargetParameterCountException).GetConstructor(Type.EmptyTypes));
                il.Emit(OpCodes.Throw);

                // Mark IL with argsOK label.
                il.MarkLabel(argsOK);
                // If method isn't static push target instance on top
                // of stack.
                if (!method.IsStatic)
                {
                    // Argument 0 of dynamic method is target instance.
                    il.Emit(OpCodes.Ldarg_0);
                }
                // Lay out args array onto stack.
                int i = 0;
                while (i < numparams)
                {
                    // Push args array reference onto the stack, followed
                    // by the current argument index (i). The Ldelem_Ref opcode
                    // will resolve them to args[i].

                    // Argument 1 of dynamic method is argument array.
                    il.Emit(OpCodes.Ldarg_1);
                    il.Emit(OpCodes.Ldc_I4, i);
                    il.Emit(OpCodes.Ldelem_Ref);

                    // If parameter [i] is a value type perform an unboxing.
                    Type parmType = parms[i].ParameterType;
                    if (parmType.IsValueType)
                    {
                        il.Emit(OpCodes.Unbox_Any, parmType);
                    }

                    i++;
                }
                // Perform actual call.
                // If method is not final a callvirt is required
                // otherwise a normal call will be emitted.
                if (method.IsFinal)
                {
                    il.Emit(OpCodes.Call, method);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (method.ReturnType != typeof(void))
                {
                    // If result is of value type it needs to be boxed
                    if (method.ReturnType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, method.ReturnType);
                    }
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                // Emit return opcode.
                il.Emit(OpCodes.Ret);


                return (DynamicMethodDelegate)
                  dynam.CreateDelegate(typeof(DynamicMethodDelegate));
            }

            public static DynamicFuncBool CreateFuncBool(MethodInfo method)
            {
                //JUtil.LogMessage(method, "CreateFuncBool delegate for {0}", method.Name);
                Type[] _argTypes = { typeof(object) };

                // Create dynamic method and obtain its IL generator to
                // inject code.
                DynamicMethod dynam =
                    new DynamicMethod(
                    "",
                    typeof(bool),
                    _argTypes,
                    typeof(DynamicMethodDelegateFactory));
                ILGenerator il = dynam.GetILGenerator();

                /* [...IL GENERATION...] */
                // If method isn't static push target instance on top
                // of stack.
                if (!method.IsStatic)
                {
                    // Argument 0 of dynamic method is target instance.
                    il.Emit(OpCodes.Ldarg_0);
                }

                // Perform actual call.
                // If method is not final a callvirt is required
                // otherwise a normal call will be emitted.
                if (method.IsFinal)
                {
                    il.Emit(OpCodes.Call, method);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (method.ReturnType != typeof(bool))
                {
                    throw new Exception("D'oh - this isn't a 'return bool' method");
                }

                // Emit return opcode.
                il.Emit(OpCodes.Ret);

                return (DynamicFuncBool) dynam.CreateDelegate(typeof(DynamicFuncBool));
            }

            public static DynamicFuncInt CreateFuncInt(MethodInfo method)
            {
                Type[] _argTypes = { typeof(object) };

                // Create dynamic method and obtain its IL generator to
                // inject code.
                DynamicMethod dynam =
                    new DynamicMethod(
                    "",
                    typeof(int),
                    _argTypes,
                    typeof(DynamicMethodDelegateFactory));
                ILGenerator il = dynam.GetILGenerator();

                /* [...IL GENERATION...] */
                // If method isn't static push target instance on top
                // of stack.
                if (!method.IsStatic)
                {
                    // Argument 0 of dynamic method is target instance.
                    il.Emit(OpCodes.Ldarg_0);
                }

                // Perform actual call.
                // If method is not final a callvirt is required
                // otherwise a normal call will be emitted.
                if (method.IsFinal)
                {
                    il.Emit(OpCodes.Call, method);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (method.ReturnType != typeof(int))
                {
                    throw new Exception("D'oh - this isn't a 'return int' method");
                }

                // Emit return opcode.
                il.Emit(OpCodes.Ret);

                return (DynamicFuncInt)dynam.CreateDelegate(typeof(DynamicFuncInt));
            }

            public static DynamicFuncDouble CreateFuncDouble(MethodInfo method)
            {
                //JUtil.LogMessage(method, "CreateFuncDouble delegate for {0}", method.Name);
                Type[] _argTypes = { typeof(object) };

                // Create dynamic method and obtain its IL generator to
                // inject code.
                DynamicMethod dynam =
                    new DynamicMethod(
                    "",
                    typeof(double),
                    _argTypes,
                    typeof(DynamicMethodDelegateFactory));
                ILGenerator il = dynam.GetILGenerator();

                /* [...IL GENERATION...] */
                // If method isn't static push target instance on top
                // of stack.
                if (!method.IsStatic)
                {
                    // Argument 0 of dynamic method is target instance.
                    il.Emit(OpCodes.Ldarg_0);
                }

                // Perform actual call.
                // If method is not final a callvirt is required
                // otherwise a normal call will be emitted.
                if (method.IsFinal)
                {
                    il.Emit(OpCodes.Call, method);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (method.ReturnType != typeof(double))
                {
                    throw new Exception("D'oh - this isn't a 'return double' method");
                }

                // Emit return opcode.
                il.Emit(OpCodes.Ret);

                return (DynamicFuncDouble)dynam.CreateDelegate(typeof(DynamicFuncDouble));
            }

            public static DynamicFuncObject CreateFuncObject(MethodInfo method)
            {
                //JUtil.LogMessage(method, "CreateFuncObject delegate for {0}: IsStatic = {1}, IsFinal = {2}", method.Name, method.IsStatic, method.IsFinal);
                //ParameterInfo[] parms = method.GetParameters();
                //int numparams = parms.Length;

                Type[] _argTypes = { typeof(object) };

                // Create dynamic method and obtain its IL generator to
                // inject code.
                DynamicMethod dynam =
                    new DynamicMethod(
                    "",
                    typeof(object),
                    _argTypes,
                    typeof(DynamicMethodDelegateFactory));
                ILGenerator il = dynam.GetILGenerator();

                /* [...IL GENERATION...] */
                // If method isn't static push target instance on top
                // of stack.
                if (!method.IsStatic)
                {
                    // Argument 0 of dynamic method is target instance.
                    il.Emit(OpCodes.Ldarg_0);
                }
                // Lay out args array onto stack.
                //int i = 0;
                //while (i < numparams)
                //{
                //    // Push args array reference onto the stack, followed
                //    // by the current argument index (i). The Ldelem_Ref opcode
                //    // will resolve them to args[i].

                //    // Argument 1 of dynamic method is argument array.
                //    il.Emit(OpCodes.Ldarg_1);
                //    il.Emit(OpCodes.Ldc_I4, i);
                //    il.Emit(OpCodes.Ldelem_Ref);

                //    // If parameter [i] is a value type perform an unboxing.
                //    Type parmType = parms[i].ParameterType;
                //    if (parmType.IsValueType)
                //    {
                //        il.Emit(OpCodes.Unbox_Any, parmType);
                //    }

                //    i++;
                //}
                // Perform actual call.
                // If method is not final a callvirt is required
                // otherwise a normal call will be emitted.
                if (method.IsFinal)
                {
                    il.Emit(OpCodes.Call, method);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (method.ReturnType != typeof(void))
                {
                    // If result is of value type it needs to be boxed
                    if (method.ReturnType.IsValueType)
                    {
                        il.Emit(OpCodes.Box, method.ReturnType);
                    }
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                // Emit return opcode.
                il.Emit(OpCodes.Ret);


                return (DynamicFuncObject) dynam.CreateDelegate(typeof(DynamicFuncObject));
            }

            public static DynamicAction CreateAction(MethodInfo method)
            {
                //JUtil.LogMessage(method, "CreateAction delegate for {0}", method.Name);
                Type[] _argTypes = { typeof(object) };

                // Create dynamic method and obtain its IL generator to
                // inject code.
                DynamicMethod dynam =
                    new DynamicMethod(
                    "",
                    typeof(object),
                    _argTypes,
                    typeof(DynamicMethodDelegateFactory));
                ILGenerator il = dynam.GetILGenerator();

                /* [...IL GENERATION...] */
                // If method isn't static push target instance on top
                // of stack.
                if (!method.IsStatic)
                {
                    // Argument 0 of dynamic method is target instance.
                    il.Emit(OpCodes.Ldarg_0);
                }

                // Perform actual call.
                // If method is not final a callvirt is required
                // otherwise a normal call will be emitted.
                if (method.IsFinal)
                {
                    il.Emit(OpCodes.Call, method);
                }
                else
                {
                    il.Emit(OpCodes.Callvirt, method);
                }

                if (method.ReturnType != typeof(void))
                {
                    throw new Exception("D'oh - this isn't a 'return void' method");
                }
                else
                {
                    il.Emit(OpCodes.Ldnull);
                }

                // Emit return opcode.
                il.Emit(OpCodes.Ret);


                return (DynamicAction) dynam.CreateDelegate(typeof(DynamicAction));
            }
        }
    }
}
