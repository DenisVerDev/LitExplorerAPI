using System;
using System.Collections.Generic;

namespace LitExplorerAPI.LitExplorerModels;

public partial class User
{
    public int UserId { get; set; }

    public string Email { get; set; } = null!;

    public string HashedPassword { get; set; } = null!;

    public DateTime RegistrationDate { get; set; }
}
