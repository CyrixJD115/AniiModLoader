using System;
using AniiModLoader.Logging;
using AniiModLoader.Utils;

namespace AniiModLoader
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public class AniiModColorAttribute : Attribute
    {
        /// <summary>
        /// Color of the AniiMod.
        /// </summary>
        [Obsolete("Color is obsolete. Use DrawingColor for full Color support. This will be removed in a future update.", true)]
        public ConsoleColor Color
        {
            get => LoggerUtils.DrawingColorToConsoleColor(DrawingColor);
            set => DrawingColor = LoggerUtils.ConsoleColorToDrawingColor(value);
        }

        /// <summary>
        /// Color of the Author Log.
        /// </summary>
        public ColorARGB DrawingColor { get; internal set; }

        public AniiModColorAttribute() 
            => DrawingColor = AniiModLogger.DefaultTextColor;

        [Obsolete("ConsoleColor is obsolete, use the (int, int, int, int) constructor instead. This will be removed in a future update.", true)]
        public AniiModColorAttribute(ConsoleColor color) 
            => Color = ((color == ConsoleColor.Black) ? LoggerUtils.DrawingColorToConsoleColor(AniiModLogger.DefaultAniiModColor) : color);

        public AniiModColorAttribute(int alpha, int red, int green, int blue) 
            => DrawingColor =  ColorARGB.FromArgb((byte)alpha, (byte)red, (byte)green, (byte)blue);
    }
}