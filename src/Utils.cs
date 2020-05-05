using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct3D11;
using Xenko.Core.Mathematics;

namespace VL.MediaFoundation
{
    public static class Utils
    {
        /// <summary>
        /// Swaps the value between two references.
        /// </summary>
        /// <typeparam name="T">Type of a data to swap.</typeparam>
        /// <param name="left">The left value.</param>
        /// <param name="right">The right value.</param>
        public static void Swap<T>(ref T left, ref T right)
        {
            var temp = left;
            left = right;
            right = temp;
        }

        public static Vector2 Texture2DInfo(Texture2D texture)
        {
            return new Vector2(texture.Description.Width, texture.Description.Height);
        }
    }
}
