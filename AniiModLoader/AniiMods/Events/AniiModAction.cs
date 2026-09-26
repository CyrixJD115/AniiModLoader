using System;
using System.Collections.Generic;

namespace AniiModLoader
{
    internal class AniiModAction<T> where T : Delegate
    {
        internal readonly T del;
        internal readonly bool unsubscribeOnFirstInvocation;
        internal readonly int priority;

        internal readonly AniiModAssembly aniimodAssembly;

        private AniiModAction(T singleDel, int priority, bool unsubscribeOnFirstInvocation)
        {
            del = singleDel;
            aniimodAssembly = AniiModAssembly.GetAniiModAssemblyOfMember(del.Method, del.Target);
            this.priority = priority;
            this.unsubscribeOnFirstInvocation = unsubscribeOnFirstInvocation;
        }

        internal static List<AniiModAction<T>> Get(T del, int priority = 0, bool unsubscribeOnFirstInvocation = false)
        {
            var mets = del.GetInvocationList();
            var result = new List<AniiModAction<T>>();
            foreach (var met in mets)
            {
                if (met.Target != null && met.Target is AniiModBase aniimod && !aniimod.Registered)
                    continue;

                result.Add(new AniiModAction<T>((T)met, priority, unsubscribeOnFirstInvocation));
            }
            return result;
        }
    }
}
