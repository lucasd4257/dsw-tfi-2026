using Dsw2026Tpi.Domain.Interfaces;

namespace Dsw2026Tpi.Domain.Entities;

public class Speciality: EntityBase, ISoftDeletable
{
    public string Name { get; init; }
    public string Description { get; init; }
    public bool IsActive { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private Speciality() { }
#pragma warning restore CS8618
    #endregion

    public Speciality(string name, string description, Guid? id = null) : base(id)
    {
        Name = name;
        Description = description;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
