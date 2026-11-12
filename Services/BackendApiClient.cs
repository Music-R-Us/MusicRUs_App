using System.Net.Http.Headers;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class BackendApiClient
{
	private readonly HttpClient httpClient;
	private readonly BackendApiOptions backendOptions;

	// Stores the HTTP client and backend settings used by API requests.
	public BackendApiClient(HttpClient httpClient, IOptions<BackendApiOptions> backendApiOptions)
	{
		this.httpClient = httpClient;
		backendOptions = backendApiOptions.Value;
	}

	public bool CanUseBackend => backendOptions.UseBackendApi && httpClient.BaseAddress is not null;

	// Fetches and deserializes JSON, returning the default value if the request fails.
	public async Task<T?> GetAsync<T>(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		var result = await GetWithStatusAsync<T>(relativeUrl, accessToken, cancellationToken);
		return result.Value;
	}

	// Keeps HTTP status codes so callers can distinguish a missing resource from a failed request.
	public async Task<(T? Value, HttpStatusCode? StatusCode)> GetWithStatusAsync<T>(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
			AddAuthorizationHeader(request, accessToken);

			using var response = await httpClient.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return (default, response.StatusCode);
			}

			return (await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken), response.StatusCode);
		}
		catch
		{
			return (default, null);
		}
	}

	// Fetches a response as text, returning null if the request fails.
	public async Task<string?> GetStringAsync(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
			AddAuthorizationHeader(request, accessToken);

			using var response = await httpClient.SendAsync(request, cancellationToken);
			return response.IsSuccessStatusCode
				? await response.Content.ReadAsStringAsync(cancellationToken)
				: null;
		}
		catch
		{
			return null;
		}
	}

	// Sends a POST request, optionally with a JSON body, and reports whether it succeeded.
	public async Task<bool> PostAsync(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Post, relativeUrl, accessToken, null, cancellationToken);
	}

	// Sends a POST request, optionally with a JSON body, and reports whether it succeeded.
	public async Task<bool> PostAsync<TBody>(string relativeUrl, TBody body, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Post, relativeUrl, accessToken, JsonContent.Create(body), cancellationToken);
	}

	// Sends a JSON PATCH request and reports whether the backend accepted it.
	public async Task<bool> PatchAsync<TBody>(string relativeUrl, TBody body, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Patch, relativeUrl, accessToken, JsonContent.Create(body), cancellationToken);
	}

	// Sends a DELETE request and reports whether the backend accepted it.
	public async Task<bool> DeleteAsync(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Delete, relativeUrl, accessToken, null, cancellationToken);
	}

	// Adds a Bearer authorization header when an access token is supplied.
	public static void AddAuthorizationHeader(HttpRequestMessage request, string? accessToken)
	{
		if (!string.IsNullOrWhiteSpace(accessToken))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		}
	}

	// User writes require a bearer token; request failures return false.
	private async Task<bool> SendAsync(HttpMethod method, string relativeUrl, string? accessToken, HttpContent? content, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(accessToken))
		{
			content?.Dispose();
			return false;
		}

		try
		{
			using var request = new HttpRequestMessage(method, relativeUrl)
			{
				Content = content
			};
			AddAuthorizationHeader(request, accessToken);

			using var response = await httpClient.SendAsync(request, cancellationToken);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
