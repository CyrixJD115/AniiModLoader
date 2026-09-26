using System;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModIDAttribute : Attribute
    {
        /// <summary>ID of the AniiMod.</summary>
        public string ID { get; internal set; }

        public AniiModIDAttribute(string id)
            => ID = id;
        public AniiModIDAttribute(int id)
            => ID = id.ToString();
    }
}