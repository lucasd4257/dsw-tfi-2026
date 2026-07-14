using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class AvailabilitySlot : EntityBase
{
    public Guid AvailabilityRuleId { get; init; }
    public AvailabilityRule? AvailabilityRule { get; private set; }
    public DateTime SlotDate { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public string Status { get; private set; } 
    public bool IsActive { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private AvailabilitySlot() { }
#pragma warning restore CS8618
    #endregion

    public AvailabilitySlot(Guid availabilityRuleId, DateTime slotDate, TimeSpan startTime, TimeSpan endTime, string status = "AVAILABLE", Guid? id = null) : base(id)
    {
        AvailabilityRuleId = availabilityRuleId;
        SlotDate = slotDate;
        StartTime = startTime;
        EndTime = endTime;
        Status = status;
        IsActive = true;
    }

    public void ChangeStatus(string newStatus)
    {
        Status = newStatus;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}