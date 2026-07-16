using VaultShare.Application.Scanning;

namespace VaultShare.Infrastructure.Scanning;

internal sealed class AlwaysCleanTestScanner : IMalwareScanner
{
    public Task<MalwareScanResult> ScanAsync(Stream content, CancellationToken cancellationToken) =>
        Task.FromResult(new MalwareScanResult(MalwareScanOutcome.Clean));
}
