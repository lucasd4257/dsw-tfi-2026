using Dsw2026Tpi.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Patient : EntityBase, ISoftDeletable
{
    // FK "lógica" hacia ApplicationUser (Identity), que vive en AuthenticationDbContext.
    public string ApplicationUserId { get; init; }
    public string Dni { get; init; }
    public string FullName { get; init; }
    public bool IsActive { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private Patient() { }
#pragma warning restore CS8618
    #endregion

    public Patient(string applicationUserId, string dni, string fullName, Guid? id = null) : base(id)
    {
        ApplicationUserId = applicationUserId;
        Dni = dni;
        FullName = fullName;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}