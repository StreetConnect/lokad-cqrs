using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Lokad.Cqrs
{
    /// <summary>
    /// Extra Interface to expose Exception from ISystemEvents.
    /// </summary>
    public interface ISystemEventExeption : ISystemEvent
    {
        Exception Exception { get; }        

    }
}
