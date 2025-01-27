﻿using System.Net.Http.Headers;
using System.Reflection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace RdtClient.Service.BackgroundServices;

public class UpdateChecker : BackgroundService
{
    public static String? CurrentVersion { get; private set; }
    public static String? LatestVersion { get; private set; }

    private readonly ILogger<UpdateChecker> _logger;

    public UpdateChecker(ILogger<UpdateChecker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!Startup.Ready)
        {
            await Task.Delay(1000, stoppingToken);
        }

        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString();

        if (String.IsNullOrWhiteSpace(version))
        {
            CurrentVersion = "";

            return;
        }

        CurrentVersion = $"v{version[..version.LastIndexOf(".", StringComparison.Ordinal)]}";

        _logger.LogInformation("UpdateChecker started, currently on version {CurrentVersion}.", CurrentVersion);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("RdtClient", CurrentVersion));
                var response = await httpClient.GetStringAsync($"https://api.github.com/repos/rogerfar/rdt-client/tags?per_page=1", stoppingToken);

                var gitHubReleases = JsonConvert.DeserializeObject<List<GitHubReleasesResponse>>(response);

                if (gitHubReleases == null || gitHubReleases.Count == 0)
                {
                    return;
                }

                var latestRelease = gitHubReleases.FirstOrDefault(m => m.Name != null)?.Name;

                if (latestRelease == null)
                {
                    _logger.LogWarning($"Unable to find latest version on GitHub");
                    return;
                }

                if (latestRelease != CurrentVersion)
                {
                    _logger.LogInformation("New version found on GitHub: {latestRelease}", latestRelease);
                }

                LatestVersion = latestRelease;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error occurred in TorrentDownloadManager.Tick");
            }

            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }

        _logger.LogInformation("UpdateChecker stopped.");
    }
}

public class GitHubReleasesResponse 
{
    [JsonProperty("name")]
    public String? Name { get; set; }
}