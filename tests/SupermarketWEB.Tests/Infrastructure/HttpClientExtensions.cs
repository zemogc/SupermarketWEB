using System.Net;
using System.Text.RegularExpressions;
using SupermarketWEB.Data;

namespace SupermarketWEB.Tests.Infrastructure;

public static partial class HttpClientExtensions
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenRegex();

    /// <summary>
    /// Simula el envío de un formulario: carga la página que lo contiene para obtener el token
    /// antiforgery y luego hace POST a <paramref name="url"/>.
    /// </summary>
    public static async Task<HttpResponseMessage> SubmitFormAsync(
        this HttpClient client,
        string url,
        IDictionary<string, string> fields,
        string? formPageUrl = null)
    {
        var pageUrl = formPageUrl ?? url;
        var page = await client.GetStringAsync(pageUrl);
        var match = AntiforgeryTokenRegex().Match(page);
        Assert.True(match.Success, $"No se encontró el token antiforgery en {pageUrl}.");

        var form = new Dictionary<string, string>(fields)
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
        };

        return await client.PostAsync(url, new FormUrlEncodedContent(form));
    }

    public static Task<HttpResponseMessage> LoginAsync(
        this HttpClient client,
        string email = DbInitializer.DemoUserEmail,
        string password = DbInitializer.DemoUserPassword,
        string url = "/Account/Login")
    {
        return client.SubmitFormAsync(url, new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
        });
    }

    /// <summary>
    /// Devuelve el HTML decodificado para poder comparar texto con tildes.
    /// </summary>
    public static async Task<string> ReadHtmlAsync(this HttpResponseMessage response) =>
        WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());

    public static async Task<string> GetHtmlAsync(this HttpClient client, string url) =>
        WebUtility.HtmlDecode(await client.GetStringAsync(url));
}
