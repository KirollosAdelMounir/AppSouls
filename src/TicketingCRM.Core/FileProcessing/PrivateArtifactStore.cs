using System.Security.Cryptography;
using TicketingCRM.Core.Security;

namespace TicketingCRM.Core.FileProcessing;

/// <summary>Bounded private storage. Keys are opaque GUIDs, never user paths.</summary>
public sealed class PrivateArtifactStore
{
    private readonly string root;
    public const long MaximumSize = 8L * 1024 * 1024 * 1024;

    public PrivateArtifactStore(string directory)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new ArgumentException("Artifact storage requires an absolute private directory.");
        root = directory;
        PrivateFileSystem.PrepareDirectory(Path.Combine(root, ".check"));
    }

    private string PathFor(Guid id)
    {
        if (id == Guid.Empty) throw new ArgumentException("An artifact ID is required.");
        return Path.Combine(root, id.ToString("N") + ".bin");
    }

    public async Task<(string Sha256, long Size)> SaveAsync(Guid id, Stream input, CancellationToken ct)
    {
        var path = PathFor(id);
        if(input.CanSeek && input.Length-input.Position>MaximumSize)throw new ArgumentException("Installation files are limited to 8 GiB (8,589,934,592 bytes).");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        long size = 0;
        try
        {
            string digest;
            await using (var output = PrivateFileSystem.Open(temporary, FileMode.CreateNew))
            using (var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[65536];
                int read;
                while ((read = await input.ReadAsync(buffer, ct)) > 0)
                {
                    size += read;
                    if (size > MaximumSize) throw new ArgumentException("Installation files are limited to 8 GiB (8,589,934,592 bytes).");
                    hash.AppendData(buffer, 0, read);
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
                if (size == 0) throw new ArgumentException("Choose a nonempty installation file.");
                await output.FlushAsync(ct);
                digest = Convert.ToHexString(hash.GetHashAndReset());
            }
            // Windows requires the exclusive temporary-file handle to close before publication.
            File.Move(temporary, path, false);
            return (digest, size);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public Stream Open(Guid id)
    {
        var path = PathFor(id);
        PrivateFileSystem.CheckFile(path);
        return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
    }

    public void Delete(Guid id)
    {
        var path = PathFor(id);
        if (File.Exists(path)) File.Delete(path);
    }

    public async Task<bool> VerifyAsync(Guid id, string digest, CancellationToken ct)
    {
        await using var file = Open(id);
        var actual = await SHA256.HashDataAsync(file, ct);
        return CryptographicOperations.FixedTimeEquals(actual, Convert.FromHexString(digest));
    }
}
