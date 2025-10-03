// Services/IAutocontrolRepository.cs
using System.Threading;
using System.Threading.Tasks;

namespace dashboardQ40.Services
{
    public interface IAutocontrolRepository
    {
        Task<IReadOnlyList<AutocontrolSeries>> GetTimeSeriesForPeriodAsync(
            string token,
            string company,
            DateTime start,
            DateTime end,
            CancellationToken ct = default);
      
    }
}
