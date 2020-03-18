using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<UnicontaConnectionProvider> _logger;
        private readonly UnicontaRestOptions _options;

        public UnicontaConnectionProvider(IMemoryCache memoryCache, ILogger<UnicontaConnectionProvider> logger, IOptions<UnicontaRestOptions> options)
        {
            _memoryCache = memoryCache;
            _logger = logger;
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

        private const int MAX_GET_COMPANIES_TRIALS = 5;

        private async Task<UnicontaConnectionDetails> ConnectAsync(Credentials credentials, Guid affiliateKey)
        {
            // Try and get company list multiple times as it seems to occasionally return null
            for (var trial = 1; trial <= MAX_GET_COMPANIES_TRIALS; trial++)
            {
                var connection = new UnicontaConnection(APITarget.Live);
                var session = new Session(connection);

                var loggedIn = await session.LoginAsync(credentials.Username, credentials.Password, LoginType.API, affiliateKey);

                if (loggedIn != ErrorCodes.Succes)
                {
                    throw new Exception($"Unable to login, got error code: {loggedIn}");
                }

                var companies = await session.GetCompanies();

                if (companies is object)
                {
                    if (trial > 1)
                    {
                        _logger.LogWarning("Got companies after {Trials} trials", trial);
                    }

                    return new UnicontaConnectionDetails(session, companies);
                }
            }

            // Ensure that only a valid entry is ever inserted in the database
            throw new Exception($"Unable to get company list after {MAX_GET_COMPANIES_TRIALS} trials, got null instead");
        }
    }

    public class UnicontaConnectionDetails
    {
        public Session Session { get; }
        public Company[] Companies { get; }

        public UnicontaConnectionDetails(Session session, Company[] companies)
        {
            Session = session;
            Companies = companies;
        }
    }
}
