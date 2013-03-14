using System;

namespace Lokad.Cqrs.Core.Dispatch.Events
{
    [Serializable]
    public sealed class DispatchingMessage : ISystemEvent
    {                
        public Type MessageType { get; private set; }
        public Type Handler { get; private set; }

        public DispatchingMessage(ImmutableEnvelope envelope, Type messageType, Type handler)
        {
            MessageType = messageType;            
            Handler = handler;
        }

        public override string ToString()
        {            
            return string.Format("Dispatching message '{0}' on '{1}'.", MessageType, Handler);
        }
    }
}