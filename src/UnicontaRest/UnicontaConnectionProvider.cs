using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Uniconta.API.Service;
using Uniconta.Common;
using Uniconta.Common.User;
using Uniconta.DataModel;

namespace UnicontaRest
{
    public class UnicontaConnectionProvider
    {
        private readonly ConcurrentDictionary<Credentials, Lazy<SemaphoreSlim>> _connectionLocks = new ConcurrentDictionary<Credentials, Lazy<SemaphoreSlim>>();
        private readonly IMemoryCache _memoryCache;
        private readonly UnicontaRestOptions _options;

        public UnicontaConnectionProvider(IMemoryCache memoryCache, IOptions<UnicontaRestOptions> options)
        {
            _memoryCache = memoryCache;
            _options = options.Value;
        }

        public Task<UnicontaConnectionDetails> GetConnectionAsync(Credentials credentials, CancellationToken cancellationToken)
        {
            // GetOrCreate is thread-safe in that the internal state of the cache can handle parallel operations,
            // but it is not however guaranteed that the factory method will not run multiple times for the same key during parallel create's,
            // see https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-3.1#additional-notes
            return _memoryCache.GetOrCreateAsync(credentials, async entry =>
            {
                var @lock = _connectionLocks.GetOrAdd(credentials, _ => new Lazy<SemaphoreSlim>(() => new SemaphoreSlim(1))).Value;
                await @lock.WaitAsync(cancellationToken);

                try
                {
                    if (_memoryCache.TryGetValue(credentials, out UnicontaConnectionDetails details))
                    {
                        return details;
                    }

                    details = await ConnectAsync(credentials, _options.AffiliateKey);

                    entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(30));

                    return details;
                }
                finally
                {
                    @lock.Release();
                }
            });
        }

        private async Task<UnicontaConnectionDetails> ConnectAsync(Credentials credentials, Guid affiliateKey)
        {
            var connection = new UnicontaConnection(APITarget.Live);
            var session = new Session(connection);

            var loggedIn = await session.LoginAsync(credentials.Username, credentials.Password, LoginType.API, affiliateKey);

            if (loggedIn != ErrorCodes.Succes)
            {
                throw new Exception($"Unable to login, got error code: {loggedIn}");
            }

            var companies = await session.GetCompanies();

            return new UnicontaConnectionDetails()
            {
                Session = session,
                Companies = companies
            };
        }
    }

    public class UnicontaConnectionDetails
    {
        public Session Session { get; set; }
        public Company[] Companies { get; set; }
    }
}
