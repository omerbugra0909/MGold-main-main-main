using System.Net.Http.Headers;
using System.Web;
using Microsoft.Extensions.Options;
using MGold.Application.DTOs;
using MGold.Application.Interfaces;
using MGold.Common;

namespace MGold.Application.Services;

public class NetgsmSmsService(
    HttpClient httpClient,
    IOptions<SmsSettings> options,
    ILogger<NetgsmSmsService> logger) : ISmsService
{
    private readonly SmsSettings _settings = options.Value;

    public async Task SendAsync(SendSmsRequestDto dto, CancellationToken cancellationToken = default)
    {
        if (!_settings.Enabled)
        {
            logger.LogInformation("SMS sending skipped because Sms:Enabled is false. Message preview: {Message}", dto.Message);
            return;
        }

        var normalizedPhone = TurkishPhoneHelper.Normalize(dto.ToPhone);
        var gsmNo = normalizedPhone.Replace("+", string.Empty, StringComparison.Ordinal);

        var query = HttpUtility.ParseQueryString(string.Empty);
        query["usercode"] = _settings.Username;
        query["password"] = _settings.Password;
        query["gsmno"] = gsmNo;
        query["message"] = dto.Message;
        query["msgheader"] = _settings.Originator;
        query["filter"] = "0";

        var requestUri = $"{_settings.ApiUrl}?{query}";
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/plain"));

        var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"SMS sending failed with status {(int)response.StatusCode}: {body}");
        }

        logger.LogInformation("SMS provider response: {Response}", body);
    }
}
