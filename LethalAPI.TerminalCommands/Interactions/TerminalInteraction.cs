﻿// -----------------------------------------------------------------------
// <copyright file="TerminalInteraction.cs" company="LethalAPI Modding Community">
// Copyright (c) LethalAPI Modding Community. All rights reserved.
// Licensed under the LGPL-3.0 license.
// </copyright>
// -----------------------------------------------------------------------

namespace LethalAPI.TerminalCommands.Interactions;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Interfaces;
using Models;
using UnityEngine;

/// <summary>
/// A basic terminal interaction, supporting custom services/context, and null cascading handlers.
/// </summary>
/// <remarks>
/// You can return this interaction from a Terminal Command to implement follow-ups/prompt the user for more information.
/// </remarks>
public class TerminalInteraction : ITerminalInteraction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalInteraction"/> class.
    /// Creates a blank terminal interaction.
    /// </summary>
    public TerminalInteraction()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalInteraction"/> class.
    /// Creates an interaction with the specified prompt and handler.
    /// </summary>
    /// <param name="prompt">The response/prompt shown to the user.</param>
    /// <param name="handler">The handler to receive the next terminal input.</param>
    public TerminalInteraction(TerminalNode prompt, Delegate handler)
    {
        Prompt = prompt;
        Handlers.Add(handler);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TerminalInteraction"/> class.
    /// Creates an interaction with the specified prompt and handler.
    /// </summary>
    /// <param name="promptBuilder">Builder method to create the <see cref="TerminalNode"/> interaction prompt.</param>
    /// <param name="handler">The handler to receive the next terminal input.</param>
    public TerminalInteraction(Action<TerminalNode> promptBuilder, Delegate handler)
    {
        TerminalNode prompt = ScriptableObject.CreateInstance<TerminalNode>();
        promptBuilder(prompt);

        WithPrompt(prompt);

        Handlers.Add(handler);
    }

    /// <summary>
    /// Gets the prompt displayed to the user.
    /// </summary>
    public TerminalNode Prompt { get; private set; }

    /// <summary>
    /// Gets the service collection containing the context for the handlers.
    /// </summary>
    public ServiceCollection Services { get; } = new ServiceCollection();

    /// <summary>
    /// Gets a list of interaction handlers.
    /// </summary>
    public List<Delegate> Handlers { get; } = new List<Delegate>()

    /// <summary>
    /// Adds a number of services to the container used to invoke the handlers.
    /// </summary>
    /// <remarks>
    /// This allows you to inject state into the parameters of the delegate handlers. Allowing you to pass state from the parent command to the handler.
    /// </remarks>
    /// <param name="services">Services to add to the container.</param>
    /// <returns>Parent terminal interaction.</returns>
    public TerminalInteraction WithContext(params object[] services)
    {
        Services.WithServices(services);
        return this;
    }

    /// <summary>
    /// Sets the command response. This is the response message to prompt the user for further input.
    /// </summary>
    /// <param name="prompt">The <see cref="TerminalNode"/> representing the command response.</param>
    /// <returns>Parent terminal interaction.</returns>
    public TerminalInteraction WithPrompt(TerminalNode prompt)
    {
        Prompt = prompt;
        return this;
    }

    /// <summary>
    /// Sets the command response. This is the response message to prompt the user for further input.
    /// </summary>
    /// <param name="promptBuilder">Builder method to create the <see cref="TerminalNode"/> interaction prompt.</param>
    /// <returns>Parent terminal interaction.</returns>
    public TerminalInteraction WithPrompt(Action<TerminalNode> promptBuilder)
    {
        TerminalNode prompt = ScriptableObject.CreateInstance<TerminalNode>();
        promptBuilder(prompt);

        WithPrompt(prompt);

        return this;
    }

    /// <summary>
    /// Sets the command response. This is the response message to prompt the user for further input.
    /// </summary>
    /// <param name="prompt">
    /// The prompt to show players.
    /// </param>
    /// <returns>Parent terminal interaction.</returns>
    public TerminalInteraction WithPrompt(string prompt)
    {
        Prompt = ScriptableObject.CreateInstance<TerminalNode>();
        Prompt.WithDisplayText(prompt);

        return this;
    }

    /// <summary>
    /// Adds an interaction handler for the interaction.
    /// </summary>
    /// <remarks>
    /// The handler is in the same format and layout as a Terminal Command method.
    /// </remarks>
    /// <param name="handler">Handler delegate, in the same format/layout as a terminal command.</param>
    /// <returns>Parent terminal interaction.</returns>
    public TerminalInteraction WithHandler(Delegate handler)
    {
        Handlers.Add(handler);
        return this;
    }

    /// <summary>
    /// Handles execution of the terminal interaction. Attempting to execute all registered handlers in descending order of parameter count.
    /// </summary>
    /// <param name="arguments">User provided arguments.</param>
    /// <returns>Object representing the response of this interaction, or <see langword="null"/> if execution should fall through to the parent interaction, or command handler.</returns>
    public object HandleTerminalResponse(ArgumentStream arguments)
    {
        // Converts all delegates into a list of Method Info and instances, ordered descending by parameter count (execution order)
        List<(MethodInfo info, object instance)> handlers =
            Handlers
                .Select(x => (info: x.GetMethodInfo(), instance: x.Target))
                .OrderByDescending(x => x.info.GetParameters().Length)
                .ToList();

        foreach (Delegate handler in Handlers)
        {
            MethodInfo method = handler.GetMethodInfo();

            arguments.Reset();
            if (CommandActivator.TryCreateInvoker(arguments, Services, method, out Func<object, object> invoker))
            {
                object result = invoker(handler.Target);

                if (result != null)
                {
                    return result;
                }
            }
        }

        return null;
    }
}