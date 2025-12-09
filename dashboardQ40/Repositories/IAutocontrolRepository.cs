// Services/IAutocontrolRepository.cs
using System.Threading;
using System.Threading.Tasks;
using dashboardQ40.Services;

namespace dashboardQ40.Repositories
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
