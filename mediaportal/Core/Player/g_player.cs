/* 
 *	Copyright (C) 2005-2006 Team MediaPortal
 *	http://www.team-mediaportal.com
 *
 *  This Program is free software; you can redistribute it and/or modify
 *  it under the terms of the GNU General Public License as published by
 *  the Free Software Foundation; either version 2, or (at your option)
 *  any later version.
 *   
 *  This Program is distributed in the hope that it will be useful,
 *  but WITHOUT ANY WARRANTY; without even the implied warranty of
 *  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 *  GNU General Public License for more details.
 *   
 *  You should have received a copy of the GNU General Public License
 *  along with GNU Make; see the file COPYING.  If not, write to
 *  the Free Software Foundation, 675 Mass Ave, Cambridge, MA 02139, USA. 
 *  http://www.gnu.org/copyleft/gpl.html
 *
 */
using System;
using System.Windows.Forms;
using System.Drawing;
using System.Collections;
using MediaPortal.GUI.Library;
using MediaPortal.Util;
using MediaPortal.Subtitle;

namespace MediaPortal.Player
{
  public class g_Player
  {
    #region enums
    public enum Steps : int
    {
      Hourm3 = -3 * 60 * 60,
      Hourm2 = -2 * 60 * 60,
      Minm90 = -90 * 60,
      Hourm1 = -60 * 60,
      Minm45 = -45 * 60,
      Minm30 = -30 * 60,
      Minm15 = -15 * 60,
      Minm10 = -10 * 60,
      Minm7 = -7 * 60,
      Minm5 = -5 * 60,
      Minm3 = -3 * 60,
      Minm1 = -1 * 60,
      Secm45 = -45,
      Secm30 = -30,
      Secm15 = -15,
      Secm5 = -5,
      Sec0 = 0,
      Sec5 = 5,
      Sec15 = 15,
      Sec30 = 30,
      Sec45 = 45,
      Min1 = 1 * 60,
      Min3 = 3 * 60,
      Min5 = 5 * 60,
      Min7 = 7 * 60,
      Min10 = 10 * 60,
      Min15 = 15 * 60,
      Min30 = 30 * 60,
      Min45 = 45 * 60,
      Hour1 = 60 * 60,
      Min90 = 90 * 60,
      Hour2 = 2 * 60 * 60,
      Hour3 = 3 * 60 * 60
    };
    public enum MediaType { Video, TV, Radio, Music, Recording };
    #endregion

    #region variables
    static Steps _currentStep = Steps.Sec0;
    static DateTime _seekTimer = DateTime.MinValue;
    static Player.IPlayer _player = null;
    static Player.IPlayer _prevPlayer = null;
    static SubTitles _subs = null;
    static bool _isInitalized = false;
    static string _currentFilePlaying = "";
    static MediaType _currentMedia;
    static Player.IPlayerFactory _factory;
    static public bool Starting = false;
    static ArrayList _seekStepList = new ArrayList();
    static bool _configLoaded = false;
    #endregion

    #region events
    public delegate void StoppedHandler(MediaType type, int stoptime, string filename);
    public delegate void EndedHandler(MediaType type, string filename);
    public delegate void StartedHandler(MediaType type, string filename);
    static public event StoppedHandler PlayBackStopped;
    static public event EndedHandler PlayBackEnded;
    static public event StartedHandler PlayBackStarted;
    #endregion


    #region ctor/dtor
    // singleton. Dont allow any instance of this class
    private g_Player()
    {
      _factory = new PlayerFactory();
    }
    public static Player.IPlayer Player
    {
      get { return _player; }
    }
    public static Player.IPlayerFactory Factory
    {
      get { return _factory; }
      set { _factory = value; }
    }
    #endregion

    #region Serialisation

    /// <summary>
    /// Read the configuration file to get the skip steps
    /// </summary>
    public static ArrayList LoadSettings()
    {
      //int[] _enabledSkipSteps;// = new int[30];
      ArrayList StepArray = new ArrayList();

      using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
        foreach (string token in (xmlreader.GetValueAsString("movieplayer", "skipsteps", "0;1;1;0;1;1;1;0;1;1;1;0;1;0;1;0").Split(new char[] { ',', ';', ' ' })))
        {
          if (token == string.Empty)
            StepArray.Add(0);
          else
            StepArray.Add(Convert.ToInt32(token));
        }
      _seekStepList = StepArray;
      _configLoaded = true;

      return StepArray;
    }

    #endregion


    #region public members

    //called when current playing file is stopped
    static void OnStopped()
    {
      //check if we're playing
      if (g_Player.Playing && PlayBackStopped != null)
      {
        //yes, then raise event 
        Log.Write("g_Player.OnStopped()");
        PlayBackStopped(_currentMedia, (int)g_Player.CurrentPosition, g_Player.CurrentFile);
      }
    }

    //called when current playing file is stopped
    static void OnEnded()
    {
      //check if we're playing
      if (PlayBackEnded != null)
      {
        //yes, then raise event 
        Log.Write("g_Player.OnEnded()");

        PlayBackEnded(_currentMedia, _currentFilePlaying);
      }
    }
    //called when starting playing a file
    static void OnStarted()
    {
      //check if we're playing
      if (_player == null) return;
      if (_player.Playing)
      {
        //yes, then raise event 
        _currentMedia = MediaType.Music;
        if (_player.IsTV)
        {
          _currentMedia = MediaType.TV;
          if (!_player.IsTimeShifting)
            _currentMedia = MediaType.Recording;
        }
        else if (_player.IsRadio)
        {
          _currentMedia = MediaType.Radio;
        }
        else if (_player.HasVideo)
        {
          if (!Utils.IsAudio(_currentFilePlaying))
          {
            _currentMedia = MediaType.Video;
          }
        }
        Log.Write("g_Player.OnStarted() {0} media:{1}", _currentFilePlaying, _currentMedia.ToString());
        if (PlayBackStarted != null)
          PlayBackStarted(_currentMedia, _currentFilePlaying);
      }
    }

    public static void PauseGraph()
    {
      if (_player != null)
      {
        _player.PauseGraph();
      }
    }

    public static void ContinueGraph()
    {
      if (_player != null)
      {
        _player.ContinueGraph();
      }
    }

    public static void Stop()
    {
      if (_player != null)
      {
        Log.Write("g_Player.Stop()");
        OnStopped();
        GUIGraphicsContext.ShowBackground = true;
        _player.Stop();
        if (GUIGraphicsContext.form != null)
        {
          GUIGraphicsContext.form.Invalidate(true);
        }
        GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYBACK_STOPPED, 0, 0, 0, 0, 0, null);
        GUIWindowManager.SendThreadMessage(msg);

        GUIGraphicsContext.IsFullScreenVideo = false;
        GUIGraphicsContext.IsPlaying = false;
        GUIGraphicsContext.IsPlayingVideo = false;
        CachePlayer();
      }
    }

    static void CachePlayer()
    {
      if (_player.SupportsReplay)
      {
        _prevPlayer = _player;
        _player = null;
      }
      else
      {
        _player.Release();
        _player = null;
        _prevPlayer = null;
      }
    }

    public static void Pause()
    {
      if (_player != null)
      {
        _currentStep = Steps.Sec0;
        _seekTimer = DateTime.MinValue;
        _player.Pause();
        if (VMR9Util.g_vmr9 != null)
        {
          if (_player.Paused) VMR9Util.g_vmr9.SetRepaint();
        }
      }
    }
    public static bool OnAction(Action action)
    {
      if (_player != null)
      {
        return _player.OnAction(action);
      }
      return false;
    }

    public static bool IsDVD
    {
      get
      {
        if (_player == null) return false;
        return _player.IsDVD;
      }
    }

    public static bool IsDVDMenu
    {
      get
      {
        if (_player == null) return false;
        return _player.IsDVDMenu;
      }
    }

    public static bool IsTV
    {
      get
      {
        if (_player == null) return false;
        return _player.IsTV;
      }
    }
    public static bool IsTVRecording
    {
      get
      {
        if (_player == null) return false;
        return (_currentMedia == MediaType.Recording);
      }
    }
    public static bool IsTimeShifting
    {
      get
      {
        if (_player == null) return false;
        return _player.IsTimeShifting;
      }
    }

    public static void Release()
    {
      if (_player != null)
      {
        _player.Stop();
        CachePlayer();
      }
    }
    public static bool PlayDVD()
    {
      return PlayDVD("");
    }

    public static bool PlayDVD(string strPath)
    {
      try
      {
        Starting = true;
        //stop playing radio

        GUIMessage msgRadio = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_RADIO, 0, 0, 0, 0, 0, null);
        GUIWindowManager.SendMessage(msgRadio);

        //stop timeshifting tv
        //GUIMessage msgTv = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_TIMESHIFT, 0, 0, 0, 0, 0, null);
        //GUIWindowManager.SendMessage(msgTv);

        Log.Write("g_Player.PlayDVD()");
        _currentStep = Steps.Sec0;
        _seekTimer = DateTime.MinValue;
        _subs = null;
        if (_player != null)
        {
          GUIGraphicsContext.ShowBackground = true;
          OnStopped();
          _player.Stop();
          CachePlayer();
          GUIGraphicsContext.form.Invalidate(true);
          _player = null;
        }

        if (Utils.PlayDVD())
        {
          return true;
        }
        _isInitalized = true;
        int iUseVMR9 = 0;
        using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
        {
          iUseVMR9 = xmlreader.GetValueAsInt("dvdplayer", "vmr9", 0);
        }

        _player = new DVDPlayer9();
        _player = CachePreviousPlayer(_player);
        bool bResult = _player.Play(strPath);
        if (!bResult)
        {
          Log.WriteFile(Log.LogType.Log, true, "g_Player.PlayDVD():failed to play");
          _player.Release();
          _player = null;
          _subs = null;
          GC.Collect(); GC.Collect(); GC.Collect();
          Log.Write("dvdplayer:bla");
        }
        else if (_player.Playing)
        {
          _isInitalized = false;
          if (!_player.IsTV)
          {
            GUIGraphicsContext.IsFullScreenVideo = true;
            GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO);
          }
          _currentFilePlaying = _player.CurrentFile;
          OnStarted();
          return true;
        }
        Log.Write("dvdplayer:sendmsg");

        //show dialog:unable to play dvd,
        GUIWindowManager.ShowWarning(722, 723, -1);
      }
      finally
      {
        Starting = false;
      }
      return false;
    }

    public static bool PlayAudioStream(string strURL)
    {
      try
      {
        Starting = true;
        //stop radio
        GUIMessage msgRadio = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_RADIO, 0, 0, 0, 0, 0, null);
        GUIWindowManager.SendMessage(msgRadio);

        //stop timeshifting tv
        //GUIMessage msgTv = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_TIMESHIFT, 0, 0, 0, 0, 0, null);
        //GUIWindowManager.SendMessage(msgTv);

        _currentStep = Steps.Sec0;
        _seekTimer = DateTime.MinValue;
        _isInitalized = true;
        _subs = null;
        Log.Write("g_Player.PlayAudioStream({0})", strURL);
        if (_player != null)
        {
          GUIGraphicsContext.ShowBackground = true;
          OnStopped();
          _player.Stop();
          CachePlayer();
          GUIGraphicsContext.form.Invalidate(true);
          _player = null;
        }
        _player = new AudioPlayerWMP9();
        _player = CachePreviousPlayer(_player);

        bool bResult = _player.Play(strURL);
        if (!bResult)
        {
          Log.Write("player:ended");
          _player.Release();
          _player = null;
          _subs = null;
          GC.Collect(); GC.Collect(); GC.Collect();
        }
        else if (_player.Playing)
        {
          _currentFilePlaying = _player.CurrentFile;
          OnStarted();
        }
        _isInitalized = false;
        return bResult;
      }
      finally
      {
        Starting = false;
      }
    }
    //Added by juvinious 19/02/2005
    public static bool PlayVideoStream(string strURL)
    {
      try
      {
        //stop radio
        GUIMessage msgRadio = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_RADIO, 0, 0, 0, 0, 0, null);
        GUIWindowManager.SendMessage(msgRadio);

        Starting = true;
        _currentStep = Steps.Sec0;
        _seekTimer = DateTime.MinValue;
        _isInitalized = true;
        _subs = null;
        Log.Write("g_Player.PlayVideoStream({0})", strURL);
        if (_player != null)
        {
          GUIGraphicsContext.ShowBackground = true;
          OnStopped();
          _player.Stop();
          CachePlayer();
          GUIGraphicsContext.form.Invalidate(true);
          _player = null;
        }
        int iUseVMR9inMYMovies = 0;
        using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
        {
          iUseVMR9inMYMovies = xmlreader.GetValueAsInt("movieplayer", "vmr9", 0);
        }
        if (iUseVMR9inMYMovies == 0)
          _player = new Player.VideoPlayerVMR7();
        else
          _player = new Player.VideoPlayerVMR9();

        _player = CachePreviousPlayer(_player);
        bool bResult = _player.Play(strURL);
        if (!bResult)
        {
          Log.Write("player:ended");
          _player.Release();
          _player = null;
          _subs = null;
          GC.Collect(); GC.Collect(); GC.Collect();
        }
        else if (_player.Playing)
        {
          _currentFilePlaying = _player.CurrentFile;
          OnStarted();
        }
        _isInitalized = false;
        return bResult;
      }
      finally
      {
        Starting = false;
      }
    }
    static IPlayer CachePreviousPlayer(IPlayer newPlayer)
    {
      IPlayer player = newPlayer;
      if (newPlayer != null)
      {
        if (_prevPlayer != null)
        {
          if (_prevPlayer.GetType() == newPlayer.GetType())
          {
            if (_prevPlayer.SupportsReplay)
            {
              player = _prevPlayer;
              _prevPlayer = null;
            }
          }
        }

        if (_prevPlayer != null)
        {
          _prevPlayer.Release();
          _prevPlayer = null;
        }
      }
      return player;
    }

    public static bool Play(string strFile)
    {
      try
      {
        Starting = true;

        //stop radio
        if (!Utils.IsLiveRadio(strFile))
        {
          GUIMessage msgRadio = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_RADIO, 0, 0, 0, 0, 0, null);
          GUIWindowManager.SendMessage(msgRadio);
        }

        if (!Utils.IsLiveTv(strFile) && !Utils.IsLiveRadio(strFile))
        {
          //file is not a live tv file
          //so tell recorder to stop timeshifting live-tv
          //Log.Write("player: file is not live tv, so stop timeshifting:{0}", strFile);
          //GUIMessage msgTv = new GUIMessage(GUIMessage.MessageType.GUI_MSG_RECORDER_STOP_TIMESHIFT, 0, 0, 0, 0, 0, null);
          //GUIWindowManager.SendMessage(msgTv);
        }

        _currentStep = Steps.Sec0;
        _seekTimer = DateTime.MinValue;
        if (strFile == null) return false;
        if (strFile.Length == 0) return false;
        _isInitalized = true;
        _subs = null;
        Log.Write("g_Player.Play({0})", strFile);
        if (_player != null)
        {
          GUIGraphicsContext.ShowBackground = true;
          OnStopped();
          _player.Stop();
          CachePlayer();
          _player = null;
          GC.Collect(); GC.Collect(); GC.Collect(); GC.Collect();
        }
        if (Utils.IsVideo(strFile))
        {
          if (Utils.PlayMovie(strFile))
          {
            _isInitalized = false;
            return false;
          }
          string extension = System.IO.Path.GetExtension(strFile).ToLower();
          if (extension == ".ifo" || extension == ".vob")
          {

            int iUseVMR9 = 0;
            using (MediaPortal.Profile.Settings xmlreader = new MediaPortal.Profile.Settings("MediaPortal.xml"))
            {
              iUseVMR9 = xmlreader.GetValueAsInt("dvdplayer", "vmr9", 0);
            }

            _player = new DVDPlayer9();
            _player = CachePreviousPlayer(_player);
            bool bResult = _player.Play(strFile);
            if (!bResult)
            {
              Log.Write("player:ended");
              _player.Release();
              _player = null;
              _subs = null;
              GC.Collect(); GC.Collect(); GC.Collect();
            }
            else if (_player.Playing)
            {
              _currentFilePlaying = _player.CurrentFile;
              OnStarted();

              _isInitalized = false;
              GUIGraphicsContext.IsFullScreenVideo = true;
              GUIWindowManager.ActivateWindow((int)GUIWindow.Window.WINDOW_FULLSCREEN_VIDEO);
            }
            _isInitalized = false;
            return bResult;
          }
        }
        _player = _factory.Create(strFile);
        if (_player != null)
        {
          _player = CachePreviousPlayer(_player);
          bool bResult = _player.Play(strFile);
          if (!bResult)
          {
            Log.Write("player:ended");
            _player.Release();
            _player = null;
            _subs = null;
            GC.Collect(); GC.Collect(); GC.Collect();
          }
          else if (_player.Playing)
          {
            _currentFilePlaying = _player.CurrentFile;
            OnStarted();
          }
          _isInitalized = false;
          return bResult;
        }
        _isInitalized = false;
      }
      finally
      {
        Starting = false;
      }
      return false;
    }

    public static bool IsExternalPlayer
    {
      get
      {
        if (_player == null) return false;
        return _player.IsExternal;
      }
    }

    public static bool IsRadio
    {
      get
      {
        if (_player == null) return false;
        return (_currentMedia == MediaType.Radio);
      }
    }

    public static bool IsMusic
    {
      get
      {
        if (_player == null) return false;
        return (_currentMedia == MediaType.Music);
      }
    }

    public static bool Playing
    {
      get
      {
        if (_player == null)
        {
          return false;
        }
        if (_isInitalized)
        {
          return false;
        }
        bool bResult = _player.Playing;
        return bResult;
      }
    }

    public static bool Paused
    {
      get
      {
        if (_player == null) return false;
        return _player.Paused;
      }
    }
    public static bool Stopped
    {
      get
      {
        if (_isInitalized) return false;
        if (_player == null) return false;
        bool bResult = _player.Stopped;
        return bResult;
      }
    }

    public static int Speed
    {
      get
      {
        if (_player == null) return 1;
        return _player.Speed;
      }
      set
      {
        if (_player == null) return;
        _player.Speed = value;
        _currentStep = Steps.Sec0;
        _seekTimer = DateTime.MinValue;
      }
    }


    public static string CurrentFile
    {
      get
      {
        if (_player == null) return "";
        return _player.CurrentFile;
      }
    }

    static public int Volume
    {
      get
      {
        if (_player == null) return 0;
        return _player.Volume;
      }
      set
      {
        if (_player != null)
        {
          _player.Volume = value;
        }
      }
    }

    public static Geometry.Type ARType
    {
      get { return GUIGraphicsContext.ARType; }
      set
      {
        if (_player != null)
        {
          _player.ARType = value;
        }
      }
    }

    static public int PositionX
    {
      get
      {
        if (_player == null) return 0;
        return _player.PositionX;
      }
      set
      {
        if (_player != null)
        {
          _player.PositionX = value;
        }
      }
    }

    static public int PositionY
    {
      get
      {
        if (_player == null) return 0;
        return _player.PositionY;
      }
      set
      {
        if (_player != null)
        {
          _player.PositionY = value;
        }
      }
    }

    static public int RenderWidth
    {
      get
      {
        if (_player == null) return 0;
        return _player.RenderWidth;
      }
      set
      {
        if (_player != null)
        {
          _player.RenderWidth = value;
        }
      }
    }
    static public bool Visible
    {
      get
      {
        if (_player == null) return false;
        return _player.Visible;
      }
      set
      {
        if (_player != null)
        {
          _player.Visible = value;
        }
      }
    }
    static public int RenderHeight
    {
      get
      {
        if (_player == null) return 0;
        return _player.RenderHeight;
      }
      set
      {
        if (_player != null)
        {
          _player.RenderHeight = value;
        }
      }
    }

    static public double Duration
    {
      get
      {
        if (_player == null) return 0;
        return _player.Duration;
      }
    }

    static public double CurrentPosition
    {
      get
      {
        if (_player == null) return 0;
        return _player.CurrentPosition;
      }
    }
    static public double ContentStart
    {
      get
      {
        if (_player == null) return 0;
        return _player.ContentStart;
      }
    }

    static public bool FullScreen
    {
      get
      {
        if (_player == null) return GUIGraphicsContext.IsFullScreenVideo;
        return _player.FullScreen;
      }
      set
      {
        if (_player != null)
        {
          _player.FullScreen = value;
        }
      }
    }
    static public int Width
    {
      get
      {
        if (_player == null) return 0;
        return _player.Width;
      }
    }

    static public int Height
    {
      get
      {
        if (_player == null) return 0;
        return _player.Height;
      }
    }
    static public void SeekRelative(double dTime)
    {
      if (_player == null) return;
      _player.SeekRelative(dTime);
      _currentStep = Steps.Sec0;
      _seekTimer = DateTime.MinValue;
      GUIMessage msgUpdate = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYER_POSITION_CHANGED, 0, 0, 0, 0, 0, null);
      GUIGraphicsContext.SendMessage(msgUpdate);

    }

    static public void StepNow()
    {
      if (_currentStep != Steps.Sec0 && _player != null)
      {
        double dTime = (int)_currentStep + _player.CurrentPosition;
        if (dTime < 0) dTime = 0d;
        if (dTime > _player.Duration) dTime = _player.Duration - 5;
        _player.SeekAbsolute(dTime);
        GUIMessage msgUpdate = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYER_POSITION_CHANGED, 0, 0, 0, 0, 0, null);
        GUIGraphicsContext.SendMessage(msgUpdate);
      }
      _currentStep = Steps.Sec0;
      _seekTimer = DateTime.MinValue;

    }

    static public string GetSingleStep(int Step)
    {
      if (Step >= 0)
      {
        if (Step >= 3600)
        {
          if ((Convert.ToSingle(Step) / 3600) > 1 && (Convert.ToSingle(Step) / 3600) != 2)
            return "+ " + Convert.ToString(Step / 60) + " " + GUILocalizeStrings.Get(2998);// "min"
          else
            return "+ " + Convert.ToString(Step / 3600) + " " + GUILocalizeStrings.Get(2997);// "hrs"
        }
        else
          if (Step >= 60)
            return "+ " + Convert.ToString(Step / 60) + " " + GUILocalizeStrings.Get(2998);// "min"
          else
            return "+ " + Convert.ToString(Step) + " " + GUILocalizeStrings.Get(2999);// "sec"
      }
      else // back = negative
      {
        if (Step <= -3600)
        {
          if ((Convert.ToSingle(Step) / 3600) < -1 && (Convert.ToSingle(Step) / 3600) != -2)
            return "- " + Convert.ToString(Math.Abs(Step / 60)) + " " + GUILocalizeStrings.Get(2998);// "min"
          else
            return "- " + Convert.ToString(Math.Abs(Step / 3600)) + " " + GUILocalizeStrings.Get(2997);// "hrs"
        }
        else
          if (Step <= -60)
            return "- " + Convert.ToString(Math.Abs(Step / 60)) + " " + GUILocalizeStrings.Get(2998);// "min"
          else
            return "- " + Convert.ToString(Math.Abs(Step)) + " " + GUILocalizeStrings.Get(2999);// "sec"
      }
    }

    static public string GetStepDescription()
    {
      if (_player == null) return "";
      int m_iTimeToStep = (int)_currentStep;
      if (m_iTimeToStep == 0) return "";
      _player.Process();
      if (_player.CurrentPosition + m_iTimeToStep <= 0) return GUILocalizeStrings.Get(773);// "START"
      if (_player.CurrentPosition + m_iTimeToStep >= _player.Duration) return GUILocalizeStrings.Get(774);// "END"
      switch (_currentStep)
      {
        case Steps.Hourm3: { int i = (int)Steps.Hourm3; return GetSingleStep(i); };
        case Steps.Hourm2: { int i = (int)Steps.Hourm2; return GetSingleStep(i); };
        case Steps.Minm90: { int i = (int)Steps.Minm90; return GetSingleStep(i); };
        case Steps.Hourm1: { int i = (int)Steps.Hourm1; return GetSingleStep(i); };
        case Steps.Minm45: { int i = (int)Steps.Minm45; return GetSingleStep(i); };
        case Steps.Minm30: { int i = (int)Steps.Minm30; return GetSingleStep(i); };
        case Steps.Minm15: { int i = (int)Steps.Minm15; return GetSingleStep(i); };
        case Steps.Minm10: { int i = (int)Steps.Minm10; return GetSingleStep(i); };
        case Steps.Minm7: { int i = (int)Steps.Minm7; return GetSingleStep(i); };
        case Steps.Minm5: { int i = (int)Steps.Minm5; return GetSingleStep(i); };
        case Steps.Minm3: { int i = (int)Steps.Minm3; return GetSingleStep(i); };
        case Steps.Minm1: { int i = (int)Steps.Minm1; return GetSingleStep(i); };
        case Steps.Secm45: { int i = (int)Steps.Secm45; return GetSingleStep(i); };
        case Steps.Secm30: { int i = (int)Steps.Secm30; return GetSingleStep(i); };
        case Steps.Secm15: { int i = (int)Steps.Secm15; return GetSingleStep(i); };
        case Steps.Secm5: { int i = (int)Steps.Secm5; return GetSingleStep(i); };
        case Steps.Sec0: { int i = (int)Steps.Sec0; return GetSingleStep(i); };
        case Steps.Sec5: { int i = (int)Steps.Sec5; return GetSingleStep(i); };
        case Steps.Sec15: { int i = (int)Steps.Sec15; return GetSingleStep(i); };
        case Steps.Sec30: { int i = (int)Steps.Sec30; return GetSingleStep(i); };
        case Steps.Sec45: { int i = (int)Steps.Sec45; return GetSingleStep(i); };
        case Steps.Min1: { int i = (int)Steps.Min1; return GetSingleStep(i); };
        case Steps.Min3: { int i = (int)Steps.Min3; return GetSingleStep(i); };
        case Steps.Min5: { int i = (int)Steps.Min5; return GetSingleStep(i); };
        case Steps.Min7: { int i = (int)Steps.Min7; return GetSingleStep(i); };
        case Steps.Min10: { int i = (int)Steps.Min10; return GetSingleStep(i); };
        case Steps.Min15: { int i = (int)Steps.Min15; return GetSingleStep(i); };
        case Steps.Min30: { int i = (int)Steps.Min30; return GetSingleStep(i); };
        case Steps.Min45: { int i = (int)Steps.Min45; return GetSingleStep(i); };
        case Steps.Hour1: { int i = (int)Steps.Hour1; return GetSingleStep(i); };
        case Steps.Min90: { int i = (int)Steps.Min90; return GetSingleStep(i); };
        case Steps.Hour2: { int i = (int)Steps.Hour2; return GetSingleStep(i); };
        case Steps.Hour3: { int i = (int)Steps.Hour3; return GetSingleStep(i); };
        default: return "Skip";
      }
    }
    static public int GetSeekStep(out bool bStart, out bool bEnd)
    {
      bStart = false;
      bEnd = false;
      if (_player == null) return 0;
      int m_iTimeToStep = (int)_currentStep;
      if (_player.CurrentPosition + m_iTimeToStep <= 0) bStart = true;//start
      if (_player.CurrentPosition + m_iTimeToStep >= _player.Duration) bEnd = true;
      return m_iTimeToStep;
    }
    // "0=5 ;1=15 ;1=30 ;0=45 ;1=1m ;1=3m ;1=5m ;0=7m ;1=10m ;1=15m ;1=30m ;0=45m ;1=1h ;0=90m ;1=2h"
    //   0    1     2     3     4     5     6     7     8      9      10     11     12    13     14
    static public void SeekStep(bool bFF)
    {
      int[] m_seekStep = new int[16];

      if (!_configLoaded)
      {
        _seekStepList = LoadSettings();
        Log.Write("g_Player loading seekstep config {0}", "");// Convert.ToString(_seekStepList[0]));
      }

      for (int i = 0; i < 16; i++)
      {
        m_seekStep[i] = (int)_seekStepList[i];
      }
      if (bFF)
      {
        switch (_currentStep)
        {
          case Steps.Hourm3:
            if (m_seekStep[14] == 1) _currentStep = Steps.Hourm2;
            else goto case Steps.Hourm2; break;
          case Steps.Hourm2:
            if (m_seekStep[13] == 1) _currentStep = Steps.Minm90;
            else goto case Steps.Minm90; break;
          case Steps.Minm90:
            if (m_seekStep[12] == 1) _currentStep = Steps.Hourm1;
            else goto case Steps.Hourm1; break;
          case Steps.Hourm1:
            if (m_seekStep[11] == 1) _currentStep = Steps.Minm45;
            else goto case Steps.Minm45; break;
          case Steps.Minm45:
            if (m_seekStep[10] == 1) _currentStep = Steps.Minm30;
            else goto case Steps.Minm30; break;
          case Steps.Minm30:
            if (m_seekStep[9] == 1) _currentStep = Steps.Minm15;
            else goto case Steps.Minm15; break;
          case Steps.Minm15:
            if (m_seekStep[8] == 1) _currentStep = Steps.Minm10;
            else goto case Steps.Minm10; break;
          case Steps.Minm10:
            if (m_seekStep[7] == 1) _currentStep = Steps.Minm7;
            else goto case Steps.Minm7; break;
          case Steps.Minm7:
            if (m_seekStep[6] == 1) _currentStep = Steps.Minm5;
            else goto case Steps.Minm5; break;
          case Steps.Minm5:
            if (m_seekStep[5] == 1) _currentStep = Steps.Minm3;
            else goto case Steps.Minm3; break;
          case Steps.Minm3:
            if (m_seekStep[4] == 1) _currentStep = Steps.Minm1;
            else goto case Steps.Minm1; break;
          case Steps.Minm1:
            if (m_seekStep[3] == 1) _currentStep = Steps.Secm45;
            else goto case Steps.Secm45; break;
          case Steps.Secm45:
            if (m_seekStep[2] == 1) _currentStep = Steps.Secm30;
            else goto case Steps.Secm30; break;
          case Steps.Secm30:
            if (m_seekStep[1] == 1) _currentStep = Steps.Secm15;
            else goto case Steps.Secm15; break;
          case Steps.Secm15:
            if (m_seekStep[0] == 1) _currentStep = Steps.Secm5;
            else goto case Steps.Secm5; break;

          case Steps.Secm5: _currentStep = Steps.Sec0; break;

          case Steps.Sec0:
            if (m_seekStep[0] == 1) _currentStep = Steps.Sec5;
            else goto case Steps.Sec5; break;
          case Steps.Sec5:
            if (m_seekStep[1] == 1) _currentStep = Steps.Sec15;
            else goto case Steps.Sec15; break;
          case Steps.Sec15:
            if (m_seekStep[2] == 1) _currentStep = Steps.Sec30;
            else goto case Steps.Sec30; break;
          case Steps.Sec30:
            if (m_seekStep[3] == 1) _currentStep = Steps.Sec45;
            else goto case Steps.Sec45; break;
          case Steps.Sec45:
            if (m_seekStep[4] == 1) _currentStep = Steps.Min1;
            else goto case Steps.Min1; break;
          case Steps.Min1:
            if (m_seekStep[5] == 1) _currentStep = Steps.Min3;
            else goto case Steps.Min3; break;
          case Steps.Min3:
            if (m_seekStep[6] == 1) _currentStep = Steps.Min5;
            else goto case Steps.Min5; break;
          case Steps.Min5:
            if (m_seekStep[7] == 1) _currentStep = Steps.Min7;
            else goto case Steps.Min7; break;
          case Steps.Min7:
            if (m_seekStep[8] == 1) _currentStep = Steps.Min10;
            else goto case Steps.Min10; break;
          case Steps.Min10:
            if (m_seekStep[9] == 1) _currentStep = Steps.Min15;
            else goto case Steps.Min15; break;
          case Steps.Min15:
            if (m_seekStep[10] == 1) _currentStep = Steps.Min30;
            else goto case Steps.Min30; break;
          case Steps.Min30:
            if (m_seekStep[11] == 1) _currentStep = Steps.Min45;
            else goto case Steps.Min45; break;
          case Steps.Min45:
            if (m_seekStep[12] == 1) _currentStep = Steps.Hour1;
            else goto case Steps.Hour1; break;
          case Steps.Hour1:
            if (m_seekStep[13] == 1) _currentStep = Steps.Min90;
            else goto case Steps.Min90; break;
          case Steps.Min90:
            if (m_seekStep[14] == 1) _currentStep = Steps.Hour2;
            else goto case Steps.Hour2; break;
          case Steps.Hour2:
            if (m_seekStep[15] == 1) _currentStep = Steps.Hour3;
            else goto case Steps.Hour3; break;
          case Steps.Hour3: break;
        }
      }
      else
      {
        switch (_currentStep)
        {
          // "0=5 ;1=15 ;1=30 ;0=45 ;1=1m ;1=3m ;1=5m ;0=7m ;1=10m ;1=15m ;1=30m ;0=45m ;1=1h ;0=90m ;1=2h"
          //   0    1     2     3     4     5     6     7     8      9      10     11     12    13     14
          case Steps.Hourm3: break;
          case Steps.Hourm2:
            if (m_seekStep[15] == 1) _currentStep = Steps.Hourm3;
            else goto case Steps.Hourm3; break;
          case Steps.Minm90:
            if (m_seekStep[14] == 1) _currentStep = Steps.Hourm2;
            else goto case Steps.Hourm2; break;
          case Steps.Hourm1:
            if (m_seekStep[13] == 1) _currentStep = Steps.Minm90;
            else goto case Steps.Minm90; break;
          case Steps.Minm45:
            if (m_seekStep[12] == 1) _currentStep = Steps.Hourm1;
            else goto case Steps.Hourm1; break;
          case Steps.Minm30:
            if (m_seekStep[11] == 1) _currentStep = Steps.Minm45;
            else goto case Steps.Minm45; break;
          case Steps.Minm15:
            if (m_seekStep[10] == 1) _currentStep = Steps.Minm30;
            else goto case Steps.Minm30; break;
          case Steps.Minm10:
            if (m_seekStep[9] == 1) _currentStep = Steps.Minm15;
            else goto case Steps.Minm15; break;
          case Steps.Minm7:
            if (m_seekStep[8] == 1) _currentStep = Steps.Minm10;
            else goto case Steps.Minm10; break;
          case Steps.Minm5:
            if (m_seekStep[7] == 1) _currentStep = Steps.Minm7;
            else goto case Steps.Minm7; break;
          case Steps.Minm3:
            if (m_seekStep[6] == 1) _currentStep = Steps.Minm5;
            else goto case Steps.Minm5; break;
          case Steps.Minm1:
            if (m_seekStep[5] == 1) _currentStep = Steps.Minm3;
            else goto case Steps.Minm3; break;
          case Steps.Secm45:
            if (m_seekStep[4] == 1) _currentStep = Steps.Minm1;
            else goto case Steps.Minm1; break;
          case Steps.Secm30:
            if (m_seekStep[3] == 1) _currentStep = Steps.Secm45;
            else goto case Steps.Secm45; break;
          case Steps.Secm15:
            if (m_seekStep[2] == 1) _currentStep = Steps.Secm30;
            else goto case Steps.Secm30; break;
          case Steps.Secm5:
            if (m_seekStep[1] == 1) _currentStep = Steps.Secm15;
            else goto case Steps.Secm15; break;
          case Steps.Sec0:
            if (m_seekStep[0] == 1) _currentStep = Steps.Secm5;
            else goto case Steps.Secm5; break;

          case Steps.Sec5: _currentStep = Steps.Sec0; break;

          case Steps.Sec15:
            if (m_seekStep[0] == 1) _currentStep = Steps.Sec5;
            else goto case Steps.Sec5; break;
          case Steps.Sec30:
            if (m_seekStep[1] == 1) _currentStep = Steps.Sec15;
            else goto case Steps.Sec15; break;
          case Steps.Sec45:
            if (m_seekStep[2] == 1) _currentStep = Steps.Sec30;
            else goto case Steps.Sec30; break;
          case Steps.Min1:
            if (m_seekStep[3] == 1) _currentStep = Steps.Sec45;
            else goto case Steps.Sec45; break;
          case Steps.Min3:
            if (m_seekStep[4] == 1) _currentStep = Steps.Min1;
            else goto case Steps.Min1; break;
          case Steps.Min5:
            if (m_seekStep[5] == 1) _currentStep = Steps.Min3;
            else goto case Steps.Min3; break;
          case Steps.Min7:
            if (m_seekStep[6] == 1) _currentStep = Steps.Min5;
            else goto case Steps.Min5; break;
          case Steps.Min10:
            if (m_seekStep[7] == 1) _currentStep = Steps.Min7;
            else goto case Steps.Min7; break;
          case Steps.Min15:
            if (m_seekStep[8] == 1) _currentStep = Steps.Min10;
            else goto case Steps.Min10; break;
          case Steps.Min30:
            if (m_seekStep[9] == 1) _currentStep = Steps.Min15;
            else goto case Steps.Min15; break;
          case Steps.Min45:
            if (m_seekStep[10] == 1) _currentStep = Steps.Min30;
            else goto case Steps.Min30; break;
          case Steps.Hour1:
            if (m_seekStep[11] == 1) _currentStep = Steps.Min45;
            else goto case Steps.Min45; break;
          case Steps.Min90:
            if (m_seekStep[12] == 1) _currentStep = Steps.Hour1;
            else goto case Steps.Hour1; break;
          case Steps.Hour2:
            if (m_seekStep[13] == 1) _currentStep = Steps.Min90;
            else goto case Steps.Min90; break;
          case Steps.Hour3:
            if (m_seekStep[14] == 1) _currentStep = Steps.Hour2;
            else goto case Steps.Hour2; break;
        }
      }
      _seekTimer = DateTime.Now;
    }

    static public void SeekRelativePercentage(int iPercentage)
    {
      if (_player == null) return;
      _player.SeekRelativePercentage(iPercentage);
      GUIMessage msgUpdate = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYER_POSITION_CHANGED, 0, 0, 0, 0, 0, null);
      GUIGraphicsContext.SendMessage(msgUpdate);

      _currentStep = Steps.Sec0;
      _seekTimer = DateTime.MinValue;
    }

    static public void SeekAbsolute(double dTime)
    {
      if (_player == null) return;
      _player.SeekAbsolute(dTime);
      GUIMessage msgUpdate = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYER_POSITION_CHANGED, 0, 0, 0, 0, 0, null);
      GUIGraphicsContext.SendMessage(msgUpdate);

      _currentStep = Steps.Sec0;
      _seekTimer = DateTime.MinValue;
    }

    static public void SeekAsolutePercentage(int iPercentage)
    {
      if (_player == null) return;
      _player.SeekAsolutePercentage(iPercentage);
      GUIMessage msgUpdate = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYER_POSITION_CHANGED, 0, 0, 0, 0, 0, null);
      GUIGraphicsContext.SendMessage(msgUpdate);

      _currentStep = Steps.Sec0;
      _seekTimer = DateTime.MinValue;
    }
    static public bool HasVideo
    {
      get
      {
        if (_player == null) return false;
        return _player.HasVideo;
      }
    }
    static public bool IsVideo
    {
      get
      {
        if (_player == null) return false;
        if (_currentMedia == MediaType.Video) return true;
        return false;
      }
    }

    static public bool HasSubs
    {
      get
      {
        if (_player == null) return false;
        return (_subs != null);
      }
    }
    static public void RenderSubtitles()
    {
      if (_player == null) return;
      if (_subs == null) return;
      if (HasSubs)
      {
        _subs.Render(_player.CurrentPosition);
      }
    }
    static public void WndProc(ref Message m)
    {
      if (_player == null) return;
      _player.WndProc(ref m);
    }


    static public void Process()
    {
      if (GUIGraphicsContext.InVmr9Render) return;
      if (GUIGraphicsContext.Vmr9Active && VMR9Util.g_vmr9 != null)
      {
        VMR9Util.g_vmr9.Process();
        VMR9Util.g_vmr9.Repaint();

      }
      if (_player == null) return;
      _player.Process();
      if (!_player.Playing)
      {
        Log.Write("g_Player.Process() player stopped...");
        if (_player.Ended)
        {
          GUIMessage msg = new GUIMessage(GUIMessage.MessageType.GUI_MSG_PLAYBACK_ENDED, 0, 0, 0, 0, 0, null);
          GUIWindowManager.SendThreadMessage(msg);
          OnEnded();
          return;
        }
        Stop();
      }
      else
      {

        if (_currentStep != Steps.Sec0)
        {
          TimeSpan ts = DateTime.Now - _seekTimer;
          if (ts.TotalMilliseconds > 1500)
          {
            StepNow();
          }
        }
      }
    }

    static public int AudioStreams
    {
      get
      {
        if (_player == null) return 0;
        return _player.AudioStreams;
      }
    }
    static public int CurrentAudioStream
    {
      get
      {
        if (_player == null) return 0;
        return _player.CurrentAudioStream;
      }
      set
      {
        if (_player != null)
        {
          _player.CurrentAudioStream = value;
        }
      }
    }
    static public string AudioLanguage(int iStream)
    {
      if (_player == null) return Strings.Unknown;
      return _player.AudioLanguage(iStream);
    }

    static public int SubtitleStreams
    {
      get
      {
        if (_player == null) return 0;
        return _player.SubtitleStreams;
      }
    }
    static public int CurrentSubtitleStream
    {
      get
      {
        if (_player == null) return 0;
        return _player.CurrentSubtitleStream;
      }
      set
      {
        if (_player != null)
        {
          _player.CurrentSubtitleStream = value;
        }
      }
    }
    static public void SetVideoWindow()
    {
      if (_player == null) return;
      _player.SetVideoWindow();
    }

    static public string SubtitleLanguage(int iStream)
    {
      if (_player == null) return Strings.Unknown;
      return _player.SubtitleLanguage(iStream);
    }
    static public bool EnableSubtitle
    {
      get
      {
        if (_player == null) return false;
        return _player.EnableSubtitle;
      }
      set
      {
        if (_player == null) return;
        _player.EnableSubtitle = value;
      }
    }

    public static void Init()
    {
      GUIGraphicsContext.OnVideoWindowChanged += new VideoWindowChangedHandler(g_Player.OnVideoWindowChanged);
      GUIGraphicsContext.OnGammaContrastBrightnessChanged += new VideoGammaContrastBrightnessHandler(g_Player.OnGammaContrastBrightnessChanged);
    }

    static void OnGammaContrastBrightnessChanged()
    {
      if (!Playing) return;
      if (!HasVideo) return;
      if (_player == null) return;
      _player.Contrast = GUIGraphicsContext.Contrast;
      _player.Brightness = GUIGraphicsContext.Brightness;
      _player.Gamma = GUIGraphicsContext.Gamma;
    }

    static void OnVideoWindowChanged()
    {
      if (!Playing) return;
      if (!HasVideo) return;

      FullScreen = GUIGraphicsContext.IsFullScreenVideo;
      ARType = GUIGraphicsContext.ARType;
      if (!FullScreen)
      {
        PositionX = GUIGraphicsContext.VideoWindow.Left;
        PositionY = GUIGraphicsContext.VideoWindow.Top;
        RenderWidth = GUIGraphicsContext.VideoWindow.Width;
        RenderHeight = GUIGraphicsContext.VideoWindow.Height;
      }
      bool inTV = false;
      int windowId = GUIWindowManager.ActiveWindow;
      if (windowId == (int)GUIWindow.Window.WINDOW_TV ||
          windowId == (int)GUIWindow.Window.WINDOW_TVGUIDE ||
          windowId == (int)GUIWindow.Window.WINDOW_SEARCHTV ||
          windowId == (int)GUIWindow.Window.WINDOW_SCHEDULER ||
          windowId == (int)GUIWindow.Window.WINDOW_RECORDEDTV)
        inTV = true;
      Visible = (FullScreen || GUIGraphicsContext.Overlay ||
          windowId == (int)GUIWindow.Window.WINDOW_SCHEDULER || inTV);
      SetVideoWindow();
    }

    /// <summary>
    /// returns video window rectangle
    /// </summary>
    static public Rectangle VideoWindow
    {
      get
      {
        if (_player == null) return new Rectangle(0, 0, 0, 0);
        return _player.VideoWindow;
      }
    }

    /// <summary>
    /// returns video source rectangle displayed
    /// </summary>
    static public Rectangle SourceWindow
    {
      get
      {
        if (_player == null) return new Rectangle(0, 0, 0, 0);
        return _player.SourceWindow;
      }
    }
    static public int GetHDC()
    {
      if (_player == null) return 0;
      return _player.GetHDC();
    }

    static public void ReleaseHDC(int HDC)
    {
      if (_player == null) return;
      _player.ReleaseHDC(HDC);
    }

    static public bool CanSeek
    {
      get
      {
        if (_player == null) return false;
        return (_player.CanSeek() && !_player.IsDVDMenu);
      }
    }

    static public void SwitchToNextAudio()
    {
      if (AudioStreams > 1)
        if (CurrentAudioStream < AudioStreams - 1)
          CurrentAudioStream++;
        else
          CurrentAudioStream = 0;
    }

    static public void SwitchToNextSubtitle()
    {
      if (EnableSubtitle)
      {
        if (SubtitleStreams > 1)
          if (CurrentSubtitleStream < SubtitleStreams - 1)
            CurrentSubtitleStream++;
          else
          {
            EnableSubtitle = false;
            CurrentSubtitleStream = 0;
          }
      }
      else
      {
        CurrentSubtitleStream = 0;
        EnableSubtitle = true;
      }
    }

    #endregion

  }
}
