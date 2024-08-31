using System;

namespace Ryujinx.Ava.UI.Models
{
    internal class RendererInitEventArgs : EventArgs
    {
        public string GpuBackend { get; }
        public string GpuName { get; }

        public RendererInitEventArgs(string gpuBackend, string gpuName)
        {
            GpuBackend = gpuBackend;
            GpuName = gpuName;
        }
    }
}
