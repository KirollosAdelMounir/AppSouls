using System.Security.Cryptography;
using System.Text;
using TicketingCRM.Core.SystemConfiguration;
using TicketingCRM.Domain.Administration.DTOs;
using Xunit;
namespace TicketingCRM.Core.Tests;
public sealed class PendingAdministratorFileTests
{
    [Fact]
    public async Task PreparedIdentityIsEncryptedTamperCheckedPermissionCheckedExpiringAndRemoved()
    {
        if(OperatingSystem.IsWindows())return;
        var root=Path.Combine(Path.GetTempPath(),"appsouls-pending-"+Guid.NewGuid().ToString("N"));
        try
        {
            var store=new PendingAdministratorFile(new(Path.Combine(root,"vault","server"),Path.Combine(root,"keys","master")));
            var input=new PreparedAdministrator{UserId=Guid.NewGuid(),TenantId=Guid.NewGuid(),Email="private@example.test",PasswordHash="opaque-password-hash",ExpiresAtUtc=DateTimeOffset.UtcNow.AddHours(1)};
            await store.SaveAsync(input);Assert.True(store.Exists);
            var bytes=await File.ReadAllBytesAsync(store.PathName);Assert.DoesNotContain(input.Email,Encoding.UTF8.GetString(bytes));Assert.DoesNotContain(input.PasswordHash,Encoding.UTF8.GetString(bytes));
            Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(store.PathName));Assert.Equal(UnixFileMode.UserRead|UnixFileMode.UserWrite,File.GetUnixFileMode(store.KeyPath));
            var restored=await store.LoadAsync();Assert.Equal(input.UserId,restored.UserId);Assert.Equal(input.PasswordHash,restored.PasswordHash);
            bytes[^1]^=1;await File.WriteAllBytesAsync(store.PathName,bytes);await Assert.ThrowsAsync<AuthenticationTagMismatchException>(()=>store.LoadAsync());
            await store.SaveAsync(input);File.SetUnixFileMode(store.KeyPath,UnixFileMode.UserRead|UnixFileMode.UserWrite|UnixFileMode.OtherRead);
            await Assert.ThrowsAsync<InvalidOperationException>(()=>store.LoadAsync());File.SetUnixFileMode(store.KeyPath,UnixFileMode.UserRead|UnixFileMode.UserWrite);
            input.ExpiresAtUtc=DateTimeOffset.UtcNow.AddMinutes(-1);await store.SaveAsync(input);await Assert.ThrowsAsync<InvalidOperationException>(()=>store.LoadAsync());
            Assert.Equal(input.UserId,(await store.LoadAsync(allowExpired:true)).UserId);
            store.Complete();Assert.False(store.Exists);Assert.False(File.Exists(store.KeyPath));
        }
        finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
