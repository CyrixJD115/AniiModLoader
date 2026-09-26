using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModPriorityAttribute : Attribute
    {
        /// <summary>
        /// Priority of the AniiMod.
        /// </summary>
        public int Priority;

        public AniiModPriorityAttribute(int priority = 0) => Priority = priority;
    }
}