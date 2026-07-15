using System;
using System.Collections.Generic;
using System.Text;

namespace Dsw2026Tpi.Domain.Interfaces
{
    public interface ISoftDeletable
    {
        bool IsActive { get; }
        void Deactivate();
    }
}
