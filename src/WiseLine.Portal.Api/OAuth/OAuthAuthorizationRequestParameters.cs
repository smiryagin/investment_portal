using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;
using WiseLine.Portal.Api.Models;

namespace WiseLine.Portal.Api.OAuth;

internal static class OAuthAuthorizationRequestParameters
{
    public static IReadOnlyList<OAuthRequestParameterViewModel> Create(OpenIddictRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var parameters = new List<OAuthRequestParameterViewModel>();
        foreach (var parameter in request.GetParameters())
        {
            foreach (var value in (StringValues)parameter.Value)
            {
                if (value is not null)
                {
                    parameters.Add(new OAuthRequestParameterViewModel(parameter.Key, value));
                }
            }
        }

        return parameters;
    }
}
