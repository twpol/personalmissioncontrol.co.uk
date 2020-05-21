using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;

namespace app
{
    public class InMemoryTicketStore : ITicketStore
    {
        IMemoryCache _cache;

        public InMemoryTicketStore(IMemoryCache cache)
        {
            _cache = cache;
        }

        public Task<string> StoreAsync(AuthenticationTicket ticket)
        {
            var key = ticket.Principal.Claims.First(c => c.Type == ClaimTypes.NameIdentifier).Value;
            _cache.Set(key, ticket);
            return Task.FromResult(key);
        }

        public Task<AuthenticationTicket> RetrieveAsync(string key)
        {
            return Task.FromResult(_cache.Get<AuthenticationTicket>(key));
        }

        public Task RenewAsync(string key, AuthenticationTicket ticket)
        {
            _cache.Set(key, ticket);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key)
        {
            _cache.Remove(key);
            return Task.CompletedTask;
        }
    }
}
