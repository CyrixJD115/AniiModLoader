using AniiModLoader.Properties;
using AniiModUnityEngine;

namespace AniiModLoader.AniiModStartScreen.UI.Objects
{
    internal class UI_Text : UI_Object
    {
        private UI_Theme.TextSettings config;
        private Mesh mesh;
        internal bool isDirty = true;
        internal string text;
        internal Font font;

        internal UI_Text(UI_Theme.TextSettings textSettings)
        {
            config = textSettings;
            text = config.Text;

            try
            {
                font = AniiModUnityEngine.Resources.GetBuiltinResource<Font>($"{config.Font}.ttf");
            }
            catch { }

            if (font == null)
                font = AniiModUnityEngine.Resources.GetBuiltinResource<Font>("Arial.ttf");

            AllElements.Add(this);
        }

        internal override void Render()
            => Render(config.Position.Item1, config.Position.Item2);
        internal void Render(int x, int y)
        {
            if (!config.Enabled)
                return;

            UpdateMesh();
            font.material.SetPass(0);
            if (mesh == null)
                return;

            UI_Utils.AnchorToScreen(config.ScreenAnchor, x, y, out int anchor_x, out int anchor_y);
            Graphics.DrawMeshNow(mesh, new Vector3(anchor_x, anchor_y, 0), Quaternion.identity);
        }

        private void UpdateMesh()
        {
            if (!isDirty)
                return;
            if (string.IsNullOrEmpty(text))
                return;

            if (mesh != null)
            {
                mesh.DestroyImmediate();
                mesh = null;
            }

            TextGenerationSettings settings = new TextGenerationSettings();
            settings.generationExtents = new Vector2(540, 47.5f);
            settings.pivot = new Vector2(0.5f, 0.5f);
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.font = font;
            settings.textAnchor = (TextAnchor)config.Anchor;
            settings.color = config.TextColor;
            settings.richText = config.RichText;
            settings.fontSize = config.TextSize;
            settings.fontStyle = config.Style;
            settings.scaleFactor = config.Scale;
            settings.lineSpacing = config.LineSpacing;

            string displayText = text;
            displayText = displayText.Replace("<loaderName/>", "AniiModLoader");
            displayText = displayText.Replace("<loaderVersion/>", Properties.BuildInfo.Version);
            displayText = displayText.Replace("AniiModLoader", "<color=#62D9DF>AniiModLoader</color>");
            displayText = displayText.Replace("<loaderNameHalloween/>", "<color=#7C2CBF>AniiMod</color><color=#FF6F00>Loader</color>");



            mesh = TextMeshGenerator.Generate(displayText, settings);
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.DontDestroyOnLoad();

            isDirty = false;
        }

        internal override void Dispose()
        {
            if (mesh == null)
                return;

            mesh.DestroyImmediate();
            mesh = null;
        }
    }
}
