﻿#region (c) 2010-2011 Lokad CQRS - New BSD License 

// Copyright (c) Lokad SAS 2010-2011 (http://www.lokad.com)
// This code is released as Open Source under the terms of the New BSD Licence
// Homepage: http://lokad.github.com/lokad-cqrs/

#endregion

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Transactions;
using Lokad.Cqrs.Core.Dispatch.Events;
using Lokad.Cqrs.Core.Outbox;
using Lokad.Cqrs.Evil;

namespace Lokad.Cqrs.Feature.HandlerClasses
{
    public enum ContainerScopeLevel
    {
        Root,
        Envelope,
        Item
    }

    public sealed class DispatchStrategy
    {
        readonly IContainerForHandlerClasses _scope;
        readonly HandlerClassTransactionFactory _scopeFactory;
        readonly Func<Type, Type, MethodInfo> _hint;
        readonly IMethodContextManager _context;
        readonly ISystemObserver _observer;

        public DispatchStrategy(IContainerForHandlerClasses scope, HandlerClassTransactionFactory scopeFactory,
            Func<Type, Type, MethodInfo> hint, IMethodContextManager context, ISystemObserver observer)
        {
            _scope = scope;
            _scopeFactory = scopeFactory;
            _hint = hint;
            _context = context;
            _observer = observer;
        }

        public void Dispatch(ImmutableEnvelope envelope, IEnumerable<Tuple<Type, ImmutableMessage>> pairs)
        {
            using (var tx = _scopeFactory(envelope))
            using (var envelopeScope = _scope.GetChildContainer(ContainerScopeLevel.Envelope))
            {
                foreach (var tuple in pairs)
                {
                    using (var itemScope = envelopeScope.GetChildContainer(ContainerScopeLevel.Item))
                    {
                        var handlerType = tuple.Item1;
                        object handlerInstance;
                        try
                        {
                            handlerInstance = itemScope.ResolveHandlerByServiceType(handlerType);
                        }
                        catch(Exception ex)
                        {
                            var msg = string.Format("Failed to resolve handler {0} from {1}. ", handlerType,
                                itemScope.GetType().Name);
                            throw new InvalidOperationException(msg, ex);
                        }
                        
                        var messageItem = tuple.Item2;
                        var consume = _hint(handlerType, messageItem.MappedType);
                        try
                        {
                            _context.SetContext(envelope, messageItem);
                            _observer.Notify(new DispatchingMessage(envelope, messageItem.MappedType, handlerType));
                            consume.Invoke(handlerInstance, new[] {messageItem.Content});
                        }
                        catch (TargetInvocationException e)
                        {
                            throw InvocationUtil.Inner(e);
                        }
                        finally
                        {
                            _context.ClearContext();
                        }
                    }
                }
                tx.Complete();
            }
        }
    }
}