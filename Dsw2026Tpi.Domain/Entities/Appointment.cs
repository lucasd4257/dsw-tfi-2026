using Dsw2026Tpi.Domain.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Entities
{
    public class Appointment : EntityBase, ISoftDeletable
    {
        public Guid AvailabilitySlotId { get; init; }
        public AvailabilitySlot? AvailabilitySlot { get; private set; }
        public Guid PatientId { get; init; }
        public Patient? Patient { get; private set; }
        public string Reason { get; init; }
        public AppointmentStatus Status { get; private set; }

        public DateTime? CancelledAt { get; private set; }
        public DateTime? AttendedAt { get; private set; }

        public bool IsActive { get; private set; }

        #region Constructor for EF
#pragma warning disable CS8618
        private Appointment() { }
#pragma warning restore CS8618
        #endregion

        public Appointment(Guid availabilitySlotId, Guid patientId, string reason, Guid? id = null) : base(id)
        {
            AvailabilitySlotId = availabilitySlotId;
            PatientId = patientId;
            Reason = reason;
            Status = AppointmentStatus.BOOKED;
        }

        public void Cancel()
        {
            Status = AppointmentStatus.CANCELLED;
            CancelledAt = DateTime.UtcNow;
        }

        public void MarkAsAttended()
        {
            Status = AppointmentStatus.ATTENDED;
            AttendedAt = DateTime.UtcNow;
        }

        public void MarkAsNoShow()
        {
            Status = AppointmentStatus.NO_SHOW;
        }

        public void Deactivate()
        {
            IsActive = false;
        }
    }
}
