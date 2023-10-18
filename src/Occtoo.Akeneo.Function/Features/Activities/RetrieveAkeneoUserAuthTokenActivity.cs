using CSharpFunctionalExtensions;
using Microsoft.DurableTask;
using Occtoo.Akeneo.External.Api.Client;
using Occtoo.Akeneo.External.Api.Client.Model;
using Occtoo.Functional.Extensions.Functional.Types;
using System;
using System.Threading.Tasks;

namespace Occtoo.Akeneo.Function.Features.Activities;

[DurableTask(nameof(RetrieveAkeneoUserAuthTokenActivity))]
public class RetrieveAkeneoUserAuthTokenActivity : TaskActivity<RetrieveAkeneoUserAuthTokenActivity.Input, Result<AkeneoAccessTokenDto, DomainError>>
{
    private readonly IAkeneoApiClient _akeneoApiClient;
    public record Input(string PimUrl, string UserName, string Password, string Base64ClientIdSecret);

    public RetrieveAkeneoUserAuthTokenActivity(IAkeneoApiClient akeneoApiClient)
    {
        _akeneoApiClient = akeneoApiClient;
    }

    public override async Task<Result<AkeneoAccessTokenDto, DomainError>> RunAsync(TaskActivityContext context, Input input)
    {
        try
        {
            return await _akeneoApiClient.GetAccessTokenUserContext(input.PimUrl, input.UserName, input.Password,
                input.Base64ClientIdSecret);
        }
        catch (Exception ex)
        {
            return new DomainError(ex.Message);
        }
    }
}
