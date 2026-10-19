using Project.Core.Caching;
using Project.Core.Log;

namespace Project.Core;

public interface IApplicationContext
{
    ICacheProvider Cache { get; }
    ILogProvider Log { get; }
}