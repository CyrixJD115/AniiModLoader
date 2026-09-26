using AniiModUnityEngine;

namespace AniiModLoader.AniiModStartScreen.UI.Themes
{
    internal class UI_Theme_Pumpkin : UI_Theme
    {
        internal UI_Theme_Pumpkin()
        {
            Background = new hBackground();
            ProgressBar = new hProgressBar();
            VersionText = new hVersionTextSettings();
            Defaults();
        }



        internal class hBackground : cBackground
        {
            public hBackground()
                => SolidColor = new Color(0.078f, 0f, 0.141f);
        }

        internal class hProgressBar : cProgressBar
        {
            public hProgressBar() : base()
            {
                OuterColor = new Color(0.478f, 0.169f, 0.749f);
                InnerColor = new Color(1f, 0.435f, 0f);
                Defaults();
            }
        }

        internal class hVersionTextSettings : VersionTextSettings
        {
            public hVersionTextSettings()
            {
                Text = $"<loaderNameHalloween/> v<loaderVersion/> {(Is_ALPHA_PreRelease ? "ALPHA Pre-Release" : "Open-Beta")}";
                Defaults();
            }
        }
    }
}
