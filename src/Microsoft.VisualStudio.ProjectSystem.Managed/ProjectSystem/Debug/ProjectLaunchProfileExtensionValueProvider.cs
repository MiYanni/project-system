﻿// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.Build.Framework.XamlTypes;
using Microsoft.VisualStudio.ProjectSystem.Properties;

namespace Microsoft.VisualStudio.ProjectSystem.Debug
{
    /// <summary>
    /// <para>
    /// Reads and writes "extension" properties in the given <see cref="ILaunchProfile"/>.
    /// </para>
    /// <para>"Extension" means properties that are stored in the <see cref="ILaunchProfile.OtherSettings"/>
    /// dictionary, rather than in named properties of <see cref="ILaunchProfile"/>
    /// itself. Those are handled by <see cref="LaunchProfileProjectProperties"/>.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Not to be confused with <see cref="ActiveLaunchProfileExtensionValueProvider" />,
    /// which serves a very similar purpose but reads and writes the _active_ profile
    /// rather than a particular one, and will go away once the Launch Profiles UI is up
    /// and running.
    /// </remarks>
    [ExportLaunchProfileExtensionValueProvider(
        new[]
        {
            AuthenticationModePropertyName,
            NativeDebuggingPropertyName,
            RemoteDebugEnabledPropertyName,
            RemoteDebugMachinePropertyName,
            SqlDebuggingPropertyName
        },
        ExportLaunchProfileExtensionValueProviderScope.LaunchProfile)]
    internal class ProjectLaunchProfileExtensionValueProvider : ILaunchProfileExtensionValueProvider
    {
        internal const string AuthenticationModePropertyName = "AuthenticationMode";
        internal const string NativeDebuggingPropertyName = "NativeDebugging";
        internal const string RemoteDebugEnabledPropertyName = "RemoteDebugEnabled";
        internal const string RemoteDebugMachinePropertyName = "RemoteDebugMachine";
        internal const string SqlDebuggingPropertyName = "SqlDebugging";

        // The CPS property system will map "true" and "false" to the localized versions of
        // "Yes" and "No" for display purposes, but not other casings like "True" and
        // "False". To ensure consistency we need to map booleans to these constants.
        private const string True = "true";
        private const string False = "false";

        public Task<string> OnGetPropertyValueAsync(string propertyName, ILaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            string propertyValue = propertyName switch
            {
                AuthenticationModePropertyName => GetOtherProperty(launchProfile, LaunchProfileExtensions.RemoteAuthenticationModeProperty, string.Empty),
                NativeDebuggingPropertyName => GetOtherProperty(launchProfile, LaunchProfileExtensions.NativeDebuggingProperty, false) ? True : False,
                RemoteDebugEnabledPropertyName => GetOtherProperty(launchProfile, LaunchProfileExtensions.RemoteDebugEnabledProperty, false) ? True : False,
                RemoteDebugMachinePropertyName => GetOtherProperty(launchProfile, LaunchProfileExtensions.RemoteDebugMachineProperty, string.Empty),
                SqlDebuggingPropertyName => GetOtherProperty(launchProfile, LaunchProfileExtensions.SqlDebuggingProperty, false) ? True : False,

                _ => throw new InvalidOperationException($"{nameof(ProjectLaunchProfileExtensionValueProvider)} does not handle property '{propertyName}'.")
            };

            return Task.FromResult(propertyValue);
        }

        public Task OnSetPropertyValueAsync(string propertyName, string propertyValue, IWritableLaunchProfile launchProfile, ImmutableDictionary<string, object> globalSettings, Rule? rule)
        {
            switch (propertyName)
            {
                case AuthenticationModePropertyName:
                    TrySetOtherProperty(launchProfile, LaunchProfileExtensions.RemoteAuthenticationModeProperty, propertyValue, string.Empty);
                    break;

                case NativeDebuggingPropertyName:
                    TrySetOtherProperty(launchProfile, LaunchProfileExtensions.NativeDebuggingProperty, bool.Parse(propertyValue), false);
                    break;

                case RemoteDebugEnabledPropertyName:
                    TrySetOtherProperty(launchProfile, LaunchProfileExtensions.RemoteDebugEnabledProperty, bool.Parse(propertyValue), false);
                    break;

                case RemoteDebugMachinePropertyName:
                    TrySetOtherProperty(launchProfile, LaunchProfileExtensions.RemoteDebugMachineProperty, propertyValue, string.Empty);
                    break;

                case SqlDebuggingPropertyName:
                    TrySetOtherProperty(launchProfile, LaunchProfileExtensions.SqlDebuggingProperty, bool.Parse(propertyValue), false);
                    break;

                default:
                    throw new InvalidOperationException($"{nameof(ProjectLaunchProfileExtensionValueProvider)} does not handle property '{propertyName}'.");
            }

            return Task.CompletedTask;
        }

        private static T GetOtherProperty<T>(ILaunchProfile launchProfile, string propertyName, T defaultValue)
        {
            if (launchProfile.OtherSettings is null)
            {
                return defaultValue;
            }

            if (launchProfile.OtherSettings.TryGetValue(propertyName, out object? value) &&
                value is T b)
            {
                return b;
            }
            else if (value is string s &&
                TypeDescriptor.GetConverter(typeof(T)) is TypeConverter converter &&
                converter.CanConvertFrom(typeof(string)))
            {
                try
                {
                    if (converter.ConvertFromString(s) is T o)
                    {
                        return o;
                    }
                }
                catch (Exception)
                {
                    // ignore bad data in the json file and just let them have the default value
                }
            }

            return defaultValue;
        }

        private static bool TrySetOtherProperty<T>(IWritableLaunchProfile launchProfile, string propertyName, T value, T defaultValue) where T : notnull
        {
            if (!launchProfile.OtherSettings.TryGetValue(propertyName, out object current))
            {
                current = defaultValue;
            }

            if (current is not T currentTyped || !Equals(currentTyped, value))
            {
                launchProfile.OtherSettings[propertyName] = value;
                return true;
            }

            return false;
        }
    }
}