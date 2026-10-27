using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using MusicRUs.Models;

namespace MusicRUs.Services;

public sealed class BackendApiClient
{
	private readonly HttpClient httpClient;
	private readonly BackendApiOptions backendOptions;

	public BackendApiClient(HttpClient httpClient, IOptions<BackendApiOptions> backendApiOptions)
	{
		this.httpClient = httpClient;
		backendOptions = backendApiOptions.Value;
	}

	public bool CanUseBackend => backendOptions.UseBackendApi && httpClient.BaseAddress is not null;

	public async Task<T?> GetAsync<T>(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
			AddAuthorizationHeader(request, accessToken);

			using var response = await httpClient.SendAsync(request, cancellationToken);
			if (!response.IsSuccessStatusCode)
			{
				return default;
			}

			return await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
		}
		catch
		{
			return default;
		}
	}

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

	public async Task<bool> PostAsync(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Post, relativeUrl, accessToken, null, cancellationToken);
	}

	public async Task<bool> PostAsync<TBody>(string relativeUrl, TBody body, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Post, relativeUrl, accessToken, JsonContent.Create(body), cancellationToken);
	}

	public async Task<bool> PatchAsync<TBody>(string relativeUrl, TBody body, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Patch, relativeUrl, accessToken, JsonContent.Create(body), cancellationToken);
	}

	public async Task<bool> DeleteAsync(string relativeUrl, string? accessToken = null, CancellationToken cancellationToken = default)
	{
		return await SendAsync(HttpMethod.Delete, relativeUrl, accessToken, null, cancellationToken);
	}

	public static void AddAuthorizationHeader(HttpRequestMessage request, string? accessToken)
	{
		if (!string.IsNullOrWhiteSpace(accessToken))
		{
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
		}
	}

	private async Task<bool> SendAsync(HttpMethod method, string relativeUrl, string? accessToken, HttpContent? content, CancellationToken cancellationToken)
	{
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
