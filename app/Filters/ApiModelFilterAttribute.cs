using System.Text.Json;
using app.Models.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace app.Filters;

public class ApiModelFilterAttribute : ActionFilterAttribute
{
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (context.Result is ObjectResult result)
        {
            var options = new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            options.Converters.Add(new JsonConverterTaskList());
            options.Converters.Add(new JsonConverterTask());
            result.Formatters.Add(new SystemTextJsonOutputFormatter(options));
        }
        base.OnActionExecuted(context);
    }
}
