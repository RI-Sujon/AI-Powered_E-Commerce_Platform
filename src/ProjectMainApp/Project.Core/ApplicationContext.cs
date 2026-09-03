using Project.Core.Caching;
using Project.Core.Log;

namespace Project.Core;

public class ApplicationContext  : IApplicationContext
{
    private readonly ICacheProvider _cache;
    private readonly ILogProvider _log;

    public ApplicationContext( ICacheProvider cache, ILogProvider log)
    {
        _log = log;
        _cache = cache;
    }

    public ICacheProvider Cache
    {
        get { return _cache; }
    }

    public ILogProvider Log
    {
        get { return _log; }
    }
}