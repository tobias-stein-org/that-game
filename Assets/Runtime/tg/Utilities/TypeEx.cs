using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;

namespace tg.util
{
    /// <summary>
    /// Type extensions for System.Type class.
    /// </summary>
    public static class TypeEx 
    {
        /// <summary>
        /// Helper method to get all public and private methods of a class type (including parent classes).
        /// </summary>
        /// <returns></returns>
        public static IEnumerable<MethodInfo> getAllMethodsInClassHierachy(this Type InType, Type InRootParentClass = null)
        {
            // grab all methods
            IEnumerable<MethodInfo> methods = InType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            // do it recursevly for parent classes
            if (InType.BaseType != InRootParentClass) { methods = methods.Concat(InType.BaseType.getAllMethodsInClassHierachy(InRootParentClass)); }
            // return all found methods
            return methods;
        }
    }
}

