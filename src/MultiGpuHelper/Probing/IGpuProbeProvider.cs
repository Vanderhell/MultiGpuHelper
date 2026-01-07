using System.Collections.Generic;
using System.Threading.Tasks;
using MultiGpuHelper.Models;

namespace MultiGpuHelper.Probing
{
    /// <summary>
    /// Abstraction for GPU detection/probing providers.
    /// </summary>
    public interface IGpuProbeProvider
    {
        /// <summary>
        /// Probe and return available GPU devices.
        /// </summary>
        Task<IList<GpuDevice>> ProbeAsync();
    }
}
