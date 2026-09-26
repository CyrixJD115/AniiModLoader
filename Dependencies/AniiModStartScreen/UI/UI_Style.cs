using AniiModUnityEngine;
using System;

namespace AniiModLoader.AniiModStartScreen.UI
{
    internal class UI_Style
    {
        internal static Objects.UI_Background Background;
        internal static Objects.UI_Image LogoImage;
        internal static Objects.UI_Image LoadingImage;
        internal static Objects.UI_Text VersionText;
        internal static Objects.UI_Text BrandText;
        internal static Objects.UI_ProgressBar ProgressBar;

        internal static void Init()
        {
            Background = new Objects.UI_Background(UI_Theme.Instance.Background);
            VersionText = new Objects.UI_Text(UI_Theme.Instance.VersionText);
            ProgressBar = new Objects.UI_ProgressBar(UI_Theme.Instance.ProgressBar, UI_Theme.Instance.ProgressText);

            if (UI_Theme.Instance.LogoImage.ScanForCustomImage)
                LogoImage = UI_Utils.LoadImage(UI_Theme.Instance.LogoImage, "Logo");
            if (LogoImage == null)
                {
                var settings = new UI_Theme.TextSettings { Text = "AniiModLoader", TextSize = 40, Anchor = UI_Anchor.MiddleCenter, ScreenAnchor = UI_Anchor.MiddleCenter };
                settings.Position.Item2 = -80;
                BrandText = new Objects.UI_Text(settings);
            }

            if (UI_Theme.Instance.LoadingImage.ScanForCustomImage)
                LoadingImage = UI_Utils.LoadImage(UI_Theme.Instance.LoadingImage, "Loading");

        }


        internal static void Render()
        {
            Background.Render();
            LogoImage?.Render();
            BrandText?.Render();
            LoadingImage?.Render();
            ProgressBar.Render();
            VersionText.Render();
        }
    }
}