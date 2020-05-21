using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace app
{
    public class PostConfigureCookieAuthenticationOptions : IPostConfigureOptions<CookieAuthenticationOptions>
    {
        private readonly ITicketStore _ticketStore;

        public PostConfigureCookieAuthenticationOptions(ITicketStore ticketStore)
        {
            _ticketStore = ticketStore;
        }

        public void PostConfigure(string name, CookieAuthenticationOptions options)
        {
            options.SessionStore = _ticketStore;
        }
    }
}
