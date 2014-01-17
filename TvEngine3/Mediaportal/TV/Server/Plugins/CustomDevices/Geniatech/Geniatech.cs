﻿#region Copyright (C) 2005-2011 Team MediaPortal

// Copyright (C) 2005-2011 Team MediaPortal
// http://www.team-mediaportal.com
// 
// MediaPortal is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 2 of the License, or
// (at your option) any later version.
// 
// MediaPortal is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with MediaPortal. If not, see <http://www.gnu.org/licenses/>.

#endregion

using System;
using System.Runtime.InteropServices;
using DirectShowLib;
using DirectShowLib.BDA;
using Mediaportal.TV.Server.TVLibrary.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Implementations.Channels;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Interfaces;
using Mediaportal.TV.Server.TVLibrary.Interfaces.Logging;
using Mediaportal.TV.Server.TVLibrary.Interfaces.TunerExtension;

namespace Mediaportal.TV.Server.Plugins.TunerExtension.Geniatech
{
  /// <summary>
  /// A class for handling DiSEqC and DVB-S2 tuning for Geniatech tuners.
  /// </summary>
  public class Geniatech : Conexant.Conexant, IPowerDevice
  {
    #region enums

    private new enum BdaExtensionProperty
    {
      /// For sending and receiving DiSEqC messages.
      DiseqcMessage = 0,
      /// For initialising DiSEqC interfaces.
      DiseqcInit,
      /// Unsupported generic Conexant property.
      ScanFrequency,
      /// For direct/custom tuning.
      ChannelChange,
      /// For retrieving demodulator firmware state and version.
      DemodInfo,
      /// Unsupported generic Conexant property.
      EffectiveFrequency,
      /// For retrieving signal quality, strength, BER and other attributes.
      SignalStatus,
      /// For retrieving demodulator lock indicators.
      LockStatus,
      /// For controlling error correction and BER window.
      ErrorControl,
      /// For retrieving the locked values of frequency, symbol rate etc. after fine tuning.
      ChannelInfo,
      /// For setting DVB-S2 parameters that could not initially be set through BDA interfaces.
      NbcParams,
      /// For controlling the LNB power supply state.
      LnbPower
    }

    private enum GtPilot
    {
      Off = 0,
      On,
      Unknown               // (Not used...)
    }

    private enum GtRollOff
    {
      Undefined = 0xff,
      Twenty = 0,           // 0.2
      TwentyFive,           // 0.25
      ThirtyFive            // 0.35
    }

    #endregion

    #region structs

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct NbcParams
    {
      public GtRollOff RollOff;
      public GtPilot Pilot;
    }

    #endregion

    #region constants

    private static readonly int NBC_PARAMS_SIZE = Marshal.SizeOf(typeof(NbcParams));    // 8
    private static readonly int PARAM_BUFFER_SIZE = NBC_PARAMS_SIZE;

    #endregion

    #region variables

    private bool _isGeniatech = false;
    private IntPtr _paramBuffer = IntPtr.Zero;

    #endregion

    #region ICustomDevice members

    /// <summary>
    /// The loading priority for this extension.
    /// </summary>
    public override byte Priority
    {
      get
      {
        return 50;
      }
    }

    /// <summary>
    /// Attempt to initialise the extension-specific interfaces used by the class. If
    /// initialisation fails, the <see ref="ICustomDevice"/> instance should be disposed
    /// immediately.
    /// </summary>
    /// <param name="tunerExternalIdentifier">The external identifier for the tuner.</param>
    /// <param name="tunerType">The tuner type (eg. DVB-S, DVB-T... etc.).</param>
    /// <param name="context">Context required to initialise the interface.</param>
    /// <returns><c>true</c> if the interfaces are successfully initialised, otherwise <c>false</c></returns>
    public override bool Initialise(string tunerExternalIdentifier, CardType tunerType, object context)
    {
      this.LogDebug("Geniatech: initialising");

      if (_isGeniatech)
      {
        this.LogWarn("Geniatech: extension already initialised");
        return true;
      }

      bool result = base.Initialise(tunerExternalIdentifier, tunerType, context);
      if (!result)
      {
        this.LogDebug("Geniatech: base Conexant interface not supported");
        return false;
      }

      KSPropertySupport support;
      int hr = _propertySet.QuerySupported(_propertySetGuid, (int)BdaExtensionProperty.NbcParams, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Set))
      {
        this.LogDebug("Geniatech: NBC parameter property not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
        return false;
      }

      this.LogInfo("Geniatech: extension supported");
      _isGeniatech = true;
      _paramBuffer = Marshal.AllocCoTaskMem(PARAM_BUFFER_SIZE);
      return true;
    }

    #region device state change call backs

    /// <summary>
    /// This call back is invoked before a tune request is assembled.
    /// </summary>
    /// <param name="tuner">The tuner instance that this extension instance is associated with.</param>
    /// <param name="currentChannel">The channel that the tuner is currently tuned to..</param>
    /// <param name="channel">The channel that the tuner will been tuned to.</param>
    /// <param name="action">The action to take, if any.</param>
    public override void OnBeforeTune(ITVCard tuner, IChannel currentChannel, ref IChannel channel, out TunerAction action)
    {
      this.LogDebug("Geniatech: on before tune call back");
      action = TunerAction.Default;

      if (!_isGeniatech)
      {
        this.LogWarn("Geniatech: not initialised or interface not supported");
        return;
      }

      // We only have work to do if the channel is a DVB-S/2 channel.
      DVBSChannel ch = channel as DVBSChannel;
      if (ch == null)
      {
        return;
      }

      if (ch.ModulationType == ModulationType.ModQpsk)
      {
        ch.ModulationType = ModulationType.ModNbcQpsk;
      }
      else if (ch.ModulationType == ModulationType.Mod8Psk)
      {
        ch.ModulationType = ModulationType.ModNbc8Psk;
      }
      this.LogDebug("  modulation = {0}", ch.ModulationType);

      NbcParams nbcParams = new NbcParams();
      if (ch.Pilot == Pilot.On)
      {
        nbcParams.Pilot = GtPilot.On;
      }
      else
      {
        nbcParams.Pilot = GtPilot.Off;
      }
      this.LogDebug("  pilot      = {0}", nbcParams.Pilot);

      if (ch.RollOff == RollOff.Twenty)
      {
        nbcParams.RollOff = GtRollOff.Twenty;
      }
      else if (ch.RollOff == RollOff.TwentyFive)
      {
        nbcParams.RollOff = GtRollOff.TwentyFive;
      }
      else if (ch.RollOff == RollOff.ThirtyFive)
      {
        nbcParams.RollOff = GtRollOff.ThirtyFive;
      }
      else
      {
        nbcParams.RollOff = GtRollOff.Undefined;
      }
      this.LogDebug("  roll-off   = {0}", nbcParams.RollOff);

      Marshal.StructureToPtr(nbcParams, _paramBuffer, true);
      int hr = _propertySet.Set(_propertySetGuid, (int)BdaExtensionProperty.NbcParams,
        _instanceBuffer, INSTANCE_SIZE,
        _paramBuffer, NBC_PARAMS_SIZE
      );
      if (hr != (int)HResult.Severity.Success)
      {
        this.LogError("Geniatech: failed to set pilot and roll-off, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }
    }

    #endregion

    #endregion

    #region IPowerDevice member

    /// <summary>
    /// Set the tuner power state.
    /// </summary>
    /// <param name="state">The power state to apply.</param>
    /// <returns><c>true</c> if the power state is set successfully, otherwise <c>false</c></returns>
    public bool SetPowerState(PowerState state)
    {
      this.LogDebug("Geniatech: set power state, state = {0}", state);

      if (!_isGeniatech)
      {
        this.LogWarn("Geniatech: not initialised or interface not supported");
        return false;
      }

      KSPropertySupport support;
      int hr = _propertySet.QuerySupported(_propertySetGuid, (int)BdaExtensionProperty.LnbPower, out support);
      if (hr != (int)HResult.Severity.Success || !support.HasFlag(KSPropertySupport.Set))
      {
        this.LogDebug("Geniatech: LNB power property not supported, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      }

      if (state == PowerState.On)
      {
        Marshal.WriteInt32(_paramBuffer, 0, 1);
      }
      else
      {
        Marshal.WriteInt32(_paramBuffer, 0, 0);
      }
      hr = _propertySet.Set(_propertySetGuid, (int)BdaExtensionProperty.LnbPower,
        _instanceBuffer, INSTANCE_SIZE,
        _paramBuffer, sizeof(int)
      );
      if (hr == (int)HResult.Severity.Success)
      {
        this.LogDebug("Geniatech: result = success");
        return true;
      }

      this.LogError("Geniatech: result = failure, hr = 0x{0:x} ({1})", hr, HResult.GetDXErrorString(hr));
      return false;
    }

    #endregion

    #region IDisposable member

    /// <summary>
    /// Release and dispose all resources.
    /// </summary>
    public override void Dispose()
    {
      base.Dispose();

      if (_paramBuffer != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(_paramBuffer);
        _paramBuffer = IntPtr.Zero;
      }
      _isGeniatech = false;
    }

    #endregion
  }
}