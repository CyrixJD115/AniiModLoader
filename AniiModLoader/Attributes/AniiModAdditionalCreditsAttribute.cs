using System;

namespace AniiModLoader {
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModAdditionalCreditsAttribute : Attribute {
        /// <summary>
        /// Any additional credits that the mod author might want to include
        /// </summary>
        public string Credits { get; internal set; }

        /// <summary>
        /// AdditionalCredits constructor
        /// </summary>
        /// <param name="credits">The additional credits of the mod</param>
        public AniiModAdditionalCreditsAttribute(string credits) {
            Credits = credits;
        }
    }
}
