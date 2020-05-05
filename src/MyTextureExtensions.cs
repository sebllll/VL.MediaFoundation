using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using Xenko.Graphics;

namespace Xenko.Graphics
{
    static class TextureExtensions
    {
        //public static void CopyInto(this Texture target, GraphicsDevice d3dDevice, SharpDX.Direct3D11.Resource source)
        //{
        //    var deviceContext = d3dDevice.NativeDevice.ImmediateContext;
        //    deviceContext.CopyResource(source, target.NativeResource);
        //}

        public static T GetFieldValue<T>(this object obj, string name)
        {
            var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field?.GetValue(obj);
        }

        public static void CopyInto(this Texture target, SharpDX.Direct3D11.Device d3dDevice, SharpDX.Direct3D11.Resource source)
        {
            var nativeResource = GetFieldValue<SharpDX.Direct3D11.Resource>(target, "NativeResource");
            var deviceContext = d3dDevice.ImmediateContext;
            deviceContext.CopyResource(source, nativeResource);
        }
    }
}
