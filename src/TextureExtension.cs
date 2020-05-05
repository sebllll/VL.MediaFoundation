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

namespace VL.MediaFoundation
{
    public static class TextureExtension
    {
        public static void CopyInto(this Texture target, GraphicsDevice d3dDevice, SharpDX.Direct3D11.Resource source)
        {
            //SharpDX.Direct3D11.Resource nativeResource2 = target.GetGraphicsResourceBaseField<SharpDX.Direct3D11.Resource>("NativeResource");
            SharpDX.Direct3D11.Resource nativeResource = (SharpDX.Direct3D11.Resource)SharpDXInterop.GetNativeResource(target);
            SharpDX.Direct3D11.Device nativeDevice = (SharpDX.Direct3D11.Device)SharpDXInterop.GetNativeDevice(d3dDevice);

            var deviceContext = nativeDevice.ImmediateContext;
            deviceContext.CopyResource(source, nativeResource);
        }

        public static void CopyInto(this Texture target, SharpDX.Direct3D11.Device d3dDevice, SharpDX.Direct3D11.Resource source)
        {
            SharpDX.Direct3D11.Resource nativeResource  = (SharpDX.Direct3D11.Resource)SharpDXInterop.GetNativeResource(target);

            var deviceContext = d3dDevice.ImmediateContext;
            deviceContext.CopyResource(source, nativeResource);
        }

        private static T GetGraphicsResourceBaseField<T>(this Texture texture, string name)
        {
            return (T)typeof(GraphicsResource).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(texture);
        }

        private static T GetFieldValue<T>(this object obj, string name)
        {
            var field = obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return (T)field?.GetValue(obj);
        }

    }
}
