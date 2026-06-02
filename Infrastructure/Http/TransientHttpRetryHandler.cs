namespace MGold.Infrastructure.Http;

public class TransientHttpRetryHandler(ILogger<TransientHttpRetryHandler> logger) : DelegatingHandler
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(600)
    ];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (!IsTransient(response) || attempt >= RetryDelays.Length)
                {
                    return response;
                }

                logger.LogWarning("Transient HTTP status {StatusCode} from {Host}. Retrying attempt {Attempt}.",
                    (int)response.StatusCode,
                    request.RequestUri?.Host,
                    attempt + 1);
                response.Dispose();
            }
            catch (HttpRequestException ex) when (attempt < RetryDelays.Length)
            {
                logger.LogWarning(ex, "Transient HTTP exception from {Host}. Retrying attempt {Attempt}.",
                    request.RequestUri?.Host,
                    attempt + 1);
            }
            catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested && attempt < RetryDelays.Length)
            {
                logger.LogWarning(ex, "Transient HTTP timeout from {Host}. Retrying attempt {Attempt}.",
                    request.RequestUri?.Host,
                    attempt + 1);
            }

            await Task.Delay(RetryDelays[attempt], cancellationToken);
        }
    }

    private static bool IsTransient(HttpResponseMessage response)
        => response.StatusCode == System.Net.HttpStatusCode.RequestTimeout
            || response.StatusCode == System.Net.HttpStatusCode.TooManyRequests
            || (int)response.StatusCode >= 500;
}
