using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Video;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
///     A VisualElement that is a VideoPlayer with a roll up play bar with play, speed and audio controls
/// </summary>
[UxmlElement]
public partial class VideoPlayerElement : VisualElement
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
    private bool m_HasFailed;

    /// <summary>Поток готов; событие не зависит от геометрии элемента.</summary>
    public event Action Prepared;

    /// <summary>Единственный отказ текущего источника; причина предназначена для диагностики.</summary>
    public event Action<string> Failed;

    /// <summary>
    ///     Build a new VideoPlayer
    /// </summary>
    public VideoPlayerElement()
    {
        EnsureAudioSource();

        RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
        RegisterCallback<DetachFromPanelEvent>(OnDetachedFromPanel);

        RegisterCallback<GeometryChangedEvent>(GeometryChangedHandler);
        this.AddManipulator(new Clickable(PlayerPause));
    }

    private static void EnsureAudioSource()
    {
        if (s_BugFixClip == null)
        {
            s_BugFixClip = AudioClip.Create("testClip", 44000, 1, 44000, false);
        }

        if (s_BugFixAudioSource == null)
        {
            GameObject sourceGo = new() { hideFlags = HideFlags.HideAndDontSave };
            s_BugFixAudioSource = sourceGo.AddComponent<AudioSource>();
            s_BugFixAudioSource.clip = s_BugFixClip;
        }
    }

    private void PreparePlayer()
    {
        if (m_HasFailed || m_VideoPlayer != null)
        {
            return;
        }

        //we try to get a player from the pool, if not available, we create a new one
        VideoPlayer player = null;
        // При выключенном domain reload очередь переживает Play, а native GameObject — нет.
        while (s_VideoPlayerPool.TryDequeue(out VideoPlayer pooled))
        {
            if (pooled == null)
            {
                continue;
            }

            player = pooled;
            break;
        }

        if (player != null)
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
        }
        else if (!string.IsNullOrEmpty(m_Url))
        {
            m_VideoPlayer.url = m_Url;
            m_VideoPlayer.Prepare();
        }

        // Prepare запускается ровно один раз. Play до prepareCompleted повторно открывал
        // неисправный URL и дублировал нативные ошибки декодера.
    }

    private void FreePlayer()
    {
        if (m_VideoPlayer == null)
        {
            return;
        }

        m_VideoPlayer.prepareCompleted -= PreparedHandler;
        m_VideoPlayer.frameReady -= FrameReadyHandler;

        m_VideoPlayer.errorReceived -= OnErrorReceived;

        m_VideoPlayer.Stop();
        m_VideoPlayer.clip = null;
        m_VideoPlayer.url = null;
        if (m_VideoPlayer.targetTexture != null)
        {
            RenderTexture texture = m_VideoPlayer.targetTexture;
            m_VideoPlayer.targetTexture = null;
            texture.Release();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(texture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        s_VideoPlayerPool.Enqueue(m_VideoPlayer);
        m_VideoPlayer = null;
        m_PlaybackObject = null;
    }

    /// <summary>
    ///     Set the player used clip. Will set url to null if one was set.
    /// </summary>
    /// <param name="clip">The VideoClip to use</param>
    /// <param name="autoplay">If true, the video will immediately start to play the clip</param>
    public void SetVideoClip(VideoClip clip, bool autoplay)
    {
        if (m_VideoClip != clip)
        {
            m_PreviousTime = 0;
        }

        m_AutoStart = autoplay;
        ResetFailure();
        m_VideoClip = clip;
        m_Url = null;

        if (m_VideoPlayer != null)
        {
            m_VideoPlayer.clip = clip;
            m_VideoPlayer.Prepare();
        }
    }

    /// <summary>
    ///     Set the URL used by the player. If a clip was set, it will be set to null.
    /// </summary>
    /// <param name="url">The Url to be used by the player</param>
    /// <param name="autoplay">If true, the player will play immediately</param>
    public void SetVideoUrl(string url, bool autoplay)
    {
        if (!string.Equals(m_Url, url, StringComparison.Ordinal))
        {
            m_PreviousTime = 0;
        }

        m_AutoStart = autoplay;
        ResetFailure();
        m_Url = url;
        m_VideoClip = null;

        if (m_VideoPlayer != null)
        {
            m_VideoPlayer.url = url;
            m_VideoPlayer.Prepare();
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
    public bool IsLooping() => m_IsLooping;

    private void ResetFailure()
    {
        m_HasFailed = false;
        // Адаптер удаляет детей между attach: ссылки на прежние controls уже недействительны.
        if (panel == null)
        {
            return;
        }

        m_PlayControlBar?.SetEnabled(true);
        if (m_ErrorString != null)
        {
            m_ErrorString.style.display = DisplayStyle.None;
        }
    }

    private void OnAttachedToPanel(AttachToPanelEvent evt)
    {
        EnsureDefaultHierarchy();

        m_PlaySurface = this.Q<VisualElement>("videoPlayer");
        m_PlayControlBar = this.Q<VisualElement>("PlayBar");

        m_PlayOverlay = this.Q<VisualElement>("PlayOverlay");
        m_PlayTrack = this.Q<VisualElement>("PlayTrackFiller");

        m_Popout = this.Q<VisualElement>("PopoutButton");
        m_PlayButton = this.Q<VisualElement>("PlayButton");

        m_AudioIcon = this.Q<VisualElement>("AudioIcon");
        m_VolumeSlider = this.Q<Slider>("VolumeSlider");

        m_ErrorString = this.Q<Label>("ErrorLabel");
        if (m_ErrorString != null)
        {
            m_ErrorString.style.display = DisplayStyle.None;
        }

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

        // Editor popout is implemented in the editor-only viewer layer; runtime/WebGL keeps media inline.
        m_Popout.style.display = DisplayStyle.None;

        //Speed selection
        m_SpeedSelectorPopup = this.Q<VisualElement>("SpeedSelectorPopup");

        m_SpeedSelectionRoot = this.Q<VisualElement>("VideoSpeedArea");
        m_SpeedSelectionRoot.RegisterCallback<MouseLeaveEvent>(evt => { m_SpeedSelectorPopup.RemoveFromClassList("video-speed-selector-opened"); });

        m_SpeedSelectionButton = this.Q<VisualElement>("SpeedSelectorButton");
        m_SpeedSelectionButton.AddManipulator(new Clickable(() => { m_SpeedSelectorPopup.AddToClassList("video-speed-selector-opened"); }));

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

        PreparePlayer();
        if (m_HasFailed)
        {
            m_ErrorString.style.display = DisplayStyle.Flex;
            m_ErrorString.text = "Видео недоступно. Проверьте ссылку на видеофайл.";
            m_PlayOverlay.visible = false;
            m_PlayControlBar.SetEnabled(false);
        }

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged += PlayModeChanged;
#endif
    }

    private void OnDetachedFromPanel(DetachFromPanelEvent evt)
    {
        // saving state to re apply if this is just detaching to reattach immediately somewhere
        if (m_VideoPlayer != null)
        {
            m_PreviousTime = m_VideoPlayer.time;
            bool resumePlaying = !m_HasFailed && m_VideoPlayer.isPlaying;
            m_VideoPlayer.Pause();
            m_AutoStart = resumePlaying;
#if UNITY_EDITOR
            if (m_AudioWasMuted)
            {
                EditorUtility.audioMasterMute = true;
            }
#endif
        }
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= PlayModeChanged;
#endif

        FreePlayer();
    }

    private void OnErrorReceived(VideoPlayer source, string message)
    {
        if (source != m_VideoPlayer || m_HasFailed)
        {
            return;
        }

        m_HasFailed = true;
        m_AutoStart = false;
        source.Stop();
        if (m_ErrorString != null)
        {
            m_ErrorString.style.display = DisplayStyle.Flex;
            m_ErrorString.text = "Видео недоступно. Проверьте ссылку на видеофайл.";
        }

        if (m_PlayOverlay != null)
        {
            m_PlayOverlay.visible = false;
        }

        m_PlayControlBar?.SetEnabled(false);
        Failed?.Invoke(message);
    }

#if UNITY_EDITOR
    private void PlayModeChanged(PlayModeStateChange state)
    {
        //reset the state as changing playmode state will pause the player and mess with its internal state
        PlayerPause();
    }
#endif

    private void EnsureDefaultHierarchy()
    {
        if (this.Q<VisualElement>("videoPlayer") != null)
        {
            return;
        }

        VisualElement videoPlayer = new() { name = "videoPlayer" };
        videoPlayer.AddToClassList("video-content");
        Add(videoPlayer);

        VisualElement playBar = new() { name = "PlayBar" };
        playBar.AddToClassList("video-control-bar");
        videoPlayer.Add(playBar);

        VisualElement playTrack = new() { name = "PlayTrack" };
        playTrack.AddToClassList("video-play-track");
        playBar.Add(playTrack);

        VisualElement playTrackFiller = new() { name = "PlayTrackFiller" };
        playTrackFiller.AddToClassList("video-play-track-filler");
        playTrack.Add(playTrackFiller);

        VisualElement playTrackHead = new() { name = "PlayTrackHead" };
        playTrackHead.AddToClassList("video-play-track-head");
        playTrackFiller.Add(playTrackHead);

        VisualElement playControl = new() { name = "PlayControl" };
        playControl.AddToClassList("video-control-container");
        playBar.Add(playControl);

        VisualElement playButton = new() { name = "PlayButton" };
        playButton.AddToClassList("video-control-button");
        playButton.AddToClassList("video-pause-button");
        // Подписи работают в player без editor-only пиктограмм и не перехватывают клики.
        Label pauseLabel = new("Пауза") { pickingMode = PickingMode.Ignore };
        pauseLabel.AddToClassList("video-pause-label");
        playButton.Add(pauseLabel);
        playButton.tooltip = "Воспроизвести или приостановить видео";
        playControl.Add(playButton);

        VisualElement audioIcon = new() { name = "AudioIcon" };
        audioIcon.AddToClassList("video-audio-icon");
        Label audioLabel = new("Звук") { pickingMode = PickingMode.Ignore };
        audioLabel.AddToClassList("video-control-label");
        audioIcon.Add(audioLabel);
        audioIcon.tooltip = "Выключить или восстановить звук";
        playControl.Add(audioIcon);

        Slider volumeSlider = new() { name = "VolumeSlider", lowValue = 0, highValue = 1.0f };
        volumeSlider.AddToClassList("video-audio-slider");
        playControl.Add(volumeSlider);

        VisualElement spacer = new() { name = "Spacer" };
        spacer.style.flexGrow = 1;
        playControl.Add(spacer);

        VisualElement videoSpeedArea = new() { name = "VideoSpeedArea" };
        videoSpeedArea.AddToClassList("video-speed-button");
        playControl.Add(videoSpeedArea);

        VisualElement speedSelectorPopup = new() { name = "SpeedSelectorPopup" };
        speedSelectorPopup.AddToClassList("video-speed-selector");
        videoSpeedArea.Add(speedSelectorPopup);

        foreach (string label in new[] { "2x", "1.5x", "1x", "0.5x", "0.25x" })
        {
            VisualElement container = new();
            container.AddToClassList("video-speed-label-container");
            container.Add(new Label(label) { name = "SpeedLabel" });
            speedSelectorPopup.Add(container);
        }

        VisualElement speedSelectorButton = new() { name = "SpeedSelectorButton" };
        speedSelectorButton.AddToClassList("video-speed-label-container");
        speedSelectorButton.Add(new Label("1x") { name = "SpeedLabel" });
        videoSpeedArea.Add(speedSelectorButton);

        VisualElement popoutButton = new() { name = "PopoutButton" };
        popoutButton.AddToClassList("video-control-button");
        popoutButton.AddToClassList("video-popup-button");
        Label popoutLabel = new("Открыть") { pickingMode = PickingMode.Ignore };
        popoutLabel.AddToClassList("video-control-label");
        popoutButton.Add(popoutLabel);
        popoutButton.tooltip = "Открыть видео отдельно";
        playControl.Add(popoutButton);

        VisualElement playOverlay = new() { name = "PlayOverlay" };
        playOverlay.AddToClassList("video-play-overlay");
        videoPlayer.Add(playOverlay);

        VisualElement playOverlayButton = new() { name = "PlayOverlayButton" };
        playOverlayButton.AddToClassList("video-play-overlay-button");
        playOverlay.Add(playOverlayButton);

        Label errorLabel = new() { name = "ErrorLabel" };
        errorLabel.AddToClassList("video-error-label");
        videoPlayer.Add(errorLabel);
    }

    private void PlayButtonPushed()
    {
        if (m_VideoPlayer == null || m_HasFailed)
        {
            return;
        }

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

    private void AudioSliderChangeHandler(ChangeEvent<float> evt) => m_VideoPlayer?.SetDirectAudioVolume(0, evt.newValue);

    private void PlayTrackPressedHandler(MouseDownEvent evt)
    {
        if (m_VideoPlayer == null || m_HasFailed || !m_VideoPlayer.isPrepared)
        {
            return;
        }

        m_PlayTrack.parent.CaptureMouse();
        evt.StopPropagation();
        m_IsScrubbing = true;

        float seekPosition = evt.localMousePosition.x / m_PlayTrack.parent.contentRect.width;
        SetPlayPercent(seekPosition);

        m_VideoPlayer.Pause();
    }

    private void PlayTrackMoveHandler(MouseMoveEvent evt)
    {
        if (!m_IsScrubbing || m_VideoPlayer == null || m_HasFailed)
        {
            return;
        }

        float seekPosition = evt.localMousePosition.x / m_PlayTrack.parent.contentRect.width;
        SetPlayPercent(seekPosition);

        m_VideoPlayer.StepForward();
    }

    private void PlayTrackReleasedHandler(MouseUpEvent evt)
    {
        if (m_VideoPlayer == null || m_HasFailed || !m_IsScrubbing)
        {
            return;
        }

        m_IsScrubbing = false;
        m_PlayTrack.parent.ReleaseMouse();
        m_VideoPlayer.Play();
    }

    private void PlayerPlay()
    {
        if (m_VideoPlayer == null || m_HasFailed)
        {
            return;
        }

        if (!m_VideoPlayer.isPrepared)
        {
            m_AutoStart = true;
            return;
        }
#if UNITY_EDITOR
        //A button in GameView can mute audio and direct audio respect that. So we need to save if it was disable
        //so we can disable it again when the video is stopped/destroyed
        m_AudioWasMuted = EditorUtility.audioMasterMute;
        EditorUtility.audioMasterMute = false;
#endif

        m_PlayOverlay.visible = false;
        m_PlayButton.RemoveFromClassList("video-play-button");
        m_PlayButton.AddToClassList("video-pause-button");
        m_PlayOverlay.visible = false;

        EnsureAudioSource();
        s_BugFixAudioSource.Play();
        m_VideoPlayer.Play();
    }

    private void PlayerPause()
    {
        if (m_VideoPlayer == null)
        {
            return;
        }

        //if we play then pause in between the player getting ready, the play when ready flag would be set and the player
        //would start playing when ready despite pausing. So always set the flag to false when pausing
        m_AutoStart = false;

        m_PlayButton.RemoveFromClassList("video-pause-button");
        m_PlayButton.AddToClassList("video-play-button");
        m_PlayOverlay.visible = true;

        m_VideoPlayer.Pause();

#if UNITY_EDITOR
        if (m_AudioWasMuted)
        {
            EditorUtility.audioMasterMute = true;
        }
#endif
    }

    private void PreparedHandler(VideoPlayer source)
    {
        if (source != m_VideoPlayer || m_HasFailed)
        {
            return;
        }

        if (source.targetTexture != null)
        {
            source.targetTexture.Release();
            if (Application.isPlaying)
            {
                UnityEngine.Object.Destroy(source.targetTexture);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(source.targetTexture);
            }
        }

        source.targetTexture = new RenderTexture((int)source.width, (int)source.height, 32);
        source.targetTexture.hideFlags = HideFlags.HideAndDontSave;

        m_PlaySurface.style.backgroundImage = Background.FromRenderTexture(source.targetTexture);
        if (m_PreviousTime > 0)
        {
            source.time = m_PreviousTime;
        }

        if (m_AutoStart)
        {
            PlayerPlay();
        }
        else
        {
            PlayerPause();
        }

        m_AutoStart = false;
        //use this to trigger a geometry change event that will take care of resizing it to keep aspect ratio
        style.width = 1;
        Prepared?.Invoke();
    }

    private void FrameReadyHandler(VideoPlayer player, long frameIdx)
    {
        MarkDirtyRepaint();
        m_PlayTrack.style.width =
            Length.Percent(GetPlayPercent() * 100.0f);
    }

    private void GeometryChangedHandler(GeometryChangedEvent evt)
    {
        if (m_VideoPlayer != null && m_VideoPlayer.targetTexture != null)
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
        if (m_VideoPlayer == null || m_VideoPlayer.length == 0)
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
        if (player == null || m_HasFailed || !player.isPrepared)
        {
            return;
        }

        player.time = percent * player.length;
    }
}
