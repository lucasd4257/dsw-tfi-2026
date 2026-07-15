using Dsw2026Tpi.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities;

public class AvailabilityRule : EntityBase, ISoftDeletable
{
    public Guid DoctorId { get; init; }
    public Doctor? Doctor { get; private set; }
    public int Month { get; init; }
    public int Year { get; init; }
    public string DayOfWeek { get; init; }
    public TimeSpan StartTime { get; init; }
    public TimeSpan EndTime { get; init; }
    public bool IsActive { get; private set; }

    #region Constructor for EF
#pragma warning disable CS8618
    private AvailabilityRule() { }
#pragma warning restore CS8618
    #endregion

    public AvailabilityRule(Guid doctorId, int month, int year, string dayOfWeek, TimeSpan startTime, TimeSpan endTime, Guid? id = null) : base(id)
    {
        DoctorId = doctorId;
        Month = month;
        Year = year;
        DayOfWeek = dayOfWeek;
        StartTime = startTime;
        EndTime = endTime;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}