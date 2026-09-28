using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WiseLine.Portal.Application.Email;

namespace WiseLine.Portal.Infrastructure.Email;

public sealed class ResendEmailSender(
    HttpClient httpClient,
    IOptions<EmailOptions> options) : IEmailSender
{
    private readonly EmailOptions _options = options.Value;

    public async Task<string> SendAsync(EmailSendRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "emails");
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        message.Headers.TryAddWithoutValidation("Idempotency-Key", request.IdempotencyKey);
        message.Content = JsonContent.Create(new
        {
            from = _options.FromAddress,
            to = new[] { request.ToAddress },
            subject = request.Subject,
            html = request.HtmlBody,
            text = request.TextBody,
            reply_to = string.IsNullOrWhiteSpace(_options.ReplyToAddress) ? null : _options.ReplyToAddress
        });

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new EmailSendException("The email provider request timed out.", retryable: true, exception);
        }
        catch (HttpRequestException exception)
        {
            throw new EmailSendException("The email provider could not be reached.", retryable: true, exception);
        }

        using (response)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var retryable = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests ||
                    (int)response.StatusCode >= 500 ||
                    (response.StatusCode == HttpStatusCode.Conflict &&
                     responseBody.Contains("concurrent_idempotent_requests", StringComparison.OrdinalIgnoreCase));
                throw new EmailSendException(
                    $"The email provider returned HTTP {(int)response.StatusCode}: {ReadProviderError(responseBody)}",
                    retryable);
            }

            try
            {
                using var json = JsonDocument.Parse(responseBody);
                var providerId = json.RootElement.GetProperty("id").GetString();
                if (string.IsNullOrWhiteSpace(providerId))
                {
                    throw new JsonException("The response did not contain an email identifier.");
                }

                return providerId;
            }
            catch (JsonException exception)
            {
                throw new EmailSendException("The email provider returned an invalid response.", retryable: true, exception);
            }
        }
    }

    private static string ReadProviderError(string responseBody)
    {
        try
        {
            using var json = JsonDocument.Parse(responseBody);
            var root = json.RootElement;
            var name = root.TryGetProperty("name", out var nameProperty) ? nameProperty.GetString() : null;
            var message = root.TryGetProperty("message", out var messageProperty) ? messageProperty.GetString() : null;
            var combined = string.Join(": ", new[] { name, message }.Where(value => !string.IsNullOrWhiteSpace(value)));
            return string.IsNullOrWhiteSpace(combined) ? "Request rejected." : combined[..Math.Min(combined.Length, 500)];
        }
        catch (JsonException)
        {
            return "Request rejected.";
        }
    }
}
