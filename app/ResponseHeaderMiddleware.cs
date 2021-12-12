using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace app
{
    public class ResponseHeaderMiddleware
    {
        readonly RequestDelegate _next;
        readonly string _name;
        readonly string _value;

        public ResponseHeaderMiddleware(RequestDelegate next, string name, string value)
        {
            _next = next;
            _name = name;
            _value = value;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.Headers[_name] = _value;
            await _next(context);
        }
    }

    public static class ResponseHeaderMiddlewareExtensions
    {
        public static IApplicationBuilder UseResponseHeader(this IApplicationBuilder builder, string name, string value)
        {
            return builder.UseMiddleware<ResponseHeaderMiddleware>(name, value);
        }
    }
}
