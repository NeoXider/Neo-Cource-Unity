using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;

/// <summary>
///     A VisualElement that is a VideoPlayer with a roll up play bar with play, speed and audio controls
/// </summary>
public class VideoPlayerElement : VisualElement
{
    // Use a pool where unused player are returned. This avoid creating player
    // in a loop.
    private static readonly Queue<VideoPlayer> s_VideoPlayerPool = new();

    // This is to fix a bug in the videoplayer in edit mode in Unity : audio isn't initialized properly until you
    // enter play mode. Audio Source seems to properly initialize the audio subsystem, so we play a dummy clip
    // which force the init of the audio system when starting playing a video
    private static AudioSource s_BugFixAudioSource;
    private static AudioClip s_BugFixClip;

    private VisualElement m_AudioIcon;
    private bool m_AudioWasMuted;
    private bool m_AutoStart;

    private Label m_ErrorString;

    private bool m_IsLooping;
    private bool m_IsScrubbing;

    private GameObject m_PlaybackObject;
    private VisualElement m_PlayButton;
    private VisualElement m_PlayControlBar;

    private VisualElement m_PlayOverlay;
    private float m_PlaySpeed = 1.0f;

    private VisualElement m_PlaySurface;
    private VisualElement m_PlayTrack;

    private VisualElement m_Popout;

    //we save the time on detaching, as this could just be moving the player from one element to another, and this should
    //maintain the same state
    private double m_PreviousTime;

    private VisualElement m_SelectedSpeedButton;
    private VisualElement m_SpeedSelectionButton;
    private Label m_SpeedSelectionLabel;

    private VisualElement m_SpeedSelectionRoot;
    private VisualElement m_SpeedSelectorPopup;
    private string m_Url;

    private VideoClip m_VideoClip;
    private VideoPlayer m_VideoPlayer;
    private Slider m_VolumeSlider;

    /// <summary>
    ///     Build a new VideoPlayer
    /// </summary>
    public VideoPlayerElement()
    {
        if (s_BugFixClip == null)
        {
            s_BugFixClip = AudioClip.Create("testClip", 44000, 1, 44000, false);
            GameObject sourceGo = new() { hideFlags = HideFlags.HideAndDontSave };
            s_BugFixAudioSource = sourceGo.AddComponent<AudioSource>();
            s_BugFixAudioSource.clip = s_BugFixClip;
        }

        RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
        RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

        RegisterCallback<GeometryChangedEvent>(GeometryChangedHandler);
    }

    private void PreparePlayer()
    {
        //we try to get a player from the pool, if not available, we create a new one
        if (s_VideoPlayerPool.TryDequeue(out VideoPlayer player))
        {
            m_PlaybackObject = player.gameObject;
            m_VideoPlayer = player;

            m_PlaySpeed = m_VideoPlayer.playbackSpeed;
        }
        else
        {
            m_PlaybackObject = new GameObject { hideFlags = HideFlags.HideAndDontSave };

            m_VideoPlayer = m_PlaybackObject.AddComponent<VideoPlayer>();

            m_VideoPlayer.playOnAwake = false;
            m_VideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            m_VideoPlayer.skipOnDrop = true;
            m_VideoPlayer.SetDirectAudioVolume(0, 1.0f);
            m_VideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;

            //Documentation not that frame ready event are likely to tax the CPU, but we need to be notified to be able to
            //refresh the element so the original framerate of the video is preserved (otherwise Unity only update the
            //element only once per second or so)
            m_VideoPlayer.sendFrameReadyEvents = true;
        }

        m_VideoPlayer.isLooping = m_IsLooping;

        m_VideoPlayer.prepareCompleted += PreparedHandler;
        m_VideoPlayer.frameReady += FrameReadyHandler;

        m_VideoPlayer.errorReceived += OnErrorReceived;

        //if a clip or url was set before the player was ready, we assign it
        if (m_VideoClip != null)
        {
            m_VideoPlayer.clip = m_VideoClip;
            m_VideoPlayer.Prepare();

            m_VideoPlayer.time = m_PreviousTime;
        }
        else if (!string.IsNullOrEmpty(m_Url))
        {
            m_VideoPlayer.url = m_Url;
            m_VideoPlayer.Prepare();

            m_VideoPlayer.time = m_PreviousTime;
        }

        //This will trigger the player prepare (which then take care of auto starting the video if set to auto start)
        m_VideoPlayer.Play();
        m_VideoPlayer.Pause();
    }

    private void FreePlayer()
    {
        m_VideoPlayer.prepareCompleted -= PreparedHandler;
        m_VideoPlayer.frameReady -= FrameReadyHandler;

        m_VideoPlayer.errorReceived -= OnErrorReceived;

        m_VideoPlayer.Stop();
        m_VideoPlayer.clip = null;
        m_VideoPlayer.url = null;

        s_VideoPlayerPool.Enqueue(m_VideoPlayer);
        m_VideoPlayer = null;
    }

    /// <summary>
    ///     Set the player used clip. Will set url to null if one was set.
    /// </summary>
    /// <param name="clip">The VideoClip to use</param>
    /// <param name="autoplay">If true, the video will immediately start to play the clip</param>
    public void SetVideoClip(VideoClip clip, bool autoplay)
    {
        m_AutoStart = autoplay;
        m_VideoClip = clip;
        m_Url = null;

        if (m_VideoPlayer != null)
        {
            m_VideoPlayer.clip = clip;
            m_VideoPlayer.Prepare();
            if (m_AutoStart)
            {
                PlayerPlay();
            }
        }
    }

    /// <summary>
    ///     Set the URL used by the player. If a clip was set, it will be set to null.
    /// </summary>
    /// <param name="url">The Url to be used by the player</param>
    /// <param name="autoplay">If true, the player will play immediately</param>
    public void SetVideoUrl(string url, bool autoplay)
    {
        m_AutoStart = autoplay;
        m_Url = url;
        m_VideoClip = null;

        if (m_VideoPlayer != null)
        {
            m_VideoPlayer.url = url;
            m_VideoPlayer.Prepare();
            if (m_AutoStart)
            {
                PlayerPlay();
            }
        }
    }

    /// <summary>
    ///     Change the looping settings of the player
    /// </summary>
    /// <param name="looping">If true the player loop once it reach the end of the video</param>
    public void SetLooping(bool looping)
    {
        m_IsLooping = looping;
        if (m_VideoPlayer != null)
        {
            m_VideoPlayer.isLooping = looping;
        }
    }

    /// <summary>
    ///     Is the player set to loop?
    /// </summary>
    /// <returns>True if looping, False otherwise</returns>
    public bool IsLooping()
    {
        return m_IsLooping;
    }

    private void OnAttachedToPanel(AttachToPanelEvent evt)
    {
        PreparePlayer();

        m_PlaySurface = this.Q<VisualElement>("videoPlayer");
        m_PlayControlBar = this.Q<VisualElement>("PlayBar");

        m_PlayOverlay = this.Q<VisualElement>("PlayOverlay");
        m_PlayTrack = this.Q<VisualElement>("PlayTrackFiller");

        m_Popout = this.Q<VisualElement>("PopoutButton");
        m_PlayButton = this.Q<VisualElement>("PlayButton");

        m_AudioIcon = this.Q<VisualElement>("AudioIcon");
        m_VolumeSlider = this.Q<Slider>("VolumeSlider");

        m_ErrorString = this.Q<Label>("ErrorLabel");
        //m_ErrorString.style.display = DisplayStyle.None;

        this.AddManipulator(new Clickable(PlayerPause));
        //we had a clickable manipulator on our control bar at the bottom so it eat click event so that
        //the player above doesn't receive it to stop the video
        m_PlayControlBar.AddManipulator(new Clickable(() => { }));

        m_PlayOverlay.AddManipulator(new Clickable(PlayerPlay));

        //Play Button
        m_PlayButton.RemoveFromClassList("video-pause-button");
        m_PlayButton.AddToClassList("video-play-button");
        m_PlayButton.AddManipulator(new Clickable(PlayButtonPushed));

        //volume slider
        m_VolumeSlider.SetValueWithoutNotify(1.0f);
        m_VolumeSlider.RegisterValueChangedCallback(AudioSliderChangeHandler);

        //clicking the audio icon set the volume to 0 (mute)
        m_AudioIcon.AddManipulator(new Clickable(AudioButtonPushed));

        //register on the parent listening to mouse events to seek times
        m_PlayTrack.parent.RegisterCallback<MouseDownEvent>(PlayTrackPressedHandler);
        m_PlayTrack.parent.RegisterCallback<MouseMoveEvent>(PlayTrackMoveHandler);
        m_PlayTrack.parent.RegisterCallback<MouseUpEvent>(PlayTrackReleasedHandler);
        m_PlayTrack.parent.RegisterCallback<MouseCaptureOutEvent>(_ => { m_IsScrubbing = false; });

        //Popout button
        m_Popout.AddManipulator(new Clickable(() => { MediaPopoutWindow.Popout(this); }));

        //Speed selection
        m_SpeedSelectorPopup = this.Q<VisualElement>("SpeedSelectorPopup");

        m_SpeedSelectionRoot = this.Q<VisualElement>("VideoSpeedArea");
        m_SpeedSelectionRoot.RegisterCallback<MouseLeaveEvent>(evt =>
        {
            m_SpeedSelectorPopup.RemoveFromClassList("video-speed-selector-opened");
        });

        m_SpeedSelectionButton = this.Q<VisualElement>("SpeedSelectorButton");
        m_SpeedSelectionButton.AddManipulator(new Clickable(() =>
        {
            m_SpeedSelectorPopup.AddToClassList("video-speed-selector-opened");
        }));

        //Bind all button to their speed
        float[] speeds = { 2.0f, 1.5f, 1.0f, 0.5f, 0.25f };

        m_SpeedSelectionLabel = m_SpeedSelectionButton.Q<Label>();

        List<VisualElement> children = m_SpeedSelectorPopup
            .Query<VisualElement>(className: "video-speed-label-container").Build().ToList();
        for (int i = 0; i < children.Count; ++i)
        {
            VisualElement child = children[i];
            Label label = children[i].Q<Label>();

            if (speeds.Length > i)
            {
                if (Mathf.Approximately(speeds[i], m_PlaySpeed))
                {
                    m_SelectedSpeedButton = child;
                    child.AddToClassList("selected");
                    m_SpeedSelectionLabel.text = label.text;
                }
            }

            int i1 = i;
            children[i].AddManipulator(new Clickable(() =>
            {
                m_SelectedSpeedButton?.RemoveFromClassList("selected");
                m_SelectedSpeedButton = child;
                m_SelectedSpeedButton.AddToClassList("selected");
                //Change play speed
                m_VideoPlayer.playbackSpeed = speeds[i1];
                m_PlaySpeed = speeds[i1];

                m_SpeedSelectionLabel.text = label.text;
            }));
        }

        EditorApplication.playModeStateChanged += PlayModeChanged;

        if (m_AutoStart)
        {
            PlayerPlay();
        }
    }

    private void OnDetachedFromPanel(DetachFromPanelEvent evt)
    {
        // saving state to re apply if this is just detaching to reattach immediately somewhere
        m_PreviousTime = m_VideoPlayer.time;
        m_AutoStart = m_VideoPlayer.isPlaying;

        m_VideoPlayer.Pause();
        EditorApplication.playModeStateChanged -= PlayModeChanged;

        FreePlayer();
    }

    private void OnErrorReceived(VideoPlayer source, string message)
    {
        m_ErrorString.style.display = DisplayStyle.Flex;
        m_ErrorString.text = message;
    }

    private void PlayModeChanged(PlayModeStateChange state)
    {
        //reset the state as changing playmode state will pause the player and mess with its internal state
        PlayerPause();
    }

    private void PlayButtonPushed()
    {
        if (m_VideoPlayer.isPlaying)
        {
            PlayerPause();
        }
        else
        {
            PlayerPlay();
        }
    }

    private void AudioButtonPushed()
    {
        //if the volume is 0 and we have a previous value, we return to that value
        if (m_VolumeSlider.value < 0.001f && m_VolumeSlider.userData != null)
        {
            m_VolumeSlider.value = (float)m_VolumeSlider.userData;
            m_VolumeSlider.userData = null;
        }
        else
        {
            //otherwise we save in the user data the previous volume and mute
            m_VolumeSlider.userData = m_VolumeSlider.value;
            m_VolumeSlider.value = 0.0f;
        }
    }

    private void AudioSliderChangeHandler(ChangeEvent<float> evt)
    {
        m_VideoPlayer.SetDirectAudioVolume(0, evt.newValue);
    }

    private void PlayTrackPressedHandler(MouseDownEvent evt)
    {
        m_PlayTrack.parent.CaptureMouse();
        evt.StopPropagation();
        m_IsScrubbing = true;

        float seekPosition = evt.localMousePosition.x / m_PlayTrack.parent.contentRect.width;
        SetPlayPercent(seekPosition);

        m_VideoPlayer.Pause();
    }

    private void PlayTrackMoveHandler(MouseMoveEvent evt)
    {
        if (!m_IsScrubbing)
        {
            return;
        }

        float seekPosition = evt.localMousePosition.x / m_PlayTrack.parent.contentRect.width;
        SetPlayPercent(seekPosition);

        m_VideoPlayer.StepForward();
    }

    private void PlayTrackReleasedHandler(MouseUpEvent evt)
    {
        m_IsScrubbing = false;
        m_PlayTrack.parent.ReleaseMouse();
        m_VideoPlayer.Play();
    }

    private void PlayerPlay()
    {
        //A button in GameView can mute audio and direct audio respect that. So we need to save if it was disable
        //so we can disable it again when the video is stopped/destroyed
        m_AudioWasMuted = EditorUtility.audioMasterMute;
        EditorUtility.audioMasterMute = false;

        m_PlayOverlay.visible = false;
        m_PlayButton.RemoveFromClassList("video-play-button");
        m_PlayButton.AddToClassList("video-pause-button");
        m_PlayOverlay.visible = false;

        s_BugFixAudioSource.Play();
        m_VideoPlayer.Play();
    }

    private void PlayerPause()
    {
        //if we play then pause in between the player getting ready, the play when ready flag would be set and the player
        //would start playing when ready despite pausing. So always set the flag to false when pausing
        m_AutoStart = false;

        m_PlayButton.RemoveFromClassList("video-pause-button");
        m_PlayButton.AddToClassList("video-play-button");
        m_PlayOverlay.visible = true;

        m_VideoPlayer.Pause();

        if (m_AudioWasMuted)
        {
            EditorUtility.audioMasterMute = true;
        }
    }

    private void PreparedHandler(VideoPlayer source)
    {
        if (source.targetTexture != null)
        {
            Object.DestroyImmediate(source.targetTexture);
        }

        source.targetTexture = new RenderTexture((int)source.width, (int)source.height, 32);
        source.targetTexture.hideFlags = HideFlags.HideAndDontSave;

        m_PlaySurface.style.backgroundImage = Background.FromRenderTexture(source.targetTexture);

        PlayerPlay();
        if (!m_AutoStart)
        {
            PlayerPause();
        }

        m_AutoStart = false;
        //use this to trigger a geometry change event that will take care of resizing it to keep aspect ratio
        style.width = 1;
    }

    private void FrameReadyHandler(VideoPlayer player, long frameIdx)
    {
        MarkDirtyRepaint();
        m_PlayTrack.style.width =
            Length.Percent(GetPlayPercent() * 100.0f);
    }

    private void GeometryChangedHandler(GeometryChangedEvent evt)
    {
        if (m_VideoPlayer.targetTexture != null)
        {
            RenderTexture texture = m_VideoPlayer.targetTexture;
            float aspectRatio = texture.width / (float)texture.height;
            float targetWidth = evt.newRect.width;
            float targetHeight = targetWidth / aspectRatio;

            if (!Mathf.Approximately(targetWidth, evt.newRect.width) ||
                !Mathf.Approximately(targetHeight, evt.newRect.height))
            {
                //we always set the width as 100% as this will allow to resize on parent resize
                //be height will be based on aspect ratio
                style.width = Length.Percent(100);
                style.height = targetHeight;
            }
        }
    }

    /// <summary>
    ///     Return the percentage of the current clip/url the player is currently at
    /// </summary>
    /// <returns>The percent from 0 to 1 at which the player is at</returns>
    public float GetPlayPercent()
    {
        if (m_VideoPlayer.length == 0)
        {
            return 0.0f;
        }

        return (float)(m_VideoPlayer.time / m_VideoPlayer.length);
    }

    /// <summary>
    ///     Set the percentage of the current clip/url the player is currently at
    /// </summary>
    /// <param name="percent">The percent of the current video from 0 to 1 to which to set the player</param>
    public void SetPlayPercent(float percent)
    {
        VideoPlayer player = m_VideoPlayer;
        player.time = percent * player.length;
    }

    public new class UxmlFactory : UxmlFactory<VideoPlayerElement>
    {
    }
}