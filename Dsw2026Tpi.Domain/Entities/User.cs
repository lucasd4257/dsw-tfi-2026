using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class User : EntityBase
{
    public string Email { get; init; }
    public string? PasswordHash { get; private set; }
    public UserRole Role { get; init; } 
    public bool IsActive { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private User() { }
#pragma warning restore CS8618
    #endregion

    public User(string email, UserRole role, string? passwordHash = null, Guid? id = null) : base(id)
    {
        Email = email;
        Role = role;
        PasswordHash = passwordHash;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void SetPassword(string passwordHash)
    {
        PasswordHash = passwordHash;
    }
}