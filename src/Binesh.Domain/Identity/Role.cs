using Microsoft.AspNetCore.Identity;

namespace Binesh.Domain.Identity;

public sealed class Role : IdentityRole<Guid>
{
    public Role()
    {
        Id = Guid.NewGuid();
    }

    public Role(string name) : this()
    {
        Name = name;
        NormalizedName = name.ToUpperInvariant();
    }
}
