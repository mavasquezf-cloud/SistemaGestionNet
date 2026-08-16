using SistemaGestion.Application.Common.Time;

namespace SistemaGestion.Infrastructure.Time;

internal sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
