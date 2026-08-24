using WolvenKit.Common.Services;

namespace Ripperdoc.Core.Archive;

/// <summary>
/// The hash service handed to the pinned library's archive reader: one that
/// knows no names of its own.
/// </summary>
/// <remarks>
/// The reader requires such a service to be passed, and the pinned packages
/// ship the interface with no implementation - so one has to exist here
/// regardless of which naming posture is in force.
/// <para>
/// It can be empty because it is not the channel names travel on. The library
/// resolves a resource path through its own process-wide pool, which an
/// archive populates from the paths it carries and which a dictionary source
/// populates when it loads. Both postures were measured through this same
/// empty service, and the wider posture's coverage arrived intact - so the
/// engine core never needs the dictionary's own service type, and therefore
/// never needs the package that carries it.
/// </para>
/// </remarks>
internal sealed class NoDictionaryHashService : IHashService
{
    internal static readonly NoDictionaryHashService Instance = new();

    private NoDictionaryHashService()
    {
    }

    public Task Loaded { get; } = Task.CompletedTask;

    public void Load()
    {
    }

    public bool Contains(ulong key, bool checkUserHashes) => false;

    public string Get(ulong key) => null!;

    public IEnumerable<ulong> GetAllHashes() => [];

    public IEnumerable<ulong> GetMissingHashes() => [];

    public string GetGuessedExtension(ulong key) => null!;
}
