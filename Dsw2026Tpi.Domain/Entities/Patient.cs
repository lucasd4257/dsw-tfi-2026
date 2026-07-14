using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class Patient : EntityBase
{
    public Guid UserId { get; init; }
    public User? User { get; private set; }
    public string Dni { get; init; }
    public string FullName { get; init; }
    public bool IsActive { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private Patient() { }
#pragma warning restore CS8618
    #endregion

    public Patient(Guid userId, string dni, string fullName, Guid? id = null) : base(id)
    {
        UserId = userId;
        Dni = dni;
        FullName = fullName;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
