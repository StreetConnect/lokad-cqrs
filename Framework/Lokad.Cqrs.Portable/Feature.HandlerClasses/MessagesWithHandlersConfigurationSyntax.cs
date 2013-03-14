#region (c) 2010-2011 Lokad CQRS - New BSD License 

// Copyright (c) Lokad SAS 2010-2011 (http://www.lokad.com)
// This code is released as Open Source under the terms of the New BSD Licence
// Homepage: http://lokad.github.com/lokad-cqrs/

#endregion

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Transactions;
using Lokad.Cqrs.Core;

// ReSharper disable UnusedMember.Global

namespace Lokad.Cqrs.Feature.HandlerClasses
{
    /// <summary>
    /// Module for building CQRS domains.
    /// </summary>
    public class MessagesWithHandlersConfigurationSyntax : HideObjectMembersFromIntelliSense
    {
        readonly DomainAssemblyScanner _scanner = new DomainAssemblyScanner();
        IMethodContextManager _contextManager;
        MethodInvokerHint _hint;
        HandlerClassTransactionFactory _scopeFactory;

        readonly BuildsContainerForMessageHandlerClasses _nestedResolver;

        /// <summary>
        /// Initializes a new instance of the <see cref="MessagesWithHandlersConfigurationSyntax"/> class.
        /// </summary>
        public MessagesWithHandlersConfigurationSyntax(BuildsContainerForMessageHandlerClasses factory)
        {
            HandlerSample<IHandle<IMessage>>(a => a.Handle(null));
            ContextFactory(
                (envelope, message) => new MessageContext(envelope.EnvelopeId, message.Index, envelope.CreatedOnUtc));

            _nestedResolver = factory;
            _scopeFactory = Factory(TransactionScopeOption.RequiresNew);
        }


        /// <summary>
        /// Allows to specify custom context factory to expose transport-level
        /// information to message handlers via IoC. By default this is configured
        /// as <see cref="Func{T}"/> returning <see cref="MessageContext"/>
        /// </summary>
        /// <typeparam name="TContext">The type of the context to return.</typeparam>
        /// <param name="contextFactory">The context factory.</param>
        public void ContextFactory<TContext>(Func<ImmutableEnvelope, ImmutableMessage, TContext> contextFactory)
            where TContext : class
        {
            if (contextFactory == null) throw new ArgumentNullException("contextFactory");
            var instance = new MethodContextManager<TContext>(contextFactory);
            _contextManager = instance;
        }

        /// <summary>
        /// Allows to specify custom context manager
        /// </summary>
        /// <param name="contextManager">The context manager.</param>
        public void ContextManager(IMethodContextManager contextManager)
        {
            _contextManager = contextManager;
        }

        /// <summary>
        /// Specifies expression describing your interface lookup rules for handlers and messages.
        /// Defaults to <code><![CDATA[HandlerSample<IConsume<IMessage>>(h => h.Consume(null))]]></code>
        /// </summary>
        /// <typeparam name="THandler">The base type of the handler.</typeparam>
        /// <param name="handlerSampleExpression">The handler sample expression.</param>
        public void HandlerSample<THandler>(Expression<Action<THandler>> handlerSampleExpression)
        {
            if (handlerSampleExpression == null) throw new ArgumentNullException("handlerSampleExpression");
            _hint = MethodInvokerHint.FromConsumerSample(handlerSampleExpression);
        }

        /// <summary>ss
        /// Sets the default transaction scope factory (to be used in handler classes).
        /// </summary>
        /// <param name="factory">The factory of transaction scopes.</param>
        public void SetTransactionScope(HandlerClassTransactionFactory factory)
        {
            _scopeFactory = factory;
        }


        /// <summary>
        /// Includes assemblies of the specified types into the discovery process
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns>same module instance for chaining fluent configurations</returns>
        public void InAssemblyOf<T>()
        {
            _scanner.WithAssemblyOf<T>();
        }

        public void InAssemblyOf(object instance)
        {
            _scanner.WithAssembly(instance.GetType().Assembly);
        }

        public void WhereMessages(Predicate<Type> constraint)
        {
            _scanner.WhereMessages(constraint);
        }

        public void WhereMessagesAre<T>()
        {
            _scanner.WhereMessages(t => typeof(T).IsAssignableFrom(t));
        }

        public IEnumerable<Type> LookupMessages()
        {
            _scanner.Constrain(_hint);
            var mappings = _scanner.Build(_hint.ConsumerTypeDefinition);

            return mappings
                .Select(m => m.Message)
                .Where(m => !m.IsAbstract)
                .Distinct();
        }

        public void Configure(Container container)
        {
            _scanner.Constrain(_hint);
            var mappings = _scanner.Build(_hint.ConsumerTypeDefinition);
            var builder = new MessageDirectoryBuilder(mappings);

            _contextManager.RegisterContextProvider(container);

            var consumers = mappings
                .Select(x => x.Consumer)
                .Where(x => !x.IsAbstract)
                .Distinct()
                .ToArray();

            var nesting = _nestedResolver(container, consumers);
            var strategy = new DispatchStrategy(nesting, _scopeFactory, _hint.Lookup, _contextManager, container.Resolve<ISystemObserver>());

            container.Register(strategy);
            container.Register(builder);
        }

        static HandlerClassTransactionFactory Factory(TransactionScopeOption option,
            IsolationLevel level = IsolationLevel.Serializable, TimeSpan timeout = default(TimeSpan))
        {
            if (timeout == (default(TimeSpan)))
            {
                timeout = TimeSpan.FromMinutes(10);
            }
            return envelope => new TransactionScope(option, new TransactionOptions
                {
                    IsolationLevel = level,
                    Timeout = Debugger.IsAttached ? TimeSpan.MaxValue : timeout
                });
        }
    }
}